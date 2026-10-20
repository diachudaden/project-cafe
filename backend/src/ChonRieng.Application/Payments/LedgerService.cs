using System.Globalization;
using ChonRieng.Domain.Payments;

namespace ChonRieng.Application.Payments;

/// <summary>Sổ thu chi: lịch sử, nhập khoản chi, đảo bút toán, báo cáo theo kỳ.</summary>
public sealed class LedgerService(ILedgerStore ledger, IPaymentStore store, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<LedgerEntryDto>> ListAsync(LedgerFilter filter, CancellationToken ct)
    {
        var f = filter with
        {
            Page = Math.Max(1, filter.Page),
            PageSize = Math.Clamp(filter.PageSize, 1, 200),
        };
        var (items, total) = await ledger.ListAsync(f, ct);
        return new PagedResult<LedgerEntryDto>(items.Select(e => e.ToDto()).ToList(), total, f.Page, f.PageSize);
    }

    public async Task<PayResult<LedgerEntryDto>> AddExpenseAsync(AddExpenseRequest req, int managerId, CancellationToken ct)
    {
        if (req.Amount <= 0)
            return PayError.Validation("AMOUNT_INVALID", "Số tiền phải lớn hơn 0.");
        if (req.Category is LedgerCategory.SeatSale or LedgerCategory.DrinkExtra)
            return PayError.Validation("CATEGORY_NOT_EXPENSE", "Danh mục này là khoản thu.");

        var now = Now;
        var occurredAt = req.OccurredAtUtc ?? now;
        if (occurredAt > now.AddMinutes(5))
            return PayError.Validation("DATE_IN_FUTURE", "Ngày chi không được ở tương lai.");

        var shiftId = await store.GetCurrentShiftIdAsync(req.BranchId, ct);
        var entry = LedgerEntry.Expense(req.BranchId, occurredAt, req.Amount, req.Category, req.Method,
                                        managerId, now, shiftId, req.Note);
        store.Add(entry);
        await store.SaveChangesAsync(ct);
        return PayResult<LedgerEntryDto>.Ok(entry.ToDto());
    }

    public async Task<PayResult<LedgerEntryDto>> ReverseAsync(long id, string reason, int managerId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return PayError.Validation("REASON_REQUIRED", "Cần ghi lý do đảo.");

        var original = await ledger.GetAsync(id, ct);
        if (original is null) return PayErrors.LedgerNotFound;
        if (original.IsReversal) return PayErrors.LedgerIsReversal;
        if (await ledger.IsReversedAsync(id, ct)) return PayErrors.LedgerAlreadyReversed;

        var reversal = original.Reverse(managerId, Now, reason);
        store.Add(reversal);

        // Unique index trên ReversalOfId chặn 2 người bấm đảo cùng lúc.
        if (await store.SaveChangesAsync(ct) == SaveOutcome.DuplicateKey)
            return PayErrors.LedgerAlreadyReversed;

        return PayResult<LedgerEntryDto>.Ok(reversal.ToDto());
    }

    public async Task<LedgerSummaryDto> SummaryAsync(int? branchId, SummaryPeriod period, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from) (from, to) = (to, from);

        var daily = await ledger.GetDailyTotalsAsync(branchId, from, to, ct);
        var periods = LedgerBuckets.Group(daily, period, from, to);

        var totalIn = periods.Sum(p => p.TotalIn);
        var totalOut = periods.Sum(p => p.TotalOut);
        return new LedgerSummaryDto(period, from, to, totalIn, totalOut, totalIn - totalOut, periods);
    }
}

/// <summary>Gom tổng theo ngày thành tuần (ISO, bắt đầu thứ Hai) / tháng / quý / năm. Thuần, dễ test.</summary>
public static class LedgerBuckets
{
    public static IReadOnlyList<PeriodSummary> Group(
        IReadOnlyList<LedgerDailyTotal> daily, SummaryPeriod period, DateOnly from, DateOnly to)
    {
        var result = new List<PeriodSummary>();
        var start = StartOf(from, period);

        while (start <= to)
        {
            var next = Next(start, period);
            var end = next.AddDays(-1);

            var rows = daily.Where(d => d.Date >= start && d.Date <= end && d.Date >= from && d.Date <= to).ToList();
            var byCategory = rows
                .GroupBy(r => (r.Category, r.Direction))
                .Select(g => new CategoryTotal(g.Key.Category, g.Key.Direction, g.Sum(x => x.Total)))
                .Where(c => c.Total != 0)
                .OrderBy(c => c.Direction).ThenByDescending(c => c.Total)
                .ToList();

            var totalIn = rows.Where(r => r.Direction == LedgerDirection.In).Sum(r => r.Total);
            var totalOut = rows.Where(r => r.Direction == LedgerDirection.Out).Sum(r => r.Total);

            result.Add(new PeriodSummary(start, end, Label(start, period), totalIn, totalOut, totalIn - totalOut, byCategory));
            start = next;
        }

        return result;
    }

    public static DateOnly StartOf(DateOnly d, SummaryPeriod p) => p switch
    {
        SummaryPeriod.Day => d,
        SummaryPeriod.Week => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)),
        SummaryPeriod.Month => new DateOnly(d.Year, d.Month, 1),
        SummaryPeriod.Quarter => new DateOnly(d.Year, (d.Month - 1) / 3 * 3 + 1, 1),
        SummaryPeriod.Year => new DateOnly(d.Year, 1, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(p)),
    };

    private static DateOnly Next(DateOnly start, SummaryPeriod p) => p switch
    {
        SummaryPeriod.Day => start.AddDays(1),
        SummaryPeriod.Week => start.AddDays(7),
        SummaryPeriod.Month => start.AddMonths(1),
        SummaryPeriod.Quarter => start.AddMonths(3),
        SummaryPeriod.Year => start.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(p)),
    };

    private static string Label(DateOnly start, SummaryPeriod p) => p switch
    {
        SummaryPeriod.Day => start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        SummaryPeriod.Week => $"Tuần {ISOWeek.GetWeekOfYear(start.ToDateTime(TimeOnly.MinValue))}/{ISOWeek.GetYear(start.ToDateTime(TimeOnly.MinValue))}",
        SummaryPeriod.Month => $"Tháng {start.Month}/{start.Year}",
        SummaryPeriod.Quarter => $"Quý {(start.Month - 1) / 3 + 1}/{start.Year}",
        SummaryPeriod.Year => $"Năm {start.Year}",
        _ => "",
    };
}
