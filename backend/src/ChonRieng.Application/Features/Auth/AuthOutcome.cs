using ChonRieng.Application.Features.Auth.Dtos;

namespace ChonRieng.Application.Features.Auth;

public sealed class AuthOutcome
{
    public required bool Succeeded { get; init; }
    public required int StatusCode { get; init; }
    public string? ErrorType { get; init; }
    public string? Detail { get; init; }
    public AuthResponse? Body { get; init; }
    public string? RefreshToken { get; init; }

    public static AuthOutcome Ok(AuthResponse body, string refreshToken) => new()
    {
        Succeeded = true,
        StatusCode = 200,
        Body = body,
        RefreshToken = refreshToken,
    };

    public static AuthOutcome Fail(int statusCode, string errorType, string detail) => new()
    {
        Succeeded = false,
        StatusCode = statusCode,
        ErrorType = errorType,
        Detail = detail,
    };

    public static AuthOutcome LoggedOut() => new()
    {
        Succeeded = true,
        StatusCode = 204,
    };
}
