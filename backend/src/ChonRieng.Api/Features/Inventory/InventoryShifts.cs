using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Inventory;

public static class InventoryShifts
{
    /// <summary>The shift the caller works at the branch now: Staff must be checked in; Admin/Manager fall back to the running shift (may be null).</summary>
    public static async Task<Result<Shift?>> CurrentAsync(AppDbContext db, CurrentUser user, Guid branchId, DateTimeOffset now, CancellationToken ct)
    {
        var own = await db.Attendances
            .Where(a => a.Employee.UserId == user.UserId && a.CheckInAt != null && a.CheckOutAt == null && a.Shift.BranchId == branchId)
            .Select(a => a.Shift)
            .FirstOrDefaultAsync(ct);
        if (own is not null)
        {
            return Result<Shift?>.From(own);
        }

        if (!user.CanManageBranch(branchId))
        {
            return InventoryErrors.NoShift();
        }

        return Result<Shift?>.From(await RunningAsync(db, branchId, now, ct));
    }

    public static Task<Shift?> RunningAsync(AppDbContext db, Guid branchId, DateTimeOffset now, CancellationToken ct) =>
        db.Shifts
            .Where(s => s.BranchId == branchId && s.Status == ShiftStatus.Published && s.StartAt <= now && now < s.EndAt)
            .OrderBy(s => s.StartAt)
            .FirstOrDefaultAsync(ct);

    /// <summary>An outer count of this shift that is still being counted blocks transfers and outer waste.</summary>
    public static Task<bool> CountInProgressAsync(AppDbContext db, Guid shiftId, CancellationToken ct) =>
        db.StockDocuments.AnyAsync(d => d.ShiftId == shiftId && d.Type == StockDocumentType.Count && d.Status == StockDocumentStatus.Draft, ct);
}
