// ============================================================================================
// THAM KHẢO — KHÔNG chép nguyên file này vào dự án.
// Đây là bản tối giản của Bill / Seat / Session / Shift để module thanh toán build được độc lập
// (dùng trong harness kiểm thử). Hãy đối chiếu với entity thật của dự án và thêm những
// property / method còn thiếu theo đúng tên bên dưới.
// ============================================================================================

using ChonRieng.Domain.Payments;

namespace ChonRieng.Domain.Entities;

public enum BillStatus : byte { Pending = 1, Paid = 2, Cancelled = 3 }

public enum SeatStatus : byte { Available = 1, Held = 2, Occupied = 3, Maintenance = 4 }

public sealed class Bill : IPayableBill
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int? CustomerId { get; set; }
    public int? PricingPlanId { get; set; }
    public decimal Total { get; set; }
    public BillStatus Status { get; set; } = BillStatus.Pending;
    public bool IsSeatBill { get; set; } = true;
    public int? RequestedSeatId { get; set; }
    public string? RequestedSeatType { get; set; }

    public PaymentMethod? PaymentMethod { get; private set; }
    public long? AmountReceived { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public bool NeedsAttention { get; private set; }
    public string? AttentionReason { get; private set; }
    public Session? Session { get; private set; }

    // ---- IPayableBill ----
    long IPayableBill.AmountDue => (long)Total;
    bool IPayableBill.IsPayable => Status == BillStatus.Pending;
    bool IPayableBill.IsPaid => Status == BillStatus.Paid;
    bool IPayableBill.RequiresSeat => IsSeatBill;
    LedgerCategory IPayableBill.LedgerCategory => IsSeatBill ? LedgerCategory.SeatSale : LedgerCategory.DrinkExtra;

    public void MarkPaid(PaymentMethod method, long amountReceived, DateTime paidAtUtc)
    {
        if (Status != BillStatus.Pending)
            throw new InvalidOperationException("Bill không ở trạng thái chờ thanh toán.");

        Status = BillStatus.Paid;
        PaymentMethod = method;
        AmountReceived = amountReceived;
        PaidAt = paidAtUtc;
    }

    public void FlagNeedsAttention(string reason)
    {
        NeedsAttention = true;
        AttentionReason = reason;
    }

    public void AttachSession(Session session) => Session = session;
}

public sealed class Seat : IHoldableSeat
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public string Code { get; set; } = "";
    public string SeatType { get; set; } = "Single";
    public int SortOrder { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;
    public int? HeldForBillId { get; private set; }
    public DateTime? HeldUntil { get; private set; }

    /// <summary>EF: <c>b.Property(s => s.RowVersion).IsRowVersion();</c></summary>
    public byte[] RowVersion { get; set; } = [];

    public bool IsAvailable(DateTime nowUtc) =>
        Status == SeatStatus.Available || (Status == SeatStatus.Held && HeldUntil < nowUtc);

    public bool IsHeldFor(int billId) => Status == SeatStatus.Held && HeldForBillId == billId;

    public void Hold(int billId, DateTime untilUtc)
    {
        Status = SeatStatus.Held;
        HeldForBillId = billId;
        HeldUntil = untilUtc;
    }

    public void ReleaseHold()
    {
        if (Status != SeatStatus.Held) return;
        Status = SeatStatus.Available;
        HeldForBillId = null;
        HeldUntil = null;
    }

    public void Occupy() => Status = SeatStatus.Occupied;
}

public sealed class Session
{
    public int Id { get; private set; }
    public int SeatId { get; private set; }
    public int? CustomerId { get; private set; }
    public int? PricingPlanId { get; private set; }
    public DateTime StartedAt { get; private set; }

    public static Session Start(Seat seat, int? customerId, int? pricingPlanId, DateTime nowUtc)
    {
        seat.Occupy();
        return new Session { SeatId = seat.Id, CustomerId = customerId, PricingPlanId = pricingPlanId, StartedAt = nowUtc };
    }
}

public sealed class Shift
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
