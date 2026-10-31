using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class SessionExtension : Entity
{
    public Guid SessionId { get; set; }
    public int AddedMinutes { get; set; }
    public decimal Price { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Session Session { get; set; } = null!;
}
