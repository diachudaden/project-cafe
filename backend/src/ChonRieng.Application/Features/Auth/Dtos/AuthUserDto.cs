namespace ChonRieng.Application.Features.Auth.Dtos;

public sealed class AuthUserDto
{
    public required Guid Id { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public required string FullName { get; init; }
    public required string Role { get; init; }
    public required IReadOnlyList<Guid> BranchIds { get; init; }
}
