using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Engagement;
using ChonRieng.Api.Features.Finance;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Api.Features.Scheduling;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Services;

/// <summary>
/// Workflow history for the demo branches, following the same rules as the API: no overlapping shifts per
/// employee, one active session per seat, every bill tied to a shift with a checked-in employee, customer
/// totals equal to their point transactions. History is written once (first run, when the demo branches
/// have no shifts); later runs only add missing upcoming shifts so the POS demo keeps working.
/// </summary>
internal static class DevelopmentHistorySeeder
{
    private const string DemoNote = "[DEMO DATA]";

    private sealed record Template(ShiftType Type, TimeOnly Start, TimeOnly End, bool EndsNextDay);

    private sealed record WorkedShift(Shift Shift, Employee Employee, Attendance Attendance);

    public static async Task<string> SeedAsync(
        AppDbContext db,
        IReadOnlyList<Branch> branches,
        IReadOnlyList<(Employee Employee, int Branch)> managers,
        IReadOnlyList<(Employee Employee, int Branch)> staff,
        IReadOnlyList<Customer> customers,
        DateTimeOffset now,
        DateOnly? historyFrom,
        CancellationToken ct)
    {
        var rng = new Random(20261031);
        var today = DateOnly.FromDateTime(now.ToVietnam().DateTime);
        var branchIds = branches.Select(b => b.Id).ToList();
        var firstRun = !await db.Shifts.AnyAsync(s => branchIds.Contains(s.BranchId), ct);
        // Default: first day of last month; DevelopmentSeed__HistoryFrom pins it (e.g. 2026-09-01 for a review demo).
        var from = firstRun ? historyFrom ?? VietnamTime.MonthStart(today).AddMonths(-1) : today;
        var to = today.AddDays(7);

        var existing = (await db.Shifts.Where(s => branchIds.Contains(s.BranchId) && s.Date >= from && s.Date <= to)
                .Select(s => new { s.BranchId, s.Date, s.Type }).ToListAsync(ct))
            .Select(s => (s.BranchId, s.Date, s.Type)).ToHashSet();
        var seats = (await db.Seats.Where(s => branchIds.Contains(s.Zone.BranchId) && s.Status == SeatStatus.Available)
                .OrderBy(s => s.Code).Select(s => new { s.Id, s.Zone.BranchId }).ToListAsync(ct))
            .GroupBy(s => s.BranchId).ToDictionary(g => g.Key, g => g.Select(s => s.Id).ToList());
        var plans = await db.PricePlans.Where(p => p.ValidTo == null && (p.BranchId == null || branchIds.Contains(p.BranchId.Value)) && p.Name.StartsWith("[DEMO ONLY] Gói 4 tiếng"))
            .ToListAsync(ct);
        var chainPlan = plans.Single(p => p.BranchId == null);
        var managerUserIds = managers.ToLookup(m => m.Branch, m => m.Employee.UserId);

        var seatBusyUntil = new Dictionary<Guid, DateTimeOffset>();
        var billCodes = new HashSet<string>();
        var worked = new List<WorkedShift>();
        var upcoming = new List<(Shift Shift, Employee Employee, ShiftAssignment Assignment, int Branch)>();
        int shiftCount = 0, billCount = 0, missing = 0, closingCount = 0;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            for (var b = 0; b < branches.Count; b++)
            {
                var branch = branches[b];
                var spec = DevelopmentDataSeeder.BranchSpecs[b];
                var crew = staff.Where(s => s.Branch == b).Select(s => s.Employee).ToList();
                var templates = Templates(spec);
                for (var k = 0; k < templates.Count; k++)
                {
                    var t = templates[k];
                    if (existing.Contains((branch.Id, date, t.Type)))
                    {
                        continue;
                    }

                    var startAt = new DateTimeOffset(date.ToDateTime(t.Start), VietnamTime.Offset);
                    var endAt = new DateTimeOffset((t.EndsNextDay ? date.AddDays(1) : date).ToDateTime(t.End), VietnamTime.Offset);
                    // Rotation keeps ≥ 24 h between one employee's shifts, so no seeded assignment breaks a rule.
                    var employee = templates.Count == 3
                        ? crew[(date.DayNumber * 3 + k) % crew.Count]
                        : crew[(date.DayNumber + (k == 0 ? 0 : 2)) % crew.Count];

                    var daysAhead = date.DayNumber - today.DayNumber;
                    var shift = new Shift
                    {
                        BranchId = branch.Id,
                        Date = date,
                        Type = t.Type,
                        StartAt = startAt,
                        EndAt = endAt,
                        RequiredStaff = 1,
                        Status = spec.Key == "E" && daysAhead > 3 ? ShiftStatus.Draft : ShiftStatus.Published,
                    };
                    var assignment = new ShiftAssignment
                    {
                        ShiftId = shift.Id,
                        EmployeeId = employee.Id,
                        Status = daysAhead > 3 ? ShiftAssignmentStatus.Offered : ShiftAssignmentStatus.Accepted,
                        RespondedAt = daysAhead > 3 ? null : startAt.AddDays(-5),
                    };

                    // A few deliberate gaps for the manager screens: an understaffed open shift and a declined one.
                    if (spec.Key == "A" && daysAhead == 1 && t.Type == ShiftType.Evening)
                    {
                        shift.RequiredStaff = 2;
                        shift.IsOpen = true;
                    }

                    if (spec.Key == "B" && daysAhead == 2 && k == templates.Count - 1)
                    {
                        assignment.Status = ShiftAssignmentStatus.Declined;
                        assignment.DeclineReason = "Bận việc gia đình";
                        assignment.RespondedAt = now.AddHours(-3);
                        shift.IsOpen = true;
                    }

                    db.Shifts.Add(shift);
                    db.ShiftAssignments.Add(assignment);
                    shiftCount++;

                    if (!firstRun || shift.StartAt > now)
                    {
                        upcoming.Add((shift, employee, assignment, b));
                        continue;
                    }

                    var isCurrent = shift.EndAt > now;
                    if (!isCurrent && shiftCount % 37 == 0)
                    {
                        missing++; // accepted but never checked in: shows as "vắng" in attendance review
                        continue;
                    }

                    var checkIn = isCurrent ? startAt.AddMinutes(3) : startAt.AddMinutes(rng.Next(-8, 17));
                    var attendance = new Attendance
                    {
                        EmployeeId = employee.Id,
                        ShiftId = shift.Id,
                        CheckInAt = checkIn,
                        CheckOutAt = isCurrent ? null : endAt.AddMinutes(rng.Next(0, 11)),
                        Method = "App",
                        Approved = !isCurrent && date < today.AddDays(-2),
                    };
                    if (attendance.Approved)
                    {
                        attendance.ApprovedAt = attendance.CheckOutAt!.Value.AddHours(12);
                        attendance.ApprovedByUserId = managerUserIds[b].First();
                    }

                    db.Attendances.Add(attendance);
                    worked.Add(new WorkedShift(shift, employee, attendance));

                    var plan = plans.FirstOrDefault(p => p.BranchId == branch.Id) ?? chainPlan;
                    var sessions = isCurrent
                        ? CurrentSessions(now)
                        : Enumerable.Range(0, rng.Next(1, 4)).Select(_ =>
                        {
                            var latest = Math.Max(1, (int)(endAt - startAt).TotalMinutes - 120);
                            return (Start: startAt.AddMinutes(rng.Next(0, latest)), Extend: rng.NextDouble() < 0.3, Active: false);
                        }).OrderBy(s => s.Start).ToList();

                    var cash = 0m;
                    foreach (var s in sessions)
                    {
                        var seatId = seats[branch.Id].FirstOrDefault(id => seatBusyUntil.GetValueOrDefault(id) <= s.Start);
                        if (seatId == Guid.Empty)
                        {
                            continue;
                        }

                        var bill = AddSession(db, rng, customers, billCodes, shift, branch.Id, seatId, plan, s.Start, s.Extend, s.Active, now, seatBusyUntil);
                        billCount++;
                        if (bill.PaymentMethod == PaymentMethod.Cash)
                        {
                            cash += bill.Total;
                        }
                    }

                    if (!isCurrent)
                    {
                        AddClosing(db, shift, employee, attendance, cash, closingCount++, date < today.AddDays(-2), managerUserIds[b].First());
                    }
                }
            }
        }

