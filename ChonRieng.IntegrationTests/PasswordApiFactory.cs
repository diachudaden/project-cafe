using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChonRieng.Api.Services.Email;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ChonRieng.IntegrationTests;

/// <summary>
/// The real API on an in-memory database with a recording <see cref="FakeEmailSender"/> behind the real queue/worker,
/// low auth rate limits, and a log capture. Each test picks its own client IP (sent as X-Forwarded-For from a
/// trusted loopback hop) so rate-limit buckets do not leak between tests.
/// </summary>
public class PasswordApiFactory : WebApplicationFactory<Program>
{
    public const string PublicBaseUrl = "https://cafe.test";
    public const string Password = "Password-Test-123!";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _database = $"PasswordTests-{Guid.NewGuid()}";
    private static int _nextIp;

    static PasswordApiFactory()
    {
        // Same process-wide variables as ApiFactory (Program reads them before host configuration applies).
        _ = ApiFactory.WebhookSecret;
    }

    public FakeEmailSender Emails { get; } = new();

    public CapturingLoggerProvider Logs { get; } = new();

    /// <summary>Reset-token lifetime override; null keeps <c>PasswordReset__TokenMinutes</c>.</summary>
    protected virtual TimeSpan? TokenLifespan => null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("App:PublicBaseUrl", PublicBaseUrl);
        builder.UseSetting("AuthRateLimits:LoginPerIp", "20");
        builder.UseSetting("AuthRateLimits:RegisterPerIp", "20");
        builder.UseSetting("AuthRateLimits:ForgotPasswordPerIp", "4");
        builder.UseSetting("AuthRateLimits:ForgotPasswordPerEmail", "2");
        builder.UseSetting("AuthRateLimits:ResetPasswordPerIp", "5");
        builder.UseSetting("AuthRateLimits:ChangePasswordPerUser", "3");
        builder.UseSetting("Inventory:JobsEnabled", "false");
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureServices(services =>
        {
            var sqlServer = services.Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                || d.ServiceType == typeof(DbContextOptions)
                || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)))
                .ToList();
            foreach (var descriptor in sqlServer)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_database)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.AddSingleton<IStartupFilter, LoopbackPeerStartupFilter>();
            if (TokenLifespan is { } lifespan)
            {
                services.PostConfigure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = lifespan);
            }
        });
    }

    /// <summary>A client whose requests come from a fresh public IP (TEST-NET-3).</summary>
    public HttpClient NewClient(string? ip = null)
    {
        var client = CreateClient();
        var n = Interlocked.Increment(ref _nextIp);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip ?? $"203.0.{n / 250 % 250}.{n % 250 + 1}");
        return client;
    }

    public static string NewEmail(string prefix = "user") => $"{prefix}.{Guid.NewGuid():N}@cafecr.invalid";

    public static async Task<JsonElement> RegisterAsync(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password, fullName = "Khách Thử" }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password }, Json);

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }, Json);

    public static Task<HttpResponseMessage> ForgotAsync(HttpClient client, string email, string app = "customer") =>
        client.PostAsJsonAsync("/api/auth/forgot-password", new { email, app }, Json);

    public static Task<HttpResponseMessage> ResetAsync(HttpClient client, string email, string token, string newPassword) =>
        client.PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword }, Json);

    /// <summary>Requests a reset and returns the link and token from the delivered email.</summary>
    public async Task<(string Link, string Token)> RequestResetLinkAsync(HttpClient client, string email, string app = "customer")
    {
        var before = Emails.CountFor(email, PasswordEmailTemplates.ResetSubject);
        Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync(client, email, app)).StatusCode);
        var mail = await Emails.WaitForAsync(email, PasswordEmailTemplates.ResetSubject, before);
        var link = FakeEmailSender.ExtractLink(mail.TextBody);
        Assert.Contains(link, mail.HtmlBody.Replace("&amp;", "&"));
        return (link, QueryHelpers.ParseQuery(new Uri(link).Query)["token"].ToString());
    }

    private sealed class LoopbackPeerStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            // TestServer has no peer address; behave like nginx on loopback so X-Forwarded-For is honoured.
            app.Use((HttpContext context, Func<Task> nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                return nextMiddleware();
            });
            next(app);
        };
    }
}

/// <summary>Same API with reset tokens that expire after one second.</summary>
public sealed class ShortTokenPasswordApiFactory : PasswordApiFactory
{
    protected override TimeSpan? TokenLifespan => TimeSpan.FromSeconds(1);
}

public sealed record SentEmail(string To, string Subject, string HtmlBody, string TextBody);

public sealed partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentEmail> _sent = new();

    public IReadOnlyList<SentEmail> Sent => _sent.ToArray();

    public Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken cancellationToken)
    {
        _sent.Enqueue(new SentEmail(to, subject, htmlBody, textBody));
        return Task.CompletedTask;
    }

    public int CountFor(string to, string subject) =>
        Sent.Count(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase) && m.Subject == subject);

    /// <summary>Waits for the (<paramref name="skip"/>+1)-th email with this subject to this address.</summary>
    public async Task<SentEmail> WaitForAsync(string to, string subject, int skip = 0)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var match = Sent.Where(m => string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase) && m.Subject == subject).Skip(skip).FirstOrDefault();
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"No '{subject}' email to {to}.");
    }

    public static string ExtractLink(string text) => LinkPattern().Match(text).Value;

    [GeneratedRegex(@"https?://\S+reset-password\?\S+")]
    private static partial Regex LinkPattern();
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages.ToArray();

    public ILogger CreateLogger(string categoryName) => new Logger(this);

    public void Dispose()
    {
    }

    private sealed class Logger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            owner._messages.Enqueue(formatter(state, exception) + (exception is null ? "" : " " + exception));
    }
}
