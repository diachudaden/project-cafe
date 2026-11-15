using ChonRieng.Api.Features.Engagement;
using ChonRieng.Api.Features.Scheduling;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>
/// Inventory events (TASK-027 §7) delivered through the existing notification feed: rows are added to the caller's
/// unit of work, so they appear only after the business change commits. POS/Web poll the feed (D28); a SignalR
/// transport can later read the same events without touching the business code.
/// </summary>
public sealed class InventoryEvents(AppDbContext db, ShiftService shifts)
{
    public const string CountNeedsApproval = "inventory.count_pending";
    public const string HandoverDisputed = "inventory.handover_disputed";
    public const string HandoverOverdue = "inventory.handover_overdue";
    public const string CountReminder = "inventory.count_reminder";
    public const string RestockSuggested = "inventory.restock";
    public const string IssueFound = "inventory.issue";

    public const string ManagerLink = "/admin/inventory/attention";
    public const string PosLink = "/";

    public async Task CountNeedsApprovalAsync(StockDocument count, int outOfTolerance, DateTimeOffset now, CancellationToken ct) =>
        await ManagersAsync(count.BranchId, CountNeedsApproval, "Số đếm kho ngoài chờ duyệt",
            $"{count.Code}: {outOfTolerance} món vượt ngưỡng. Lý do: {count.Reason}", now, ct);

    public async Task HandoverDisputedAsync(StockDocument count, int items, DateTimeOffset now, CancellationToken ct) =>
        await ManagersAsync(count.BranchId, HandoverDisputed, "Ca sau báo không khớp kho ngoài",
            $"{count.Code}: {items} món lệch so với số bàn giao, cần chốt số.", now, ct);

    public async Task HandoverOverdueAsync(StockDocument count, int minutes, DateTimeOffset now, CancellationToken ct) =>
        await ManagersAsync(count.BranchId, HandoverOverdue, "Bàn giao kho ngoài chưa được nhận",
            $"{count.Code}: ca sau chưa xác nhận sau {minutes} phút.", now, ct);

    /// <summary>The link carries the shift id so the reminder job can tell it was already sent.</summary>
    public static string CountMissingLink(Guid shiftId) => $"{ManagerLink}?shift={shiftId}";

    public async Task CountMissingAsync(Shift shift, int minutes, DateTimeOffset now, CancellationToken ct) =>
        await ManagersAsync(shift.BranchId, CountReminder, "Ca chưa đếm kho ngoài",
            $"{ShiftService.Describe(shift)} đã kết thúc {minutes} phút nhưng chưa xác nhận số đếm kho ngoài.", now, ct, CountMissingLink(shift.Id));

    public async Task IssueAsync(Guid branchId, string title, string body, DateTimeOffset now, CancellationToken ct) =>
        await ManagersAsync(branchId, IssueFound, title, body, now, ct);

    /// <summary>Staff currently checked in at the branch (the POS of that branch).</summary>
    public async Task BranchStaffAsync(Guid branchId, string type, string title, string body, DateTimeOffset now, CancellationToken ct)
    {
        var userIds = await db.Attendances.AsNoTracking()
            .Where(a => a.Shift.BranchId == branchId && a.CheckInAt != null && a.CheckOutAt == null)
            .Select(a => a.Employee.UserId).Distinct().ToListAsync(ct);
        foreach (var userId in userIds)
        {
            db.Notify(userId, type, title, body, PosLink, now);
        }
    }

    private async Task ManagersAsync(Guid branchId, string type, string title, string body, DateTimeOffset now, CancellationToken ct, string link = ManagerLink)
    {
        foreach (var userId in await shifts.ManagerUserIdsAsync(branchId, ct))
        {
            db.Notify(userId, type, title, body, link, now);
        }
    }
}
