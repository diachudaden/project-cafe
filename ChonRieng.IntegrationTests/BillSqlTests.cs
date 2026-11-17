using ChonRieng.Api.Features.Sales;
using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.IntegrationTests;

/// <summary>TASK-029 (D34): one session bill per session, any number of add-on bills, enforced by SQL Server.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class BillSqlTests(SqlServerFixture sql)
{
    [Fact]
    public async Task A_session_has_one_session_bill_and_many_add_on_bills()
    {
        var bed = await SqlBed.CreateAsync(sql);
        var (sessionId, sessionBillId) = await NewSessionAsync(bed);

        await using (var db = sql.NewContext())
        {
            db.Bills.AddRange(AddOnBill(bed, sessionBillId, 7_000), AddOnBill(bed, sessionBillId, 7_000));
            await db.SaveChangesAsync();
        }

        await using (var db = sql.NewContext())
        {
            db.Bills.Add(SessionBill(bed, sessionId, 40_000));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var check = sql.NewContext();
        Assert.Equal(1, await check.Bills.CountAsync(b => b.SessionId == sessionId));
        Assert.Equal(2, await check.Bills.CountAsync(b => b.ParentBillId == sessionBillId && b.Kind == BillKind.AddOn));
    }

    [Fact]
    public async Task Check_constraint_rejects_an_add_on_bill_that_names_a_session()
    {
        var bed = await SqlBed.CreateAsync(sql);
        var (sessionId, sessionBillId) = await NewSessionAsync(bed);

        await using var db = sql.NewContext();
        var wrong = AddOnBill(bed, sessionBillId, 7_000);
        wrong.SessionId = sessionId;
        db.Bills.Add(wrong);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Receipt_and_bill_lookup_for_an_add_on_bill_run_on_sql_server()
    {
        var bed = await SqlBed.CreateAsync(sql);
        var (sessionId, sessionBillId) = await NewSessionAsync(bed);
        var addOn = AddOnBill(bed, sessionBillId, 7_000);
        await using (var db = sql.NewContext())
        {
            db.Bills.Add(addOn);
            db.BillItems.Add(new BillItem { BillId = addOn.Id, Name = "Mì ly", UnitPrice = 7_000, Quantity = 1, Amount = 7_000, PaymentMethod = PaymentMethod.Cash, CreatedAt = addOn.CreatedAt });
            await db.SaveChangesAsync();
        }

        await using var check = sql.NewContext();
        var receipt = await new ReceiptService(check, TimeProvider.System).LoadAsync(bed.Manager, addOn.Code, default);
        Assert.NotNull(receipt);
        Assert.True(receipt.IsAddOn);
        Assert.Equal(("S-01", 7_000m, 0m), (receipt.SeatCode, receipt.Total, receipt.PlanAmount));
        Assert.Empty(receipt.Extensions);
        Assert.Single(receipt.Items);

        var sessionReceipt = await new ReceiptService(check, TimeProvider.System).LoadAsync(bed.Manager, (await check.Bills.SingleAsync(b => b.Id == sessionBillId)).Code, default);
        Assert.Equal((false, 40_000m), (sessionReceipt!.IsAddOn, sessionReceipt.Total));

        var view = await new SessionService(check, TimeProvider.System).GetByBillCodeAsync(bed.Manager, addOn.Code, default);
        Assert.True(view.IsSuccess);
        Assert.Equal((sessionId, BillKind.AddOn), (view.Value!.Id, view.Value.Kind));
        Assert.Equal(40_000, view.Value.Bill.Total);
        Assert.Equal(addOn.Code, Assert.Single(view.Value.AddOnBills!).Code);
    }

    internal async Task<(Guid SessionId, Guid BillId)> NewSessionAsync(SqlBed bed)
    {
        await using var db = sql.NewContext();
        var now = DateTimeOffset.UtcNow;
        var session = new Session
        {
            SeatId = bed.SeatId, PricePlanId = bed.PlanId, StartAt = now, EndAt = now.AddHours(4), Status = SessionStatus.Active,
        };
        var bill = SessionBill(bed, session.Id, 40_000);
        db.AddRange(session, bill);
        await db.SaveChangesAsync();
        return (session.Id, bill.Id);
    }

    internal static Bill SessionBill(SqlBed bed, Guid sessionId, decimal total) => new()
    {
        Code = NewCode(), Kind = BillKind.Session, BranchId = bed.BranchId, ShiftId = bed.ShiftId, SessionId = sessionId,
        Total = total, CashPaidTotal = total, PaymentMethod = PaymentMethod.Cash, CreatedAt = DateTimeOffset.UtcNow,
    };

    internal static Bill AddOnBill(SqlBed bed, Guid parentBillId, decimal total) => new()
    {
        Code = NewCode(), Kind = BillKind.AddOn, BranchId = bed.BranchId, ShiftId = bed.ShiftId, ParentBillId = parentBillId,
        Total = total, CashPaidTotal = total, PaymentMethod = PaymentMethod.Cash, CreatedAt = DateTimeOffset.UtcNow,
    };

    private static string NewCode() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
