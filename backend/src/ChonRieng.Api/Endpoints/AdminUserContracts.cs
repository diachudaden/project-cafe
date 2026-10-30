using System.ComponentModel.DataAnnotations;

namespace ChonRieng.Api.Endpoints;

/// <summary>Safe user data returned in administrator user lists.</summary>
public sealed record AdminUserResponse(Guid Id, string Email, string FullName, string? PhoneNumber, DateTimeOffset? LockoutEnd);

/// <summary>Safe user data, including roles, returned to administrators.</summary>
public sealed record AdminUserDetailResponse(Guid Id, string Email, string FullName, string? PhoneNumber, DateTimeOffset? LockoutEnd, IReadOnlyList<string> Roles);

/// <summary>Payload for creating a user account as an administrator.</summary>
public sealed record CreateAdminUserRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MinLength(12), MaxLength(128)] string Password,
    [property: Required, MaxLength(150)] string FullName,
    [property: Phone, MaxLength(32)] string? PhoneNumber,
    [property: Required, MinLength(1)] IReadOnlyList<string> Roles);

/// <summary>Payload for replacing a user profile and role set.</summary>
public sealed record UpdateAdminUserRequest(
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MaxLength(150)] string FullName,
    [property: Phone, MaxLength(32)] string? PhoneNumber,
    [property: Required, MinLength(1)] IReadOnlyList<string> Roles);
