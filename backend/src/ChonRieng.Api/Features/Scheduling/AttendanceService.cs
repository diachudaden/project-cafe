using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Scheduling;

public sealed class AttendanceService(AppDbContext db, TimeProvider clock)
{
    /// <summary>Arriving more than 10 minutes after the shift start counts as late (reference: "trễ quá 10 phút").</summary>
    public const int LateThresholdMinutes = 10;

    public async Task<Result<AttendanceView>> CheckInAsync(CurrentUser user, Guid shiftId, CancellationToken ct)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.UserId == user.UserId, ct);
        var shift = await db.Shifts.SingleOrDefaultAsync(s => s.Id == shiftId, ct);
        if (employee is null || shift is null)
        {
            return ServiceError.NotFound();
        }

        var accepted = await db.ShiftAssignments.AnyAsync(a => a.ShiftId == shiftId && a.EmployeeId == employee.Id && a.Status == ShiftAssignmentStatus.Accepted, ct);
        if (!accepted || shift.Status != ShiftStatus.Published)
        {
            return ServiceError.Conflict("Chưa nhận ca", "Bạn chỉ điểm danh được ca đã công bố và đã nhận.");
        }

        var now = clock.GetUtcNow();
        if (now < shift.StartAt - ShiftService.CheckInOpensBefore)
        {
            return ServiceError.Conflict("Chưa đến giờ", "Nút Tham gia ca mở từ 15 phút trước giờ bắt đầu.");
        }

        if (now >= shift.EndAt)
        {
            return ServiceError.Conflict("Ca đã kết thúc", "Không thể điểm danh ca đã kết thúc.");
        }

        if (await db.Attendances.AnyAsync(a => a.ShiftId == shiftId && a.EmployeeId == employee.Id, ct))
        {
            return ServiceError.Conflict("Đã điểm danh", "Bạn đã tham gia ca này.");
        }

        // One open attendance at a time keeps every bill tied to exactly one shift.
        if (await db.Attendances.AnyAsync(a => a.EmployeeId == employee.Id && a.CheckInAt != null && a.CheckOutAt == null, ct))
        {
            return ServiceError.Conflict("Đang trong ca khác", "Hãy kết ca đang làm trước khi tham gia ca mới.");
        }

        var attendance = new Attendance { EmployeeId = employee.Id, ShiftId = shiftId, CheckInAt = now, Method = "App" };
        db.Attendances.Add(attendance);
        await db.SaveChangesAsync(ct);
        return ToView(attendance, shift);
    }

    public async Task<Result<AttendanceView>> CheckOutAsync(CurrentUser user, Guid shiftId, CancellationToken ct)
    {
        var attendance = await db.Attendances.Include(a => a.Shift)
            .SingleOrDefaultAsync(a => a.ShiftId == shiftId && a.Employee.UserId == user.UserId, ct);
        if (attendance is null)
        {
            return ServiceError.NotFound("Bạn chưa điểm danh ca này.");
        }

        if (attendance.CheckOutAt is not null)
        {
            return ServiceError.Conflict("Đã rời ca", "Bạn đã rời ca này.");
        }

        attendance.CheckOutAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return ToView(attendance, attendance.Shift);
    }

    /// <summary>The shift the employee is currently working (checked in, not checked out).</summary>
    public async Task<CurrentShiftResponse?> CurrentAsync(CurrentUser user, CancellationToken ct)
    {
        var row = await db.Attendances.AsNoTracking()
            .Where(a => a.Employee.UserId == user.UserId && a.CheckInAt != null && a.CheckOutAt == null)
            .OrderByDescending(a => a.CheckInAt)
            .Select(a => new
            {
                a.Shift,
                BranchName = a.Shift.Branch.Name,
                CheckInAt = a.CheckInAt!.Value,
                Cash = a.Shift.Bills.Sum(b => (decimal?)b.CashPaidTotal) ?? 0,
                BillCount = a.Shift.Bills.Count(),
            })
            .FirstOrDefaultAsync(ct);
        return row is null ? null : new CurrentShiftResponse(row.Shift.Id, row.Shift.BranchId, row.BranchName, row.Shift.Type,
            row.Shift.StartAt, row.Shift.EndAt, row.CheckInAt, row.Cash, row.BillCount);
    }

    public async Task<Result<IReadOnlyList<AttendanceReviewRow>>> ListAsync(CurrentUser user, Guid branchId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var now = clock.GetUtcNow();
        var rows = await db.ShiftAssignments.AsNoTracking()
            .Where(a => a.Shift.BranchId == branchId && a.Shift.Date >= from && a.Shift.Date <= to
                && a.Status == ShiftAssignmentStatus.Accepted && a.Shift.StartAt <= now)
            .Select(a => new
            {
                a.Shift,
                a.EmployeeId,
                Name = a.Employee.User.FullName,
                Attendance = a.Shift.Attendances.FirstOrDefault(x => x.EmployeeId == a.EmployeeId),
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<AttendanceReviewRow>>.From(rows
            .OrderByDescending(r => r.Shift.StartAt).ThenBy(r => r.Name)
            .Select(r =>
            {
                var att = r.Attendance;
                return new AttendanceReviewRow(att?.Id, r.Shift.Id, r.Shift.Date, r.Shift.Type, r.Shift.StartAt, r.Shift.EndAt,
                    r.EmployeeId, r.Name, att?.CheckInAt, att?.CheckOutAt,
                    LateMinutes(att?.CheckInAt, r.Shift.StartAt),
                    att?.CheckOutAt is { } outAt && outAt < r.Shift.EndAt ? (int)(r.Shift.EndAt - outAt).TotalMinutes : 0,
                    att?.CheckOutAt is { } outAt2 && outAt2 > r.Shift.EndAt ? (int)(outAt2 - r.Shift.EndAt).TotalMinutes : 0,
                    att is null && r.Shift.EndAt <= now,
                    att?.Approved ?? false);
            }).ToList());
    }

    public async Task<Result<AttendanceView>> ApproveAsync(CurrentUser user, Guid attendanceId, bool approved, CancellationToken ct)
    {
        var attendance = await db.Attendances.Include(a => a.Shift).SingleOrDefaultAsync(a => a.Id == attendanceId, ct);
        if (attendance is null || !user.CanManageBranch(attendance.Shift.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (approved && attendance.CheckOutAt is null)
        {
            return ServiceError.Conflict("Chưa rời ca", "Chỉ duyệt được chấm công đã có giờ ra.");
        }

        if (await db.Payrolls.AnyAsync(p => p.EmployeeId == attendance.EmployeeId
            && p.Month == VietnamTime.MonthStart(attendance.Shift.Date) && p.Status == PayrollStatuses.Finalized, ct))
        {
            return ServiceError.Conflict("Lương đã chốt", "Không thể đổi chấm công của tháng đã chốt lương.");
        }

        attendance.Approved = approved;
        attendance.ApprovedByUserId = approved ? user.UserId : null;
        attendance.ApprovedAt = approved ? clock.GetUtcNow() : null;
        await db.SaveChangesAsync(ct);
        return ToView(attendance, attendance.Shift);
    }

    internal static int LateMinutes(DateTimeOffset? checkIn, DateTimeOffset start) =>
        checkIn is { } c && c > start ? (int)(c - start).TotalMinutes : 0;

    private static AttendanceView ToView(Attendance a, Shift shift)
    {
        var late = LateMinutes(a.CheckInAt, shift.StartAt);
        return new AttendanceView(a.Id, a.ShiftId, a.EmployeeId, a.CheckInAt, a.CheckOutAt, late, late > LateThresholdMinutes, a.Approved);
    }
}

public static class PayrollStatuses
{
    public const string Draft = "Draft";
    public const string Finalized = "Finalized";
}
