using ChonRieng.Application.Payments;
using ChonRieng.Domain.Payments;

namespace ChonRieng.UnitTests.Payments;

internal sealed class FakeBill : IPayableBill
{
    public int Id { get; init; }
    public int BranchId { get; init; } = 1;
    public long AmountDue { get; init; } = 25_000;
    public bool IsPayable => !IsPaid && !Cancelled;
    public bool IsPaid { get; private set; }
    public bool Cancelled { get; init; }
    public bool RequiresSeat { get; init; } = true;
    public int? RequestedSeatId { get; init; }
    public string? RequestedSeatType { get; init; } = "Single";
    public LedgerCategory LedgerCategory { get; init; } = LedgerCategory.SeatSale;

    public PaymentMethod? PaidWith { get; private set; }
    public long AmountReceived { get; private set; }
    public string? AttentionReason { get; private set; }

    public void MarkPaid(PaymentMethod method, long amountReceived, DateTime paidAtUtc)
    {
        if (!IsPayable) throw new InvalidOperationException("Bill không chờ thanh toán.");
        IsPaid = true;
        PaidWith = method;
        AmountReceived = amountReceived;
    }

    public void FlagNeedsAttention(string reason) => AttentionReason = reason;
}

internal sealed class FakeSeat : IHoldableSeat
{
    public int Id { get; init; }
    public int BranchId { get; init; } = 1;
    public string Code { get; init; } = "";
    public string SeatType { get; init; } = "Single";
    public int SortOrder { get; init; }
    public int? HeldForBillId { get; private set; }
    public DateTime? HeldUntil { get; private set; }
    public bool Occupied { get; set; }

    public bool IsAvailable(DateTime nowUtc) => !Occupied && (HeldForBillId is null || HeldUntil < nowUtc);
    public bool IsHeldFor(int billId) => !Occupied && HeldForBillId == billId;

    public void Hold(int billId, DateTime untilUtc)
    {
        HeldForBillId = billId;
        HeldUntil = untilUtc;
    }

    public void ReleaseHold()
    {
        HeldForBillId = null;
        HeldUntil = null;
    }
}

/// <summary>
/// Store trong bộ nhớ. Không mô phỏng rollback đầy đủ — đủ cho test nghiệp vụ.
/// <see cref="NextSaveOutcome"/> cho phép giả lập xung đột/trùng khoá ở lần lưu kế tiếp.
/// </summary>
internal sealed class InMemoryPaymentStore : IPaymentStore, ILedgerStore
{
    public List<FakeBill> Bills { get; } = [];
    public List<FakeSeat> Seats { get; } = [];
    public List<PaymentTransaction> Transactions { get; } = [];
    public List<LedgerEntry> Ledger { get; } = [];
    public List<ReconciliationIssue> Issues { get; } = [];
    public List<(int BillId, int SeatId)> Sessions { get; } = [];
    public int? CurrentShiftId { get; set; } = 7;

    public Queue<SaveOutcome> NextSaveOutcomes { get; } = new();
    public int SaveCalls { get; private set; }

    private readonly List<PaymentTransaction> _pendingTx = [];
    private readonly List<LedgerEntry> _pendingLedger = [];
    private readonly List<ReconciliationIssue> _pendingIssues = [];
    private readonly List<(int, int)> _pendingSessions = [];
    private long _ledgerId;

    public Task<IPayableBill?> GetBillAsync(int billId, CancellationToken ct) =>
        Task.FromResult<IPayableBill?>(Bills.FirstOrDefault(b => b.Id == billId));

    public Task<IHoldableSeat?> GetSeatAsync(int seatId, CancellationToken ct) =>
        Task.FromResult<IHoldableSeat?>(Seats.FirstOrDefault(s => s.Id == seatId));

    public Task<IHoldableSeat?> FindSeatHeldForBillAsync(int billId, CancellationToken ct) =>
        Task.FromResult<IHoldableSeat?>(Seats.FirstOrDefault(s => s.IsHeldFor(billId)));

