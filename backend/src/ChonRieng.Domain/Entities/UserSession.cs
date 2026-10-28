using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class UserSession : Entity
{
    public Guid UserId { get; set; }
    public string DeviceId { get; set; } = "";
    public bool IsPos { get; set; }
    public Guid? BranchId { get; set; }
    public string RefreshTokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public User User { get; set; } = null!;
    public Branch? Branch { get; set; }
}
