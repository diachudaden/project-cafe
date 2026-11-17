using System.ComponentModel;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Scheduling;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Reports;

public enum ReportPeriod
{
    Week,
    Month,
    Quarter,
    Year,
}

public sealed record RevenueBucket(string Label, DateOnly Start, decimal Revenue, int Bills);

/// <summary>Doanh thu theo kỳ, so với kỳ trước.</summary>
public sealed record RevenueReport(
    DateOnly From,
    DateOnly To,
    decimal Total,
    int Bills,
    decimal Cash,
    decimal VietQr,
    decimal PreviousTotal,
    [property: Description("Thay đổi so với kỳ trước (%); null nếu kỳ trước bằng 0.")] decimal? ChangePercent,
    IReadOnlyList<RevenueBucket> Buckets);

/// <summary>Lãi lỗ = doanh thu − chi phí − lương.</summary>
public sealed record ProfitLossReport(DateOnly From, DateOnly To, decimal Revenue, decimal Expenses, decimal Payroll, decimal Profit, IReadOnlyDictionary<string, decimal> ExpensesByCategory);

public sealed record OccupancyRow(Guid BranchId, string BranchName, SeatType SeatType, int Seats, decimal SoldHours, decimal AvailableHours, decimal Rate);

/// <summary>So sánh chi nhánh.</summary>
public sealed record BranchComparisonRow(
    Guid BranchId,
    string BranchName,
    decimal Revenue,
    int Sessions,
    decimal Occupancy,
    decimal PayrollCost,
    [property: Description("Chi phí lương trên doanh thu (%).")] decimal? PayrollRatio,
    int Rank);

public sealed record OnShiftEmployee(string Name, string BranchName, DateTimeOffset CheckInAt, DateTimeOffset ShiftEnd);

public sealed record DashboardAlert(string Type, string Message, string Link);

/// <summary>Tổng quan hôm nay.</summary>
public sealed record DashboardResponse(
    decimal RevenueToday,
    int BillsToday,
    int ActiveSessions,
    int Seats,
    [property: Description("Tỷ lệ lấp đầy hiện tại (%).")] decimal OccupancyNow,
    IReadOnlyList<OnShiftEmployee> OnShift,
    IReadOnlyList<DashboardAlert> Alerts);

public sealed class ReportService(AppDbContext db, TimeProvider clock)
{
    public async Task<Result<RevenueReport>> RevenueAsync(CurrentUser user, Guid? branchId, ReportPeriod period, DateOnly date, CancellationToken ct)
    {
        var scope = ScopeFor(user, branchId);
        if (scope.Error is not null)
        {
            return scope.Error;
        }

        var (from, to) = Range(period, date);
        var (prevFrom, _) = Range(period, Shift(period, from, -1));
        var bills = await BillsAsync(scope.Value, prevFrom, to, ct);
        var current = bills.Where(b => b.Day >= from && b.Day < to).ToList();
        var previous = bills.Where(b => b.Day >= prevFrom && b.Day < from).Sum(b => b.Total);
        var total = current.Sum(b => b.Total);

        var buckets = period is ReportPeriod.Week or ReportPeriod.Month
            ? Enumerable.Range(0, to.DayNumber - from.DayNumber).Select(i => from.AddDays(i))
                .Select(d => new RevenueBucket(d.ToString("dd/MM"), d, current.Where(b => b.Day == d).Sum(b => b.Total), current.Count(b => b.Day == d)))
            : Enumerable.Range(0, period == ReportPeriod.Quarter ? 3 : 12).Select(i => from.AddMonths(i))
                .Select(m => new RevenueBucket(m.ToString("MM/yyyy"), m,
                    current.Where(b => b.Day >= m && b.Day < m.AddMonths(1)).Sum(b => b.Total),
                    current.Count(b => b.Day >= m && b.Day < m.AddMonths(1))));

        return new RevenueReport(from, to.AddDays(-1), total, current.Count,
            current.Sum(b => b.CashPaidTotal),
            current.Sum(b => b.VietQrPaidTotal),
            previous, previous == 0 ? null : Math.Round((total - previous) / previous * 100, 1), buckets.ToList());
    }

