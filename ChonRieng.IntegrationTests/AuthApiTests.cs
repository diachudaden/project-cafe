using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChonRieng.IntegrationTests;

[Collection(AuthApiSerialCollection.Name)]
public class AuthApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AuthApiFactory _factory;

    public AuthApiTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_AdminWithEmail_ReturnsUserAndAccessToken()
    {
        var client = CreateClient();

        var response = await LoginAsync(client, "admin@chonrieng.local", _factory.TestPassword, "admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<AuthResponseDto>(response);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal(900, body.ExpiresIn);
        Assert.Equal("Admin", body.User.Role);
        Assert.Equal("admin@chonrieng.local", body.User.Email);
        var refreshCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("cr_refresh=", refreshCookie);
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", refreshCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_StaffWithPhone_SucceedsOnPos()
    {
        var client = CreateClient();

        var local = await LoginAsync(client, "0901234567", _factory.TestPassword, "pos");
        Assert.Equal(HttpStatusCode.OK, local.StatusCode);

        var plus = await LoginAsync(client, "+84901234567", _factory.TestPassword, "pos");
        Assert.Equal(HttpStatusCode.OK, plus.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_DoesNotRevealAccount()
    {
        var client = CreateClient();

        var response = await LoginAsync(client, "admin@chonrieng.local", "wrong-password", "admin");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadAsync<ProblemDto>(response);
        Assert.Equal("auth.invalid_credentials", problem.Type);
        Assert.Equal("Email/số điện thoại hoặc mật khẩu không đúng.", problem.Detail);
    }

    [Fact]
    public async Task Login_UnknownUser_SameAsWrongPassword()
    {
        var client = CreateClient();

        var response = await LoginAsync(client, "nobody@chonrieng.local", _factory.TestPassword, "admin");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadAsync<ProblemDto>(response);
        Assert.Equal("auth.invalid_credentials", problem.Type);
    }

    [Fact]
    public async Task Login_StaffOnAdmin_ForbiddenClient()
    {
        var client = CreateClient();

        var response = await LoginAsync(client, "staff@chonrieng.local", _factory.TestPassword, "admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await ReadAsync<ProblemDto>(response);
        Assert.Equal("auth.forbidden_client", problem.Type);
    }

    [Fact]
    public async Task Login_MissingFields_Validation()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { identifier = "", password = "", client = "nope" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadAsync<ProblemDto>(response);
        Assert.Equal("validation", problem.Type);
    }

    [Fact]
    public async Task Me_WithAccessToken_ReturnsCurrentUser()
    {
        var client = CreateClient();
        var login = await ReadAsync<AuthResponseDto>(await LoginAsync(client, "customer@chonrieng.local", _factory.TestPassword, "customer"));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var me = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await ReadAsync<AuthUserDto>(me);
        Assert.Equal("Customer", user.Role);
        Assert.Equal("customer@chonrieng.local", user.Email);
    }

    [Fact]
    public async Task Me_WithoutToken_InvalidToken()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadAsync<ProblemDto>(response);
        Assert.Equal("auth.invalid_token", problem.Type);
    }

    [Fact]
    public async Task Refresh_RotatesCookieAndIssuesNewAccessToken()
    {
        var client = CreateClient();
        var loginResponse = await LoginAsync(client, "manager@chonrieng.local", _factory.TestPassword, "admin");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var firstCookie = GetCookieValue(loginResponse, "cr_refresh");

        var refresh = await client.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var second = await ReadAsync<AuthResponseDto>(refresh);
        Assert.False(string.IsNullOrWhiteSpace(second.AccessToken));
        Assert.NotEqual(firstCookie, GetCookieValue(refresh, "cr_refresh"));
        Assert.Equal("BranchManager", second.User.Role);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_SessionRevoked()
    {
        var client = CreateClient();

        var response = await client.PostAsync("/api/v1/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadAsync<ProblemDto>(response);
        Assert.Equal("auth.session_revoked", problem.Type);
    }

    [Fact]
    public async Task Logout_RevokesRefreshAndIsIdempotent()
    {
        var client = CreateClient();
        await LoginAsync(client, "admin@chonrieng.local", _factory.TestPassword, "admin");

        var logout = await client.PostAsync("/api/v1/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await client.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        var logoutAgain = await client.PostAsync("/api/v1/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutAgain.StatusCode);
    }

    [Fact]
    public async Task Login_Pos_RevokesPreviousPosSession()
    {
        var first = CreateClient();
        var second = CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(first, "staff@chonrieng.local", _factory.TestPassword, "pos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(second, "staff@chonrieng.local", _factory.TestPassword, "pos")).StatusCode);

        var stale = await first.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);

        var active = await second.PostAsync("/api/v1/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
    }

    [Fact]
    public async Task Login_Lockout_AfterFiveFailedAttempts()
    {
        var client = CreateClient();
        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await LoginAsync(client, "lockout@chonrieng.local", "bad-password", "customer");
        }

        Assert.Equal(HttpStatusCode.Unauthorized, last.StatusCode);
        var problem = await ReadAsync<ProblemDto>(last);
        Assert.Equal("auth.account_locked", problem.Type);
    }

    [Fact]
    public async Task OpenApi_RequiresBearerOnlyForProtectedEndpoint()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(document);
        var paths = document["paths"]!.AsObject();
        Assert.Null(paths["/api/v1/auth/login"]!["post"]!["security"]);
        Assert.Null(paths["/api/v1/auth/refresh"]!["post"]!["security"]);
        Assert.Null(paths["/api/v1/auth/logout"]!["post"]!["security"]);
        Assert.Equal("Bearer", paths["/api/v1/auth/me"]!["get"]!["security"]![0]!.AsObject().Single().Key);
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOriginsWithCredentials()
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string identifier, string password, string app) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            identifier,
            password,
            client = app,
        });

    private static string GetCookieValue(HttpResponseMessage response, string name)
    {
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith($"{name}=", StringComparison.Ordinal));
        return cookie.Split(';', 2)[0][(name.Length + 1)..];
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
        Assert.NotNull(value);
        return value;
    }

    private sealed class AuthResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public AuthUserDto User { get; set; } = new();
    }

    private sealed class AuthUserDto
    {
        public Guid Id { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public List<Guid> BranchIds { get; set; } = [];
    }

    private sealed class ProblemDto
    {
        public string? Type { get; set; }
        public string? Detail { get; set; }
    }
}
