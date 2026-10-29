namespace ChonRieng.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "ChonRieng";
    public string Audience { get; set; } = "ChonRieng";
    public int AccessTokenSeconds { get; set; } = 900;
}