    public async Task<Result<ProfitLossReport>> ProfitLossAsync(CurrentUser user, Guid? branchId, ReportPeriod period, DateOnly date, CancellationToken ct)
    {
        var scope = ScopeFor(user, branchId);
        if (scope.Error is not null)
        {
            return scope.Error;
        }

        var (from, to) = Range(period, date);
        var revenue = (await BillsAsync(scope.Value, from, to, ct)).Sum(b => b.Total);
        var expenses = await ExpensesAsync(scope.Value, from, to, ct);
        var payroll = await PayrollCostAsync(scope.Value, from, to, ct);
        var expenseTotal = expenses.Sum(e => e.Amount);
        return new ProfitLossReport(from, to.AddDays(-1), revenue, expenseTotal, payroll, revenue - expenseTotal - payroll,
            expenses.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount)));
    }

    public async Task<Result<IReadOnlyList<OccupancyRow>>> OccupancyAsync(CurrentUser user, Guid? branchId, ReportPeriod period, DateOnly date, CancellationToken ct)
    {
        var scope = ScopeFor(user, branchId);
        if (scope.Error is not null)
        {
            return scope.Error;
        }

        var (from, to) = Range(period, date);
        return Result<IReadOnlyList<OccupancyRow>>.From(await OccupancyRowsAsync(scope.Value, from, to, ct));
    }

    public async Task<Result<IReadOnlyList<BranchComparisonRow>>> CompareBranchesAsync(CurrentUser user, ReportPeriod period, DateOnly date, CancellationToken ct)
    {
        var scope = ScopeFor(user, null);
        if (scope.Error is not null)
        {
            return scope.Error;
        }

        var (from, to) = Range(period, date);
        var branches = await BranchesInScope(scope.Value).Select(b => new { b.Id, b.Name }).ToListAsync(ct);
        var bills = await BillsAsync(scope.Value, from, to, ct);
        var occupancy = await OccupancyRowsAsync(scope.Value, from, to, ct);
        var rows = new List<BranchComparisonRow>();
        foreach (var b in branches)
        {
            var revenue = bills.Where(x => x.BranchId == b.Id).Sum(x => x.Total);
            var payroll = await PayrollCostAsync([b.Id], from, to, ct);
            var occ = occupancy.Where(o => o.BranchId == b.Id).ToList();
            var available = occ.Sum(o => o.AvailableHours);
            rows.Add(new BranchComparisonRow(b.Id, b.Name, revenue, bills.Count(x => x.BranchId == b.Id),
                available == 0 ? 0 : Math.Round(occ.Sum(o => o.SoldHours) / available * 100, 1),
                payroll, revenue == 0 ? null : Math.Round(payroll / revenue * 100, 1), 0));
        }

        var ranked = rows.OrderByDescending(r => r.Revenue).Select((r, i) => r with { Rank = i + 1 }).ToList();
        return Result<IReadOnlyList<BranchComparisonRow>>.From(ranked);
    }

    public async Task<Result<DashboardResponse>> DashboardAsync(CurrentUser user, Guid? branchId, CancellationToken ct)
    {
        var scope = ScopeFor(user, branchId);
        if (scope.Error is not null)
        {
            return scope.Error;
        }

        var ids = scope.Value;
        var today = clock.Today();
        var now = clock.GetUtcNow();
        var bills = await BillsAsync(ids, today, today.AddDays(1), ct);
        var seats = await db.Seats.Where(s => ids == null || ids.Contains(s.Zone.BranchId)).CountAsync(s => s.Status != SeatStatus.Maintenance, ct);
        var active = await db.Sessions.CountAsync(s => s.Status == SessionStatus.Active && (ids == null || ids.Contains(s.Seat.Zone.BranchId)), ct);
        var onShift = await db.Attendances.AsNoTracking()
            .Where(a => a.CheckInAt != null && a.CheckOutAt == null && (ids == null || ids.Contains(a.Shift.BranchId)))
            .OrderBy(a => a.Shift.Branch.Name)
            .Select(a => new OnShiftEmployee(a.Employee.User.FullName, a.Shift.Branch.Name, a.CheckInAt!.Value, a.Shift.EndAt))
            .ToListAsync(ct);

        var alerts = new List<DashboardAlert>();
        var horizon = now.AddHours(36);
        var understaffed = await db.Shifts.AsNoTracking()
            .Where(s => (ids == null || ids.Contains(s.BranchId)) && s.StartAt < horizon && s.EndAt > now
                && s.Assignments.Count(a => a.Status == ShiftAssignmentStatus.Accepted) < s.RequiredStaff)
            .OrderBy(s => s.StartAt)
            .Select(s => new { s.StartAt, s.EndAt, Branch = s.Branch.Name, Accepted = s.Assignments.Count(a => a.Status == ShiftAssignmentStatus.Accepted), s.RequiredStaff })
            .Take(10)
            .ToListAsync(ct);
        alerts.AddRange(understaffed.Select(s => new DashboardAlert("understaffed",
            $"{s.Branch}: ca {s.StartAt.ToVietnam():dd/MM HH:mm}–{s.EndAt.ToVietnam():HH:mm} mới có {s.Accepted}/{s.RequiredStaff} người.", "/admin/schedule")));

        var closings = await db.ShiftClosings.AsNoTracking()
            .Where(c => c.Status == ClosingStatus.Submitted && (ids == null || ids.Contains(c.Shift.BranchId)))
            .Select(c => new { Branch = c.Shift.Branch.Name, c.Shift.StartAt, c.Difference })
            .ToListAsync(ct);
        alerts.AddRange(closings.Select(c => new DashboardAlert(c.Difference != 0 ? "closing-difference" : "closing-pending",
            $"{c.Branch}: biên bản kết ca {c.StartAt.ToVietnam():dd/MM HH:mm} chờ duyệt" + (c.Difference != 0 ? $", chênh lệch {c.Difference:N0}đ." : "."), "/admin/closings")));

        var pendingRequests = await db.ShiftRequests.CountAsync(r => r.Status == ShiftRequestStatuses.Pending && (ids == null || ids.Contains(r.Shift.BranchId)), ct);
        if (pendingRequests > 0)
        {
            alerts.Add(new DashboardAlert("requests", $"{pendingRequests} đơn đổi ca / nghỉ đang chờ duyệt.", "/admin/requests"));
        }

        return new DashboardResponse(bills.Sum(b => b.Total), bills.Count, active, seats,
            seats == 0 ? 0 : Math.Round((decimal)active / seats * 100, 1), onShift, alerts);
    }

    public static (DateOnly From, DateOnly ToExclusive) Range(ReportPeriod period, DateOnly date) => period switch
    {
        ReportPeriod.Week => (VietnamTime.WeekStart(date), VietnamTime.WeekStart(date).AddDays(7)),
        ReportPeriod.Month => (VietnamTime.MonthStart(date), VietnamTime.MonthStart(date).AddMonths(1)),
        ReportPeriod.Quarter => (new DateOnly(date.Year, (date.Month - 1) / 3 * 3 + 1, 1), new DateOnly(date.Year, (date.Month - 1) / 3 * 3 + 1, 1).AddMonths(3)),
        _ => (new DateOnly(date.Year, 1, 1), new DateOnly(date.Year + 1, 1, 1)),
    };

    private static DateOnly Shift(ReportPeriod period, DateOnly from, int n) => period switch
    {
        ReportPeriod.Week => from.AddDays(7 * n),
        ReportPeriod.Month => from.AddMonths(n),
        ReportPeriod.Quarter => from.AddMonths(3 * n),
        _ => from.AddYears(n),
    };

    /// <summary>null = whole chain (Admin without a branch filter).</summary>
    private static Result<Guid[]?> ScopeFor(CurrentUser user, Guid? branchId)
    {
        if (branchId is { } id)
        {
            return user.CanManageBranch(id) ? Result<Guid[]?>.From(new[] { id }) : ServiceError.NotFound();
        }

        if (user.IsAdmin)
        {
            return Result<Guid[]?>.From(null);
        }

        return user.IsManager ? Result<Guid[]?>.From(user.Scope.BranchIds.ToArray()) : ServiceError.Forbidden("Chỉ Admin và quản lý chi nhánh xem được báo cáo.");
    }

    private IQueryable<Branch> BranchesInScope(Guid[]? ids) =>
        db.Branches.AsNoTracking().Where(b => ids == null || ids.Contains(b.Id)).OrderBy(b => b.Name);

    private sealed record BillRow(Guid BranchId, DateOnly Day, decimal Total, decimal CashPaidTotal, decimal VietQrPaidTotal);

    private async Task<List<BillRow>> BillsAsync(Guid[]? ids, DateOnly from, DateOnly toExclusive, CancellationToken ct)
    {
        var start = VietnamTime.StartOfDay(from);
        var end = VietnamTime.StartOfDay(toExclusive);
        var rows = await db.Bills.AsNoTracking()
            .Where(b => b.CreatedAt >= start && b.CreatedAt < end && (ids == null || ids.Contains(b.BranchId)))
            .Select(b => new { b.BranchId, b.CreatedAt, b.Total, b.CashPaidTotal, b.VietQrPaidTotal })
            .ToListAsync(ct);
        return rows.Select(r => new BillRow(r.BranchId, DateOnly.FromDateTime(r.CreatedAt.ToVietnam().DateTime), r.Total,
            r.CashPaidTotal, r.VietQrPaidTotal)).ToList();
    }

    private async Task<List<Expense>> ExpensesAsync(Guid[]? ids, DateOnly from, DateOnly toExclusive, CancellationToken ct)
    {
        var start = VietnamTime.StartOfDay(from);
        var end = VietnamTime.StartOfDay(toExclusive);
        return await db.Expenses.AsNoTracking()
            .Where(e => e.Date >= start && e.Date < end && (ids == null || ids.Contains(e.BranchId)))
            .ToListAsync(ct);
    }

    /// <summary>Payroll net for months overlapping the range, attributed to each employee's home branch.</summary>
    private async Task<decimal> PayrollCostAsync(Guid[]? ids, DateOnly from, DateOnly toExclusive, CancellationToken ct)
    {
        var firstMonth = VietnamTime.MonthStart(from);
        return await db.Payrolls.AsNoTracking()
            .Where(p => p.Month >= firstMonth && p.Month < toExclusive
                && p.Employee.Branches.Any(b => b.IsHome && (ids == null || ids.Contains(b.BranchId))))
            .SumAsync(p => (decimal?)p.NetAmount, ct) ?? 0;
    }

    private async Task<IReadOnlyList<OccupancyRow>> OccupancyRowsAsync(Guid[]? ids, DateOnly from, DateOnly toExclusive, CancellationToken ct)
    {
        var start = VietnamTime.StartOfDay(from);
        var end = VietnamTime.StartOfDay(toExclusive);
        var nowCap = clock.GetUtcNow() < end ? clock.GetUtcNow() : end;
        var hours = (decimal)Math.Max(0, (nowCap - start).TotalHours);

        var seats = await db.Seats.AsNoTracking()
            .Where(s => ids == null || ids.Contains(s.Zone.BranchId))
            .GroupBy(s => new { s.Zone.BranchId, BranchName = s.Zone.Branch.Name, s.Type })
            .Select(g => new { g.Key.BranchId, g.Key.BranchName, g.Key.Type, Count = g.Count() })
            .ToListAsync(ct);
        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.StartAt < end && (s.ClosedAt ?? s.EndAt) > start && (ids == null || ids.Contains(s.Seat.Zone.BranchId)))
            .Select(s => new { s.Seat.Zone.BranchId, s.Seat.Type, s.StartAt, End = s.ClosedAt ?? s.EndAt })
            .ToListAsync(ct);

        return seats.OrderBy(s => s.BranchName).ThenBy(s => s.Type).Select(s =>
        {
            var sold = (decimal)sessions.Where(x => x.BranchId == s.BranchId && x.Type == s.Type)
                .Sum(x => Math.Max(0, ((x.End < nowCap ? x.End : nowCap) - (x.StartAt > start ? x.StartAt : start)).TotalHours));
            var available = s.Count * hours;
            return new OccupancyRow(s.BranchId, s.BranchName, s.Type, s.Count, Math.Round(sold, 1), Math.Round(available, 1),
                available == 0 ? 0 : Math.Round(sold / available * 100, 1));
        }).ToList();
    }
}