    public Task<IReadOnlyList<IHoldableSeat>> GetAvailableSeatsAsync(int branchId, string? seatType, DateTime nowUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IHoldableSeat>>(Seats
            .Where(s => s.BranchId == branchId && s.IsAvailable(nowUtc) && (seatType is null || s.SeatType == seatType))
            .ToList<IHoldableSeat>());

    public Task<IReadOnlyList<IHoldableSeat>> GetExpiredHoldsAsync(DateTime nowUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IHoldableSeat>>(Seats
            .Where(s => !s.Occupied && s.HeldForBillId is not null && s.HeldUntil < nowUtc)
            .ToList<IHoldableSeat>());

    public Task StartSessionAsync(IPayableBill bill, IHoldableSeat seat, DateTime nowUtc, CancellationToken ct)
    {
        _pendingSessions.Add((bill.Id, seat.Id));
        ((FakeSeat)seat).Occupied = true;
        return Task.CompletedTask;
    }

    public Task<int?> GetCurrentShiftIdAsync(int branchId, CancellationToken ct) => Task.FromResult(CurrentShiftId);

    public Task<bool> TransactionExistsAsync(long sepayId, CancellationToken ct) =>
        Task.FromResult(Transactions.Any(t => t.SepayId == sepayId));

    public Task<PaymentTransaction?> GetTransactionAsync(long sepayId, CancellationToken ct) =>
        Task.FromResult(Transactions.FirstOrDefault(t => t.SepayId == sepayId));

    public Task<IReadOnlyList<PaymentTransaction>> GetTransactionsNeedingAttentionAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PaymentTransaction>>(Transactions
            .Where(t => t.NeedsAttention
                        && (fromUtc is null || t.TransactionAtUtc >= fromUtc)
                        && (toUtc is null || t.TransactionAtUtc < toUtc))
            .ToList());

    public void Add(PaymentTransaction transaction) => _pendingTx.Add(transaction);
    public void Add(LedgerEntry entry) => _pendingLedger.Add(entry);
    public void Add(ReconciliationIssue issue) => _pendingIssues.Add(issue);

    public Task<SaveOutcome> SaveChangesAsync(CancellationToken ct)
    {
        SaveCalls++;
        if (NextSaveOutcomes.TryDequeue(out var forced) && forced != SaveOutcome.Saved)
            return Task.FromResult(forced);

        if (_pendingTx.Any(p => Transactions.Any(t => t.SepayId == p.SepayId)))
            return Task.FromResult(SaveOutcome.DuplicateKey);
        if (_pendingLedger.Any(p => p.ReversalOfId is { } r && Ledger.Any(l => l.ReversalOfId == r)))
            return Task.FromResult(SaveOutcome.DuplicateKey);

        Transactions.AddRange(_pendingTx);
        foreach (var e in _pendingLedger)
        {
            typeof(LedgerEntry).GetProperty(nameof(LedgerEntry.Id))!.SetValue(e, ++_ledgerId);
            Ledger.Add(e);
        }
        Issues.AddRange(_pendingIssues);
        Sessions.AddRange(_pendingSessions);
        ClearPending();
        return Task.FromResult(SaveOutcome.Saved);
    }

    public void ResetChanges() => ClearPending();

    private void ClearPending()
    {
        _pendingTx.Clear();
        _pendingLedger.Clear();
        _pendingIssues.Clear();
        _pendingSessions.Clear();
    }

    // ---- ILedgerStore ----

    public Task<LedgerEntry?> GetAsync(long id, CancellationToken ct) => Task.FromResult(Ledger.FirstOrDefault(e => e.Id == id));

    public Task<bool> IsReversedAsync(long id, CancellationToken ct) => Task.FromResult(Ledger.Any(e => e.ReversalOfId == id));

    public Task<(IReadOnlyList<LedgerEntry> Items, int Total)> ListAsync(LedgerFilter filter, CancellationToken ct) =>
        Task.FromResult<(IReadOnlyList<LedgerEntry>, int)>((Ledger.ToList(), Ledger.Count));

    public Task<IReadOnlyList<LedgerDailyTotal>> GetDailyTotalsAsync(int? branchId, DateOnly fromDate, DateOnly toDateInclusive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LedgerDailyTotal>>(Ledger
            .Where(e => branchId is null || e.BranchId == branchId)
            .GroupBy(e => (Date: VietnamTime.Today(e.OccurredAtUtc), e.Direction, e.Category))
            .Where(g => g.Key.Date >= fromDate && g.Key.Date <= toDateInclusive)
            .Select(g => new LedgerDailyTotal(g.Key.Date, g.Key.Direction, g.Key.Category, g.Sum(e => e.SignedAmount)))
            .ToList());

    public Task<long> SumVietQrIncomeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        Task.FromResult(Ledger
            .Where(e => e.Method == PaymentMethod.VietQr && e.Direction == LedgerDirection.In
                        && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc)
            .Sum(e => e.SignedAmount));

    public Task<long> SumAppliedBankTransactionsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        Task.FromResult(Transactions
            .Where(t => t.MatchStatus is PaymentMatchStatus.Matched or PaymentMatchStatus.Overpaid or PaymentMatchStatus.ManuallyAssigned
                        && t.TransactionAtUtc >= fromUtc && t.TransactionAtUtc < toUtc)
            .Sum(t => t.Amount));
}

internal sealed class RecordingNotifier : IPaymentNotifier
{
    public List<object> Events { get; } = [];

    public Task PaymentReceivedAsync(int branchId, PaymentReceivedEvent evt, CancellationToken ct) { Events.Add(evt); return Task.CompletedTask; }
    public Task SeatAssignedAsync(int branchId, SeatAssignedEvent evt, CancellationToken ct) { Events.Add(evt); return Task.CompletedTask; }
    public Task NeedsAttentionAsync(int? branchId, PaymentNeedsAttentionEvent evt, CancellationToken ct) { Events.Add(evt); return Task.CompletedTask; }
    public Task SeatHoldExpiredAsync(int branchId, SeatHoldExpiredEvent evt, CancellationToken ct) { Events.Add(evt); return Task.CompletedTask; }

    public IEnumerable<T> Of<T>() => Events.OfType<T>();
}
