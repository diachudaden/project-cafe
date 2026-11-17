using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChonRieng.Api.Endpoints;
using ChonRieng.Api.Services.Email;
using Microsoft.AspNetCore.WebUtilities;
using static ChonRieng.IntegrationTests.PasswordApiFactory;

namespace ChonRieng.IntegrationTests;

public sealed class PasswordResetTests(PasswordApiFactory factory) : IClassFixture<PasswordApiFactory>
{
    private const string NewPassword = "New-Password-456!";

    [Fact]
    public async Task Forgot_password_answers_identically_for_registered_and_unknown_emails_and_mails_only_registered()
    {
        var client = factory.NewClient();
        var registered = NewEmail();
        var unknown = NewEmail("unknown");
        await RegisterAsync(client, registered);

        var unknownResponse = await ForgotAsync(client, unknown);
        var registeredResponse = await ForgotAsync(client, registered);

        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);
        Assert.Equal(unknownResponse.StatusCode, registeredResponse.StatusCode);
        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();
        Assert.Equal(unknownBody, await registeredResponse.Content.ReadAsStringAsync());
        Assert.Contains("Nếu email đã đăng ký", JsonDocument.Parse(unknownBody).RootElement.GetProperty("message").GetString());
        Assert.Equal(unknownResponse.Headers.Location, registeredResponse.Headers.Location);

        // The queue is FIFO: once the registered mail is out, a mail for the unknown address would already be too.
        await factory.Emails.WaitForAsync(registered, PasswordEmailTemplates.ResetSubject);
        Assert.DoesNotContain(factory.Emails.Sent, m => m.To == unknown);
    }

    [Theory]
    [InlineData("customer", "/reset-password")]
    [InlineData("admin", "/admin/reset-password")]
    [InlineData("pos", "/pos/reset-password")]
    public async Task Reset_link_uses_the_app_path_and_the_configured_public_host(string app, string path)
    {
        var client = factory.NewClient();
        client.DefaultRequestHeaders.Host = "evil.example";
        var email = NewEmail("Link+Tag");
        await RegisterAsync(client, email);

        var (link, token) = await factory.RequestResetLinkAsync(client, email, app);

        var uri = new Uri(link);
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("cafe.test", uri.Host);
        Assert.Equal(path, uri.AbsolutePath);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal(email, query["email"].ToString());
        Assert.Contains("%2B", uri.Query); // '+' in the email is escaped, not turned into a space
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public async Task Unknown_app_is_rejected_by_validation()
    {
        var response = await ForgotAsync(factory.NewClient(), NewEmail(), "https://evil.example");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Successful_reset_changes_the_password_revokes_every_session_and_clears_the_lockout()
    {
        var client = factory.NewClient();
        var email = NewEmail();
        var registration = await RegisterAsync(client, email);
        var firstRefresh = registration.GetProperty("refreshToken").GetString()!;
        var secondLogin = await LoginAsync(client, email, Password);
        var secondRefresh = (await secondLogin.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("refreshToken").GetString()!;
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, email, "Wrong-Password-000!")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, email, Password)).StatusCode); // locked out

        var (link, token) = await factory.RequestResetLinkAsync(client, email);
        var response = await ResetAsync(client, email, token, NewPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, firstRefresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, secondRefresh)).StatusCode);

        var changed = await factory.Emails.WaitForAsync(email, PasswordEmailTemplates.ChangedSubject);
        Assert.Contains("Nếu không phải bạn, hãy liên hệ quản trị viên ngay", changed.TextBody);
        Assert.Contains("(giờ Việt Nam)", changed.TextBody);

        // Nothing secret reaches the logs.
        var logs = string.Join('\n', factory.Logs.Messages);
        Assert.DoesNotContain(token, logs);
        Assert.DoesNotContain(link, logs);
        Assert.DoesNotContain(NewPassword, logs);
        Assert.DoesNotContain(email, logs);
    }

    [Fact]
    public async Task Wrong_used_and_unknown_email_tokens_all_return_the_same_400()
    {
        var client = factory.NewClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        var (_, token) = await factory.RequestResetLinkAsync(client, email);
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(client, email, token, NewPassword)).StatusCode);

        var otherClient = factory.NewClient();
        await AssertInvalidLinkAsync(await ResetAsync(client, email, token, "Another-Password-789!"));        // used
        await AssertInvalidLinkAsync(await ResetAsync(client, email, token[..^4] + "AAAA", NewPassword));       // tampered
        await AssertInvalidLinkAsync(await ResetAsync(client, email, "not*base64url!", NewPassword));          // garbage
        await AssertInvalidLinkAsync(await ResetAsync(otherClient, NewEmail("nobody"), token, NewPassword));   // unknown email
    }

    [Fact]
    public async Task Weak_password_returns_validation_problem_and_keeps_the_link_usable()
    {
        var client = factory.NewClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        var (_, token) = await factory.RequestResetLinkAsync(client, email);

        var weak = await ResetAsync(client, email, token, "short");

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var problem = await weak.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(problem.TryGetProperty("errors", out _));
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(client, email, token, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_is_limited_per_ip_and_per_email()
    {
        // Per IP (4 in the test factory): different emails, one address.
        var client = factory.NewClient();
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync(client, NewEmail())).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await ForgotAsync(client, NewEmail())).StatusCode);

        // Per email (2): one email, different addresses; same for unknown and registered emails.
        var email = NewEmail();
        Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync(factory.NewClient(), email)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await ForgotAsync(factory.NewClient(), email.ToUpperInvariant())).StatusCode);
        var limited = await ForgotAsync(factory.NewClient(), email);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Contains("thử lại sau", await limited.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reset_password_is_limited_per_ip()
    {
        var client = factory.NewClient();
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(client, NewEmail(), "AAAA", NewPassword)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await ResetAsync(client, NewEmail(), "AAAA", NewPassword)).StatusCode);
    }

    internal static async Task AssertInvalidLinkAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(AuthEndpoints.InvalidResetLinkMessage, problem.GetProperty("title").GetString());
        Assert.Equal("INVALID_RESET_LINK", problem.GetProperty("code").GetString());
        Assert.False(problem.TryGetProperty("errors", out _));
    }
}

public sealed class PasswordResetExpiryTests(ShortTokenPasswordApiFactory factory) : IClassFixture<ShortTokenPasswordApiFactory>
{
    [Fact]
    public async Task Expired_token_returns_the_same_400()
    {
        var client = factory.NewClient();
        var email = NewEmail();
        await RegisterAsync(client, email);
        var (_, token) = await factory.RequestResetLinkAsync(client, email);

        await Task.Delay(TimeSpan.FromSeconds(2.5));

        await PasswordResetTests.AssertInvalidLinkAsync(await ResetAsync(client, email, token, "New-Password-456!"));
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email, Password)).StatusCode);
    }
}
