using ChonRieng.Api.Features.Inventory;
using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChonRieng.UnitTests.Features.Inventory;

public sealed class InventoryJobsTests : IDisposable
{
    private readonly InventoryTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private StockDocument LockedCount(Warehouse warehouse, Shift? shift, DateTimeOffset at, int sessions,
        params (InventoryItem Item, int Variance, decimal Usage, int Opening, int In, int Counted)[] lines)
    {
        var doc = new StockDocument
        {
            Code = $"KK-T-{Guid.NewGuid():N}"[..20], Type = StockDocumentType.Count, BranchId = _bed.Branch.Id, ToWarehouseId = warehouse.Id,
            ShiftId = shift?.Id, Status = StockDocumentStatus.Confirmed, CreatedAt = at, CreatedBy = _bed.Staff.User.Id,
            ConfirmedAt = at, ConfirmedBy = _bed.Staff.User.Id, SessionCount = shift is null ? null : sessions,
        };
        foreach (var l in lines)
        {
            doc.Lines.Add(new StockDocumentLine
            {
                ItemId = l.Item.Id, Qty = l.Counted, CountedQty = l.Counted, FirstCountedQty = l.Counted, Variance = l.Variance, Usage = l.Usage,
                OpeningQty = l.Opening, TransferInQty = l.In, WasteQty = 0, ExpectedQty = l.Counted - l.Variance, Tolerance = 2,
            });
        }

        _bed.Db.StockDocuments.Add(doc);
        _bed.Db.SaveChanges();
        return doc;
    }

    [Theory]
    [InlineData(0, -3, InventoryIssueKind.Normal, InventoryIssueStatus.Resolved)]
    [InlineData(-10, 10, InventoryIssueKind.TransferMisrecorded, InventoryIssueStatus.Open)]
    [InlineData(-10, 0, InventoryIssueKind.RealLoss, InventoryIssueStatus.Open)]
    [InlineData(0, -13, InventoryIssueKind.RealLoss, InventoryIssueStatus.Open)]
    [InlineData(0, 12, InventoryIssueKind.UnexplainedSurplus, InventoryIssueStatus.Open)]
    public async Task Nightly_reconciliation_classifies_between_two_inner_counts(int innerVar, int outerVar, InventoryIssueKind kind, InventoryIssueStatus status)
    {
        var t0 = _bed.Clock.GetUtcNow().AddDays(-7);
        LockedCount(_bed.Inner, null, t0, 0, (_bed.Coffee, 0, 0, 0, 0, 500));
        // Two shift counts in the period: usage 100 in total → tolerance 8.
        var s1 = _bed.NewShift(ShiftType.Morning, t0.AddDays(1), t0.AddDays(1).AddHours(8));
        var s2 = _bed.NewShift(ShiftType.Afternoon, t0.AddDays(2), t0.AddDays(2).AddHours(8));
        LockedCount(_bed.Outer, s1, t0.AddDays(1).AddHours(8), 40, (_bed.Coffee, outerVar, 50m, 60, 40, 50));
        LockedCount(_bed.Outer, s2, t0.AddDays(2).AddHours(8), 40, (_bed.Coffee, 0, 50m, 50, 50, 50));
        _bed.Db.SaveChanges();
        var pc = new StockDocument
        {
            Code = "PC-T-001", Type = StockDocumentType.Transfer, BranchId = _bed.Branch.Id, FromWarehouseId = _bed.Inner.Id, ToWarehouseId = _bed.Outer.Id,
            Status = StockDocumentStatus.Confirmed, CreatedAt = t0.AddDays(1).AddHours(2), CreatedBy = _bed.Staff.User.Id,
        };
        pc.Lines.Add(new StockDocumentLine { ItemId = _bed.Coffee.Id, Qty = 60 });
        _bed.Db.StockDocuments.Add(pc);
        _bed.Db.SaveChanges();
        LockedCount(_bed.Inner, null, t0.AddDays(6), 0, (_bed.Coffee, innerVar, 0, 0, 0, 400));

        Assert.Equal(1, await _bed.Jobs.ReconcileAsync(_bed.Clock.GetUtcNow(), default));
        Assert.Equal(0, await _bed.Jobs.ReconcileAsync(_bed.Clock.GetUtcNow(), default)); // idempotent

        var issue = await _bed.Db.InventoryIssues.SingleAsync();
        Assert.Equal((kind, status, innerVar, outerVar, 8), (issue.Kind, issue.Status, issue.InnerVar, issue.OuterVar, issue.Tolerance));
        var notes = await _bed.Db.Notifications.ToListAsync();
        if (kind == InventoryIssueKind.TransferMisrecorded)
        {
            var dc = await _bed.Db.StockDocuments.Include(d => d.Lines).SingleAsync(d => d.Type == StockDocumentType.Adjustment);
            Assert.Equal((StockDocumentStatus.Draft, pc.Id, (Guid?)null, (Guid?)null), (dc.Status, dc.AdjustsDocumentId!.Value, dc.FromWarehouseId, dc.ToWarehouseId));
            Assert.Equal(10, Assert.Single(dc.Lines).Qty);
            Assert.Equal(dc.Id, issue.AdjustmentDocumentId);
            Assert.Contains(notes, n => n.UserId == _bed.Staff.User.Id && n.Type == InventoryEvents.IssueFound);
            // Approving records it without moving stock (both books were already right).
            var movements = await _bed.Db.StockMovements.CountAsync();
            await _bed.Review.ApproveAsync(_bed.ManagerUser, dc.Id, new ReviewDocumentRequest(null), default);
            Assert.Equal(movements, await _bed.Db.StockMovements.CountAsync());
        }
        else if (status == InventoryIssueStatus.Open)
        {
            Assert.Contains(notes, n => n.UserId == _bed.Manager.User.Id && n.Type == InventoryEvents.IssueFound);
        }
        else
        {
            Assert.Empty(notes);
        }
    }