        if (!firstRun)
        {
            return $"History already present; added {shiftCount} upcoming shifts.";
        }

        AddExpenses(db, rng, branches, managers, from, today);
        // Payroll for every full month in the history; the latest one stays draft for D–E so it can be finalized live.
        var lastFullMonth = VietnamTime.MonthStart(today).AddMonths(-1);
        for (var month = VietnamTime.MonthStart(from); month <= lastFullMonth; month = month.AddMonths(1))
        {
            AddPayroll(db, worked, staff, month, month == lastFullMonth);
        }
        await AddAvailabilityAsync(db, staff, ct);
        AddRequestsAndNotifications(db, upcoming, worked, managerUserIds, today, now);

        return $"History: {shiftCount} shifts, {worked.Count} attendances ({missing} missed), {billCount} bills, {closingCount} closings.";
    }

    private static List<Template> Templates(DevelopmentDataSeeder.BranchSpec spec) => spec.Is24h
        ?
        [
            new(ShiftType.Morning, new(6, 0), new(14, 0), false),
            new(ShiftType.Afternoon, new(14, 0), new(22, 0), false),
            new(ShiftType.Night, new(22, 0), new(6, 0), true),
        ]
        :
        [
            new(ShiftType.Morning, spec.Open, new(15, 0), false),
            new(ShiftType.Evening, new(15, 0), spec.Close, false),
        ];

    // One session ending soon, one overdue and one with time left, for the POS seat-map colours.
    private static List<(DateTimeOffset Start, bool Extend, bool Active)> CurrentSessions(DateTimeOffset now) =>
    [
        (now.AddMinutes(-230), false, true),
        (now.AddMinutes(-250), false, true),
        (now.AddMinutes(-60), false, true),
    ];

    private static Bill AddSession(
        AppDbContext db,
        Random rng,
        IReadOnlyList<Customer> customers,
        HashSet<string> billCodes,
        Shift shift,
        Guid branchId,
        Guid seatId,
        PricePlan plan,
        DateTimeOffset start,
        bool extend,
        bool active,
        DateTimeOffset now,
        Dictionary<Guid, DateTimeOffset> seatBusyUntil)
    {
        var scheduledEnd = start.AddMinutes(plan.DurationMinutes * (extend ? 2 : 1));
        var closedAt = active ? (DateTimeOffset?)null : scheduledEnd.AddMinutes(-rng.Next(0, 40));
        seatBusyUntil[seatId] = active ? DateTimeOffset.MaxValue : closedAt!.Value;

        // Skewed choice gives a spread of tiers: a few frequent Gold customers, many Bronze.
        var roll = rng.NextDouble();
        var customer = roll < 0.28 ? customers[(int)(customers.Count * Math.Pow(rng.NextDouble(), 2))] : null;
        var attachedAtCheckIn = customer is not null && roll < 0.2;

        var session = new Session
        {
            SeatId = seatId,
            CustomerId = attachedAtCheckIn ? customer!.Id : null,
            PricePlanId = plan.Id,
            StartAt = start,
            EndAt = scheduledEnd,
            Status = active ? SessionStatus.Active : SessionStatus.Closed,
            ClosedAt = closedAt,
        };
        string code;
        do
        {
            code = Loyalty.NewBillCode();
        }
        while (!billCodes.Add(code));

        var bill = new Bill
        {
            Code = code,
            BranchId = branchId,
            ShiftId = shift.Id,
            SessionId = session.Id,
            Total = plan.Price,
            PaymentMethod = rng.NextDouble() < 0.7 ? PaymentMethod.Cash : PaymentMethod.VietQr,
            CreatedAt = start,
        };
        db.Sessions.Add(session);
        db.Bills.Add(bill);

        if (attachedAtCheckIn)
        {
            bill.ClaimedByCustomerId = customer!.Id;
            bill.ClaimedAt = start;
            db.PointTransactions.Add(Loyalty.Award(customer, bill, plan.Price, start, $"Tích điểm hóa đơn {code}"));
        }

        if (extend)
        {
            var extendedAt = start.AddMinutes(plan.DurationMinutes - 10);
            db.SessionExtensions.Add(new SessionExtension { SessionId = session.Id, AddedMinutes = plan.DurationMinutes, Price = plan.ExtendPrice, CreatedAt = extendedAt });
            bill.Total += plan.ExtendPrice;
            if (attachedAtCheckIn)
            {
                db.PointTransactions.Add(Loyalty.Award(customer!, bill, plan.ExtendPrice, extendedAt, $"Gia hạn hóa đơn {code}"));
            }
        }

        var claimedLater = start.AddDays(1 + rng.Next(0, 3));
        if (customer is not null && !attachedAtCheckIn && claimedLater <= now)
        {
            bill.ClaimedByCustomerId = customer.Id;
            bill.ClaimedAt = claimedLater;
            db.PointTransactions.Add(Loyalty.Award(customer, bill, bill.Total, claimedLater, $"Tích điểm hóa đơn {code}"));
        }

        return bill;
    }

    private static void AddClosing(AppDbContext db, Shift shift, Employee employee, Attendance attendance, decimal expected, int index, bool approved, Guid reviewerUserId)
    {
        var difference = index % 11 == 5 ? -20_000m : index % 17 == 3 ? 10_000m : 0m;
        var counted = Math.Max(0, expected + difference);
        var remaining = counted;
        var notes = new Dictionary<string, int>();
        foreach (var value in FinanceService.Denominations)
        {
            var count = (int)(remaining / value);
            if (count > 0)
            {
                notes[value.ToString()] = count;
                remaining -= count * value;
            }
        }

        db.ShiftClosings.Add(new ShiftClosing
        {
            ShiftId = shift.Id,
            ClosedByEmployeeId = employee.Id,
            ExpectedCash = expected,
            CountedCash = counted,
            Difference = counted - expected,
            Note = counted == expected ? null : counted < expected ? $"{DemoNote} Thiếu tiền lẻ khi thối cho khách" : $"{DemoNote} Khách không lấy tiền thối",
            Denominations = System.Text.Json.JsonSerializer.Serialize(notes),
            Status = approved ? ClosingStatus.Approved : ClosingStatus.Submitted,
            SubmittedAt = attendance.CheckOutAt,
            ReviewedAt = approved ? attendance.CheckOutAt!.Value.AddHours(10) : null,
            ReviewedByUserId = approved ? reviewerUserId : null,
        });
    }

    private static void AddExpenses(AppDbContext db, Random rng, IReadOnlyList<Branch> branches, IReadOnlyList<(Employee Employee, int Branch)> managers, DateOnly from, DateOnly today)
    {
        Expense? toReverse = null;
        for (var b = 0; b < branches.Count; b++)
        {
            var creator = managers.First(m => m.Branch == b).Employee;
            for (var date = from; date <= today; date = date.AddDays(1))
            {
                var at = new DateTimeOffset(date.ToDateTime(new TimeOnly(10, 0)), VietnamTime.Offset);
                void Add(string category, decimal amount)
                {
                    var expense = new Expense
                    {
                        BranchId = branches[b].Id,
                        Category = category,
                        Amount = amount,
                        Date = at,
                        Note = DemoNote,
                        CreatedByEmployeeId = creator.Id,
                        CreatedByUserId = creator.UserId,
                    };
                    db.Expenses.Add(expense);
                    if (b == 0 && category == "Nguyên liệu")
                    {
                        toReverse ??= expense;
                    }
                }

                if (date.Day == 1)
                {
                    Add("Thuê mặt bằng", DevelopmentDataSeeder.BranchSpecs[b].Is24h ? 18_000_000m : 15_000_000m);
                }

                if (date.DayOfWeek == DayOfWeek.Monday)
                {
                    Add("Nguyên liệu", 600_000m + rng.Next(0, 7) * 100_000m);
                    Add("Điện nước", 400_000m + rng.Next(0, 6) * 100_000m);
                }
            }
        }

        if (toReverse is not null)
        {
            db.Expenses.Add(new Expense
            {
                BranchId = toReverse.BranchId,
                Category = toReverse.Category,
                Amount = -toReverse.Amount,
                Date = toReverse.Date.AddDays(1),
                Note = $"Đảo bút toán: {DemoNote} nhập trùng hóa đơn",
                ReversesExpenseId = toReverse.Id,
                CreatedByEmployeeId = toReverse.CreatedByEmployeeId,
                CreatedByUserId = toReverse.CreatedByUserId,
            });
        }
    }

    /// <summary>One month's payroll from approved attendance; in the latest month D–E stay draft.</summary>
    private static void AddPayroll(AppDbContext db, IReadOnlyList<WorkedShift> worked, IReadOnlyList<(Employee Employee, int Branch)> staff, DateOnly month, bool latest)
    {
        var next = month.AddMonths(1);
        for (var index = 0; index < staff.Count; index++)
        {
            var (employee, branch) = staff[index];
            var lines = worked.Where(w => w.Employee.Id == employee.Id && w.Attendance.Approved && w.Shift.Date >= month && w.Shift.Date < next)
                .Select(w => PayrollCalculator.Hours(w.Attendance.CheckInAt!.Value, w.Attendance.CheckOutAt!.Value))
                .ToList();
            if (lines.Count == 0)
            {
                continue;
            }

            var hours = lines.Sum(l => l.Hours);
            var payroll = new Payroll
            {
                EmployeeId = employee.Id,
                Month = month,
                TotalHours = hours,
                BaseAmount = PayrollCalculator.Vnd(hours * employee.HourlyRate),
                Allowance = PayrollCalculator.Vnd(lines.Sum(l => l.NightHours) * employee.NightAllowance),
                Bonus = index % 5 == 0 ? 200_000m : 0,
                Deduction = index % 7 == 3 ? 50_000m : 0,
                Status = !latest || branch <= 2 ? PayrollStatuses.Finalized : PayrollStatuses.Draft,
            };
            payroll.NetAmount = payroll.BaseAmount + payroll.Allowance + payroll.Bonus - payroll.Deduction;
            db.Payrolls.Add(payroll);
        }
    }

    private static async Task AddAvailabilityAsync(AppDbContext db, IReadOnlyList<(Employee Employee, int Branch)> staff, CancellationToken ct)
    {
        for (var i = 0; i < staff.Count; i++)
        {
            var employeeId = staff[i].Employee.Id;
            if (await db.Availabilities.AnyAsync(a => a.EmployeeId == employeeId, ct))
            {
                continue;
            }

            foreach (var offset in new[] { 0, 2, 4 })
            {
                db.Availabilities.Add(new Availability
                {
                    EmployeeId = employeeId,
                    IsRecurring = true,
                    DayOfWeek = (DayOfWeek)((i + offset) % 7),
                    From = new TimeOnly(6, 0),
                    To = new TimeOnly(23, 0),
                });
            }
        }
    }

    private static void AddRequestsAndNotifications(
        AppDbContext db,
        IReadOnlyList<(Shift Shift, Employee Employee, ShiftAssignment Assignment, int Branch)> upcoming,
        IReadOnlyList<WorkedShift> worked,
        ILookup<int, Guid> managerUserIds,
        DateOnly today,
        DateTimeOffset now)
    {
        void NotifyManagers(int branch, string title, string body, string link)
        {
            foreach (var userId in managerUserIds[branch])
            {
                db.Notify(userId, title.StartsWith("Đơn") ? Notifier.RequestCreated : Notifier.ShiftDeclined, title, body, link, now);
            }
        }

        var accepted = upcoming.Where(u => u.Assignment.Status == ShiftAssignmentStatus.Accepted && u.Shift.Status == ShiftStatus.Published).ToList();
        var leave = accepted.FirstOrDefault(u => u.Branch == 0 && u.Shift.Date > today);
        if (leave.Shift is not null)
        {
            db.ShiftRequests.Add(new ShiftRequest
            {
                Type = ShiftRequestType.Leave,
                ShiftId = leave.Shift.Id,
                RequesterEmployeeId = leave.Employee.Id,
                Reason = $"{DemoNote} Việc gia đình",
                Status = ShiftRequestStatuses.Pending,
                CreatedAt = now.AddHours(-5),
            });
            NotifyManagers(0, "Đơn xin nghỉ mới", ShiftService.Describe(leave.Shift), "/admin/requests");
        }

        // Swap at a 24/7 branch: the colleague two slots later has ≥ 8 h rest on both sides.
        var swap = accepted.FirstOrDefault(u => u.Branch == 2 && u.Shift.Date > today);
        if (swap.Shift is not null)
        {
            var crew = upcoming.Where(u => u.Branch == 2).Select(u => u.Employee).Distinct().OrderBy(e => e.Id).ToList();
            var target = crew.FirstOrDefault(e => e.Id != swap.Employee.Id
                && !upcoming.Any(u => u.Employee.Id == e.Id && u.Assignment.Status != ShiftAssignmentStatus.Declined
                    && u.Shift.StartAt < swap.Shift.EndAt.AddHours(8) && swap.Shift.StartAt < u.Shift.EndAt.AddHours(8)));
            if (target is not null)
            {
                db.ShiftRequests.Add(new ShiftRequest
                {
                    Type = ShiftRequestType.Swap,
                    ShiftId = swap.Shift.Id,
                    RequesterEmployeeId = swap.Employee.Id,
                    TargetEmployeeId = target.Id,
                    Reason = $"{DemoNote} Trùng lịch thi",
                    Status = ShiftRequestStatuses.Pending,
                    CreatedAt = now.AddHours(-2),
                });
                NotifyManagers(2, "Đơn đổi ca mới", ShiftService.Describe(swap.Shift), "/admin/requests");
            }
        }

        var past = worked.FirstOrDefault(w => w.Shift.BranchId == upcoming.FirstOrDefault(u => u.Branch == 3).Shift?.BranchId && w.Shift.Date == today.AddDays(-10));
        if (past is not null)
        {
            db.ShiftRequests.Add(new ShiftRequest
            {
                Type = ShiftRequestType.Leave,
                ShiftId = past.Shift.Id,
                RequesterEmployeeId = past.Employee.Id,
                Reason = $"{DemoNote} Đi du lịch",
                Status = ShiftRequestStatuses.Rejected,
                CreatedAt = past.Shift.StartAt.AddDays(-4),
                ReviewedAt = past.Shift.StartAt.AddDays(-3),
            });
        }

        var declined = upcoming.FirstOrDefault(u => u.Assignment.Status == ShiftAssignmentStatus.Declined);
        if (declined.Shift is not null)
        {
            NotifyManagers(declined.Branch, "Nhân viên từ chối ca", $"{ShiftService.Describe(declined.Shift)}. Lý do: {declined.Assignment.DeclineReason}", "/admin/schedule");
        }

        foreach (var employee in upcoming.Where(u => u.Assignment.Status == ShiftAssignmentStatus.Offered && u.Shift.Status == ShiftStatus.Published)
                     .Select(u => u.Employee).DistinctBy(e => e.Id))
        {
            db.Notify(employee.UserId, Notifier.ShiftOffered, "Lịch ca tuần mới đã được công bố",
                "Hãy xác nhận các ca được giao trong tuần tới.", "/pos/schedule", now.AddHours(-1));
        }
    }
}
