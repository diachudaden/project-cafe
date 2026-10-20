using ChonRieng.Application.Payments;
using ChonRieng.Domain.Payments;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// INTEGRATE: sửa 2 using dưới cho đúng namespace của DbContext và entity Bill/Seat/Session/Shift trong dự án.
using ChonRieng.Infrastructure.Persistence;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Infrastructure.Payments;

/// <summary>
/// Cài đặt <see cref="IPaymentStore"/> trên DbContext của dự án.
/// Các chỗ <c>INTEGRATE</c> là nơi phải sửa cho khớp entity thật.
/// </summary>
public sealed class EfPaymentStore(AppDbContext db) : IPaymentStore
{
    private static readonly PaymentMatchStatus[] AttentionStatuses =
        [PaymentMatchStatus.Unmatched, PaymentMatchStatus.Underpaid, PaymentMatchStatus.Duplicate];

    public async Task<IPayableBill?> GetBillAsync(int billId, CancellationToken ct) =>
        await db.Set<Bill>().FirstOrDefaultAsync(b => b.Id == billId, ct);

    public async Task<IHoldableSeat?> GetSeatAsync(int seatId, CancellationToken ct) =>
        await db.Set<Seat>().FirstOrDefaultAsync(s => s.Id == seatId, ct);

    public async Task<IHoldableSeat?> FindSeatHeldForBillAsync(int billId, CancellationToken ct) =>
        // INTEGRATE: SeatStatus.Held là enum trạng thái ghế của dự án.
        await db.Set<Seat>().FirstOrDefaultAsync(s => s.HeldForBillId == billId && s.Status == SeatStatus.Held, ct);

    public async Task<IReadOnlyList<IHoldableSeat>> GetAvailableSeatsAsync(int branchId, string? seatType, DateTime nowUtc, CancellationToken ct)
    {
        var q = db.Set<Seat>().Where(s => s.BranchId == branchId &&
            (s.Status == SeatStatus.Available || (s.Status == SeatStatus.Held && s.HeldUntil < nowUtc)));

        if (!string.IsNullOrEmpty(seatType))
            q = q.Where(s => s.SeatType == seatType);

        return await q.OrderBy(s => s.SortOrder).ThenBy(s => s.Id).Take(50).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<IHoldableSeat>> GetExpiredHoldsAsync(DateTime nowUtc, CancellationToken ct) =>
        await db.Set<Seat>()
            .Where(s => s.Status == SeatStatus.Held && s.HeldUntil < nowUtc)
            .Take(200)
            .ToListAsync(ct);

    public Task StartSessionAsync(IPayableBill bill, IHoldableSeat seat, DateTime nowUtc, CancellationToken ct)
    {
        // INTEGRATE: thay bằng cách tạo phiên ngồi thật của dự án. Trước đây check-in tạo Session + Bill cùng lúc;
        // với luồng trả trước, Session chỉ được tạo ở đây, sau khi tiền về.
        // Session.Start phải chuyển ghế sang Occupied.
        var b = (Bill)bill;
        var s = (Seat)seat;
        var session = Session.Start(s, b.CustomerId, b.PricingPlanId, nowUtc);
        b.AttachSession(session);
        db.Set<Session>().Add(session);
        return Task.CompletedTask;
    }

    public Task<int?> GetCurrentShiftIdAsync(int branchId, CancellationToken ct) =>
        // INTEGRATE: điều kiện "ca đang mở" theo entity Shift của dự án.
        db.Set<Shift>()
            .Where(x => x.BranchId == branchId && x.ClosedAt == null)
            .OrderByDescending(x => x.OpenedAt)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

    public Task<bool> TransactionExistsAsync(long sepayId, CancellationToken ct) =>
        db.Set<PaymentTransaction>().AnyAsync(t => t.SepayId == sepayId, ct);

    public Task<PaymentTransaction?> GetTransactionAsync(long sepayId, CancellationToken ct) =>
        db.Set<PaymentTransaction>().FirstOrDefaultAsync(t => t.SepayId == sepayId, ct);

    public async Task<IReadOnlyList<PaymentTransaction>> GetTransactionsNeedingAttentionAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
    {
        var q = db.Set<PaymentTransaction>()
            .Where(t => AttentionStatuses.Contains(t.MatchStatus) && t.ResolvedAtUtc == null);

        if (fromUtc is { } from) q = q.Where(t => t.TransactionAtUtc >= from);
        if (toUtc is { } to) q = q.Where(t => t.TransactionAtUtc < to);

        return await q.OrderByDescending(t => t.TransactionAtUtc).Take(500).ToListAsync(ct);
    }

    public void Add(PaymentTransaction transaction) => db.Set<PaymentTransaction>().Add(transaction);
    public void Add(LedgerEntry entry) => db.Set<LedgerEntry>().Add(entry);
    public void Add(ReconciliationIssue issue) => db.Set<ReconciliationIssue>().Add(issue);

    public async Task<SaveOutcome> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            return SaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
        {
            return SaveOutcome.DuplicateKey;
        }
    }

