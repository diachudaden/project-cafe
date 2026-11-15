using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Inventory;
using ChonRieng.Api.Services;
using ChonRieng.Application.Persistence;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.IntegrationTests;

/// <summary>TASK-027 behaviour that needs real SQL Server: concurrent ledger writes, filtered unique indexes, SQL translation.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class InventorySqlTests(SqlServerFixture sql)
{
    private static readonly TimeProvider Clock = TimeProvider.System;

    [Fact]
    public async Task Two_parallel_transfers_cannot_drive_the_inner_warehouse_negative()
    {
        var bed = await SqlBed.CreateAsync(sql, innerCoffee: 100);

        async Task<Result<StockDocumentView>> Transfer()
        {
            await using var db = sql.NewContext();
            var service = new StockDocumentService(db, new OuterStockCalculator(db), Clock);
            return await service.CreateTransferAsync(bed.Staff,
                new CreateTransferRequest(bed.BranchId, [new QtyLineInput(bed.CoffeeId, 70)], "Khách đoàn"), default);
        }

        var results = await Task.WhenAll(Transfer(), Transfer());

        Assert.Single(results, r => r.IsSuccess);
        var failed = Assert.Single(results, r => !r.IsSuccess);
        Assert.Equal(409, failed.Error!.Status);
        Assert.Contains(failed.Error.Violations![0].Rule, new[] { InventoryErrors.InsufficientStock, InventoryErrors.Busy });

        await using var check = sql.NewContext();
        var inner = await check.StockBalances.SingleAsync(b => b.WarehouseId == bed.InnerId && b.ItemId == bed.CoffeeId);
        Assert.Equal(30, inner.Qty);
        Assert.Equal(30, await check.StockMovements.Where(m => m.WarehouseId == bed.InnerId && m.ItemId == bed.CoffeeId).SumAsync(m => m.Qty));
    }

    [Fact]
    public async Task Suggestion_and_balances_translate_to_sql()
    {
        var bed = await SqlBed.CreateAsync(sql, innerCoffee: 100);
        await using var db = sql.NewContext();
        var calculator = new OuterStockCalculator(db);

        var suggestion = await new StockDocumentService(db, calculator, Clock).SuggestAsync(bed.Staff, bed.BranchId, default);
        var balances = await new InventoryCatalogService(db, calculator, Clock).BalancesAsync(bed.Manager, bed.BranchId, WarehouseType.Outer, default);

        Assert.True(suggestion.IsSuccess);
        Assert.Equal(60, suggestion.Value!.Single().Suggested);
        Assert.True(balances.IsSuccess);
    }

    private static ShiftCountService Counts(AppDbContext db) => new(db, new OuterStockCalculator(db),
        new InventoryEvents(db, new ChonRieng.Api.Features.Scheduling.ShiftService(db, Clock)),
        Microsoft.Extensions.Options.Options.Create(new InventoryOptions()), Clock);

    [Fact]
    public async Task Parallel_opens_share_one_count_and_parallel_confirms_write_once()
    {
        var bed = await SqlBed.CreateAsync(sql, innerCoffee: 100, outerCoffee: 40);

        async Task<Guid> Open()
        {
            await using var db = sql.NewContext();
            return (await Counts(db).OpenShiftCountAsync(bed.Staff, bed.ShiftId, default)).Value!.CountId;
        }

        var ids = await Task.WhenAll(Open(), Open());
        Assert.Equal(ids[0], ids[1]);
        var countId = ids[0];

        await using (var db = sql.NewContext())
        {
            var saved = await Counts(db).SaveLinesAsync(bed.Staff, countId, new SaveCountLinesRequest([new CountLineInput(bed.CoffeeId, 39)]), default);
            Assert.True(saved.IsSuccess);
            var summary = await Counts(db).SummaryAsync(bed.Staff, countId, default);
            Assert.Equal(40, summary.Value!.Rows.Single().Expected);
        }

        async Task<ConfirmCountResult> Confirm()
        {
            await using var db = sql.NewContext();
            return (await Counts(db).ConfirmAsync(bed.Staff, countId, new ConfirmCountRequest(null), default)).Value!;
        }

        var results = await Task.WhenAll(Confirm(), Confirm());

        Assert.All(results, r => Assert.Equal(StockDocumentStatus.Confirmed, r.Status));
        Assert.Single(results, r => !r.AlreadyConfirmed);
        await using var check = sql.NewContext();
        Assert.Equal(1, await check.StockMovements.CountAsync(m => m.DocumentId == countId));
        Assert.Equal(1, await check.ShiftCountHandovers.CountAsync(h => h.CountDocumentId == countId));
        Assert.Equal(39, (await check.StockBalances.SingleAsync(b => b.WarehouseId == bed.OuterId && b.ItemId == bed.CoffeeId)).Qty);

        var edit = await Counts(check).SaveLinesAsync(bed.Staff, countId, new SaveCountLinesRequest([new CountLineInput(bed.CoffeeId, 1)]), default);
        Assert.Equal(InventoryErrors.CountAlreadyConfirmed, edit.Error!.Violations![0].Rule);
    }

    [Fact]
    public async Task Attention_box_translates_to_sql()
    {
        var bed = await SqlBed.CreateAsync(sql, innerCoffee: 100, outerCoffee: 40);
        await using var db = sql.NewContext();
        var counts = Counts(db);
        var sheet = (await counts.OpenShiftCountAsync(bed.Staff, bed.ShiftId, default)).Value!;
        await counts.SaveLinesAsync(bed.Staff, sheet.CountId, new SaveCountLinesRequest([new CountLineInput(bed.CoffeeId, 20)]), default);
        Assert.Equal(StockDocumentStatus.PendingApproval,
            (await counts.ConfirmAsync(bed.Staff, sheet.CountId, new ConfirmCountRequest("Thiếu 20 gói cà phê"), default)).Value!.Status);

        var calculator = new OuterStockCalculator(db);
        var review = new InventoryReviewService(db, new StockDocumentService(db, calculator, Clock), Clock);
        var attention = (await review.AttentionAsync(bed.Manager, bed.BranchId, default)).Value!;
        var issues = await review.ListIssuesAsync(bed.Manager, bed.BranchId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), default);

        Assert.Equal(1, Assert.Single(attention.PendingCounts).OutOfTolerance);
        Assert.True(issues.IsSuccess);
    }
}

