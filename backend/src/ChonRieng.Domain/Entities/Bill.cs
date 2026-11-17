using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

/// <summary>
/// A paid bill. <see cref="BillKind.Session"/> bills belong to one session (<see cref="SessionId"/>, unique);
/// <see cref="BillKind.AddOn"/> bills have no <see cref="SessionId"/> and point at the session bill through <see cref="ParentBillId"/> (D34).
/// </summary>
public sealed class Bill : Entity
{
    public string Code { get; set; } = "";
    public Guid BranchId { get; set; }
    public Guid ShiftId { get; set; }
    public BillKind Kind { get; set; } = BillKind.Session;
    public Guid? SessionId { get; set; }
    public Guid? ParentBillId { get; set; }
    public decimal Total { get; set; }
    public decimal CashPaidTotal { get; set; }
    public decimal VietQrPaidTotal { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public Guid? ClaimedByCustomerId { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Branch Branch { get; set; } = null!;
    public Shift Shift { get; set; } = null!;
    public Session? Session { get; set; }
    public Bill? ParentBill { get; set; }
    public ICollection<Bill> AddOnBills { get; } = new List<Bill>();
    public Customer? ClaimedByCustomer { get; set; }
    public ICollection<PointTransaction> PointTransactions { get; } = new List<PointTransaction>();
    public ICollection<BillItem> Items { get; } = new List<BillItem>();
}
