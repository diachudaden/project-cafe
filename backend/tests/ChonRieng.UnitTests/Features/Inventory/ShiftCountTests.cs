using System.Text.Json;
using ChonRieng.Api.Features.Inventory;
using ChonRieng.Domain.Entities;
using ChonRieng.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChonRieng.UnitTests.Features.Inventory;

public sealed class ShiftCountTests : IDisposable
{
    private readonly InventoryTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    /// <summary>The spec example: opening 42, transfer in 72, 64 sessions × 1.2 → expected 37.</summary>
    private async Task<CountSheetView> SampleShiftAsync()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        var before = _bed.Shift.StartAt.AddHours(-1);
        await _bed.StockAsync(_bed.Inner, _bed.Coffee, 500, before);
        await _bed.StockAsync(_bed.Inner, _bed.Cups, 500, before);
        await _bed.StockAsync(_bed.Outer, _bed.Coffee, 42, before);
        await _bed.StockAsync(_bed.Outer, _bed.Cups, 100, before);
        Assert.True((await _bed.Documents.CreateTransferAsync(_bed.StaffUser,
            new CreateTransferRequest(_bed.Branch.Id, [new QtyLineInput(_bed.Coffee.Id, 72)], "Khách đông buổi sáng"), default)).IsSuccess);
        _bed.Sell(64, _bed.Shift.StartAt.AddHours(1)); // 64 drinks; cups norm 1 → 100 − 64 = 36
        return (await _bed.Counts.OpenShiftCountAsync(_bed.StaffUser, _bed.Shift.Id, default)).Value!;
    }

    private Task Count(Guid countId, int coffee, int cups) =>
        _bed.Counts.SaveLinesAsync(_bed.StaffUser, countId, new SaveCountLinesRequest(
            [new CountLineInput(_bed.Coffee.Id, coffee), new CountLineInput(_bed.Cups.Id, cups)]), default);

    [Fact]
    public async Task Open_and_lines_never_expose_the_expected_quantity()
    {
        var sheet = await SampleShiftAsync();
        await Count(sheet.CountId, 30, 36);
        var saved = (await _bed.Counts.SaveLinesAsync(_bed.StaffUser, sheet.CountId,
            new SaveCountLinesRequest([new CountLineInput(_bed.Coffee.Id, 31)]), default)).Value!;

        foreach (var type in new[] { typeof(CountSheetView), typeof(CountItemView) })
        {
            Assert.DoesNotContain(type.GetProperties(), p => p.Name.Contains("Expected") || p.Name.Contains("Variance") || p.Name.Contains("Opening"));
        }

        foreach (var json in new[] { JsonSerializer.Serialize(sheet), JsonSerializer.Serialize(saved) })
        {
            Assert.DoesNotContain("expected", json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal([_bed.Coffee.Id, _bed.Cups.Id], sheet.Items.Select(i => i.ItemId)); // shelf order
    }

    [Fact]
    public async Task Summary_is_refused_until_every_line_has_been_counted()
    {
        var sheet = await SampleShiftAsync();
        await _bed.Counts.SaveLinesAsync(_bed.StaffUser, sheet.CountId, new SaveCountLinesRequest([new CountLineInput(_bed.Coffee.Id, 30)]), default);

        var summary = await _bed.Counts.SummaryAsync(_bed.StaffUser, sheet.CountId, default);

        Assert.Equal(409, summary.Error!.Status);
        Assert.Equal(InventoryErrors.CountIncomplete, summary.Error.Violations![0].Rule);
    }

    [Fact]
    public async Task Summary_shows_the_spec_example_and_a_recount_keeps_the_first_count()
    {
        var sheet = await SampleShiftAsync();
        await Count(sheet.CountId, 22, 36);

        var summary = (await _bed.Counts.SummaryAsync(_bed.StaffUser, sheet.CountId, default)).Value!;
        var coffee = summary.Rows.Single(r => r.ItemId == _bed.Coffee.Id);
        Assert.Equal((42, 72, 0, 64, 1.2m, 76.8m, 37, 22, -15, 6, false),
            (coffee.Opening, coffee.TransfersIn, coffee.Waste, coffee.Sessions, coffee.Norm, coffee.Usage, coffee.Expected, coffee.Counted, coffee.Variance, coffee.Tolerance, coffee.WithinTolerance));
        Assert.Equal(1, summary.WithinCount);
        Assert.Equal(2, summary.ItemCount);

        await Count(sheet.CountId, 35, 36); // "Đếm lại"
        var line = await _bed.Db.StockDocumentLines.AsNoTracking().SingleAsync(l => l.DocumentId == sheet.CountId && l.ItemId == _bed.Coffee.Id);
        Assert.Equal(22, line.FirstCountedQty);
        Assert.Equal(35, line.CountedQty);
    }

    [Fact]
    public async Task Confirm_within_tolerance_locks_resets_the_book_and_opens_a_handover()
    {
        var sheet = await SampleShiftAsync();
        await Count(sheet.CountId, 35, 36);

        var result = (await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest(null), default)).Value!;

        Assert.Equal(StockDocumentStatus.Confirmed, result.Status);
        Assert.False(result.AlreadyConfirmed);
        Assert.Empty(result.OutOfTolerance);
        Assert.Equal(35, _bed.Book(_bed.Outer, _bed.Coffee));
        Assert.Equal(35, _bed.LedgerSum(_bed.Outer, _bed.Coffee));
        var doc = await _bed.Db.StockDocuments.Include(d => d.Lines).AsNoTracking().SingleAsync(d => d.Id == sheet.CountId);
        Assert.Equal(64, doc.SessionCount);
        Assert.Equal(64, doc.PayloadHash!.Length);
        Assert.Equal(CountPayload.Hash(doc.Lines.Select(l => new CountPayload.Line(l.ItemId, l.CountedQty, l.ExpectedQty)), doc.ConfirmedBy!.Value, doc.ConfirmedAt!.Value), doc.PayloadHash);
        Assert.Equal(HandoverStatus.Waiting, (await _bed.Db.ShiftCountHandovers.SingleAsync()).Status);
    }

    [Fact]
    public async Task Out_of_tolerance_needs_a_ten_character_reason_and_goes_to_the_manager()
    {
        var sheet = await SampleShiftAsync();
        await Count(sheet.CountId, 22, 36);

        var none = await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest(null), default);
        var shortReason = await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest("rơi vãi"), default);
        Assert.Equal(InventoryErrors.ReasonRequired, none.Error!.Violations![0].Rule);
        Assert.Equal(InventoryErrors.ReasonRequired, shortReason.Error!.Violations![0].Rule);

        var result = (await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest("Khách đoàn lấy thêm nhiều gói"), default)).Value!;

        Assert.Equal(StockDocumentStatus.PendingApproval, result.Status);
        var over = Assert.Single(result.OutOfTolerance);
        Assert.Equal((_bed.Coffee.Id, -15, 6), (over.ItemId, over.Variance, over.Tolerance));
        var note = Assert.Single(await _bed.Db.Notifications.ToListAsync());
        Assert.Equal(_bed.Manager.User.Id, note.UserId);
        Assert.Equal(InventoryEvents.CountNeedsApproval, note.Type);
    }

    [Fact]
    public async Task A_locked_count_cannot_be_edited_and_confirming_again_returns_the_old_result()
    {
        var sheet = await SampleShiftAsync();
        await Count(sheet.CountId, 35, 36);
        await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest(null), default);
        var movements = await _bed.Db.StockMovements.CountAsync();

        var edit = await _bed.Counts.SaveLinesAsync(_bed.StaffUser, sheet.CountId, new SaveCountLinesRequest([new CountLineInput(_bed.Coffee.Id, 40)]), default);
        var managerEdit = await _bed.Counts.SaveLinesAsync(_bed.ManagerUser, sheet.CountId, new SaveCountLinesRequest([new CountLineInput(_bed.Coffee.Id, 40)]), default);
        var again = (await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest(null), default)).Value!;

        Assert.Equal(InventoryErrors.CountAlreadyConfirmed, edit.Error!.Violations![0].Rule);
        Assert.Equal(InventoryErrors.CountAlreadyConfirmed, managerEdit.Error!.Violations![0].Rule);
        Assert.True(again.AlreadyConfirmed);
        Assert.Equal(StockDocumentStatus.Confirmed, again.Status);
        Assert.Equal(movements, await _bed.Db.StockMovements.CountAsync());
        Assert.Equal(1, await _bed.Db.ShiftCountHandovers.CountAsync());
        Assert.Equal(sheet.CountId, (await _bed.Counts.OpenShiftCountAsync(_bed.StaffUser, _bed.Shift.Id, default)).Value!.CountId);
    }

    [Fact]
    public async Task Only_staff_in_the_shift_count_it()
    {
        await SampleShiftAsync();
        var later = _bed.NewShift(ShiftType.Afternoon, _bed.Shift.EndAt, _bed.Shift.EndAt.AddHours(8));
        _bed.CheckIn(_bed.NextStaff, later);

        var other = await _bed.Counts.OpenShiftCountAsync(_bed.NextStaffUser, _bed.Shift.Id, default);

        Assert.Equal(InventoryErrors.NotYourShift, other.Error!.Violations![0].Rule);
    }

    [Fact]
    public async Task Next_shift_accepts_and_its_opening_is_the_previous_count()
    {
        var sheet = await SampleShiftAsync();
        await Count(sheet.CountId, 35, 36);
        await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest(null), default);
        _bed.Clock.Advance(TimeSpan.FromHours(4));
        _bed.CheckOut(_bed.Staff);
        var next = _bed.NewShift(ShiftType.Afternoon, _bed.Shift.EndAt, _bed.Shift.EndAt.AddHours(8));
        _bed.CheckIn(_bed.NextStaff, next);

        var pending = (await _bed.Counts.PendingHandoversAsync(_bed.NextStaffUser, _bed.Branch.Id, default)).Value!;
        Assert.Equal(sheet.CountId, Assert.Single(pending).CountId);
        var readOnly = (await _bed.Counts.SummaryAsync(_bed.NextStaffUser, sheet.CountId, default)).Value!;
        Assert.Equal(35, readOnly.Rows.Single(r => r.ItemId == _bed.Coffee.Id).Counted);

        var accepted = (await _bed.Counts.AcceptAsync(_bed.NextStaffUser, sheet.CountId, default)).Value!;
        Assert.Equal(HandoverStatus.Accepted, accepted.Handover!.Status);
        Assert.Equal("Nhân viên B", accepted.Handover.ReceivedByName);
        Assert.Empty((await _bed.Counts.PendingHandoversAsync(_bed.NextStaffUser, _bed.Branch.Id, default)).Value!);

        var figures = await _bed.Calculator.ComputeAsync(_bed.Branch.Id, _bed.Outer.Id, next, _bed.Clock.GetUtcNow(), default);
        Assert.Equal(35, figures[_bed.Coffee.Id].Opening);
        Assert.Equal(0, figures[_bed.Coffee.Id].TransfersIn);
    }

    [Fact]
    public async Task The_counting_shift_cannot_accept_its_own_handover()
    {
        var sheet = await SampleShiftAsync();
        await Count(sheet.CountId, 35, 36);
        await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest(null), default);

        var own = await _bed.Counts.AcceptAsync(_bed.StaffUser, sheet.CountId, default);

        Assert.Equal(InventoryErrors.NotYourShift, own.Error!.Violations![0].Rule);
    }

    [Fact]
    public async Task Dispute_then_resolve_sets_the_next_opening_to_the_final_quantity()
    {
        var sheet = await SampleShiftAsync();
        await Count(sheet.CountId, 35, 36);
        await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest(null), default);
        _bed.Clock.Advance(TimeSpan.FromHours(4));
        _bed.CheckOut(_bed.Staff);
        var next = _bed.NewShift(ShiftType.Afternoon, _bed.Shift.EndAt, _bed.Shift.EndAt.AddHours(8));
        _bed.CheckIn(_bed.NextStaff, next);

        var disputed = (await _bed.Counts.DisputeAsync(_bed.NextStaffUser, sheet.CountId,
            new DisputeHandoverRequest([new CountLineInput(_bed.Coffee.Id, 30)], "Đếm lại chỉ còn 30 gói"), default)).Value!;
        Assert.Equal(HandoverStatus.Disputed, disputed.Handover!.Status);
        Assert.Equal(30, Assert.Single(disputed.Handover.DisputeLines).CountedQty);
        Assert.Contains(await _bed.Db.Notifications.ToListAsync(), n => n.Type == InventoryEvents.HandoverDisputed && n.UserId == _bed.Manager.User.Id);

        Assert.Equal(404, (await _bed.Counts.ResolveAsync(_bed.NextStaffUser, sheet.CountId,
            new ResolveHandoverRequest([new ResolveLineInput(_bed.Coffee.Id, 31)], "x"), default)).Error!.Status);
        var resolved = (await _bed.Counts.ResolveAsync(_bed.ManagerUser, sheet.CountId,
            new ResolveHandoverRequest([new ResolveLineInput(_bed.Coffee.Id, 31)], "Quản lý đếm lại: 31"), default)).Value!;

        Assert.Equal(HandoverStatus.Resolved, resolved.Handover!.Status);
        Assert.Equal(35, resolved.Rows.Single(r => r.ItemId == _bed.Coffee.Id).Counted); // the count stays as it was locked
        var adjustment = await _bed.Db.StockDocuments.Include(d => d.Lines).SingleAsync(d => d.Type == StockDocumentType.Adjustment);
        Assert.Equal(sheet.CountId, adjustment.AdjustsDocumentId);
        Assert.Equal(-4, Assert.Single(adjustment.Lines).Qty);
        Assert.Equal(31, _bed.Book(_bed.Outer, _bed.Coffee));

        var figures = await _bed.Calculator.ComputeAsync(_bed.Branch.Id, _bed.Outer.Id, next, _bed.Clock.GetUtcNow(), default);
        Assert.Equal(31, figures[_bed.Coffee.Id].Opening);
        Assert.Equal(0, figures[_bed.Coffee.Id].TransfersIn);
    }

    [Fact]
    public async Task Transfers_wait_while_the_shift_is_counting()
    {
        await SampleShiftAsync();

        var transfer = await _bed.Documents.CreateTransferAsync(_bed.StaffUser,
            new CreateTransferRequest(_bed.Branch.Id, [new QtyLineInput(_bed.Coffee.Id, 5)], "thêm"), default);

        Assert.Equal(InventoryErrors.CountInProgress, transfer.Error!.Violations![0].Rule);
    }

    [Fact]
    public async Task Rotating_items_are_counted_only_on_their_day()
    {
        _bed.Db.InventoryItems.Single(i => i.Id == _bed.Cups.Id).CountDaily = false;
        _bed.Db.SaveChanges();
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        var included = InventoryMath.IsRotationDay(_bed.Shift.Date, _bed.Cups.SortOrder, 3);

        var sheet = (await _bed.Counts.OpenShiftCountAsync(_bed.StaffUser, _bed.Shift.Id, default)).Value!;

        Assert.Equal(included, sheet.Items.Any(i => i.ItemId == _bed.Cups.Id));
        Assert.Contains(sheet.Items, i => i.ItemId == _bed.Coffee.Id);
    }

    [Fact]
    public async Task Inner_count_sets_the_storeroom_book_to_the_count()
    {
        await _bed.StockAsync(_bed.Inner, _bed.Coffee, 200, _bed.Shift.StartAt);
        await _bed.StockAsync(_bed.Inner, _bed.Cups, 300, _bed.Shift.StartAt);
        Assert.Equal(404, (await _bed.Counts.OpenInnerCountAsync(_bed.StaffUser, _bed.Branch.Id, default)).Error!.Status);
        var sheet = (await _bed.Counts.OpenInnerCountAsync(_bed.ManagerUser, _bed.Branch.Id, default)).Value!;
        await _bed.Counts.SaveLinesAsync(_bed.ManagerUser, sheet.CountId, new SaveCountLinesRequest(
            [new CountLineInput(_bed.Coffee.Id, 190), new CountLineInput(_bed.Cups.Id, 300)]), default);

        var summary = (await _bed.Counts.SummaryAsync(_bed.ManagerUser, sheet.CountId, default)).Value!;
        Assert.Equal(-10, summary.Rows.Single(r => r.ItemId == _bed.Coffee.Id).Variance);
        var result = (await _bed.Counts.ConfirmAsync(_bed.ManagerUser, sheet.CountId, new ConfirmCountRequest(null), default));

        Assert.Equal(InventoryErrors.ReasonRequired, result.Error!.Violations![0].Rule);
        var ok = (await _bed.Counts.ConfirmAsync(_bed.ManagerUser, sheet.CountId, new ConfirmCountRequest("Kiểm kê tuần: thiếu 10 gói"), default)).Value!;
        Assert.Equal(StockDocumentStatus.Confirmed, ok.Status);
        Assert.Equal(190, _bed.Book(_bed.Inner, _bed.Coffee));
        Assert.Empty(await _bed.Db.ShiftCountHandovers.ToListAsync());
    }
}
