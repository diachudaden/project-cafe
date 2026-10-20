using ChonRieng.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace ChonRieng.Application.Payments;

/// <summary>Chạy mỗi phút: nhả các ghế giữ quá hạn mà khách chưa trả.</summary>
public sealed class ReleaseExpiredSeatHoldsJob(
    IPaymentStore store, IPaymentNotifier notifier, TimeProvider clock, ILogger<ReleaseExpiredSeatHoldsJob> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expired = await store.GetExpiredHoldsAsync(now, ct);
        if (expired.Count == 0) return;

        var released = new List<(int BranchId, SeatHoldExpiredEvent Evt)>();
        foreach (var seat in expired)
        {
            if (seat.HeldForBillId is not int billId) continue;
            seat.ReleaseHold();
            released.Add((seat.BranchId, new SeatHoldExpiredEvent(billId, seat.Id)));
        }

        // Xung đột = webhook vừa xếp đúng ghế đó cho bill. Bỏ qua, phút sau job chạy lại.
        if (await store.SaveChangesAsync(ct) != SaveOutcome.Saved)
        {
            logger.LogInformation("Nhả ghế bị xung đột, sẽ thử lại ở lần chạy sau");
            return;
        }

        foreach (var (branchId, evt) in released)
        {
            try { await notifier.SeatHoldExpiredAsync(branchId, evt, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Không gửi được SeatHoldExpired cho ghế {SeatId}", evt.SeatId); }
        }

        logger.LogInformation("Đã nhả {Count} ghế giữ quá hạn", released.Count);
    }
}

/// <summary>
/// Chạy 00:30 giờ VN: so tổng thu VietQR trong sổ với tổng tiền ngân hàng đã gắn vào bill của ngày hôm trước,
/// và liệt kê giao dịch cần xử lý còn tồn.
/// </summary>
public sealed class ReconcileVietQrJob(
    IPaymentStore store, ILedgerStore ledger, IPaymentNotifier notifier, TimeProvider clock, ILogger<ReconcileVietQrJob> logger)
{
    public async Task RunAsync(CancellationToken ct) =>
        await RunForDateAsync(VietnamTime.Today(clock.GetUtcNow().UtcDateTime).AddDays(-1), ct);

    public async Task<int> RunForDateAsync(DateOnly businessDate, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var (fromUtc, toUtc) = VietnamTime.DayRangeUtc(businessDate);
        var issues = 0;

        var ledgerTotal = await ledger.SumVietQrIncomeAsync(fromUtc, toUtc, ct);
        var bankTotal = await ledger.SumAppliedBankTransactionsAsync(fromUtc, toUtc, ct);

        if (ledgerTotal != bankTotal)
        {
            store.Add(ReconciliationIssue.TotalMismatch(businessDate, ledgerTotal, bankTotal, now));
            issues++;
        }

        var pending = await store.GetTransactionsNeedingAttentionAsync(fromUtc, toUtc, ct);
        foreach (var tx in pending)
        {
            store.Add(ReconciliationIssue.Unresolved(businessDate, tx, now));
            issues++;
        }

        if (issues > 0)
        {
            await store.SaveChangesAsync(ct);
            try
            {
                await notifier.NeedsAttentionAsync(null,
                    new PaymentNeedsAttentionEvent(null, null, $"RECONCILE:{businessDate:yyyy-MM-dd}:{issues}", bankTotal - ledgerTotal), ct);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Không gửi được thông báo đối soát"); }
        }

        logger.LogInformation("Đối soát VietQR {Date}: sổ {Ledger}đ, ngân hàng {Bank}đ, {Issues} vấn đề",
                              businessDate, ledgerTotal, bankTotal, issues);
        return issues;
    }
}