    public void ResetChanges() => db.ChangeTracker.Clear();
}

public sealed class EfLedgerStore(AppDbContext db) : ILedgerStore
{
    private static readonly PaymentMatchStatus[] AppliedStatuses =
        [PaymentMatchStatus.Matched, PaymentMatchStatus.Overpaid, PaymentMatchStatus.ManuallyAssigned];

    public Task<LedgerEntry?> GetAsync(long id, CancellationToken ct) =>
        db.Set<LedgerEntry>().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> IsReversedAsync(long id, CancellationToken ct) =>
        db.Set<LedgerEntry>().AnyAsync(e => e.ReversalOfId == id, ct);

    public async Task<(IReadOnlyList<LedgerEntry> Items, int Total)> ListAsync(LedgerFilter f, CancellationToken ct)
    {
        var q = db.Set<LedgerEntry>().AsNoTracking();
        if (f.BranchId is { } branchId) q = q.Where(e => e.BranchId == branchId);
        if (f.FromUtc is { } from) q = q.Where(e => e.OccurredAtUtc >= from);
        if (f.ToUtc is { } to) q = q.Where(e => e.OccurredAtUtc < to);
        if (f.Direction is { } direction) q = q.Where(e => e.Direction == direction);
        if (f.Category is { } category) q = q.Where(e => e.Category == category);
        if (f.Method is { } method) q = q.Where(e => e.Method == method);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<LedgerDailyTotal>> GetDailyTotalsAsync(int? branchId, DateOnly fromDate, DateOnly toDateInclusive, CancellationToken ct)
    {
        var fromUtc = VietnamTime.DayRangeUtc(fromDate).FromUtc;
        var toUtc = VietnamTime.DayRangeUtc(toDateInclusive).ToUtc;
        var offsetHours = (int)VietnamTime.Offset.TotalHours;

        var q = db.Set<LedgerEntry>().AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc);
        if (branchId is { } id) q = q.Where(e => e.BranchId == id);

        var rows = await q
            .GroupBy(e => new { Day = e.OccurredAtUtc.AddHours(offsetHours).Date, e.Direction, e.Category })
            .Select(g => new
            {
                g.Key.Day,
                g.Key.Direction,
                g.Key.Category,
                Total = g.Sum(e => e.ReversalOfId == null ? e.Amount : -e.Amount),
            })
            .ToListAsync(ct);

        return rows.Select(r => new LedgerDailyTotal(DateOnly.FromDateTime(r.Day), r.Direction, r.Category, r.Total)).ToList();
    }

    public Task<long> SumVietQrIncomeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        db.Set<LedgerEntry>()
            .Where(e => e.Method == PaymentMethod.VietQr && e.Direction == LedgerDirection.In &&
                        e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc)
            .SumAsync(e => e.ReversalOfId == null ? e.Amount : -e.Amount, ct);

    public Task<long> SumAppliedBankTransactionsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        db.Set<PaymentTransaction>()
            .Where(t => AppliedStatuses.Contains(t.MatchStatus) &&
                        t.TransactionAtUtc >= fromUtc && t.TransactionAtUtc < toUtc)
            .SumAsync(t => t.Amount, ct);
}
