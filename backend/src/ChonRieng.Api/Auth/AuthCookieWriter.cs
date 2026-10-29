using ChonRieng.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace ChonRieng.Api.Auth;

public sealed class AuthCookieWriter
{
    public const string CookieName = "cr_refresh";

    private readonly AuthCookieOptions _options;
    private readonly IHostEnvironment _environment;

    public AuthCookieWriter(IOptions<AuthCookieOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public void SetRefreshToken(HttpResponse response, string refreshToken)
    {
        response.Cookies.Append(CookieName, refreshToken, CreateOptions(expires: true));
    }

    public void ClearRefreshToken(HttpResponse response)
    {
        response.Cookies.Delete(CookieName, CreateOptions(expires: false));
    }

    public string? ReadRefreshToken(HttpRequest request) =>
        request.Cookies.TryGetValue(CookieName, out var value) ? value : null;

    private CookieOptions CreateOptions(bool expires)
    {
        var sameSite = ParseSameSite(_options.CookieSameSite);
        var secure = !_environment.IsDevelopment() || sameSite == SameSiteMode.None;
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Path = _options.CookiePath,
            IsEssential = true,
        };

        if (expires)
        {
            options.Expires = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays);
        }

        return options;
    }

    private static SameSiteMode ParseSameSite(string value) =>
        value.Equals("None", StringComparison.OrdinalIgnoreCase)
            ? SameSiteMode.None
            : SameSiteMode.Lax;
}
