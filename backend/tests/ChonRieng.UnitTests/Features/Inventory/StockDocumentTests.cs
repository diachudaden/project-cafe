using ChonRieng.Api.Features.Inventory;
using ChonRieng.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChonRieng.UnitTests.Features.Inventory;

public sealed class StockDocumentTests : IDisposable
{
    private readonly InventoryTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task Paid_receipt_adds_inner_stock_in_base_units_and_exactly_one_expense()
    {
        var result = await _bed.Documents.CreateReceiptAsync(_bed.ManagerUser, new CreateReceiptRequest(
            _bed.Branch.Id, "NCC A", "HD-01",
            [new ReceiptLineInput(_bed.Coffee.Id, "Hộp", 3, 60_000, null), new ReceiptLineInput(_bed.Cups.Id, null, 100, 500, null)],
            Paid: true, PaymentMethod.Cash, null, null), default);

        var doc = result.Value!;
        Assert.Equal(StockDocumentStatus.Confirmed, doc.Status);
        Assert.Matches(@"^PN-[0-9A-F]{4}-261115-001$", doc.Code);
        Assert.Equal(60, doc.Lines.Single(l => l.ItemId == _bed.Coffee.Id).Qty); // 3 hộp × 20
        Assert.Equal(60, _bed.Book(_bed.Inner, _bed.Coffee));
        Assert.Equal(100, _bed.LedgerSum(_bed.Inner, _bed.Cups));
        var expense = Assert.Single(await _bed.Db.Expenses.ToListAsync());
        Assert.Equal(StockDocumentService.IngredientsExpenseCategory, expense.Category);
        Assert.Equal(230_000m, expense.Amount); // 3 × 60.000 + 100 × 500
        Assert.Equal(doc.Id, expense.StockDocumentId);
        Assert.Equal(expense.Id, doc.ExpenseId);
    }

    [Fact]
    public async Task Unpaid_receipt_writes_no_expense_and_staff_cannot_create_receipts()
    {
        var unpaid = await _bed.Documents.CreateReceiptAsync(_bed.ManagerUser, new CreateReceiptRequest(
            _bed.Branch.Id, null, null, [new ReceiptLineInput(_bed.Coffee.Id, null, 10, 0, null)], false, null, null, null), default);
        Assert.True(unpaid.IsSuccess);
        Assert.Empty(await _bed.Db.Expenses.ToListAsync());

        var staff = await _bed.Documents.CreateReceiptAsync(_bed.StaffUser, new CreateReceiptRequest(
            _bed.Branch.Id, null, null, [new ReceiptLineInput(_bed.Coffee.Id, null, 10, 0, null)], false, null, null, null), default);
        Assert.Equal(404, staff.Error!.Status);
    }

    [Fact]
    public async Task Staff_must_be_in_the_shift_to_transfer()
    {
        var result = await _bed.Documents.CreateTransferAsync(_bed.StaffUser,
            new CreateTransferRequest(_bed.Branch.Id, [new QtyLineInput(_bed.Coffee.Id, 5)], null), default);

        Assert.Equal(InventoryErrors.ShiftNotOpen, result.Error!.Violations![0].Rule);
    }

    [Fact]
    public async Task Transfer_moves_stock_inner_to_outer_and_follows_the_suggestion()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        await _bed.StockAsync(_bed.Inner, _bed.Coffee, 200, _bed.Shift.StartAt.AddHours(-1));
        await _bed.StockAsync(_bed.Outer, _bed.Coffee, 42, _bed.Shift.StartAt.AddHours(-1));
        _bed.Sell(10, _bed.Shift.StartAt.AddHours(1)); // 10 × 1.2 = 12 used → expected 30

        var suggestion = (await _bed.Documents.SuggestAsync(_bed.StaffUser, _bed.Branch.Id, default)).Value!.Single(s => s.ItemId == _bed.Coffee.Id);
        Assert.Equal(30, suggestion.ExpectedNow);
        Assert.Equal(30, suggestion.Suggested); // par 60 − 30
        Assert.Equal(200, suggestion.InnerQty);

        var transfer = await _bed.Documents.CreateTransferAsync(_bed.StaffUser,
            new CreateTransferRequest(_bed.Branch.Id, [new QtyLineInput(_bed.Coffee.Id, 30)], null), default);

