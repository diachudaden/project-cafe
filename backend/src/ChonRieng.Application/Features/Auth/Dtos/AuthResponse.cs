namespace ChonRieng.Application.Features.Auth.Dtos;

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }
    public required int ExpiresIn { get; init; }
    public required AuthUserDto User { get; init; }
}
