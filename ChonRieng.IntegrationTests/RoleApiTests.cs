using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChonRieng.Api.Services;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChonRieng.IntegrationTests;

/// <summary>Boots the real API (JWT, policies, rate limiting, Swagger) on an in-memory database with the demo seed.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SeedPassword = "Seed-Test-Only-123!";
    public static readonly string WebhookSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly string _database = $"RoleApiTests-{Guid.NewGuid()}";

    static ApiFactory()
    {
        // Program reads these before the test host can override configuration.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=unused-in-memory");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-test-signing-key-at-least-32-bytes-long");
        Environment.SetEnvironmentVariable("Payment__WebhookSecret", WebhookSecret);
        Environment.SetEnvironmentVariable("BootstrapAdmin__Email", "admin.test@cafecr.invalid");
        Environment.SetEnvironmentVariable("BootstrapAdmin__Password", "Admin-Test-Only-123!");
        Environment.SetEnvironmentVariable("DevelopmentSeed__Password", SeedPassword);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
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

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_database));
        });
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await DevelopmentDataSeeder.SeedAsync(scope.ServiceProvider, Services.GetRequiredService<IConfiguration>(), NullLogger.Instance, default);
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}

public sealed class RoleApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task SePay_webhook_accepts_a_signed_event_through_the_real_http_pipeline_and_replay_is_safe()
    {
        var client = factory.CreateClient();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var transactionId = $"pipeline-{Guid.NewGuid():N}";
        var body = $$"""
            {
              "id": "{{transactionId}}",
              "transactionDate": "2026-11-02 10:00:00",
              "accountNumber": "0000000000",
              "transferType": "out",
              "transferAmount": 1
            }
            """;
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(ApiFactory.WebhookSecret),
            Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();

        async Task<HttpResponseMessage> SendWebhookAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/hooks/sepay-payment")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-SePay-Timestamp", timestamp);
            request.Headers.Add("X-SePay-Signature", $"sha256={signature}");
            return await client.SendAsync(request);
        }

        using var first = await SendWebhookAsync();
        using var retry = await SendWebhookAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.PaymentTransactions.CountAsync(transaction => transaction.ProviderTxId == transactionId));
    }

    [Fact]
    public async Task Login_returns_branch_ids_for_staff_and_none_for_customers()
    {
        var staff = await LoginAsync("staff01.demo@cafecr.invalid");
        var customer = await LoginAsync("customer01.demo@cafecr.invalid");

        Assert.Single(staff.GetProperty("user").GetProperty("branchIds").EnumerateArray());
        Assert.Empty(customer.GetProperty("user").GetProperty("branchIds").EnumerateArray());
    }

    [Fact]
    public async Task Customers_browse_public_branches_but_cannot_use_admin_or_staff_apis()
    {
        var anonymous = factory.CreateClient();
        var branches = await anonymous.GetFromJsonAsync<JsonElement>("/api/branches?lat=10.78&lng=106.69", Json);
        Assert.True(branches.GetArrayLength() >= 5);
        Assert.True(branches[0].GetProperty("distanceKm").GetDouble() >= 0);

        var customer = await ClientAsync("customer01.demo@cafecr.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/branches")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/shifts/open")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/me/points")).StatusCode);
    }

    [Fact]
    public async Task Staff_see_only_their_branch_floor_and_cannot_open_management_apis()
    {
        var staff = await ClientAsync("staff01.demo@cafecr.invalid");
        var (branchA, branchC) = (await BranchIdAsync("Chi nhánh A"), await BranchIdAsync("Chi nhánh C"));

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/branches/{branchA}/floor")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/branches/{branchC}/floor")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/dashboard/today")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/me/shifts?from={Today}&to={Today.AddDays(7)}")).StatusCode);
    }

    [Fact]
    public async Task Managers_get_dashboard_and_schedule_only_for_their_branches()
    {
        var manager = await ClientAsync("manager01.demo@cafecr.invalid");
        var (branchA, branchB) = (await BranchIdAsync("Chi nhánh A"), await BranchIdAsync("Chi nhánh B"));

        var dashboard = await manager.GetFromJsonAsync<JsonElement>("/api/dashboard/today", Json);
        Assert.True(dashboard.GetProperty("seats").GetInt32() > 0);
        var shifts = await manager.GetFromJsonAsync<JsonElement>($"/api/shifts?branchId={branchA}&from={Today}&to={Today.AddDays(6)}", Json);
        Assert.True(shifts.GetArrayLength() > 0);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/shifts?branchId={branchB}&from={Today}&to={Today}")).StatusCode);
    }

    [Fact]
    public async Task Admin_runs_a_session_through_check_in_extend_and_checkout()
    {
        var admin = await ClientAsync("admin01.demo@cafecr.invalid");
        var branchC = await BranchIdAsync("Chi nhánh C");
        var floor = await admin.GetFromJsonAsync<JsonElement>($"/api/branches/{branchC}/floor", Json);
        var seatId = floor.GetProperty("zones").EnumerateArray()
            .SelectMany(z => z.GetProperty("seats").EnumerateArray())
            .First(s => s.GetProperty("state").GetString() == "Available").GetProperty("id").GetGuid();
        var plan = (await admin.GetFromJsonAsync<JsonElement>($"/api/branches/{branchC}/price-plans", Json))[0];

        var checkIn = await admin.PostAsJsonAsync("/api/sessions", new { seatId, pricePlanId = plan.GetProperty("id").GetGuid(), paymentMethod = "Cash" }, Json);
        Assert.Equal(HttpStatusCode.Created, checkIn.StatusCode);
        var session = await checkIn.Content.ReadFromJsonAsync<JsonElement>(Json);
        var sessionId = session.GetProperty("id").GetGuid();

        var again = await admin.PostAsJsonAsync("/api/sessions", new { seatId, pricePlanId = plan.GetProperty("id").GetGuid(), paymentMethod = "Cash" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var extended = await (await admin.PostAsync($"/api/sessions/{sessionId}/extend", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(plan.GetProperty("price").GetDecimal() + plan.GetProperty("extendPrice").GetDecimal(), extended.GetProperty("bill").GetProperty("total").GetDecimal());

        var closed = await admin.PostAsync($"/api/sessions/{sessionId}/close", null);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        var code = session.GetProperty("bill").GetProperty("code").GetString();
        var customer = await ClientAsync("customer05.demo@cafecr.invalid");
        var claim = await customer.PostAsJsonAsync("/api/me/bills/claim", new { code }, Json);
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostAsJsonAsync("/api/me/bills/claim", new { code }, Json)).StatusCode);
    }

    [Fact]
    public async Task Bill_claims_are_rate_limited_per_customer()
    {
        var customer = await ClientAsync("customer20.demo@cafecr.invalid");
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
        {
            statuses.Add((await customer.PostAsJsonAsync("/api/me/bills/claim", new { code = "ZZZZZZZZ" }, Json)).StatusCode);
        }

        Assert.All(statuses.Take(10), s => Assert.Equal(HttpStatusCode.NotFound, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task Workflow_tables_are_read_only_in_generic_admin_crud_and_swagger_builds()
    {
        var admin = await ClientAsync("admin01.demo@cafecr.invalid");

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/bills?page=1&pageSize=5")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.PostAsJsonAsync("/api/admin/bills", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);

    private async Task<JsonElement> LoginAsync(string email)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = ApiFactory.SeedPassword }, Json);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private async Task<HttpClient> ClientAsync(string email)
    {
        var tokens = await LoginAsync(email);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<Guid> BranchIdAsync(string suffix)
    {
        var branches = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/branches", Json);
        return branches.EnumerateArray().Single(b => b.GetProperty("name").GetString()!.EndsWith(suffix)).GetProperty("id").GetGuid();
    }
}
