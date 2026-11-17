using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ChonRieng.Api.Services;

/// <summary>
/// Section <c>AuthRateLimits</c>: fixed windows for the anonymous auth endpoints. Per-IP limits rely on
/// <c>UseForwardedHeaders</c> to see the client behind Caddy/nginx; a branch behind one NAT shares its IP bucket.
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "AuthRateLimits";

    [Range(1, 10_000)] public int LoginPerIp { get; set; } = 30;
    [Range(1, 1440)] public int LoginWindowMinutes { get; set; } = 5;
    [Range(1, 10_000)] public int RegisterPerIp { get; set; } = 10;
    [Range(1, 1440)] public int RegisterWindowMinutes { get; set; } = 60;
    [Range(1, 10_000)] public int ForgotPasswordPerIp { get; set; } = 5;
    [Range(1, 10_000)] public int ForgotPasswordPerEmail { get; set; } = 5;
    [Range(1, 1440)] public int ForgotPasswordWindowMinutes { get; set; } = 15;
    [Range(1, 10_000)] public int ResetPasswordPerIp { get; set; } = 10;
    [Range(1, 1440)] public int ResetPasswordWindowMinutes { get; set; } = 15;
    [Range(1, 10_000)] public int ChangePasswordPerUser { get; set; } = 5;
    [Range(1, 1440)] public int ChangePasswordWindowMinutes { get; set; } = 15;
}

public static class AuthRateLimits
{
    public const string Login = "auth-login";
    public const string Register = "auth-register";
    public const string ForgotPassword = "auth-forgot-password";
    public const string ResetPassword = "auth-reset-password";
    public const string ChangePassword = "auth-change-password";

    public const string TooManyRequestsDetail = "Bạn đã thử quá nhiều lần. Vui lòng thử lại sau ít phút.";

    public static IServiceCollection AddAuthRateLimits(this IServiceCollection services)
    {
        services.AddOptions<AuthRateLimitOptions>()
            .BindConfiguration(AuthRateLimitOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<ForgotPasswordEmailLimiter>();
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Login, context => PerIp(context, o => (o.LoginPerIp, o.LoginWindowMinutes)));
            options.AddPolicy(Register, context => PerIp(context, o => (o.RegisterPerIp, o.RegisterWindowMinutes)));
            options.AddPolicy(ForgotPassword, context => PerIp(context, o => (o.ForgotPasswordPerIp, o.ForgotPasswordWindowMinutes)));
            options.AddPolicy(ResetPassword, context => PerIp(context, o => (o.ResetPasswordPerIp, o.ResetPasswordWindowMinutes)));
            options.AddPolicy(ChangePassword, context =>
            {
                var o = Options(context);
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ClientIp(context),
                    _ => Window(o.ChangePasswordPerUser, o.ChangePasswordWindowMinutes));
            });
        });
        return services;
    }

    public static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    internal static FixedWindowRateLimiterOptions Window(int permits, int minutes) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(minutes),
        QueueLimit = 0,
    };

    private static AuthRateLimitOptions Options(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;

    private static RateLimitPartition<string> PerIp(HttpContext context, Func<AuthRateLimitOptions, (int Permits, int Minutes)> select)
    {
        var (permits, minutes) = select(Options(context));
        return RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => Window(permits, minutes));
    }
}

/// <summary>
/// Per-email limit for forgot-password, applied before the user lookup so a registered and an unknown email are
/// throttled identically. The key is the normalized email; it is never logged.
/// </summary>
public sealed class ForgotPasswordEmailLimiter(IOptions<AuthRateLimitOptions> options) : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(email =>
        RateLimitPartition.GetFixedWindowLimiter(email, _ => AuthRateLimits.Window(
            options.Value.ForgotPasswordPerEmail, options.Value.ForgotPasswordWindowMinutes)));

    public bool TryAcquire(string email)
    {
        using var lease = _limiter.AttemptAcquire(email.Trim().ToUpperInvariant());
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
