namespace ChonRieng.Api.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "CafeCR";
    public string Audience { get; set; } = "CafeCR.Api";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
