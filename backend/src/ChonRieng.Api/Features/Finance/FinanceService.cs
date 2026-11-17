using System.Text.Json;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Engagement;
using ChonRieng.Api.Features.Scheduling;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Finance;

public sealed class FinanceService(AppDbContext db, ShiftService shifts, TimeProvider clock)
{
    /// <summary>Vietnamese banknotes accepted in the closing count.</summary>
    public static readonly int[] Denominations = [500_000, 200_000, 100_000, 50_000, 20_000, 10_000, 5_000, 2_000, 1_000];

    public async Task<Result<ClosingPreview>> PreviewAsync(CurrentUser user, Guid shiftId, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking().Include(s => s.Branch).SingleOrDefaultAsync(s => s.Id == shiftId, ct);
        if (shift is null || !await CanCloseAsync(user, shift, ct))
        {
            return ServiceError.NotFound();
        }

        var bills = await db.Bills.AsNoTracking().Where(b => b.ShiftId == shiftId)
            .Select(b => new { b.CashPaidTotal, b.VietQrPaidTotal })
            .ToListAsync(ct);
        var active = await db.Sessions.CountAsync(s => s.Status == SessionStatus.Active && s.Seat.Zone.BranchId == shift.BranchId, ct);
        var closing = await QueryClosings(db.ShiftClosings.Where(c => c.ShiftId == shiftId), ct);
        return new ClosingPreview(shift.Id, shift.BranchId, shift.Branch.Name, shift.Type, shift.StartAt, shift.EndAt,
            bills.Sum(b => b.CashPaidTotal),
            bills.Sum(b => b.VietQrPaidTotal),
            await db.Bills.CountAsync(b => b.ShiftId == shiftId, ct), active, closing.SingleOrDefault()?.Closing);
    }

    public async Task<Result<ClosingPreview>> SubmitClosingAsync(CurrentUser user, Guid shiftId, SubmitClosingRequest request, CancellationToken ct)
    {
        var shift = await db.Shifts.SingleOrDefaultAsync(s => s.Id == shiftId, ct);
        var employee = await db.Employees.SingleOrDefaultAsync(e => e.UserId == user.UserId, ct);
        if (shift is null || employee is null || !await CanCloseAsync(user, shift, ct))
        {
            return ServiceError.NotFound();
        }

        var counts = new Dictionary<string, int>();
        foreach (var (key, count) in request.Denominations)
        {
            if (!int.TryParse(key, out var value) || !Denominations.Contains(value))
            {
                return ServiceError.Invalid("Denominations", $"Mệnh giá {key} không hợp lệ.");
            }

            if (count < 0)
            {
                return ServiceError.Invalid("Denominations", "Số tờ không được âm.");
            }

            if (count > 0)
            {
                counts[value.ToString()] = count;
            }
        }

        var expected = await db.Bills.Where(b => b.ShiftId == shiftId).SumAsync(b => (decimal?)b.CashPaidTotal, ct) ?? 0;
        var counted = counts.Sum(c => (decimal)int.Parse(c.Key) * c.Value);
        var difference = counted - expected;
        if (request.Submit && difference != 0 && string.IsNullOrWhiteSpace(request.Note))
        {
            return ServiceError.Invalid("Note", "Có chênh lệch tiền mặt: hãy ghi lý do.");
        }

        var closing = await db.ShiftClosings.SingleOrDefaultAsync(c => c.ShiftId == shiftId, ct);
        if (closing is { Status: ClosingStatus.Submitted or ClosingStatus.Approved })
        {
            return ServiceError.Conflict("Ca đã được kết", "Biên bản kết ca đã nộp; chỉ sửa được khi bị từ chối.");
        }

        closing ??= db.ShiftClosings.Add(new ShiftClosing { ShiftId = shiftId }).Entity;
        closing.ClosedByEmployeeId = employee.Id;
        closing.ExpectedCash = expected;
        closing.CountedCash = counted;
        closing.Difference = difference;
        closing.Note = request.Note?.Trim();
        closing.Denominations = JsonSerializer.Serialize(counts);
        closing.Status = request.Submit ? ClosingStatus.Submitted : ClosingStatus.Draft;

        if (request.Submit)
        {
            var now = clock.GetUtcNow();
            closing.SubmittedAt = now;
            closing.ReviewedAt = null;
            closing.ReviewedByUserId = null;
            closing.ReviewNote = null;
            // "Rời ca": submitting the closing also checks the employee out.
            var attendance = await db.Attendances.SingleOrDefaultAsync(a => a.ShiftId == shiftId && a.EmployeeId == employee.Id && a.CheckOutAt == null, ct);
            if (attendance is not null)
            {
                attendance.CheckOutAt = now;
            }

            foreach (var managerUserId in await shifts.ManagerUserIdsAsync(shift.BranchId, ct))
            {
                db.Notify(managerUserId, Notifier.ClosingSubmitted, "Biên bản kết ca chờ duyệt",
                    $"{ShiftService.Describe(shift)}: chênh lệch {difference:N0}đ.", "/admin/closings", now);
            }
        }

        await db.SaveChangesAsync(ct);
        return await PreviewAsync(user, shiftId, ct);
    }

