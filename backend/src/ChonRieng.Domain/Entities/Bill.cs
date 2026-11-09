using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Bill : Entity
{
    public string Code { get; set; } = "";
    public Guid BranchId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid SessionId { get; set; }
    public decimal Total { get; set; }
    public decimal CashPaidTotal { get; set; }
    public decimal VietQrPaidTotal { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public Guid? ClaimedByCustomerId { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Branch Branch { get; set; } = null!;
    public Shift Shift { get; set; } = null!;
    public Session Session { get; set; } = null!;
    public Customer? ClaimedByCustomer { get; set; }
    public ICollection<PointTransaction> PointTransactions { get; } = new List<PointTransaction>();
}
