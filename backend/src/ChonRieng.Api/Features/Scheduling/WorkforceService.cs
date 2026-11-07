using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Engagement;
using ChonRieng.Api.Services;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Scheduling;

public sealed class WorkforceService(AppDbContext db, ShiftService shifts, TimeProvider clock)
{
    public async Task<Result<IReadOnlyList<EmployeeView>>> ListEmployeesAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var ids = await db.EmployeeBranches.Where(b => b.BranchId == branchId).Select(b => b.EmployeeId).ToListAsync(ct);
        return Result<IReadOnlyList<EmployeeView>>.From(await QueryEmployees(db.Employees.Where(e => ids.Contains(e.Id)), ct));
    }

    public async Task<Result<EmployeeView>> UpdateEmployeeAsync(CurrentUser user, Guid employeeId, UpdateEmployeeRequest request, CancellationToken ct)
    {
        var employee = await db.Employees.Include(e => e.Branches).SingleOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null || !await CanManageEmployeeAsync(user, employee, ct))
        {
            return ServiceError.NotFound();
        }

        employee.HourlyRate = request.HourlyRate;
        employee.NightAllowance = request.NightAllowance;
        await db.SaveChangesAsync(ct);
        return (await QueryEmployees(db.Employees.Where(e => e.Id == employeeId), ct)).Single();
    }

    /// <summary>
    /// Admin may set any branches. A manager (decision D16) may only add or remove their own branches
    /// for Staff accounts; assignments to branches outside the manager's scope are kept unchanged.
    /// </summary>
    public async Task<Result<EmployeeView>> UpdateBranchesAsync(CurrentUser user, Guid employeeId, UpdateEmployeeBranchesRequest request, CancellationToken ct)
    {
        var employee = await db.Employees.Include(e => e.Branches).SingleOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null || !await CanManageEmployeeAsync(user, employee, ct))
        {
            return ServiceError.NotFound();
        }

        var requested = request.BranchIds.ToHashSet();
        if (!requested.Contains(request.HomeBranchId))
        {
            return ServiceError.Invalid("HomeBranchId", "Chi nhánh chính phải nằm trong danh sách chi nhánh.");
        }

        var requestedIds = requested.ToList();
        if (await db.Branches.CountAsync(b => requestedIds.Contains(b.Id), ct) != requested.Count)
        {
            return ServiceError.Invalid("BranchIds", "Có chi nhánh không tồn tại.");
        }

        var current = employee.Branches.Select(b => b.BranchId).ToHashSet();
        var final = user.IsAdmin
            ? requested
            : current.Where(id => !user.Scope.BranchIds.Contains(id)).Concat(requested.Where(user.Scope.BranchIds.Contains)).ToHashSet();
        if (!user.IsAdmin && requested.Any(id => !user.Scope.BranchIds.Contains(id) && !current.Contains(id)))
        {
            return ServiceError.Forbidden("Bạn chỉ được gán nhân viên vào chi nhánh mình quản lý.");
        }

        if (!final.Contains(request.HomeBranchId))
        {
            return ServiceError.Invalid("HomeBranchId", "Chi nhánh chính không hợp lệ.");
        }

        foreach (var removed in employee.Branches.Where(b => !final.Contains(b.BranchId)).ToList())
        {
            db.EmployeeBranches.Remove(removed);
        }

        foreach (var branchId in final.Where(id => !current.Contains(id)))
        {
            db.EmployeeBranches.Add(new EmployeeBranch { EmployeeId = employee.Id, BranchId = branchId });
        }

        // Clear IsHome first: the filtered unique index allows only one home branch per employee.
        foreach (var b in employee.Branches)
        {
            b.IsHome = false;
        }

        await db.SaveChangesAsync(ct);
        var home = await db.EmployeeBranches.SingleAsync(b => b.EmployeeId == employee.Id && b.BranchId == request.HomeBranchId, ct);
        home.IsHome = true;
        await db.SaveChangesAsync(ct);
        return (await QueryEmployees(db.Employees.Where(e => e.Id == employeeId), ct)).Single();
    }

    public async Task<Result<ShiftRequestView>> CreateRequestAsync(CurrentUser user, CreateShiftRequestRequest request, CancellationToken ct)
    {
        var employee = await db.Employees.Include(e => e.User).SingleOrDefaultAsync(e => e.UserId == user.UserId, ct);
        var shift = await db.Shifts.SingleOrDefaultAsync(s => s.Id == request.ShiftId, ct);
        if (employee is null || shift is null)
        {
            return ServiceError.NotFound();
        }

        var holds = await db.ShiftAssignments.AnyAsync(a => a.ShiftId == shift.Id && a.EmployeeId == employee.Id && a.Status == ShiftAssignmentStatus.Accepted, ct);
        if (!holds || shift.StartAt <= clock.GetUtcNow())
        {
            return ServiceError.Conflict("Không thể gửi đơn", "Chỉ gửi đơn cho ca sắp tới mà bạn đã nhận.");
        }

        if (await db.ShiftRequests.AnyAsync(r => r.ShiftId == shift.Id && r.RequesterEmployeeId == employee.Id && r.Status == ShiftRequestStatuses.Pending, ct))
        {
            return ServiceError.Conflict("Đã có đơn", "Bạn đã có đơn đang chờ duyệt cho ca này.");
        }

        if (request.Type == ShiftRequestType.Swap)
        {
            if (request.TargetEmployeeId is not { } targetId || targetId == employee.Id || !await db.Employees.AnyAsync(e => e.Id == targetId, ct))
            {
                return ServiceError.Invalid("TargetEmployeeId", "Đổi ca cần chọn một đồng nghiệp nhận thay.");
            }

            var violations = await shifts.CheckRulesAsync(targetId, shift, ct);
            if (violations.Any(v => v.IsBlocking))
            {
                return ServiceError.Conflict("Người nhận thay không phù hợp", "Đồng nghiệp này bị trùng lịch hoặc không đủ điều kiện.", violations);
            }
        }

        var entity = new ShiftRequest
        {
            Type = request.Type,
            ShiftId = shift.Id,
            RequesterEmployeeId = employee.Id,
            TargetEmployeeId = request.Type == ShiftRequestType.Swap ? request.TargetEmployeeId : null,
            Reason = request.Reason.Trim(),
            Status = ShiftRequestStatuses.Pending,
            CreatedAt = clock.GetUtcNow(),
        };
        db.ShiftRequests.Add(entity);
        foreach (var managerUserId in await shifts.ManagerUserIdsAsync(shift.BranchId, ct))
        {
            db.Notify(managerUserId, Notifier.RequestCreated, $"Đơn mới từ {employee.User.FullName}", ShiftService.Describe(shift), "/admin/requests", entity.CreatedAt);
        }

        await db.SaveChangesAsync(ct);
        return (await QueryRequests(db.ShiftRequests.Where(r => r.Id == entity.Id), ct)).Single();
    }

    public async Task<IReadOnlyList<ShiftRequestView>> MyRequestsAsync(CurrentUser user, CancellationToken ct) =>
        await QueryRequests(db.ShiftRequests.Where(r => r.RequesterEmployee.UserId == user.UserId
            || (r.TargetEmployee != null && r.TargetEmployee.UserId == user.UserId)), ct);

    public async Task<Result<IReadOnlyList<ShiftRequestView>>> ListRequestsAsync(CurrentUser user, Guid branchId, string? status, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var query = db.ShiftRequests.Where(r => r.Shift.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        return Result<IReadOnlyList<ShiftRequestView>>.From(await QueryRequests(query, ct));
    }

    public async Task<Result<ShiftRequestView>> ReviewAsync(CurrentUser user, Guid requestId, bool approve, string? note, CancellationToken ct)
    {
        var request = await db.ShiftRequests.Include(r => r.Shift).Include(r => r.RequesterEmployee)
            .SingleOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null || !user.CanManageBranch(request.Shift.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (request.Status != ShiftRequestStatuses.Pending)
        {
            return ServiceError.Conflict("Đơn đã xử lý", "Đơn này không còn chờ duyệt.");
        }

        var now = clock.GetUtcNow();
        if (approve)
        {
            var assignment = await db.ShiftAssignments.SingleOrDefaultAsync(a => a.ShiftId == request.ShiftId && a.EmployeeId == request.RequesterEmployeeId, ct);
            if (assignment is null || await db.Attendances.AnyAsync(a => a.ShiftId == request.ShiftId && a.EmployeeId == request.RequesterEmployeeId, ct))
            {
                return ServiceError.Conflict("Không thể duyệt", "Nhân viên không còn giữ ca hoặc đã điểm danh.");
            }

            if (request.Type == ShiftRequestType.Swap)
            {
                var targetId = request.TargetEmployeeId!.Value;
                var violations = await shifts.CheckRulesAsync(targetId, request.Shift, ct);
                if (violations.Any(v => v.IsBlocking))
                {
                    return ServiceError.Conflict("Người nhận thay bị trùng lịch", "Không thể đổi ca.", violations);
                }

                db.ShiftAssignments.Remove(assignment);
                var existing = await db.ShiftAssignments.SingleOrDefaultAsync(a => a.ShiftId == request.ShiftId && a.EmployeeId == targetId, ct);
                if (existing is null)
                {
                    db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = request.ShiftId, EmployeeId = targetId, Status = ShiftAssignmentStatus.Accepted, RespondedAt = now });
                }
                else
                {
                    existing.Status = ShiftAssignmentStatus.Accepted;
                    existing.RespondedAt = now;
                }

                var targetUserId = await db.Employees.Where(e => e.Id == targetId).Select(e => e.UserId).SingleAsync(ct);
                db.Notify(targetUserId, Notifier.ShiftOffered, "Bạn nhận thay một ca", ShiftService.Describe(request.Shift), "/pos/schedule", now);
            }
            else
            {
                // Leave and drop release the spot; the shift becomes open so eligible staff can claim it.
                db.ShiftAssignments.Remove(assignment);
                request.Shift.IsOpen = true;
            }
        }

        request.Status = approve ? ShiftRequestStatuses.Approved : ShiftRequestStatuses.Rejected;
        request.ReviewedAt = now;
        request.ReviewedByEmployeeId = await db.Employees.Where(e => e.UserId == user.UserId).Select(e => (Guid?)e.Id).SingleOrDefaultAsync(ct);
        db.Notify(request.RequesterEmployee.UserId, Notifier.RequestReviewed,
            approve ? "Đơn của bạn đã được duyệt" : "Đơn của bạn bị từ chối",
            ShiftService.Describe(request.Shift) + (string.IsNullOrWhiteSpace(note) ? "" : $". Ghi chú: {note.Trim()}"), "/pos/schedule", now);
        await db.SaveChangesAsync(ct);
        return (await QueryRequests(db.ShiftRequests.Where(r => r.Id == requestId), ct)).Single();
    }

    public async Task<Result<Done>> CancelRequestAsync(CurrentUser user, Guid requestId, CancellationToken ct)
    {
        var request = await db.ShiftRequests.SingleOrDefaultAsync(r => r.Id == requestId && r.RequesterEmployee.UserId == user.UserId, ct);
        if (request is null)
        {
            return ServiceError.NotFound();
        }

        if (request.Status != ShiftRequestStatuses.Pending)
        {
            return ServiceError.Conflict("Đơn đã xử lý", "Chỉ hủy được đơn đang chờ duyệt.");
        }

        request.Status = ShiftRequestStatuses.Cancelled;
        await db.SaveChangesAsync(ct);
        return new Done();
    }

    /// <summary>Employees sharing at least one branch with the caller (for choosing a swap partner).</summary>
    public async Task<IReadOnlyList<Colleague>> ColleaguesAsync(CurrentUser user, CancellationToken ct)
    {
        var me = await db.Employees.AsNoTracking().Include(e => e.Branches).SingleOrDefaultAsync(e => e.UserId == user.UserId, ct);
        if (me is null)
        {
            return [];
        }

        var branchIds = me.Branches.Select(b => b.BranchId).ToList();
        return await db.Employees.AsNoTracking()
            .Where(e => e.Id != me.Id && e.Branches.Any(b => branchIds.Contains(b.BranchId)))
            .OrderBy(e => e.User.FullName)
            .Select(e => new Colleague(e.Id, e.User.FullName, ShiftAssignmentStatus.Accepted))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AvailabilitySlot>> MyAvailabilityAsync(CurrentUser user, CancellationToken ct) =>
        await db.Availabilities.AsNoTracking()
            .Where(a => a.Employee.UserId == user.UserId)
            .OrderBy(a => a.IsRecurring ? 0 : 1).ThenBy(a => a.DayOfWeek).ThenBy(a => a.SpecificDate).ThenBy(a => a.From)
            .Select(a => new AvailabilitySlot(a.IsRecurring, a.DayOfWeek, a.SpecificDate, a.From, a.To))
            .ToListAsync(ct);

    public async Task<Result<IReadOnlyList<AvailabilitySlot>>> ReplaceAvailabilityAsync(CurrentUser user, IReadOnlyList<AvailabilitySlot> slots, CancellationToken ct)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.UserId == user.UserId, ct);
        if (employee is null)
        {
            return ServiceError.NotFound("Tài khoản không có hồ sơ nhân viên.");
        }

        foreach (var slot in slots)
        {
            if (slot.IsRecurring ? slot.DayOfWeek is null : slot.SpecificDate is null)
            {
                return ServiceError.Invalid("Slots", "Khung giờ lặp lại cần ngày trong tuần; khung giờ cụ thể cần ngày.");
            }

            if (slot.To == slot.From)
            {
                return ServiceError.Invalid("Slots", "Giờ bắt đầu và kết thúc phải khác nhau.");
            }
        }

        db.Availabilities.RemoveRange(db.Availabilities.Where(a => a.EmployeeId == employee.Id));
        db.Availabilities.AddRange(slots.Select(s => new Availability
        {
            EmployeeId = employee.Id,
            IsRecurring = s.IsRecurring,
            DayOfWeek = s.IsRecurring ? s.DayOfWeek : null,
            SpecificDate = s.IsRecurring ? null : s.SpecificDate,
            From = s.From,
            To = s.To,
        }));
        await db.SaveChangesAsync(ct);
        return Result<IReadOnlyList<AvailabilitySlot>>.From(await MyAvailabilityAsync(user, ct));
    }

    public async Task<Result<IReadOnlyList<AssistantEmployee>>> AssistantAsync(CurrentUser user, Guid branchId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var start = VietnamTime.StartOfDay(from);
        var end = VietnamTime.StartOfDay(to.AddDays(1));
        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.Branches.Any(b => b.BranchId == branchId))
            .Select(e => new
            {
                e.Id,
                e.User.FullName,
                Availability = db.Availabilities.Where(a => a.EmployeeId == e.Id)
                    .Select(a => new AvailabilitySlot(a.IsRecurring, a.DayOfWeek, a.SpecificDate, a.From, a.To)).ToList(),
                Busy = e.ShiftAssignments.Where(a => a.Status != ShiftAssignmentStatus.Declined && a.Shift.StartAt < end && a.Shift.EndAt > start)
                    .Select(a => new ShiftSlot(a.ShiftId, a.Shift.BranchId, a.Shift.StartAt, a.Shift.EndAt)).ToList(),
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<AssistantEmployee>>.From(employees.OrderBy(e => e.FullName)
            .Select(e => new AssistantEmployee(e.Id, e.FullName, e.Availability, e.Busy, Math.Round(e.Busy.Sum(b => b.Hours), 1)))
            .ToList());
    }

    private async Task<bool> CanManageEmployeeAsync(CurrentUser user, Employee employee, CancellationToken ct)
    {
        if (user.IsAdmin)
        {
            return true;
        }

        if (!user.IsManager || !employee.Branches.Any(b => user.Scope.BranchIds.Contains(b.BranchId)))
        {
            return false;
        }

        // Managers manage Staff only, never other managers or admins (no privilege escalation).
        var roles = await db.UserRoles.Where(ur => ur.UserId == employee.UserId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name).ToListAsync(ct);
        return roles.All(r => r == BusinessRoles.Staff);
    }

    private async Task<IReadOnlyList<EmployeeView>> QueryEmployees(IQueryable<Employee> query, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = await query.AsNoTracking()
            .Select(e => new
            {
                e.Id,
                e.UserId,
                e.User.FullName,
                Email = e.User.Email ?? "",
                e.User.PhoneNumber,
                e.User.LockoutEnd,
                e.HourlyRate,
                e.NightAllowance,
                Branches = e.Branches.Select(b => new { b.BranchId, b.IsHome }).ToList(),
                Roles = db.UserRoles.Where(ur => ur.UserId == e.UserId).Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!).ToList(),
            })
            .ToListAsync(ct);

        return rows.OrderBy(r => r.FullName).Select(r => new EmployeeView(r.Id, r.UserId, r.FullName, r.Email, r.PhoneNumber, r.Roles,
            r.HourlyRate, r.NightAllowance, r.Branches.Select(b => b.BranchId).ToList(),
            r.Branches.FirstOrDefault(b => b.IsHome)?.BranchId, r.LockoutEnd > now)).ToList();
    }

    private static async Task<IReadOnlyList<ShiftRequestView>> QueryRequests(IQueryable<ShiftRequest> query, CancellationToken ct) =>
        await query.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .Select(r => new ShiftRequestView(r.Id, r.Type, r.Status, r.ShiftId, r.Shift.BranchId, r.Shift.Branch.Name, r.Shift.Date,
                r.Shift.Type, r.Shift.StartAt, r.Shift.EndAt, r.RequesterEmployeeId, r.RequesterEmployee.User.FullName,
                r.TargetEmployeeId, r.TargetEmployee != null ? r.TargetEmployee.User.FullName : null, r.Reason, r.CreatedAt, r.ReviewedAt))
            .ToListAsync(ct);
}
