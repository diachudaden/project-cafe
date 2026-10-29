namespace ChonRieng.Infrastructure.Auth;

public sealed class AuthCookieOptions
{
    public const string SectionName = "Auth";

    public int RefreshTokenDays { get; set; } = 7;
    public string CookieName { get; set; } = "cr_refresh";
    public string CookiePath { get; set; } = "/api/v1/auth";
    public string CookieSameSite { get; set; } = "Lax";
}
