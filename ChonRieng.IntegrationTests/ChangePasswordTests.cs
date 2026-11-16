using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChonRieng.Api.Services.Email;
using static ChonRieng.IntegrationTests.PasswordApiFactory;

namespace ChonRieng.IntegrationTests;

public sealed class ChangePasswordTests(PasswordApiFactory factory) : IClassFixture<PasswordApiFactory>
{
    private const string NewPassword = "Changed-Password-789!";

    [Fact]
    public async Task Change_keeps_the_current_session_and_signs_out_the_others()
    {
        var client = factory.NewClient();
        var email = NewEmail();
        var current = await RegisterAsync(client, email);
        var other = await (await LoginAsync(client, email, Password)).Content.ReadFromJsonAsync<JsonElement>(Json);

        var response = await ChangeAsync(client, current, Password, NewPassword, keepSession: true);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(client, Token(current, "refreshToken"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, Token(other, "refreshToken"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, email, Password)).StatusCode);
        await factory.Emails.WaitForAsync(email, PasswordEmailTemplates.ChangedSubject);
    }

    [Fact]
    public async Task Change_without_a_refresh_token_signs_out_every_session()
    {
        var client = factory.NewClient();
        var current = await RegisterAsync(client, NewEmail());

        Assert.Equal(HttpStatusCode.NoContent, (await ChangeAsync(client, current, Password, NewPassword, keepSession: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, Token(current, "refreshToken"))).StatusCode);
    }

    [Fact]
    public async Task Wrong_current_password_is_400_on_currentPassword_is_rate_limited_and_does_not_count_towards_lockout()
    {
        var client = factory.NewClient();
        var email = NewEmail();
        var current = await RegisterAsync(client, email);

        for (var i = 0; i < 3; i++)
        {
            var wrong = await ChangeAsync(client, current, "Wrong-Password-000!", NewPassword, keepSession: true);
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
            var problem = await wrong.Content.ReadFromJsonAsync<JsonElement>(Json);
            Assert.True(problem.GetProperty("errors").TryGetProperty("currentPassword", out _));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await ChangeAsync(client, current, "Wrong-Password-000!", NewPassword, keepSession: true)).StatusCode);

        // 3 wrong changes + 4 wrong sign-ins would reach the 5-failure lockout if the changes counted.
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, email, "Wrong-Password-000!")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email, Password)).StatusCode);
    }

    [Fact]
    public async Task Weak_or_unchanged_new_password_is_a_validation_problem()
    {
        var client = factory.NewClient();
        var current = await RegisterAsync(client, NewEmail());

        var weak = await ChangeAsync(client, current, Password, "weakpassword", keepSession: true);
        var same = await ChangeAsync(client, current, Password, Password, keepSession: true);

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.True((await weak.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("errors").TryGetProperty("Identity", out _));
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.True((await same.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("errors").TryGetProperty("newPassword", out _));
    }

    [Fact]
    public async Task Change_requires_sign_in()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = Password, newPassword = NewPassword }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_and_register_are_limited_per_ip()
    {
        var client = factory.NewClient();
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, NewEmail("nobody"), "Wrong-Password-000!")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(client, NewEmail("nobody"), "Wrong-Password-000!")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(factory.NewClient(), NewEmail("nobody"), "Wrong-Password-000!")).StatusCode);

        var registering = factory.NewClient();
        for (var i = 0; i < 20; i++)
        {
            await RegisterAsync(registering, NewEmail());
        }

        var limited = await registering.PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail(), password = Password, fullName = "Khách Thử" }, Json);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/forgot-password", "202,400,429")]
    [InlineData("/api/auth/reset-password", "204,400,429")]
    [InlineData("/api/auth/change-password", "204,400,401,429")]
    [InlineData("/api/auth/login", "200,401,429")]
    public async Task Swagger_documents_the_status_codes(string path, string codes)
    {
        var swagger = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", Json);
        var responses = swagger.GetProperty("paths").GetProperty(path).GetProperty("post").GetProperty("responses");

        foreach (var code in codes.Split(','))
        {
            Assert.True(responses.TryGetProperty(code, out _), $"{path} is missing {code}");
        }
    }

    private static string Token(JsonElement tokens, string name) => tokens.GetProperty(name).GetString()!;

    private static async Task<HttpResponseMessage> ChangeAsync(HttpClient client, JsonElement tokens, string currentPassword, string newPassword, bool keepSession)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(new
            {
                currentPassword,
                newPassword,
                refreshToken = keepSession ? Token(tokens, "refreshToken") : null,
            }, options: Json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(tokens, "accessToken"));
        return await client.SendAsync(request);
    }
}
