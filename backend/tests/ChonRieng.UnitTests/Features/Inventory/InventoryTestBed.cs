using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Inventory;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChonRieng.UnitTests.Features.Inventory;

/// <summary>One branch with a running morning shift, a staff member checked in, a manager and two items.</summary>
public sealed class InventoryTestBed : IDisposable
{
    public AppDbContext Db { get; }
    public TestClock Clock { get; } = new(new DateTimeOffset(2026, 11, 15, 10, 0, 0, VietnamTime.Offset));
    public Branch Branch { get; } = NewBranch("A");
    public Branch OtherBranch { get; } = NewBranch("B");
    public Seat Seat { get; }
    public PricePlan Plan { get; } = new() { Name = "4 tiếng", DurationMinutes = 240, Price = 45_000, ExtendPrice = 40_000 };
    public Shift Shift { get; }
    public (User User, Employee Employee) Staff { get; }
    public (User User, Employee Employee) NextStaff { get; }
    public (User User, Employee Employee) Manager { get; }
    public InventoryItem Coffee { get; }
    public InventoryItem Cups { get; }
    public Warehouse Inner { get; private set; } = null!;
    public Warehouse Outer { get; private set; } = null!;

    public InventoryTestBed(string? databaseName = null)
    {
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"InventoryTests-{Guid.NewGuid()}").Options);
        var zone = new Zone { BranchId = Branch.Id, Name = "Khu A" };
        Seat = new Seat { ZoneId = zone.Id, Code = "A-01", Type = SeatType.Single };
        Staff = NewEmployee("staff@test.invalid", "Nhân viên A");
        NextStaff = NewEmployee("next@test.invalid", "Nhân viên B");
        Manager = NewEmployee("manager@test.invalid", "Quản lý");
        // Morning 06:00–14:00 VN; "now" is 10:00.
        Shift = NewShift(ShiftType.Morning, Clock.GetUtcNow().AddHours(-4), Clock.GetUtcNow().AddHours(4));
        Coffee = new InventoryItem { Name = "Cà phê 3in1", BaseUnit = "gói", Category = "Đồ uống", CountDaily = true, SortOrder = 1 };
        Coffee.Units.Add(new ItemUnit { UnitName = "Hộp", Factor = 20 });
        Cups = new InventoryItem { Name = "Ly giấy", BaseUnit = "cái", Category = "Vật tư", CountDaily = true, SortOrder = 2 };
        Db.AddRange(Branch, OtherBranch, zone, Seat, Plan, Shift, Coffee, Cups);
        Db.ItemBranchSettings.AddRange(
            new ItemBranchSetting { ItemId = Coffee.Id, BranchId = Branch.Id, NormPerSession = 1.2m, OuterMin = 20, OuterPar = 60, InnerMin = 100 },
            new ItemBranchSetting { ItemId = Cups.Id, BranchId = Branch.Id, NormPerSession = 1m, OuterMin = 30, OuterPar = 100, InnerMin = 300 });
        Db.ShiftAssignments.Add(new ShiftAssignment { ShiftId = Shift.Id, EmployeeId = Staff.Employee.Id, Status = ShiftAssignmentStatus.Accepted });
        Db.SaveChanges();
        (Inner, Outer) = Warehouses.EnsureAsync(Db, Branch.Id, default).GetAwaiter().GetResult();
    }

    public void Dispose() => Db.Dispose();

    public CurrentUser StaffUser => As(Staff.User, BusinessRoles.Staff, Branch.Id);
    public CurrentUser NextStaffUser => As(NextStaff.User, BusinessRoles.Staff, Branch.Id);
    public CurrentUser ManagerUser => As(Manager.User, BusinessRoles.BranchManager, Branch.Id);
    public OuterStockCalculator Calculator => new(Db);
    public StockDocumentService Documents => new(Db, Calculator, Clock);
    public InventoryCatalogService Catalog => new(Db, Calculator, Clock);
    public static IOptions<InventoryOptions> Options => Microsoft.Extensions.Options.Options.Create(new InventoryOptions());

    public Shift NewShift(ShiftType type, DateTimeOffset start, DateTimeOffset end)
    {
        var shift = new Shift
        {
            BranchId = Branch.Id, Date = DateOnly.FromDateTime(start.ToOffset(VietnamTime.Offset).DateTime), Type = type,
            StartAt = start, EndAt = end, RequiredStaff = 1, Status = ShiftStatus.Published,
        };
        Db.Shifts.Add(shift);
        return shift;
    }

    public void CheckIn((User User, Employee Employee) who, Shift shift)
    {
        Db.Attendances.Add(new Attendance { EmployeeId = who.Employee.Id, ShiftId = shift.Id, CheckInAt = Clock.GetUtcNow(), Method = "POS" });
        Db.SaveChanges();
    }

    public void CheckOut((User User, Employee Employee) who)
    {
        foreach (var a in Db.Attendances.Where(a => a.EmployeeId == who.Employee.Id && a.CheckOutAt == null))
        {
            a.CheckOutAt = Clock.GetUtcNow();
        }

        Db.SaveChanges();
    }

    /// <summary>Adds <paramref name="count"/> sessions starting at <paramref name="at"/> (each = one drink).</summary>
    public void Sell(int count, DateTimeOffset at)
    {
        for (var i = 0; i < count; i++)
        {
            Db.Sessions.Add(new Session { SeatId = Seat.Id, PricePlanId = Plan.Id, StartAt = at, EndAt = at.AddHours(4), Status = SessionStatus.Closed });
        }

        Db.SaveChanges();
    }

    /// <summary>Puts opening stock straight into the ledger.</summary>
    public async Task StockAsync(Warehouse warehouse, InventoryItem item, int qty, DateTimeOffset at)
    {
        var doc = new StockDocument
        {
            Code = $"PN-SEED-{Guid.NewGuid():N}"[..30], Type = StockDocumentType.Receipt, BranchId = Branch.Id, ToWarehouseId = warehouse.Id,
            Status = StockDocumentStatus.Confirmed, CreatedAt = at, CreatedBy = Manager.User.Id, ConfirmedAt = at,
        };
        Db.StockDocuments.Add(doc);
        await StockLedger.PostAsync(Db, warehouse.Id, item.Id, qty, doc.Id, at, default);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    public int Book(Warehouse warehouse, InventoryItem item) =>
        Db.StockBalances.AsNoTracking().Where(b => b.WarehouseId == warehouse.Id && b.ItemId == item.Id).Select(b => b.Qty).FirstOrDefault();

    public int LedgerSum(Warehouse warehouse, InventoryItem item) =>
        Db.StockMovements.AsNoTracking().Where(m => m.WarehouseId == warehouse.Id && m.ItemId == item.Id).Sum(m => m.Qty);

    private (User, Employee) NewEmployee(string email, string name)
    {
        var user = new User { UserName = email, Email = email, FullName = name };
        var employee = new Employee { UserId = user.Id, HourlyRate = 25_000, PinHash = "x" };
        Db.AddRange(user, employee, new EmployeeBranch { EmployeeId = employee.Id, BranchId = Branch.Id, IsHome = true });
        return (user, employee);
    }

    private static Branch NewBranch(string key) => new()
    {
        Name = $"Branch {key}", Address = "x", OpenTime = new TimeOnly(0, 0), CloseTime = new TimeOnly(0, 0), Is24h = true,
    };

    public static CurrentUser As(User user, string role, params Guid[] branchIds) =>
        new(user.Id, new HashSet<string> { role }, BranchAccessScope.ForBranches(branchIds.ToHashSet()));
}

public sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now.ToUniversalTime();

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
