using ChonRieng.Application.Payments;
using ChonRieng.Domain.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChonRieng.UnitTests.Payments;

public class LedgerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);
    private readonly InMemoryPaymentStore _store = new();
    private readonly FixedTime _clock = new(Now);

    private LedgerService Service() => new(_store, _store, _clock);

    private LedgerEntry SeedIncome(long amount, DateTime atUtc, PaymentMethod method = PaymentMethod.VietQr, long? txId = null)
    {
        var e = LedgerEntry.Income(1, atUtc, amount, LedgerCategory.SeatSale, method, atUtc, paymentTransactionId: txId);
        _store.Add(e);
        _store.SaveChangesAsync(default).GetAwaiter().GetResult();
        return e;
    }

    [Fact]
    public async Task Reverse_creates_opposite_entry_once()
    {
        var original = SeedIncome(25_000, Now.UtcDateTime);

        var result = await Service().ReverseAsync(original.Id, "Khách huỷ, đã hoàn tiền", managerId: 9, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(original.Id, result.Value!.ReversalOfId);
        Assert.Equal(-25_000, result.Value.SignedAmount);
        Assert.Equal(0, _store.Ledger.Sum(e => e.SignedAmount));

        Assert.Equal("LEDGER_ALREADY_REVERSED", (await Service().ReverseAsync(original.Id, "lần 2", 9, default)).Error?.Code);
        Assert.Equal("LEDGER_IS_REVERSAL", (await Service().ReverseAsync(result.Value.Id, "đảo cái đảo", 9, default)).Error?.Code);
    }

    [Fact]
    public async Task Expense_cannot_use_income_category()
    {
        var req = new AddExpenseRequest(1, 100_000, LedgerCategory.SeatSale, PaymentMethod.Cash, null, null);
        Assert.Equal("CATEGORY_NOT_EXPENSE", (await Service().AddExpenseAsync(req, 9, default)).Error?.Code);
    }

    [Fact]
    public async Task Summary_by_month_nets_income_expense_and_reversals()
    {
        // 18/10 15:00 VN
        SeedIncome(25_000, Now.UtcDateTime);
        var refunded = SeedIncome(30_000, Now.UtcDateTime);
        // 31/10 23:30 VN = 31/10 16:30 UTC → vẫn tháng 10
        SeedIncome(10_000, new DateTime(2026, 10, 31, 16, 30, 0, DateTimeKind.Utc));
        // 31/10 17:30 UTC = 01/11 00:30 VN → sang tháng 11
        SeedIncome(5_000, new DateTime(2026, 10, 31, 17, 30, 0, DateTimeKind.Utc));

        await Service().AddExpenseAsync(new AddExpenseRequest(1, 12_000, LedgerCategory.Ingredients, PaymentMethod.Cash, Now.UtcDateTime, "Cà phê hạt"), 9, default);
        await Service().ReverseAsync(refunded.Id, "hoàn", 9, default);

        var summary = await Service().SummaryAsync(1, SummaryPeriod.Month, new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 30), default);

        Assert.Equal(2, summary.Periods.Count);
        var oct = summary.Periods[0];
        Assert.Equal("Tháng 10/2026", oct.Label);
        Assert.Equal(35_000, oct.TotalIn);   // 25k + 30k - 30k + 10k
        Assert.Equal(12_000, oct.TotalOut);
        Assert.Equal(23_000, oct.Net);
        Assert.Equal(5_000, summary.Periods[1].TotalIn);
        Assert.Equal(40_000, summary.TotalIn);
    }

    [Fact]
    public void Week_buckets_start_on_monday()
    {
        // 18/10/2026 là Chủ nhật → tuần bắt đầu thứ Hai 12/10
        Assert.Equal(new DateOnly(2026, 10, 12), LedgerBuckets.StartOf(new DateOnly(2026, 10, 18), SummaryPeriod.Week));
        Assert.Equal(new DateOnly(2026, 10, 1), LedgerBuckets.StartOf(new DateOnly(2026, 11, 30), SummaryPeriod.Quarter));
    }

    [Fact]
    public async Task Reconcile_flags_mismatch_and_unresolved_transactions()
    {
        var day = new DateOnly(2026, 10, 18);
        var at = VietnamTime.DayRangeUtc(day).FromUtc.AddHours(10);

        var matched = PaymentTransaction.Receive(1, "MB", "0123", null, "CR1", "CR1", null, null, 25_000, at, at, "{}");
        matched.SetMatch(PaymentMatchStatus.Matched, 1);
        var orphan = PaymentTransaction.Receive(2, "MB", "0123", null, null, "ck", null, null, 40_000, at, at, "{}");
        _store.Add(matched);
        _store.Add(orphan);
        await _store.SaveChangesAsync(default);

        // Sổ chỉ ghi 20k thay vì 25k → lệch
        SeedIncome(20_000, at, txId: 1);

        _clock.Now = new DateTimeOffset(2026, 10, 18, 17, 30, 0, TimeSpan.Zero); // 00:30 ngày 19 giờ VN
        var job = new ReconcileVietQrJob(_store, _store, new RecordingNotifier(), _clock, NullLogger<ReconcileVietQrJob>.Instance);
        await job.RunAsync(default); // tự chọn "hôm qua" = 18/10

        Assert.Equal(2, _store.Issues.Count);
        Assert.Contains(_store.Issues, i => i.Kind == ReconciliationIssueKind.TotalMismatch && i.LedgerTotal == 20_000 && i.BankTotal == 25_000);
        Assert.Contains(_store.Issues, i => i.Kind == ReconciliationIssueKind.UnresolvedTransaction && i.PaymentTransactionId == 2);
    }
}