    [Fact]
    public async Task Missing_counts_remind_staff_at_30_minutes_and_managers_at_60_once()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        _bed.Clock.Advance(_bed.Shift.EndAt - _bed.Clock.GetUtcNow() + TimeSpan.FromMinutes(35));

        await _bed.Jobs.RemindUnconfirmedCountsAsync(_bed.Clock.GetUtcNow(), default);
        var notes = await _bed.Db.Notifications.ToListAsync();
        Assert.Single(notes, n => n.UserId == _bed.Staff.User.Id && n.Type == InventoryEvents.CountReminder);
        Assert.DoesNotContain(notes, n => n.UserId == _bed.Manager.User.Id);

        _bed.Clock.Advance(TimeSpan.FromMinutes(30));
        await _bed.Jobs.RemindUnconfirmedCountsAsync(_bed.Clock.GetUtcNow(), default);
        await _bed.Jobs.RemindUnconfirmedCountsAsync(_bed.Clock.GetUtcNow(), default);
        notes = await _bed.Db.Notifications.ToListAsync();
        Assert.Single(notes, n => n.UserId == _bed.Staff.User.Id);
        Assert.Single(notes, n => n.UserId == _bed.Manager.User.Id && n.Type == InventoryEvents.CountReminder);
    }

    [Fact]
    public async Task Waiting_handovers_remind_the_pos_then_the_manager()
    {
        var count = LockedCount(_bed.Outer, _bed.Shift, _bed.Clock.GetUtcNow(), 10, (_bed.Coffee, 0, 12m, 40, 0, 28));
        _bed.Db.ShiftCountHandovers.Add(new ShiftCountHandover { CountDocumentId = count.Id, CreatedAt = _bed.Clock.GetUtcNow() });
        _bed.Db.SaveChanges();
        var next = _bed.NewShift(ShiftType.Afternoon, _bed.Shift.EndAt, _bed.Shift.EndAt.AddHours(8));
        _bed.CheckIn(_bed.NextStaff, next);

        _bed.Clock.Advance(TimeSpan.FromMinutes(31));
        await _bed.Jobs.RemindUnconfirmedCountsAsync(_bed.Clock.GetUtcNow(), default);
        Assert.Contains(await _bed.Db.Notifications.ToListAsync(), n => n.UserId == _bed.NextStaff.User.Id && n.Type == InventoryEvents.HandoverOverdue);
        Assert.DoesNotContain(await _bed.Db.Notifications.ToListAsync(), n => n.UserId == _bed.Manager.User.Id);

        _bed.Clock.Advance(TimeSpan.FromMinutes(30));
        await _bed.Jobs.RemindUnconfirmedCountsAsync(_bed.Clock.GetUtcNow(), default);
        await _bed.Jobs.RemindUnconfirmedCountsAsync(_bed.Clock.GetUtcNow(), default);
        var handover = await _bed.Db.ShiftCountHandovers.AsNoTracking().SingleAsync();
        Assert.NotNull(handover.RemindedAt);
        Assert.NotNull(handover.EscalatedAt);
        Assert.Single(await _bed.Db.Notifications.Where(n => n.UserId == _bed.Manager.User.Id && n.Type == InventoryEvents.HandoverOverdue).ToListAsync());
    }

    [Fact]
    public async Task Restock_is_suggested_below_min_at_most_hourly()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        await _bed.StockAsync(_bed.Outer, _bed.Coffee, 25, _bed.Shift.StartAt.AddHours(-1));
        await _bed.StockAsync(_bed.Outer, _bed.Cups, 100, _bed.Shift.StartAt.AddHours(-1));
        _bed.Sell(10, _bed.Shift.StartAt.AddHours(1)); // coffee 25 − 12 = 13 < min 20; cups 90 ≥ 30

        Assert.Equal(1, await _bed.Jobs.SuggestRestockAsync(_bed.Clock.GetUtcNow(), default));
        Assert.Equal(0, await _bed.Jobs.SuggestRestockAsync(_bed.Clock.GetUtcNow().AddMinutes(10), default));

        var note = await _bed.Db.Notifications.SingleAsync();
        Assert.Equal(InventoryEvents.RestockSuggested, note.Type);
        Assert.Contains("Cà phê 3in1 (gợi ý lôi 47)", note.Body);
        Assert.DoesNotContain("Ly giấy", note.Body);
    }

    [Fact]
    public async Task Norms_are_learned_from_locked_counts_once_a_day()
    {
        var start = _bed.Clock.GetUtcNow().AddDays(-3);
        // Actual usage per session: (60+0−0−48)/10 = 1.2, (48−33)/10 = 1.5, (33+40−48)/20 = 1.25 → median 1.25
        LockedCount(_bed.Outer, _bed.NewShift(ShiftType.Morning, start, start.AddHours(8)), start.AddHours(8), 10, (_bed.Coffee, 0, 12, 60, 0, 48));
        LockedCount(_bed.Outer, _bed.NewShift(ShiftType.Morning, start.AddDays(1), start.AddDays(1).AddHours(8)), start.AddDays(1).AddHours(8), 10, (_bed.Coffee, 0, 12, 48, 0, 33));
        LockedCount(_bed.Outer, _bed.NewShift(ShiftType.Morning, start.AddDays(2), start.AddDays(2).AddHours(8)), start.AddDays(2).AddHours(8), 20, (_bed.Coffee, 0, 24, 33, 40, 48));

        Assert.Equal(1, await _bed.Jobs.RecalcNormsAsync(_bed.Clock.GetUtcNow(), default));
        Assert.Equal(0, await _bed.Jobs.RecalcNormsAsync(_bed.Clock.GetUtcNow().AddHours(1), default));

        var setting = await _bed.Db.ItemBranchSettings.AsNoTracking().SingleAsync(s => s.ItemId == _bed.Coffee.Id);
        Assert.Equal(1.25m, setting.NormPerSession);
        Assert.Equal(21, setting.OuterPar); // ceil(avg(12, 15, 25) × 1.2) = ceil(20.8); never below OuterMin (20)
        Assert.NotNull(setting.NormLearnedAt);
    }
}
