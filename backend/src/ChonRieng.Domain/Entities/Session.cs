using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Session : Entity
{
    public Guid SeatId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid PricePlanId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    public DateTimeOffset? ClosedAt { get; set; }
    /// <summary>Null while active.</summary>
    public SessionCloseReason? CloseReason { get; set; }
    /// <summary>Staff member who pressed "Trả chỗ"; null when the system released the seat.</summary>
    public Guid? ClosedByUserId { get; set; }

    public Seat Seat { get; set; } = null!;
    public Customer? Customer { get; set; }
    public PricePlan PricePlan { get; set; } = null!;
    public ICollection<SessionExtension> Extensions { get; } = new List<SessionExtension>();
    public Bill? Bill { get; set; }
}
