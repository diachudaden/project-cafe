using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class PaymentRequest : Entity
{
    public string Code { get; set; } = "";
    public PaymentRequestKind Kind { get; set; }
    public Guid BranchId { get; set; }
    public Guid SeatId { get; set; }
    public Guid PricePlanId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid ShiftId { get; set; }
    public decimal Amount { get; set; }
    public PaymentRequestStatus Status { get; set; } = PaymentRequestStatus.Pending;
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public Guid? BillId { get; set; }
    /// <summary>AddOn requests only: JSON snapshot of the lines (product id, name, unit price, quantity) being paid for.</summary>
    public string? Items { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Branch Branch { get; set; } = null!;
    public Seat Seat { get; set; } = null!;
    public PricePlan PricePlan { get; set; } = null!;
    public Customer? Customer { get; set; }
    public Session? Session { get; set; }
    public Shift? Shift { get; set; }
    public Bill? Bill { get; set; }
    public ICollection<PaymentTransaction> Transactions { get; } = new List<PaymentTransaction>();
}
