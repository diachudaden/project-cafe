using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Notification : Entity
{
    public Guid UserId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? Link { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
