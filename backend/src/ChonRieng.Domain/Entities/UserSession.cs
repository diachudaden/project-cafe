namespace ChonRieng.Domain.Entities;

public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public required string Client { get; set; }
    public required string RefreshTokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && ExpiresAt > now;
}
