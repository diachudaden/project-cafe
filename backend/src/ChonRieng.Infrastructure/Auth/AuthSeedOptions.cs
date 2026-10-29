namespace ChonRieng.Infrastructure.Auth;

public sealed class AuthSeedOptions
{
    public const string SectionName = "AuthSeed";

    public string Password { get; set; } = string.Empty;
}
