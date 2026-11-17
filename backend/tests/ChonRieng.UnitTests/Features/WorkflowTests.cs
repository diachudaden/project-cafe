using ChonRieng.Api.Features.Catalog;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Finance;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Api.Features.Scheduling;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChonRieng.UnitTests.Features;

/// <summary>Role workflows run through the real services on an isolated in-memory database.</summary>
public sealed class WorkflowTests : IDisposable
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"WorkflowTests-{Guid.NewGuid()}").Options);
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 11, 2, 10, 0, 0, VietnamTime.Offset));
    private readonly Branch _branch = NewBranch("A");
    private readonly Branch _otherBranch = NewBranch("B");
    private readonly Seat _seat;
    private readonly Seat _seat2;
    private readonly PricePlan _plan = new() { Name = "4 tiếng", DurationMinutes = 240, Price = 45_000, ExtendPrice = 40_000 };
    private readonly Shift _shift;
    private readonly (User User, Employee Employee) _staff;
    private readonly (User User, Employee Employee) _otherStaff;
    private readonly (User User, Employee Employee) _manager;
    private readonly (User User, Customer Customer) _customer;

    public WorkflowTests()
    {
        var zone = new Zone { BranchId = _branch.Id, Name = "Khu A" };
        _seat = new Seat { ZoneId = zone.Id, Code = "A-01", Type = SeatType.Single };
        _seat2 = new Seat { ZoneId = zone.Id, Code = "A-02", Type = SeatType.Single };
        _staff = NewEmployee("staff@test.invalid", _branch, 25_000);
        _otherStaff = NewEmployee("other@test.invalid", _otherBranch, 25_000);
        _manager = NewEmployee("manager@test.invalid", _branch, 35_000);
        var customerUser = new User { UserName = "c@test.invalid", Email = "c@test.invalid", NormalizedEmail = "C@TEST.INVALID", FullName = "Khách" };
        _customer = (customerUser, new Customer { UserId = customerUser.Id, Tier = Loyalty.Bronze, MemberCode = "CRTEST01" });
        _shift = new Shift
        {
            BranchId = _branch.Id,
            Date = DateOnly.FromDateTime(_clock.Now().DateTime),
            Type = ShiftType.Morning,
            StartAt = _clock.GetUtcNow().AddHours(-1),
            EndAt = _clock.GetUtcNow().AddHours(7),
            RequiredStaff = 1,
            Status = ShiftStatus.Published,
        };
        _db.AddRange(_branch, _otherBranch, zone, _seat, _seat2, _plan, _shift, customerUser, _customer.Customer);
        _db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = _shift.Id, EmployeeId = _staff.Employee.Id, Status = ShiftAssignmentStatus.Accepted });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private CurrentUser Staff => As(_staff.User, BusinessRoles.Staff, _branch.Id);
    private CurrentUser Manager => As(_manager.User, BusinessRoles.BranchManager, _branch.Id);
    private CurrentUser Customer => As(_customer.User, BusinessRoles.Customer);
    private SessionService Sessions => new(_db, _clock);
    private AttendanceService Attendance => new(_db, _clock);
    private ShiftService Shifts => new(_db, _clock);
    private FinanceService Finance => new(_db, Shifts, _clock);
    private LoyaltyService Loyalty_ => new(_db, _clock);

    [Fact]
    public async Task Staff_must_join_a_shift_before_selling()
    {
        var result = await Sessions.CheckInAsync(Staff, new CheckInSessionRequest(_seat.Id, _plan.Id, PaymentMethod.Cash, null), default);

        Assert.Equal(409, result.Error!.Status);
        Assert.Equal("Bạn chưa tham gia ca", result.Error.Title);
    }

    [Fact]
    public async Task Check_in_extend_and_checkout_keep_seat_bill_and_points_consistent()
    {
        Assert.True((await Attendance.CheckInAsync(Staff, _shift.Id, default)).IsSuccess);

        var checkIn = await Sessions.CheckInAsync(Staff, new CheckInSessionRequest(_seat.Id, _plan.Id, PaymentMethod.Cash, _customer.Customer.Id), default);
        var session = checkIn.Value!;
        Assert.Equal(45_000, session.Bill.Total);
        Assert.True(session.Bill.Claimed);
        Assert.Equal(45, session.PointsAwarded);
        Assert.Equal(_shift.Id, (await _db.Bills.SingleAsync()).ShiftId);

        var second = await Sessions.CheckInAsync(Staff, new CheckInSessionRequest(_seat.Id, _plan.Id, PaymentMethod.Cash, null), default);
        Assert.Equal("Chỗ đã có người", second.Error!.Title);

        var floor = (await new CatalogService(_db, _clock).GetFloorAsync(Staff, _branch.Id, default)).Value!;
        Assert.Equal(SeatLiveState.Occupied, floor.Zones.Single().Seats.Single(s => s.Id == _seat.Id).State);

        _clock.Advance(TimeSpan.FromMinutes(230));
        floor = (await new CatalogService(_db, _clock).GetFloorAsync(Staff, _branch.Id, default)).Value!;
        Assert.Equal(SeatLiveState.EndingSoon, floor.Zones.Single().Seats.Single(s => s.Id == _seat.Id).State);

        var extended = (await Sessions.ExtendAsync(Staff, session.Id, default)).Value!;
        Assert.Equal(85_000, extended.Bill.Total);
        Assert.Equal(session.EndAt.AddMinutes(240), extended.EndAt);
        Assert.Equal(85, (await _db.Customers.SingleAsync()).Points);

        var closed = (await Sessions.CloseAsync(Staff, session.Id, default)).Value!;
        Assert.Equal(SessionStatus.Closed, closed.Status);
        Assert.True((await Sessions.CheckInAsync(Staff, new CheckInSessionRequest(_seat.Id, _plan.Id, PaymentMethod.VietQr, null), default)).IsSuccess);
    }

    [Fact]
    public async Task Mixed_tender_cannot_be_submitted_for_a_new_check_in()
    {
        var result = await Sessions.CheckInAsync(
            Staff,
            new CheckInSessionRequest(_seat.Id, _plan.Id, PaymentMethod.Mixed, null),
            default);

        Assert.Equal(400, result.Error!.Status);
        Assert.Empty(await _db.Bills.ToListAsync());
    }

    [Fact]
    public async Task Staff_from_another_branch_cannot_see_or_sell_here()
    {
        var other = As(_otherStaff.User, BusinessRoles.Staff, _otherBranch.Id);

        Assert.Equal(404, (await new CatalogService(_db, _clock).GetFloorAsync(other, _branch.Id, default)).Error!.Status);
        Assert.Equal(404, (await Sessions.CheckInAsync(other, new CheckInSessionRequest(_seat.Id, _plan.Id, PaymentMethod.Cash, null), default)).Error!.Status);
    }

    [Fact]
    public async Task A_bill_can_be_claimed_once_within_seven_days()
    {
        await Attendance.CheckInAsync(Staff, _shift.Id, default);
        var walkIn = (await Sessions.CheckInAsync(Staff, new CheckInSessionRequest(_seat.Id, _plan.Id, PaymentMethod.Cash, null), default)).Value!;
        var other = (await Sessions.CheckInAsync(Staff, new CheckInSessionRequest(_seat2.Id, _plan.Id, PaymentMethod.Cash, null), default)).Value!;

        var claim = await Loyalty_.ClaimAsync(Customer, walkIn.Bill.Code.ToLowerInvariant(), default);
        Assert.Equal(45, claim.Value!.PointsEarned);
        Assert.Equal("Bill đã được tích điểm", (await Loyalty_.ClaimAsync(Customer, walkIn.Bill.Code, default)).Error!.Title);

        _clock.Advance(TimeSpan.FromDays(8));
        Assert.Equal("Bill đã quá hạn", (await Loyalty_.ClaimAsync(Customer, other.Bill.Code, default)).Error!.Title);

        var points = (await Loyalty_.PointsAsync(Customer, default)).Value!;
        Assert.Equal(45, points.Points);
        Assert.Single(points.History);
    }

    [Fact]
    public async Task Closing_requires_a_note_for_differences_and_manager_approval()
    {
        await Attendance.CheckInAsync(Staff, _shift.Id, default);
        await Sessions.CheckInAsync(Staff, new CheckInSessionRequest(_seat.Id, _plan.Id, PaymentMethod.Cash, null), default);
        await Sessions.CheckInAsync(Staff, new CheckInSessionRequest(_seat2.Id, _plan.Id, PaymentMethod.VietQr, null), default);

        var preview = (await Finance.PreviewAsync(Staff, _shift.Id, default)).Value!;
        Assert.Equal(45_000, preview.ExpectedCash);
        Assert.Equal(45_000, preview.VietQrTotal);

        var counted = new Dictionary<string, int> { ["20000"] = 2 };
        var missingNote = await Finance.SubmitClosingAsync(Staff, _shift.Id, new SubmitClosingRequest(counted, null, true), default);
        Assert.Equal(400, missingNote.Error!.Status);

        var submitted = (await Finance.SubmitClosingAsync(Staff, _shift.Id, new SubmitClosingRequest(counted, "Thiếu 5.000đ", true), default)).Value!;
        Assert.Equal(-5_000, submitted.Closing!.Difference);
        Assert.Equal(ClosingStatus.Submitted, submitted.Closing.Status);
        Assert.NotNull((await _db.Attendances.SingleAsync()).CheckOutAt);

        Assert.Equal(409, (await Finance.SubmitClosingAsync(Staff, _shift.Id, new SubmitClosingRequest(counted, "lại", true), default)).Error!.Status);
        Assert.Equal(404, (await Finance.ReviewClosingAsync(Staff, submitted.Closing.Id, new ReviewClosingRequest(true, null), default)).Error!.Status);

        var reviewed = (await Finance.ReviewClosingAsync(Manager, submitted.Closing.Id, new ReviewClosingRequest(true, "OK"), default)).Value!;
        Assert.Equal(ClosingStatus.Approved, reviewed.Closing.Status);
    }

    [Fact]
    public async Task Assignment_rules_run_on_the_server()
    {
        var overlapping = new Shift
        {
            BranchId = _branch.Id,
            Date = _shift.Date,
            Type = ShiftType.Afternoon,
            StartAt = _shift.EndAt.AddHours(-1),
            EndAt = _shift.EndAt.AddHours(5),
            RequiredStaff = 1,
        };
        var afterShortRest = new Shift
        {
            BranchId = _branch.Id,
            Date = _shift.Date,
            Type = ShiftType.Night,
            StartAt = _shift.EndAt.AddHours(4),
            EndAt = _shift.EndAt.AddHours(10),
            RequiredStaff = 1,
        };
        _db.Shifts.AddRange(overlapping, afterShortRest);
        await _db.SaveChangesAsync();

        var blocked = await Shifts.AssignAsync(Manager, overlapping.Id, new AssignRequest(_staff.Employee.Id, null), default);
        Assert.Contains(blocked.Error!.Violations!, v => v.Rule == "overlap");

        var needsReason = await Shifts.AssignAsync(Manager, afterShortRest.Id, new AssignRequest(_staff.Employee.Id, null), default);
        Assert.Contains(needsReason.Error!.Violations!, v => v.Rule == "rest" && v.CanOverride);

        var overridden = await Shifts.AssignAsync(Manager, afterShortRest.Id, new AssignRequest(_staff.Employee.Id, "Thiếu người"), default);
        Assert.Equal("Thiếu người", overridden.Value!.Assignments.Single().OverrideReason);

        var staffCannotAssign = await Shifts.AssignAsync(Staff, afterShortRest.Id, new AssignRequest(_staff.Employee.Id, null), default);
        Assert.Equal(404, staffCannotAssign.Error!.Status);
    }

    [Fact]
    public async Task Payroll_uses_only_approved_attendance_and_only_admin_finalizes()
    {
        _db.Attendances.Add(new Attendance
        {
            EmployeeId = _staff.Employee.Id,
            ShiftId = _shift.Id,
            CheckInAt = _shift.StartAt,
            CheckOutAt = _shift.StartAt.AddHours(8),
            Method = "App",
            Approved = true,
        });
        await _db.SaveChangesAsync();

        var month = VietnamTime.MonthStart(_shift.Date);
        var payroll = (await Finance.GeneratePayrollAsync(Manager, new GeneratePayrollRequest(_branch.Id, month), default)).Value!;
        var staffPayroll = payroll.Single(p => p.EmployeeId == _staff.Employee.Id);
        Assert.Equal(8, staffPayroll.TotalHours);
        Assert.Equal(200_000, staffPayroll.BaseAmount);

        Assert.Equal(403, (await Finance.FinalizePayrollAsync(Manager, staffPayroll.Id, default)).Error!.Status);
        var admin = new CurrentUser(Guid.NewGuid(), new HashSet<string> { BusinessRoles.Admin }, BranchAccessScope.Global);
        Assert.Equal(PayrollStatuses.Finalized, (await Finance.FinalizePayrollAsync(admin, staffPayroll.Id, default)).Value!.Status);
        Assert.Equal(409, (await Finance.AdjustPayrollAsync(Manager, staffPayroll.Id, new AdjustPayrollRequest(100_000, 0), default)).Error!.Status);
    }

    private static Branch NewBranch(string key) => new()
    {
        Name = $"Branch {key}",
        Address = "x",
        OpenTime = new TimeOnly(0, 0),
        CloseTime = new TimeOnly(0, 0),
        Is24h = true,
    };

    private (User, Employee) NewEmployee(string email, Branch branch, decimal rate)
    {
        var user = new User { UserName = email, Email = email, FullName = email };
        var employee = new Employee { UserId = user.Id, HourlyRate = rate, PinHash = "x" };
        _db.AddRange(user, employee, new EmployeeBranch { EmployeeId = employee.Id, BranchId = branch.Id, IsHome = true });
        return (user, employee);
    }

    private static CurrentUser As(User user, string role, params Guid[] branchIds) =>
        new(user.Id, new HashSet<string> { role }, BranchAccessScope.ForBranches(branchIds.ToHashSet()));

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now.ToUniversalTime();

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
