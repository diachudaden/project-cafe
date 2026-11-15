using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;
using ChonRieng.Domain.Inventory;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>Usage and norm per session from locked outer shift counts; reconciliation results by kind (TASK-027 §5 reports).</summary>
public sealed class InventoryReportService(AppDbContext db)
{
    private const int MaxDays = 92;

    public async Task<Result<UsageReport>> UsageAsync(CurrentUser user, Guid branchId, DateOnly from, DateOnly to, UsageGroupBy groupBy, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        if (RangeError(from, to) is { } invalid)
        {
            return invalid;
        }

        var lines = await LockedLines(from, to, branchId).ToListAsync(ct);
        var rows = groupBy == UsageGroupBy.Shift
            ? lines.GroupBy(l => (l.Code, l.Date, l.ShiftType, l.ItemId)).Select(g => Row(g.Key.Code, g.Key.Date, g.Key.ShiftType, g.ToList()))
            : lines.GroupBy(l => (l.Date, l.ItemId)).Select(g => Row(g.Key.Date.ToString("yyyy-MM-dd"), g.Key.Date, null, g.ToList()));
        return new UsageReport(branchId, from, to, groupBy, rows.OrderBy(r => r.Date).ThenBy(r => r.ShiftType).ThenBy(r => r.ItemName).ToList());
    }

    public async Task<Result<IssuesReport>> IssuesAsync(CurrentUser user, Guid branchId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        if (RangeError(from, to) is { } invalid)
        {
            return invalid;
        }

        var start = VietnamTime.StartOfDay(from);
        var end = VietnamTime.StartOfDay(to.AddDays(1));
        var issues = await InventoryReviewService.IssuesAsync(
            db.InventoryIssues.Where(i => i.BranchId == branchId && i.PeriodTo >= start && i.PeriodTo < end
                && i.Kind != InventoryIssueKind.Normal && i.Kind != InventoryIssueKind.WithinTolerance), ct);
        // Last known receipt unit cost per base unit gives a rough value of the real loss.
        var costs = await db.StockDocumentLines.AsNoTracking()
            .Where(l => l.Document.BranchId == branchId && l.Document.Type == StockDocumentType.Receipt && l.UnitCost != null && l.Qty > 0)
            .OrderByDescending(l => l.Document.CreatedAt)
            .Select(l => new { l.ItemId, l.UnitCost, l.UnitQty, l.Qty })
            .ToListAsync(ct);
        decimal? Cost(Guid itemId) => costs.FirstOrDefault(c => c.ItemId == itemId) is { } c ? c.UnitCost!.Value * (c.UnitQty ?? c.Qty) / c.Qty : null;

        // A misrecorded transfer nets to ~0 across both warehouses; its size is the inner-side gap.
        var kinds = issues.GroupBy(i => i.Kind).Select(g => new IssueKindTotal(g.Key, g.Count(),
            g.Sum(i => i.Kind == InventoryIssueKind.TransferMisrecorded ? Math.Abs(i.InnerVar) : Math.Abs(i.Total)))).ToList();
        var items = issues.GroupBy(i => (i.ItemId, i.ItemName, i.BaseUnit)).Select(g =>
        {
            var loss = -g.Where(i => i.Kind == InventoryIssueKind.RealLoss).Sum(i => i.Total);
            return new IssueItemTotal(g.Key.ItemId, g.Key.ItemName, g.Key.BaseUnit,
                g.Where(i => i.Kind == InventoryIssueKind.TransferMisrecorded).Sum(i => Math.Abs(i.InnerVar)),
                loss, g.Where(i => i.Kind == InventoryIssueKind.UnexplainedSurplus).Sum(i => i.Total),
                Cost(g.Key.ItemId) is { } unit ? Math.Round(unit * loss, 0) : null);
        }).OrderByDescending(i => i.RealLoss).ToList();
        return new IssuesReport(branchId, from, to, kinds, items, issues);
    }

    /// <summary>Norm per session by branch: Admin sees every branch, a manager their own branches.</summary>
    public async Task<Result<IReadOnlyList<BranchUsageRow>>> BranchesAsync(CurrentUser user, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!user.IsAdmin && !user.IsManager)
        {
            return ServiceError.Forbidden("Chỉ quản lý được xem.");
        }

        if (RangeError(from, to) is { } invalid)
        {
            return invalid;
        }

        var lines = (await LockedLines(from, to, null).ToListAsync(ct)).Where(l => user.CanManageBranch(l.BranchId)).ToList();
        var names = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var settings = await db.ItemBranchSettings.AsNoTracking().ToListAsync(ct);
        return Result<IReadOnlyList<BranchUsageRow>>.From(lines.GroupBy(l => (l.BranchId, l.ItemId)).Select(g =>
        {
            var r = Row("", default, null, g.ToList());
            var norm = settings.FirstOrDefault(s => s.BranchId == g.Key.BranchId && s.ItemId == g.Key.ItemId)?.EffectiveNorm ?? r.Norm;
            return new BranchUsageRow(g.Key.BranchId, names.GetValueOrDefault(g.Key.BranchId) ?? "", g.Key.ItemId, r.ItemName, r.BaseUnit,
                r.Sessions, r.ActualUsage, r.ActualPerSession, norm);
        }).OrderBy(r => r.ItemName).ThenBy(r => r.BranchName).ToList());
    }

    private sealed record Line(
        Guid BranchId, string Code, DateOnly Date, ShiftType ShiftType, Guid ItemId, string ItemName, string BaseUnit,
        int Sessions, int Opening, int In, int Waste, int Counted, decimal Usage, decimal Norm, int Variance);

    private IQueryable<Line> LockedLines(DateOnly from, DateOnly to, Guid? branchId) =>
        db.StockDocumentLines.AsNoTracking()
            .Where(l => (branchId == null || l.Document.BranchId == branchId) && l.Document.Type == StockDocumentType.Count && l.Document.ShiftId != null && l.CountedQty != null
                && (l.Document.Status == StockDocumentStatus.Confirmed || l.Document.Status == StockDocumentStatus.Approved
                    || l.Document.Status == StockDocumentStatus.PendingApproval)
                && l.Document.Shift!.Date >= from && l.Document.Shift.Date <= to)
            .Select(l => new Line(l.Document.BranchId, l.Document.Code, l.Document.Shift!.Date, l.Document.Shift.Type, l.ItemId, l.Item.Name, l.Item.BaseUnit,
                l.Document.SessionCount ?? 0, l.OpeningQty ?? 0, l.TransferInQty ?? 0, l.WasteQty ?? 0, l.CountedQty!.Value,
                l.Usage ?? 0, l.Norm ?? 0, l.Variance ?? 0));

    private static UsageReportRow Row(string period, DateOnly date, ShiftType? shiftType, List<Line> lines)
    {
        var sessions = lines.Sum(l => l.Sessions);
        var actual = lines.Sum(l => InventoryMath.ActualUsage(l.Opening, l.In, l.Waste, l.Counted));
        var first = lines[0];
        return new UsageReportRow(period, date, shiftType, first.ItemId, first.ItemName, first.BaseUnit, sessions, actual,
            lines.Sum(l => l.Usage), sessions > 0 ? Math.Round((decimal)actual / sessions, 3) : null,
            lines.Average(l => l.Norm), lines.Sum(l => l.Variance), lines.Count);
    }

    private static ServiceError? RangeError(DateOnly from, DateOnly to) =>
        to < from || to.DayNumber - from.DayNumber > MaxDays
            ? ServiceError.Invalid("to", $"Khoảng ngày không hợp lệ (tối đa {MaxDays} ngày).")
            : null;
}
