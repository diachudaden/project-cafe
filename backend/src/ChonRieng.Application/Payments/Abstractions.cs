using ChonRieng.Domain.Payments;

namespace ChonRieng.Application.Payments;

public enum SaveOutcome
{
    Saved,
    /// <summary>Vi phạm unique key — thường là webhook trùng hoặc bút toán đã được đảo.</summary>
    DuplicateKey,
    /// <summary>Vi phạm RowVersion — hai yêu cầu cùng giành một ghế.</summary>
    ConcurrencyConflict,
}

/// <summary>
/// Cổng dữ liệu của module thanh toán. Infrastructure implement bằng EF Core (EfPaymentStore).
/// Một instance = một DbContext scoped = một unit of work.
/// </summary>
public interface IPaymentStore
{
    Task<IPayableBill?> GetBillAsync(int billId, CancellationToken ct);
    Task<IHoldableSeat?> GetSeatAsync(int seatId, CancellationToken ct);
    Task<IHoldableSeat?> FindSeatHeldForBillAsync(int billId, CancellationToken ct);

    /// <summary>Ghế trống (hoặc giữ đã quá hạn) của chi nhánh, lọc theo loại nếu có.</summary>
    Task<IReadOnlyList<IHoldableSeat>> GetAvailableSeatsAsync(int branchId, string? seatType, DateTime nowUtc, CancellationToken ct);

    Task<IReadOnlyList<IHoldableSeat>> GetExpiredHoldsAsync(DateTime nowUtc, CancellationToken ct);

    /// <summary>Bắt đầu phiên ngồi cho bill trên ghế. Ghế chuyển sang Occupied.</summary>
    Task StartSessionAsync(IPayableBill bill, IHoldableSeat seat, DateTime nowUtc, CancellationToken ct);

    Task<int?> GetCurrentShiftIdAsync(int branchId, CancellationToken ct);

    Task<bool> TransactionExistsAsync(long sepayId, CancellationToken ct);
    Task<PaymentTransaction?> GetTransactionAsync(long sepayId, CancellationToken ct);
    Task<IReadOnlyList<PaymentTransaction>> GetTransactionsNeedingAttentionAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);

    void Add(PaymentTransaction transaction);
    void Add(LedgerEntry entry);
    void Add(ReconciliationIssue issue);

    Task<SaveOutcome> SaveChangesAsync(CancellationToken ct);

    /// <summary>Bỏ mọi thay đổi chưa lưu (dùng trước khi thử lại sau xung đột).</summary>
    void ResetChanges();
}

public sealed record LedgerFilter(
    int? BranchId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    LedgerDirection? Direction,
    LedgerCategory? Category,
    PaymentMethod? Method,
    int Page = 1,
    int PageSize = 50);

/// <summary>Tổng theo ngày (giờ VN), đã cộng có dấu (dòng đảo bị trừ).</summary>
public sealed record LedgerDailyTotal(DateOnly Date, LedgerDirection Direction, LedgerCategory Category, long Total);

public interface ILedgerStore
{
    Task<LedgerEntry?> GetAsync(long id, CancellationToken ct);
    Task<bool> IsReversedAsync(long id, CancellationToken ct);
    Task<(IReadOnlyList<LedgerEntry> Items, int Total)> ListAsync(LedgerFilter filter, CancellationToken ct);
    Task<IReadOnlyList<LedgerDailyTotal>> GetDailyTotalsAsync(int? branchId, DateOnly fromDate, DateOnly toDateInclusive, CancellationToken ct);

    /// <summary>Tổng có dấu các dòng Thu bằng VietQR trong khoảng thời gian.</summary>
    Task<long> SumVietQrIncomeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>Tổng tiền các giao dịch SePay đã gắn vào bill (Matched/Overpaid/ManuallyAssigned).</summary>
    Task<long> SumAppliedBankTransactionsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}

/// <summary>Đẩy sự kiện realtime về POS. Infrastructure implement bằng SignalR.</summary>
public interface IPaymentNotifier
{
    Task PaymentReceivedAsync(int branchId, PaymentReceivedEvent evt, CancellationToken ct);
    Task SeatAssignedAsync(int branchId, SeatAssignedEvent evt, CancellationToken ct);
    Task NeedsAttentionAsync(int? branchId, PaymentNeedsAttentionEvent evt, CancellationToken ct);
    Task SeatHoldExpiredAsync(int branchId, SeatHoldExpiredEvent evt, CancellationToken ct);
}

/// <summary>Chiến lược chọn ghế mặc định khi ghế khách chọn không còn.</summary>
public interface IDefaultSeatStrategy
{
    IHoldableSeat? Pick(IReadOnlyList<IHoldableSeat> candidates, IPayableBill bill);
}

/// <summary>Một cổng thanh toán. Hiện có VietQR; MoMo/ZaloPay sau này implement thêm.</summary>
public interface IPaymentProvider
{
    PaymentMethod Method { get; }
    PaymentRequest CreateRequest(IPayableBill bill, DateTime expiresAtUtc);
}

public sealed record PaymentRequest(string Code, long Amount, string QrImageUrl, DateTime ExpiresAtUtc);
