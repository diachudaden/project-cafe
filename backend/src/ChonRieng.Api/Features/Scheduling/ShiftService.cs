using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Engagement;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Scheduling;

public sealed class ShiftService(AppDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan CheckInOpensBefore = TimeSpan.FromMinutes(15);
    /// <summary>An open shift can still be claimed while it runs, as long as at least this much of it is left.</summary>
    public static readonly TimeSpan OpenShiftMinimumRemaining = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxShiftLength = TimeSpan.FromHours(16);

    public async Task<Result<IReadOnlyList<ShiftView>>> ListAsync(CurrentUser user, Guid branchId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!user.CanAccessBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        if (to < from || to.DayNumber - from.DayNumber > 62)
        {
            return ServiceError.Invalid("to", "Khoảng thời gian tối đa 62 ngày.");
        }

        var shifts = await QueryViews(db.Shifts.Where(s => s.BranchId == branchId && s.Date >= from && s.Date <= to), ct);
        return Result<IReadOnlyList<ShiftView>>.From(shifts);
    }

    public async Task<Result<ShiftView>> GetAsync(CurrentUser user, Guid shiftId, CancellationToken ct)
    {
        var view = (await QueryViews(db.Shifts.Where(s => s.Id == shiftId), ct)).SingleOrDefault();
        return view is null || !user.CanAccessBranch(view.BranchId) ? ServiceError.NotFound() : view;
    }

    public async Task<Result<ShiftView>> CreateAsync(CurrentUser user, CreateShiftRequest request, CancellationToken ct)
    {
        if (!user.CanManageBranch(request.BranchId) || !await db.Branches.AnyAsync(b => b.Id == request.BranchId, ct))
        {
            return ServiceError.NotFound();
        }

        var error = ValidateTimes(request.StartAt, request.EndAt);
        if (error is not null)
        {
            return error;
        }

        if (await db.Shifts.AnyAsync(s => s.BranchId == request.BranchId && s.StartAt == request.StartAt && s.Type == request.Type, ct))
        {
            return ServiceError.Conflict("Ca đã tồn tại", "Chi nhánh đã có ca cùng loại và giờ bắt đầu.");
        }

        // A shift added to a week that is already published goes out immediately; otherwise it would
        // silently stay Draft (invisible to staff, e.g. an open shift nobody can claim) until someone
        // presses "Công bố" again.
        var weekStart = request.Date.AddDays(-(((int)request.Date.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(6);
        var weekPublished = await db.Shifts.AnyAsync(s => s.BranchId == request.BranchId && s.Status == ShiftStatus.Published
            && s.Date >= weekStart && s.Date <= weekEnd, ct);

        var shift = new Shift
        {
            BranchId = request.BranchId,
            Date = request.Date,
            Type = request.Type,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            RequiredStaff = request.RequiredStaff,
            IsOpen = request.IsOpen,
            Status = weekPublished ? ShiftStatus.Published : ShiftStatus.Draft,
        };
        db.Shifts.Add(shift);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(user, shift.Id, ct)).Value!;
    }

    public async Task<Result<ShiftView>> UpdateAsync(CurrentUser user, Guid shiftId, UpdateShiftRequest request, CancellationToken ct)
    {
        var shift = await db.Shifts.SingleOrDefaultAsync(s => s.Id == shiftId, ct);
        if (shift is null || !user.CanManageBranch(shift.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (!RowVersionMatches(shift, request.RowVersion))
        {
            return Stale();
        }

        if (await db.Attendances.AnyAsync(a => a.ShiftId == shiftId, ct))
        {
            return ServiceError.Conflict("Ca đã bắt đầu", "Không thể sửa ca đã có nhân viên điểm danh.");
        }

        var error = ValidateTimes(request.StartAt, request.EndAt);
        if (error is not null)
        {
            return error;
        }

        var timeChanged = shift.StartAt != request.StartAt || shift.EndAt != request.EndAt;
        shift.Date = request.Date;
        shift.Type = request.Type;
        shift.StartAt = request.StartAt;
        shift.EndAt = request.EndAt;
        shift.RequiredStaff = request.RequiredStaff;
        shift.IsOpen = request.IsOpen;

        if (timeChanged && shift.Status == ShiftStatus.Published)
        {
            await NotifyAssigneesAsync(shift, Notifier.ShiftChanged, "Ca làm đã thay đổi", ct);
        }

        if (!await TrySaveAsync(ct))
        {
            return Stale();
        }

        return (await GetAsync(user, shift.Id, ct)).Value!;
    }

    public async Task<Result<Done>> DeleteAsync(CurrentUser user, Guid shiftId, CancellationToken ct)
    {
        var shift = await db.Shifts.Include(s => s.Assignments).SingleOrDefaultAsync(s => s.Id == shiftId, ct);
        if (shift is null || !user.CanManageBranch(shift.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (await db.Attendances.AnyAsync(a => a.ShiftId == shiftId, ct) || await db.Bills.AnyAsync(b => b.ShiftId == shiftId, ct))
        {
            return ServiceError.Conflict("Ca đã có dữ liệu", "Không thể xóa ca đã có chấm công hoặc hóa đơn.");
        }

        if (shift.Status == ShiftStatus.Published)
        {
            await NotifyAssigneesAsync(shift, Notifier.ShiftChanged, "Ca làm đã bị hủy", ct);
        }

        db.ShiftRequests.RemoveRange(db.ShiftRequests.Where(r => r.ShiftId == shiftId));
        db.ShiftAssignments.RemoveRange(shift.Assignments);
        db.Shifts.Remove(shift);
        await db.SaveChangesAsync(ct);
        return new Done();
    }

    public async Task<Result<ValidationResultResponse>> ValidateAsync(CurrentUser user, Guid shiftId, Guid employeeId, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking().SingleOrDefaultAsync(s => s.Id == shiftId, ct);
        if (shift is null || !user.CanManageBranch(shift.BranchId) || !await db.Employees.AnyAsync(e => e.Id == employeeId, ct))
        {
            return ServiceError.NotFound();
        }

        var violations = await CheckRulesAsync(employeeId, shift, ct);
        return ToValidation(violations);
    }

    public async Task<Result<ShiftView>> AssignAsync(CurrentUser user, Guid shiftId, AssignRequest request, CancellationToken ct)
    {
        var shift = await db.Shifts.SingleOrDefaultAsync(s => s.Id == shiftId, ct);
        var employee = await db.Employees.Include(e => e.User).SingleOrDefaultAsync(e => e.Id == request.EmployeeId, ct);
        if (shift is null || employee is null || !user.CanManageBranch(shift.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (shift.EndAt <= clock.GetUtcNow())
        {
            return ServiceError.Conflict("Ca đã kết thúc", "Không thể phân công cho ca đã qua.");
        }

        var violations = await CheckRulesAsync(employee.Id, shift, ct);
        if (!ShiftConflictChecker.IsAllowed(violations, request.OverrideReason))
        {
            return ServiceError.Conflict("Vi phạm quy tắc xếp ca", "Không thể phân công nhân viên vào ca này.", violations);
        }

        var assignment = await db.ShiftAssignments.SingleOrDefaultAsync(a => a.ShiftId == shiftId && a.EmployeeId == employee.Id, ct);
        if (assignment is { Status: not ShiftAssignmentStatus.Declined })
        {
            return ServiceError.Conflict("Đã phân công", "Nhân viên đã có trong ca này.");
        }

        assignment ??= db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = shiftId, EmployeeId = employee.Id }).Entity;
        assignment.Status = ShiftAssignmentStatus.Offered;
        assignment.RespondedAt = null;
        assignment.DeclineReason = null;
        assignment.OverrideReason = violations.Any(v => v.IsBlocking) ? request.OverrideReason?.Trim() : null;

        if (shift.Status == ShiftStatus.Published)
        {
            db.Notify(employee.UserId, Notifier.ShiftOffered, "Bạn được giao ca mới", Describe(shift), "/pos/schedule", clock.GetUtcNow());
        }

        TouchShift(shift);
        if (!await TrySaveAsync(ct))
        {
            return Stale();
        }

        return (await GetAsync(user, shift.Id, ct)).Value!;
    }

    public async Task<Result<Done>> UnassignAsync(CurrentUser user, Guid assignmentId, CancellationToken ct)
    {
        var assignment = await db.ShiftAssignments.Include(a => a.Shift).Include(a => a.Employee)
            .SingleOrDefaultAsync(a => a.Id == assignmentId, ct);
        if (assignment is null || !user.CanManageBranch(assignment.Shift.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (await db.Attendances.AnyAsync(a => a.ShiftId == assignment.ShiftId && a.EmployeeId == assignment.EmployeeId, ct))
        {
            return ServiceError.Conflict("Nhân viên đã điểm danh", "Không thể gỡ nhân viên đã điểm danh ca này.");
        }

        if (assignment.Shift.Status == ShiftStatus.Published)
        {
            db.Notify(assignment.Employee.UserId, Notifier.ShiftChanged, "Bạn đã được gỡ khỏi một ca", Describe(assignment.Shift), "/pos/schedule", clock.GetUtcNow());
        }

        db.ShiftAssignments.Remove(assignment);
        await db.SaveChangesAsync(ct);
        return new Done();
    }

    public async Task<Result<PublishResponse>> PublishAsync(CurrentUser user, PublishRequest request, CancellationToken ct)
    {
        if (!user.CanManageBranch(request.BranchId))
        {
            return ServiceError.NotFound();
        }

        var weekEnd = request.WeekStart.AddDays(6);
        var drafts = await db.Shifts.Include(s => s.Assignments).ThenInclude(a => a.Employee)
            .Where(s => s.BranchId == request.BranchId && s.Status == ShiftStatus.Draft && s.Date >= request.WeekStart && s.Date <= weekEnd)
            .ToListAsync(ct);

        var now = clock.GetUtcNow();
        var notified = new HashSet<Guid>();
        foreach (var shift in drafts)
        {
            shift.Status = ShiftStatus.Published;
            foreach (var a in shift.Assignments.Where(a => a.Status == ShiftAssignmentStatus.Offered))
            {
                notified.Add(a.Employee.UserId);
            }
        }

        foreach (var userId in notified)
        {
            db.Notify(userId, Notifier.ShiftOffered, "Lịch ca tuần mới đã được công bố",
                $"Tuần {request.WeekStart:dd/MM} – {weekEnd:dd/MM}: hãy xác nhận các ca được giao.", "/pos/schedule", now);
        }

        await db.SaveChangesAsync(ct);
        return new PublishResponse(drafts.Count, notified.Count);
    }

    public async Task<Result<CopyWeekResponse>> CopyWeekAsync(CurrentUser user, CopyWeekRequest request, CancellationToken ct)
    {
        if (!user.CanManageBranch(request.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (request.SourceWeekStart == request.TargetWeekStart)
        {
            return ServiceError.Invalid("TargetWeekStart", "Tuần đích phải khác tuần nguồn.");
        }

        var dayOffset = request.TargetWeekStart.DayNumber - request.SourceWeekStart.DayNumber;
        var source = await db.Shifts.AsNoTracking().Include(s => s.Assignments).ThenInclude(a => a.Employee).ThenInclude(e => e.User)
            .Where(s => s.BranchId == request.BranchId && s.Date >= request.SourceWeekStart && s.Date <= request.SourceWeekStart.AddDays(6))
            .ToListAsync(ct);
        var existing = await db.Shifts.AsNoTracking()
            .Where(s => s.BranchId == request.BranchId && s.Date >= request.TargetWeekStart && s.Date <= request.TargetWeekStart.AddDays(6))
            .Select(s => new { s.StartAt, s.Type })
            .ToListAsync(ct);

        int created = 0, skipped = 0;
        var conflicts = new List<CopyConflict>();
        var now = clock.GetUtcNow();
        foreach (var s in source.OrderBy(s => s.StartAt))
        {
            var startAt = s.StartAt.AddDays(dayOffset);
            if (startAt <= now || existing.Any(e => e.StartAt == startAt && e.Type == s.Type))
            {
                skipped++;
                continue;
            }

            var copy = new Shift
            {
                BranchId = s.BranchId,
                Date = s.Date.AddDays(dayOffset),
                Type = s.Type,
                StartAt = startAt,
                EndAt = s.EndAt.AddDays(dayOffset),
                RequiredStaff = s.RequiredStaff,
                IsOpen = s.IsOpen,
                Status = ShiftStatus.Draft,
            };
            db.Shifts.Add(copy);
            created++;

            if (!request.IncludeAssignments)
            {
                continue;
            }

            foreach (var a in s.Assignments.Where(a => a.Status != ShiftAssignmentStatus.Declined))
            {
                // Rules see shifts saved earlier plus copies added in this request.
                var violations = await CheckRulesAsync(a.EmployeeId, copy, ct, pendingShifts: db.ChangeTracker.Entries<ShiftAssignment>()
                    .Where(e => e.State == EntityState.Added && e.Entity.EmployeeId == a.EmployeeId)
                    .Select(e => db.ChangeTracker.Entries<Shift>().Single(x => x.Entity.Id == e.Entity.ShiftId).Entity)
                    .Select(x => new ShiftSlot(x.Id, x.BranchId, x.StartAt, x.EndAt))
                    .ToList());
                if (violations.Any(v => v.IsBlocking))
                {
                    conflicts.Add(new CopyConflict(copy.Date, copy.Type, a.Employee.User.FullName, violations));
                    continue;
                }

                db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = copy.Id, EmployeeId = a.EmployeeId, Status = ShiftAssignmentStatus.Offered });
            }
        }

        await db.SaveChangesAsync(ct);
        return new CopyWeekResponse(created, skipped, conflicts);
    }

    public async Task<IReadOnlyList<MyShiftView>> MyShiftsAsync(CurrentUser user, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var employeeId = await db.Employees.Where(e => e.UserId == user.UserId).Select(e => (Guid?)e.Id).SingleOrDefaultAsync(ct);
        if (employeeId is null)
        {
            return [];
        }

        var now = clock.GetUtcNow();
        var rows = await db.ShiftAssignments.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.Shift.Status == ShiftStatus.Published && a.Shift.Date >= from && a.Shift.Date <= to)
            .OrderBy(a => a.Shift.StartAt)
            .Select(a => new
            {
                a.Id,
                a.Status,
                Shift = a.Shift,
                BranchName = a.Shift.Branch.Name,
                BranchAddress = a.Shift.Branch.Address,
                Attendance = a.Shift.Attendances.Where(x => x.EmployeeId == employeeId).Select(x => new { x.CheckInAt, x.CheckOutAt }).FirstOrDefault(),
                ClosingStatus = a.Shift.Closing != null ? (ClosingStatus?)a.Shift.Closing.Status : null,
                HasAttendance = a.Shift.Attendances.Any(),
                Colleagues = a.Shift.Assignments
                    .Where(x => x.EmployeeId != employeeId && x.Status != ShiftAssignmentStatus.Declined)
                    .Select(x => new Colleague(x.EmployeeId, x.Employee.User.FullName, x.Status))
                    .ToList(),
            })
            .ToListAsync(ct);

        return rows.Select(r => new MyShiftView(
            r.Id, r.Status, r.Shift.Id, r.Shift.BranchId, r.BranchName, r.BranchAddress, r.Shift.Date, r.Shift.Type,
            r.Shift.StartAt, r.Shift.EndAt, PhaseOf(r.Shift.Status, r.HasAttendance, r.ClosingStatus),
            r.Attendance?.CheckInAt, r.Attendance?.CheckOutAt,
            r.Status == ShiftAssignmentStatus.Accepted && r.Attendance is null
                && now >= r.Shift.StartAt - CheckInOpensBefore && now < r.Shift.EndAt,
            r.Colleagues)).ToList();
    }

    public async Task<Result<Done>> AcceptAsync(CurrentUser user, Guid assignmentId, CancellationToken ct)
    {
        var assignment = await OwnAssignmentAsync(user, assignmentId, ct);
        if (assignment is null)
        {
            return ServiceError.NotFound();
        }

        if (assignment.Status != ShiftAssignmentStatus.Offered)
        {
            return ServiceError.Conflict("Không thể nhận ca", "Ca này không còn chờ phản hồi.");
        }

        var violations = await CheckRulesAsync(assignment.EmployeeId, assignment.Shift, ct);
        if (!ShiftConflictChecker.IsAllowed(violations, assignment.OverrideReason))
        {
            return ServiceError.Conflict("Vi phạm quy tắc xếp ca", "Bạn không thể nhận ca này.", violations);
        }

        assignment.Status = ShiftAssignmentStatus.Accepted;
        assignment.RespondedAt = clock.GetUtcNow();
        TouchShift(assignment.Shift);
        return await TrySaveAsync(ct) ? new Done() : Stale();
    }

    public async Task<Result<Done>> DeclineAsync(CurrentUser user, Guid assignmentId, string reason, CancellationToken ct)
    {
        var assignment = await OwnAssignmentAsync(user, assignmentId, ct);
        if (assignment is null)
        {
            return ServiceError.NotFound();
        }

        if (assignment.Status != ShiftAssignmentStatus.Offered)
        {
            return ServiceError.Conflict("Không thể từ chối", "Ca đã được nhận; hãy gửi đơn xin nghỉ hoặc trả ca.");
        }

        var now = clock.GetUtcNow();
        assignment.Status = ShiftAssignmentStatus.Declined;
        assignment.RespondedAt = now;
        assignment.DeclineReason = reason.Trim();

        foreach (var managerUserId in await ManagerUserIdsAsync(assignment.Shift.BranchId, ct))
        {
            db.Notify(managerUserId, Notifier.ShiftDeclined, $"{assignment.Employee.User.FullName} từ chối ca",
                $"{Describe(assignment.Shift)}. Lý do: {assignment.DeclineReason}", "/admin/schedule", now);
        }

        await db.SaveChangesAsync(ct);
        return new Done();
    }

    /// <summary>
    /// Open shifts form one chain-wide pool: every employee sees published open shifts of every active branch,
    /// their own branches first. Before, only linked branches were listed, so a staff member linked to the main
    /// branch never saw open shifts posted at the other branches.
    /// </summary>
    public async Task<IReadOnlyList<OpenShiftView>> OpenShiftsAsync(CurrentUser user, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().Include(e => e.Branches).SingleOrDefaultAsync(e => e.UserId == user.UserId, ct);
        if (employee is null)
        {
            return [];
        }

        var branchIds = employee.Branches.Select(b => b.BranchId).ToList();
        // Running shifts stay listed while enough of them is left to be worth joining.
        var claimableUntil = clock.GetUtcNow() + OpenShiftMinimumRemaining;
        return await db.Shifts.AsNoTracking()
            .Where(s => s.IsOpen && s.Status == ShiftStatus.Published && s.EndAt > claimableUntil && s.Branch.IsActive
                && !s.Assignments.Any(a => a.EmployeeId == employee.Id && a.Status != ShiftAssignmentStatus.Declined)
                && s.Assignments.Count(a => a.Status == ShiftAssignmentStatus.Accepted) < s.RequiredStaff)
            .OrderByDescending(s => branchIds.Contains(s.BranchId))
            .ThenBy(s => s.StartAt)
            .Select(s => new OpenShiftView(s.Id, s.BranchId, s.Branch.Name, s.Date, s.Type, s.StartAt, s.EndAt, s.RequiredStaff,
                s.Assignments.Count(a => a.Status == ShiftAssignmentStatus.Accepted), branchIds.Contains(s.BranchId)))
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task<Result<Done>> ClaimAsync(CurrentUser user, Guid shiftId, CancellationToken ct)
    {
        var employee = await db.Employees.Include(e => e.Branches).SingleOrDefaultAsync(e => e.UserId == user.UserId, ct);
        var shift = await db.Shifts.Include(s => s.Assignments).Include(s => s.Branch).SingleOrDefaultAsync(s => s.Id == shiftId, ct);
        if (employee is null || shift is null || !shift.IsOpen || shift.Status != ShiftStatus.Published
            || shift.EndAt <= clock.GetUtcNow() + OpenShiftMinimumRemaining || !shift.Branch.IsActive)
        {
            return ServiceError.NotFound("Không tìm thấy ca trống.");
        }

        var accepted = shift.Assignments.Count(a => a.Status == ShiftAssignmentStatus.Accepted);
        if (accepted >= shift.RequiredStaff)
        {
            return ServiceError.Conflict("Ca đã đủ người", "Ca vừa được người khác nhận.");
        }

        // Claiming an open shift at another branch links the employee to that branch, so the
        // branch rule does not apply here (the remaining rules still do).
        var violations = (await CheckRulesAsync(employee.Id, shift, ct)).Where(v => v.Rule != "branch").ToList();
        if (!ShiftConflictChecker.IsAllowed(violations, null))
        {
            return ServiceError.Conflict("Vi phạm quy tắc xếp ca", "Bạn không thể nhận ca này.", violations);
        }

        if (employee.Branches.All(b => b.BranchId != shift.BranchId))
        {
            db.EmployeeBranches.Add(new EmployeeBranch { EmployeeId = employee.Id, BranchId = shift.BranchId, IsHome = false });
        }

        var assignment = shift.Assignments.SingleOrDefault(a => a.EmployeeId == employee.Id)
            ?? db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = shift.Id, EmployeeId = employee.Id }).Entity;
        assignment.Status = ShiftAssignmentStatus.Accepted;
        assignment.RespondedAt = clock.GetUtcNow();
        assignment.DeclineReason = null;
        // Writing the shift row makes RowVersion reject a simultaneous claim of the last spot.
        shift.IsOpen = accepted + 1 < shift.RequiredStaff;
        return await TrySaveAsync(ct) ? new Done() : ServiceError.Conflict("Ca vừa được người khác nhận", "Hãy tải lại danh sách ca trống.");
    }

    internal async Task<IReadOnlyList<RuleViolation>> CheckRulesAsync(
        Guid employeeId,
        Shift candidate,
        CancellationToken ct,
        IReadOnlyCollection<ShiftSlot>? pendingShifts = null)
    {
        var windowStart = candidate.StartAt.AddDays(-8);
        var windowEnd = candidate.EndAt.AddDays(8);
        var others = await db.ShiftAssignments.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.ShiftId != candidate.Id && a.Status != ShiftAssignmentStatus.Declined
                && a.Shift.StartAt < windowEnd && a.Shift.EndAt > windowStart)
            .Select(a => new ShiftSlot(a.ShiftId, a.Shift.BranchId, a.Shift.StartAt, a.Shift.EndAt))
            .ToListAsync(ct);
        if (pendingShifts is not null)
        {
            others.AddRange(pendingShifts.Where(p => p.ShiftId != candidate.Id));
        }

        var branches = await db.EmployeeBranches.AsNoTracking()
            .Where(b => b.EmployeeId == employeeId).Select(b => b.BranchId).ToHashSetAsync(ct);
        var onLeave = await db.ShiftRequests.AsNoTracking().AnyAsync(r => r.ShiftId == candidate.Id
            && r.RequesterEmployeeId == employeeId && r.Type == ShiftRequestType.Leave && r.Status == ShiftRequestStatuses.Approved, ct);

        return ShiftConflictChecker.Default.Check(new ShiftRuleContext(
            employeeId,
            new ShiftSlot(candidate.Id, candidate.BranchId, candidate.StartAt, candidate.EndAt),
            others,
            branches,
            onLeave));
    }

    internal static ShiftPhase PhaseOf(ShiftStatus status, bool hasAttendance, ClosingStatus? closing) => closing switch
    {
        ClosingStatus.Approved => ShiftPhase.Approved,
        ClosingStatus.Submitted or ClosingStatus.Rejected => ShiftPhase.Closed,
        _ when hasAttendance => ShiftPhase.InProgress,
        _ => status == ShiftStatus.Published ? ShiftPhase.Published : ShiftPhase.Draft,
    };

    internal static string Describe(Shift shift) =>
        $"Ca {shift.StartAt.ToVietnam():HH:mm}–{shift.EndAt.ToVietnam():HH:mm} ngày {shift.Date:dd/MM}";

    internal async Task<IReadOnlyList<Guid>> ManagerUserIdsAsync(Guid branchId, CancellationToken ct)
    {
        var managerRoleId = await db.Roles.Where(r => r.Name == Services.BusinessRoles.BranchManager).Select(r => r.Id).SingleOrDefaultAsync(ct);
        return await db.EmployeeBranches.AsNoTracking()
            .Where(b => b.BranchId == branchId && db.UserRoles.Any(ur => ur.UserId == b.Employee.UserId && ur.RoleId == managerRoleId))
            .Select(b => b.Employee.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    private async Task<ShiftAssignment?> OwnAssignmentAsync(CurrentUser user, Guid assignmentId, CancellationToken ct) =>
        await db.ShiftAssignments.Include(a => a.Shift).Include(a => a.Employee).ThenInclude(e => e.User)
            .SingleOrDefaultAsync(a => a.Id == assignmentId && a.Employee.UserId == user.UserId, ct);

    private async Task NotifyAssigneesAsync(Shift shift, string type, string title, CancellationToken ct)
    {
        var userIds = await db.ShiftAssignments.Where(a => a.ShiftId == shift.Id && a.Status != ShiftAssignmentStatus.Declined)
            .Select(a => a.Employee.UserId).ToListAsync(ct);
        foreach (var userId in userIds)
        {
            db.Notify(userId, type, title, Describe(shift), "/pos/schedule", clock.GetUtcNow());
        }
    }

    private async Task<IReadOnlyList<ShiftView>> QueryViews(IQueryable<Shift> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking()
            .OrderBy(s => s.StartAt)
            .Select(s => new
            {
                Shift = s,
                BranchName = s.Branch.Name,
                HasAttendance = s.Attendances.Any(),
                ClosingStatus = s.Closing != null ? (ClosingStatus?)s.Closing.Status : null,
                Assignments = s.Assignments.OrderBy(a => a.Employee.User.FullName).Select(a => new AssignmentView(
                    a.Id, a.EmployeeId, a.Employee.User.FullName, a.Status, a.RespondedAt, a.DeclineReason, a.OverrideReason)).ToList(),
            })
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            var accepted = r.Assignments.Count(a => a.Status == ShiftAssignmentStatus.Accepted);
            return new ShiftView(r.Shift.Id, r.Shift.BranchId, r.BranchName, r.Shift.Date, r.Shift.Type, r.Shift.StartAt, r.Shift.EndAt,
                r.Shift.RequiredStaff, r.Shift.IsOpen, r.Shift.Status, PhaseOf(r.Shift.Status, r.HasAttendance, r.ClosingStatus),
                Convert.ToBase64String(r.Shift.RowVersion ?? []), accepted, accepted < r.Shift.RequiredStaff, r.Assignments);
        }).ToList();
    }

    private static ServiceError? ValidateTimes(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            return ServiceError.Invalid("EndAt", "Giờ kết thúc phải sau giờ bắt đầu.");
        }

        return end - start > MaxShiftLength ? ServiceError.Invalid("EndAt", "Một ca dài tối đa 16 giờ.") : null;
    }

    private static ValidationResultResponse ToValidation(IReadOnlyList<RuleViolation> violations)
    {
        var blocking = violations.Where(v => v.IsBlocking).ToList();
        return new ValidationResultResponse(blocking.Count == 0, blocking.Count > 0 && blocking.All(v => v.CanOverride), violations);
    }

    private static bool RowVersionMatches(Shift shift, string rowVersion) =>
        shift.RowVersion is not { Length: > 0 } || Convert.ToBase64String(shift.RowVersion) == rowVersion;

    // Marks the shift row as modified so its RowVersion is checked and bumped on save.
    private void TouchShift(Shift shift) => db.Entry(shift).Property(s => s.RequiredStaff).IsModified = true;

    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private static ServiceError Stale() =>
        ServiceError.Conflict("Ca vừa được người khác cập nhật", "Hãy tải lại lịch ca rồi thử lại.");
}

public static class ShiftRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}