/// <summary>A fresh branch (own shift, staff checked in, one item) inside the shared SQL Server database.</summary>
internal sealed record SqlBed(
    Guid BranchId, Guid ShiftId, Guid InnerId, Guid OuterId, Guid CoffeeId, CurrentUser Staff, CurrentUser NextStaff, CurrentUser Manager,
    Guid StaffUserId, Guid SeatId, Guid PlanId)
{
    public static async Task<SqlBed> CreateAsync(SqlServerFixture sql, int innerCoffee = 0, int outerCoffee = 0)
    {
        await using var db = sql.NewContext();
        var now = DateTimeOffset.UtcNow;
        var key = Guid.NewGuid().ToString("N")[..8];
        var branch = new Branch { Name = $"SQL {key}", Address = "x", Is24h = true };
        var zone = new Zone { BranchId = branch.Id, Name = "Khu" };
        var seat = new Seat { ZoneId = zone.Id, Code = "S-01", Type = SeatType.Single };
        var plan = new PricePlan { BranchId = branch.Id, Name = $"Gói {key}", DurationMinutes = 240, Price = 45_000, ExtendPrice = 40_000 };
        var shift = new Shift
        {
            BranchId = branch.Id, Date = DateOnly.FromDateTime(now.DateTime), Type = ShiftType.Morning,
            StartAt = now.AddHours(-2), EndAt = now.AddHours(6), RequiredStaff = 1, Status = ShiftStatus.Published,
        };
        var (staffUser, staffEmployee) = NewEmployee(db, branch, $"staff.{key}@test.invalid");
        var (nextUser, _) = NewEmployee(db, branch, $"next.{key}@test.invalid");
        var (managerUser, _) = NewEmployee(db, branch, $"manager.{key}@test.invalid");
        var coffee = new InventoryItem { Name = $"Cà phê {key}", BaseUnit = "gói", Category = "Đồ uống", CountDaily = true, SortOrder = 1, CreatedAt = now };
        db.AddRange(branch, zone, seat, plan, shift, coffee);
        db.ItemBranchSettings.Add(new ItemBranchSetting { ItemId = coffee.Id, BranchId = branch.Id, NormPerSession = 1.2m, OuterMin = 20, OuterPar = 60, InnerMin = 100 });
        db.Attendances.Add(new Attendance { EmployeeId = staffEmployee.Id, ShiftId = shift.Id, CheckInAt = now.AddHours(-2), Method = "POS" });
        await db.SaveChangesAsync();

        var (inner, outer) = await Warehouses.EnsureAsync(db, branch.Id, default);
        var seedAt = now.AddHours(-3);
        var seed = new StockDocument
        {
            Code = $"PN-SQL-{key}", Type = StockDocumentType.Receipt, BranchId = branch.Id, ToWarehouseId = inner.Id,
            Status = StockDocumentStatus.Confirmed, CreatedBy = managerUser.Id, CreatedAt = seedAt, ConfirmedAt = seedAt,
        };
        db.StockDocuments.Add(seed);
        await StockLedger.PostAsync(db, inner.Id, coffee.Id, innerCoffee, seed.Id, seedAt, default);
        await StockLedger.PostAsync(db, outer.Id, coffee.Id, outerCoffee, seed.Id, seedAt, default);
        await db.SaveChangesAsync();

        return new SqlBed(branch.Id, shift.Id, inner.Id, outer.Id, coffee.Id,
            As(staffUser, BusinessRoles.Staff, branch.Id), As(nextUser, BusinessRoles.Staff, branch.Id),
            As(managerUser, BusinessRoles.BranchManager, branch.Id), staffUser.Id, seat.Id, plan.Id);
    }

    private static (User, Employee) NewEmployee(AppDbContext db, Branch branch, string email)
    {
        var user = new User { UserName = email, NormalizedUserName = email.ToUpperInvariant(), Email = email, NormalizedEmail = email.ToUpperInvariant(), FullName = email };
        var employee = new Employee { UserId = user.Id, HourlyRate = 25_000, PinHash = "x" };
        db.AddRange(user, employee, new EmployeeBranch { EmployeeId = employee.Id, BranchId = branch.Id, IsHome = true });
        return (user, employee);
    }

    private static CurrentUser As(User user, string role, Guid branchId) =>
        new(user.Id, new HashSet<string> { role }, BranchAccessScope.ForBranches(new HashSet<Guid> { branchId }));
}
