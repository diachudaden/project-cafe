using ChonRieng.Api.Features.Inventory;
using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChonRieng.UnitTests.Features.Inventory;

public sealed class InventoryReviewTests : IDisposable
{
    private readonly InventoryTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    private async Task<Guid> PendingCountAsync()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        await _bed.StockAsync(_bed.Outer, _bed.Coffee, 42, _bed.Shift.StartAt.AddHours(-1));
        await _bed.StockAsync(_bed.Outer, _bed.Cups, 10, _bed.Shift.StartAt.AddHours(-1));
        var sheet = (await _bed.Counts.OpenShiftCountAsync(_bed.StaffUser, _bed.Shift.Id, default)).Value!;
        await _bed.Counts.SaveLinesAsync(_bed.StaffUser, sheet.CountId, new SaveCountLinesRequest(
            [new CountLineInput(_bed.Coffee.Id, 30), new CountLineInput(_bed.Cups.Id, 10)]), default);
        var result = (await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest("Thiếu 12 gói chưa rõ lý do"), default)).Value!;
        Assert.Equal(StockDocumentStatus.PendingApproval, result.Status);
        return sheet.CountId;
    }

    [Fact]
    public async Task Pending_count_appears_in_attention_and_can_be_approved()
    {
        var countId = await PendingCountAsync();

        var attention = (await _bed.Review.AttentionAsync(_bed.ManagerUser, _bed.Branch.Id, default)).Value!;
        var pending = Assert.Single(attention.PendingCounts);
        Assert.Equal((countId, 1), (pending.CountId, pending.OutOfTolerance));
        Assert.Equal(404, (await _bed.Review.AttentionAsync(_bed.StaffUser, _bed.Branch.Id, default)).Error!.Status);

        var approved = await _bed.Review.ApproveAsync(_bed.ManagerUser, countId, new ReviewDocumentRequest("Đã kiểm tra camera"), default);

        Assert.True(approved.IsSuccess);
        Assert.Equal(StockDocumentStatus.Approved, (await _bed.Db.StockDocuments.SingleAsync(d => d.Id == countId)).Status);
        Assert.Empty((await _bed.Review.AttentionAsync(_bed.ManagerUser, _bed.Branch.Id, default)).Value!.PendingCounts);
        Assert.Equal(30, _bed.Book(_bed.Outer, _bed.Coffee));
    }

    [Fact]
    public async Task Rejected_count_reverses_its_book_movement_and_the_shift_counts_again()
    {
        var countId = await PendingCountAsync();
        Assert.Equal(InventoryErrors.ReasonRequired, (await _bed.Review.RejectAsync(_bed.ManagerUser, countId, new ReviewDocumentRequest(null), default)).Error!.Violations![0].Rule);

        var rejected = await _bed.Review.RejectAsync(_bed.ManagerUser, countId, new ReviewDocumentRequest("Đếm thiếu kệ dưới"), default);

        Assert.True(rejected.IsSuccess);
        Assert.Equal(42, _bed.Book(_bed.Outer, _bed.Coffee));
        Assert.Equal(42, _bed.LedgerSum(_bed.Outer, _bed.Coffee));
        Assert.Equal(HandoverStatus.Resolved, (await _bed.Db.ShiftCountHandovers.SingleAsync()).Status);
        Assert.Contains(await _bed.Db.Notifications.ToListAsync(), n => n.UserId == _bed.Staff.User.Id);
        var again = (await _bed.Counts.OpenShiftCountAsync(_bed.StaffUser, _bed.Shift.Id, default)).Value!;
        Assert.NotEqual(countId, again.CountId);
        Assert.Equal(StockDocumentStatus.Draft, again.Status);
    }

    [Fact]
    public async Task Adjustment_corrects_a_transfer_on_both_warehouses_and_leaves_it_untouched()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        await _bed.StockAsync(_bed.Inner, _bed.Coffee, 100, _bed.Shift.StartAt);
        var transfer = (await _bed.Documents.CreateTransferAsync(_bed.StaffUser,
            new CreateTransferRequest(_bed.Branch.Id, [new QtyLineInput(_bed.Coffee.Id, 60)], "Khách đoàn đặt trước"), default)).Value!;

        Assert.Equal(404, (await _bed.Review.CreateAdjustmentAsync(_bed.StaffUser,
            new CreateAdjustmentRequest(transfer.Id, [new AdjustmentLineInput(_bed.Coffee.Id, 10)], "x"), default)).Error!.Status);
        var dc = (await _bed.Review.CreateAdjustmentAsync(_bed.ManagerUser,
            new CreateAdjustmentRequest(transfer.Id, [new AdjustmentLineInput(_bed.Coffee.Id, 10)], "Thực lôi 70, ghi nhầm 60"), default)).Value!;

        Assert.Equal(StockDocumentType.Adjustment, dc.Type);
        Assert.Equal(transfer.Id, dc.AdjustsDocumentId);
        Assert.Equal(30, _bed.Book(_bed.Inner, _bed.Coffee));
        Assert.Equal(70, _bed.Book(_bed.Outer, _bed.Coffee));
        Assert.Equal(60, (await _bed.Db.StockDocumentLines.SingleAsync(l => l.DocumentId == transfer.Id)).Qty);

        var tooMuch = await _bed.Review.CreateAdjustmentAsync(_bed.ManagerUser,
            new CreateAdjustmentRequest(transfer.Id, [new AdjustmentLineInput(_bed.Coffee.Id, 31)], "Sai tiếp"), default);
        Assert.Equal(InventoryErrors.InsufficientStock, tooMuch.Error!.Violations![0].Rule);
        Assert.Equal(InventoryErrors.InvalidState, (await _bed.Review.CreateAdjustmentAsync(_bed.ManagerUser,
            new CreateAdjustmentRequest(dc.Id, [new AdjustmentLineInput(_bed.Coffee.Id, 1)], "DC của DC"), default)).Error!.Violations![0].Rule);
    }

    [Fact]
    public async Task Draft_adjustment_moves_stock_only_when_approved()
    {
        var draft = new StockDocument
        {
            Code = "DC-TEST-001", Type = StockDocumentType.Adjustment, BranchId = _bed.Branch.Id, FromWarehouseId = _bed.Inner.Id,
            ToWarehouseId = _bed.Outer.Id, Status = StockDocumentStatus.Draft, CreatedAt = _bed.Clock.GetUtcNow(), CreatedBy = _bed.Manager.User.Id,
        };
        draft.Lines.Add(new StockDocumentLine { ItemId = _bed.Coffee.Id, Qty = 10 });
        _bed.Db.StockDocuments.Add(draft);
        _bed.Db.InventoryIssues.Add(new InventoryIssue
        {
            BranchId = _bed.Branch.Id, ItemId = _bed.Coffee.Id, Kind = InventoryIssueKind.TransferMisrecorded, InnerVar = -10, OuterVar = 10,
            AdjustmentDocumentId = draft.Id, InnerCountDocumentId = Guid.NewGuid(), CreatedAt = _bed.Clock.GetUtcNow(),
        });
        await _bed.Db.SaveChangesAsync();
        await _bed.StockAsync(_bed.Inner, _bed.Coffee, 50, _bed.Shift.StartAt);

        var attention = (await _bed.Review.AttentionAsync(_bed.ManagerUser, _bed.Branch.Id, default)).Value!;
        Assert.Single(attention.DraftAdjustments);
        Assert.Single(attention.MisrecordedIssues);
        Assert.Empty(attention.LossIssues);
        Assert.Equal(0, _bed.Book(_bed.Outer, _bed.Coffee));

        await _bed.Review.ApproveAsync(_bed.ManagerUser, draft.Id, new ReviewDocumentRequest(null), default);

        Assert.Equal(40, _bed.Book(_bed.Inner, _bed.Coffee));
        Assert.Equal(10, _bed.Book(_bed.Outer, _bed.Coffee));
        Assert.Equal(InventoryIssueStatus.Resolved, (await _bed.Db.InventoryIssues.SingleAsync()).Status);
    }
}
