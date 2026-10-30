using System.ComponentModel.DataAnnotations;

namespace ChonRieng.Api.Endpoints;

/// <summary>Payload for creating a customer account.</summary>
public sealed record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MinLength(12), MaxLength(128)]
    public required string Password { get; init; }

    [Required, MaxLength(150)]
    public required string FullName { get; init; }

    [Phone, MaxLength(32)]
    public string? PhoneNumber { get; init; }

    [MaxLength(200)]
    public string? DeviceId { get; init; }
}

/// <summary>Credentials for signing in to an existing account.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MaxLength(128)]
    public required string Password { get; init; }

    [MaxLength(200)]
    public string? DeviceId { get; init; }
}

/// <summary>Payload for exchanging a refresh token for a new token pair.</summary>
public sealed record RefreshTokenRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}

/// <summary>Payload for revoking a refresh token.</summary>
public sealed record RevokeTokenRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}

/// <summary>Safe account information returned by authentication endpoints.</summary>
public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string FullName,
    string? PhoneNumber,
    IReadOnlyList<string> Roles);

/// <summary>Access and rotating refresh credentials returned after authentication.</summary>
public sealed record AuthTokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthUserResponse User);

/// <summary>Result of creating an account, including safe validation errors on failure.</summary>
public sealed record RegistrationResult(
    AuthTokenResponse? Tokens,
    IReadOnlyDictionary<string, string[]> Errors);