    public async Task<Result<IReadOnlyList<ClosingListItem>>> ListClosingsAsync(CurrentUser user, Guid branchId, DateOnly from, DateOnly to, ClosingStatus? status, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var query = db.ShiftClosings.Where(c => c.Shift.BranchId == branchId && c.Shift.Date >= from && c.Shift.Date <= to && c.Status != ClosingStatus.Draft);
        if (status is not null)
        {
            query = query.Where(c => c.Status == status);
        }

        return Result<IReadOnlyList<ClosingListItem>>.From(await QueryClosings(query, ct));
    }

    public async Task<Result<ClosingListItem>> ReviewClosingAsync(CurrentUser user, Guid closingId, ReviewClosingRequest request, CancellationToken ct)
    {
        var closing = await db.ShiftClosings.Include(c => c.Shift).Include(c => c.ClosedByEmployee).SingleOrDefaultAsync(c => c.Id == closingId, ct);
        if (closing is null || !user.CanManageBranch(closing.Shift.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (closing.Status != ClosingStatus.Submitted)
        {
            return ServiceError.Conflict("Không thể duyệt", "Chỉ duyệt được biên bản đang chờ duyệt; biên bản đã duyệt không thể sửa.");
        }

        if (!request.Approve && string.IsNullOrWhiteSpace(request.Note))
        {
            return ServiceError.Invalid("Note", "Hãy ghi lý do từ chối.");
        }

        var now = clock.GetUtcNow();
        closing.Status = request.Approve ? ClosingStatus.Approved : ClosingStatus.Rejected;
        closing.ReviewedAt = now;
        closing.ReviewedByUserId = user.UserId;
        closing.ReviewNote = request.Note?.Trim();
        db.Notify(closing.ClosedByEmployee.UserId, Notifier.ClosingReviewed,
            request.Approve ? "Biên bản kết ca đã được duyệt" : "Biên bản kết ca bị từ chối",
            ShiftService.Describe(closing.Shift) + (string.IsNullOrWhiteSpace(request.Note) ? "" : $". Ghi chú: {request.Note.Trim()}"), "/pos", now);
        await db.SaveChangesAsync(ct);
        return (await QueryClosings(db.ShiftClosings.Where(c => c.Id == closingId), ct)).Single();
    }

    public async Task<Result<IReadOnlyList<ExpenseView>>> ListExpensesAsync(CurrentUser user, Guid branchId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var start = VietnamTime.StartOfDay(from);
        var end = VietnamTime.StartOfDay(to.AddDays(1));
        return Result<IReadOnlyList<ExpenseView>>.From(await QueryExpenses(db.Expenses.Where(e => e.BranchId == branchId && e.Date >= start && e.Date < end), ct));
    }

    public async Task<Result<ExpenseView>> CreateExpenseAsync(CurrentUser user, CreateExpenseRequest request, CancellationToken ct)
    {
        if (!user.CanManageBranch(request.BranchId) || !await db.Branches.AnyAsync(b => b.Id == request.BranchId, ct))
        {
            return ServiceError.NotFound();
        }

        var expense = new Expense
        {
            BranchId = request.BranchId,
            Category = request.Category.Trim(),
            Amount = PayrollCalculator.Vnd(request.Amount),
            Date = request.Date,
            Note = request.Note?.Trim(),
            CreatedByUserId = user.UserId,
            CreatedByEmployeeId = await EmployeeIdAsync(user, ct),
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return (await QueryExpenses(db.Expenses.Where(e => e.Id == expense.Id), ct)).Single();
    }

    public async Task<Result<ExpenseView>> ReverseExpenseAsync(CurrentUser user, Guid expenseId, string reason, CancellationToken ct)
    {
        var original = await db.Expenses.SingleOrDefaultAsync(e => e.Id == expenseId, ct);
        if (original is null || !user.CanManageBranch(original.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (original.ReversesExpenseId is not null || await db.Expenses.AnyAsync(e => e.ReversesExpenseId == expenseId, ct))
        {
            return ServiceError.Conflict("Không thể đảo", "Bút toán này đã được đảo hoặc chính nó là bút toán đảo.");
        }

        var reversal = new Expense
        {
            BranchId = original.BranchId,
            Category = original.Category,
            Amount = -original.Amount,
            Date = clock.GetUtcNow(),
            Note = $"Đảo bút toán: {reason.Trim()}",
            ReversesExpenseId = original.Id,
            CreatedByUserId = user.UserId,
            CreatedByEmployeeId = await EmployeeIdAsync(user, ct),
        };
        db.Expenses.Add(reversal);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceError.Conflict("Không thể đảo", "Bút toán này vừa được đảo.");
        }

        return (await QueryExpenses(db.Expenses.Where(e => e.Id == reversal.Id), ct)).Single();
    }

    /// <summary>Draft payroll for employees whose home branch is <paramref name="branchId"/>, from approved attendance.</summary>
    public async Task<Result<IReadOnlyList<PayrollView>>> GeneratePayrollAsync(CurrentUser user, GeneratePayrollRequest request, CancellationToken ct)
    {
        if (!user.CanManageBranch(request.BranchId))
        {
            return ServiceError.NotFound();
        }

        var month = VietnamTime.MonthStart(request.Month);
        var employees = await db.Employees.Where(e => e.Branches.Any(b => b.BranchId == request.BranchId && b.IsHome)).ToListAsync(ct);
        var ids = employees.Select(e => e.Id).ToList();
        var existing = await db.Payrolls.Where(p => ids.Contains(p.EmployeeId) && p.Month == month).ToDictionaryAsync(p => p.EmployeeId, ct);
        var lines = await LinesAsync(ids, month, ct);

        foreach (var employee in employees)
        {
            var payroll = existing.GetValueOrDefault(employee.Id);
            if (payroll is { Status: PayrollStatuses.Finalized })
            {
                continue;
            }

            payroll ??= db.Payrolls.Add(new Payroll { EmployeeId = employee.Id, Month = month, Status = PayrollStatuses.Draft }).Entity;
            var approved = lines.Where(l => l.EmployeeId == employee.Id && l.Approved).ToList();
            payroll.TotalHours = approved.Sum(l => l.Hours);
            payroll.BaseAmount = PayrollCalculator.Vnd(payroll.TotalHours * employee.HourlyRate);
            payroll.Allowance = PayrollCalculator.Vnd(approved.Sum(l => l.NightHours) * employee.NightAllowance);
            payroll.NetAmount = Math.Max(0, payroll.BaseAmount + payroll.Allowance + payroll.Bonus - payroll.Deduction);
        }

        await db.SaveChangesAsync(ct);
        return await ListPayrollAsync(user, request.BranchId, month, ct);
    }

    public async Task<Result<IReadOnlyList<PayrollView>>> ListPayrollAsync(CurrentUser user, Guid branchId, DateOnly month, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var start = VietnamTime.MonthStart(month);
        return Result<IReadOnlyList<PayrollView>>.From(await QueryPayroll(db.Payrolls.Where(p => p.Month == start
            && p.Employee.Branches.Any(b => b.BranchId == branchId && b.IsHome)), ct));
    }

    public async Task<Result<PayrollView>> AdjustPayrollAsync(CurrentUser user, Guid payrollId, AdjustPayrollRequest request, CancellationToken ct)
    {
        var payroll = await PayrollInScopeAsync(user, payrollId, ct);
        if (payroll is null)
        {
            return ServiceError.NotFound();
        }

        if (payroll.Status == PayrollStatuses.Finalized)
        {
            return ServiceError.Conflict("Lương đã chốt", "Không thể sửa bảng lương đã chốt.");
        }

        payroll.Bonus = PayrollCalculator.Vnd(request.Bonus);
        payroll.Deduction = PayrollCalculator.Vnd(request.Deduction);
        payroll.NetAmount = Math.Max(0, payroll.BaseAmount + payroll.Allowance + payroll.Bonus - payroll.Deduction);
        await db.SaveChangesAsync(ct);
        return (await QueryPayroll(db.Payrolls.Where(p => p.Id == payrollId), ct)).Single();
    }

    /// <summary>Only Admin finalizes (decision D10); finalized payroll is immutable.</summary>
    public async Task<Result<PayrollView>> FinalizePayrollAsync(CurrentUser user, Guid payrollId, CancellationToken ct)
    {
        if (!user.IsAdmin)
        {
            return ServiceError.Forbidden("Chỉ Admin được chốt lương.");
        }

        var payroll = await db.Payrolls.Include(p => p.Employee).SingleOrDefaultAsync(p => p.Id == payrollId, ct);
        if (payroll is null)
        {
            return ServiceError.NotFound();
        }

        if (payroll.Status == PayrollStatuses.Finalized)
        {
            return ServiceError.Conflict("Lương đã chốt", "Bảng lương này đã được chốt.");
        }

        payroll.Status = PayrollStatuses.Finalized;
        db.Notify(payroll.Employee.UserId, Notifier.PayrollFinalized, $"Lương tháng {payroll.Month:MM/yyyy} đã chốt",
            $"Thực nhận {payroll.NetAmount:N0}đ.", "/pos/payroll", clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return (await QueryPayroll(db.Payrolls.Where(p => p.Id == payrollId), ct)).Single();
    }

    public async Task<Result<MyPayrollResponse>> MyPayrollAsync(CurrentUser user, DateOnly month, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.UserId == user.UserId, ct);
        if (employee is null)
        {
            return ServiceError.NotFound("Tài khoản không có hồ sơ nhân viên.");
        }

        var start = VietnamTime.MonthStart(month);
        var lines = await LinesAsync([employee.Id], start, ct);
        var payroll = (await QueryPayroll(db.Payrolls.Where(p => p.EmployeeId == employee.Id && p.Month == start), ct)).SingleOrDefault();
        var result = lines.Select(l => new PayrollLine(l.Date, l.BranchName, l.ShiftType, l.CheckInAt, l.CheckOutAt, l.Hours, l.NightHours, l.Approved,
            l.Approved ? PayrollCalculator.Amount(l.Hours, l.NightHours, employee.HourlyRate, employee.NightAllowance) : 0)).ToList();
        return new MyPayrollResponse(start, employee.HourlyRate, employee.NightAllowance, result.Sum(l => l.Amount), payroll, result);
    }

    private sealed record WorkedLine(Guid EmployeeId, DateOnly Date, string BranchName, ShiftType ShiftType,
        DateTimeOffset CheckInAt, DateTimeOffset CheckOutAt, decimal Hours, decimal NightHours, bool Approved);

    private async Task<List<WorkedLine>> LinesAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly month, CancellationToken ct)
    {
        var end = month.AddMonths(1);
        var rows = await db.Attendances.AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.CheckInAt != null && a.CheckOutAt != null && a.Shift.Date >= month && a.Shift.Date < end)
            .OrderBy(a => a.CheckInAt)
            .Select(a => new { a.EmployeeId, a.Shift.Date, Branch = a.Shift.Branch.Name, a.Shift.Type, In = a.CheckInAt!.Value, Out = a.CheckOutAt!.Value, a.Approved })
            .ToListAsync(ct);
        return rows.Select(r =>
        {
            var (hours, night) = PayrollCalculator.Hours(r.In, r.Out);
            return new WorkedLine(r.EmployeeId, r.Date, r.Branch, r.Type, r.In, r.Out, hours, night, r.Approved);
        }).ToList();
    }

    private async Task<Payroll?> PayrollInScopeAsync(CurrentUser user, Guid payrollId, CancellationToken ct)
    {
        var payroll = await db.Payrolls.Include(p => p.Employee).ThenInclude(e => e.Branches).SingleOrDefaultAsync(p => p.Id == payrollId, ct);
        return payroll is not null && payroll.Employee.Branches.Any(b => b.IsHome && user.CanManageBranch(b.BranchId)) ? payroll : null;
    }

    private async Task<bool> CanCloseAsync(CurrentUser user, Shift shift, CancellationToken ct) =>
        user.CanManageBranch(shift.BranchId)
        || await db.Attendances.AnyAsync(a => a.ShiftId == shift.Id && a.Employee.UserId == user.UserId, ct);

    private async Task<Guid?> EmployeeIdAsync(CurrentUser user, CancellationToken ct) =>
        await db.Employees.Where(e => e.UserId == user.UserId).Select(e => (Guid?)e.Id).SingleOrDefaultAsync(ct);

    private static async Task<IReadOnlyList<ClosingListItem>> QueryClosings(IQueryable<ShiftClosing> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking()
            .OrderByDescending(c => c.Shift.StartAt)
            .Take(300)
            .Select(c => new { c, c.Shift.Date, c.Shift.Type, c.Shift.StartAt, c.Shift.EndAt, Name = c.ClosedByEmployee.User.FullName })
            .ToListAsync(ct);
        return rows.Select(r => new ClosingListItem(r.c.Id, r.c.ShiftId, r.Date, r.Type, r.StartAt, r.EndAt, new ClosingSummary(
            r.c.Id, r.c.Status, r.c.ExpectedCash, r.c.CountedCash, r.c.Difference, r.c.Note,
            JsonSerializer.Deserialize<Dictionary<string, int>>(string.IsNullOrWhiteSpace(r.c.Denominations) ? "{}" : r.c.Denominations) ?? [],
            r.Name, r.c.SubmittedAt, r.c.ReviewedAt, r.c.ReviewNote))).ToList();
    }

    private async Task<IReadOnlyList<ExpenseView>> QueryExpenses(IQueryable<Expense> query, CancellationToken ct) =>
        await query.AsNoTracking()
            .OrderByDescending(e => e.Date)
            .Take(500)
            .Select(e => new ExpenseView(e.Id, e.BranchId, e.Category, e.Amount, e.Date, e.Note,
                e.CreatedByEmployee != null ? e.CreatedByEmployee.User.FullName : null,
                e.ReversesExpenseId, db.Expenses.Any(x => x.ReversesExpenseId == e.Id)))
            .ToListAsync(ct);

    private static async Task<IReadOnlyList<PayrollView>> QueryPayroll(IQueryable<Payroll> query, CancellationToken ct) =>
        await query.AsNoTracking()
            .OrderBy(p => p.Employee.User.FullName)
            .Select(p => new PayrollView(p.Id, p.EmployeeId, p.Employee.User.FullName, p.Month, p.TotalHours, p.BaseAmount,
                p.Allowance, p.Bonus, p.Deduction, p.NetAmount, p.Status))
            .ToListAsync(ct);
}