        Assert.True(transfer.IsSuccess);
        Assert.Equal(_bed.Shift.Id, transfer.Value!.ShiftId);
        Assert.Equal(30, transfer.Value.Lines.Single().SuggestedQty);
        Assert.Equal(170, _bed.Book(_bed.Inner, _bed.Coffee));
        Assert.Equal(72, _bed.Book(_bed.Outer, _bed.Coffee));
        Assert.Equal(_bed.Book(_bed.Inner, _bed.Coffee), _bed.LedgerSum(_bed.Inner, _bed.Coffee));
    }

    [Fact]
    public async Task Transfer_above_suggestion_requires_a_reason_but_is_not_blocked()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        await _bed.StockAsync(_bed.Inner, _bed.Coffee, 200, _bed.Shift.StartAt.AddHours(-1));
        await _bed.StockAsync(_bed.Outer, _bed.Coffee, 50, _bed.Shift.StartAt.AddHours(-1));

        var noReason = await _bed.Documents.CreateTransferAsync(_bed.StaffUser,
            new CreateTransferRequest(_bed.Branch.Id, [new QtyLineInput(_bed.Coffee.Id, 20)], null), default);
        Assert.Equal(InventoryErrors.ReasonRequired, noReason.Error!.Violations![0].Rule);

        var withReason = await _bed.Documents.CreateTransferAsync(_bed.StaffUser,
            new CreateTransferRequest(_bed.Branch.Id, [new QtyLineInput(_bed.Coffee.Id, 20)], "Chuẩn bị khách đoàn"), default);
        Assert.True(withReason.IsSuccess);
        Assert.Equal("Chuẩn bị khách đoàn", withReason.Value!.Reason);
    }

    [Fact]
    public async Task Transfer_cannot_take_more_than_the_inner_warehouse_holds()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        await _bed.StockAsync(_bed.Inner, _bed.Coffee, 10, _bed.Shift.StartAt);

        var result = await _bed.Documents.CreateTransferAsync(_bed.StaffUser,
            new CreateTransferRequest(_bed.Branch.Id, [new QtyLineInput(_bed.Coffee.Id, 11)], "thử"), default);

        Assert.Equal(409, result.Error!.Status);
        Assert.Equal(InventoryErrors.InsufficientStock, result.Error.Violations![0].Rule);
        Assert.Equal(10, _bed.Book(_bed.Inner, _bed.Coffee));
        Assert.Equal(0, await _bed.Db.StockDocuments.CountAsync(d => d.Type == StockDocumentType.Transfer));
    }

    [Fact]
    public async Task Waste_needs_a_reason_and_inner_stock_cannot_go_negative()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        await _bed.StockAsync(_bed.Inner, _bed.Cups, 5, _bed.Shift.StartAt);

        var noReason = await _bed.Documents.CreateWasteAsync(_bed.StaffUser,
            new CreateWasteRequest(_bed.Branch.Id, WarehouseType.Outer, [new QtyLineInput(_bed.Cups.Id, 2)], " ", null), default);
        Assert.Equal(InventoryErrors.ReasonRequired, noReason.Error!.Violations![0].Rule);

        var tooMany = await _bed.Documents.CreateWasteAsync(_bed.StaffUser,
            new CreateWasteRequest(_bed.Branch.Id, WarehouseType.Inner, [new QtyLineInput(_bed.Cups.Id, 6)], "Rách", null), default);
        Assert.Equal(InventoryErrors.InsufficientStock, tooMany.Error!.Violations![0].Rule);

        var outer = await _bed.Documents.CreateWasteAsync(_bed.StaffUser,
            new CreateWasteRequest(_bed.Branch.Id, WarehouseType.Outer, [new QtyLineInput(_bed.Cups.Id, 2)], "Đổ vỡ", null), default);
        Assert.True(outer.IsSuccess);
        Assert.Equal(-2, _bed.Book(_bed.Outer, _bed.Cups));
    }

    [Fact]
    public async Task Waste_in_the_outer_warehouse_lowers_the_expected_stock()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        await _bed.StockAsync(_bed.Outer, _bed.Coffee, 42, _bed.Shift.StartAt.AddHours(-1));
        await _bed.Documents.CreateWasteAsync(_bed.StaffUser,
            new CreateWasteRequest(_bed.Branch.Id, WarehouseType.Outer, [new QtyLineInput(_bed.Coffee.Id, 2)], "Rách gói", null), default);

        var figures = await _bed.Calculator.ComputeAsync(_bed.Branch.Id, _bed.Outer.Id, _bed.Shift, _bed.Clock.GetUtcNow(), default);

        Assert.Equal(42, figures[_bed.Coffee.Id].Opening);
        Assert.Equal(2, figures[_bed.Coffee.Id].Waste);
        Assert.Equal(40, figures[_bed.Coffee.Id].Expected);
    }

    [Fact]
    public async Task Balances_are_management_only()
    {
        Assert.Equal(404, (await _bed.Catalog.BalancesAsync(_bed.StaffUser, _bed.Branch.Id, WarehouseType.Outer, default)).Error!.Status);
        var rows = (await _bed.Catalog.BalancesAsync(_bed.ManagerUser, _bed.Branch.Id, WarehouseType.Inner, default)).Value!;
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.True(r.BelowMin));
    }
}
