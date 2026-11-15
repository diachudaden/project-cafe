using ChonRieng.Api.Features.Inventory;
using ChonRieng.Domain.Entities;
using Xunit;

namespace ChonRieng.UnitTests.Features.Inventory;

public sealed class InventoryReportTests : IDisposable
{
    private readonly InventoryTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Fact]
    public async Task Usage_report_gives_actual_norm_per_session_by_shift_and_day()
    {
        _bed.CheckIn(_bed.Staff, _bed.Shift);
        await _bed.StockAsync(_bed.Inner, _bed.Coffee, 500, _bed.Shift.StartAt.AddHours(-1));
        await _bed.StockAsync(_bed.Outer, _bed.Coffee, 42, _bed.Shift.StartAt.AddHours(-1));
        await _bed.StockAsync(_bed.Outer, _bed.Cups, 100, _bed.Shift.StartAt.AddHours(-1));
        await _bed.Documents.CreateTransferAsync(_bed.StaffUser, new CreateTransferRequest(_bed.Branch.Id, [new QtyLineInput(_bed.Coffee.Id, 72)], "Khách đông"), default);
        _bed.Sell(64, _bed.Shift.StartAt.AddHours(1));
        var sheet = (await _bed.Counts.OpenShiftCountAsync(_bed.StaffUser, _bed.Shift.Id, default)).Value!;
        await _bed.Counts.SaveLinesAsync(_bed.StaffUser, sheet.CountId, new SaveCountLinesRequest(
            [new CountLineInput(_bed.Coffee.Id, 22), new CountLineInput(_bed.Cups.Id, 36)]), default);
        await _bed.Counts.ConfirmAsync(_bed.StaffUser, sheet.CountId, new ConfirmCountRequest("Khách đoàn lấy thêm nhiều"), default);
        var day = _bed.Shift.Date;

        var byShift = (await _bed.Reports.UsageAsync(_bed.ManagerUser, _bed.Branch.Id, day, day, UsageGroupBy.Shift, default)).Value!;
        var coffee = byShift.Rows.Single(r => r.ItemId == _bed.Coffee.Id);
        Assert.Equal((64, 92, 76.8m, 1.438m, -15), (coffee.Sessions, coffee.ActualUsage, coffee.NormUsage, coffee.ActualPerSession, coffee.Variance));
        Assert.Equal(ShiftType.Morning, coffee.ShiftType);

        var byDay = (await _bed.Reports.UsageAsync(_bed.ManagerUser, _bed.Branch.Id, day, day, UsageGroupBy.Day, default)).Value!;
        Assert.Equal(2, byDay.Rows.Count);
        Assert.Equal(404, (await _bed.Reports.UsageAsync(_bed.StaffUser, _bed.Branch.Id, day, day, UsageGroupBy.Day, default)).Error!.Status);

        var branches = (await _bed.Reports.BranchesAsync(_bed.ManagerUser, day, day, default)).Value!;
        Assert.Equal(1.438m, branches.Single(r => r.ItemId == _bed.Coffee.Id).ActualPerSession);
    }

    [Fact]
    public async Task Issues_report_separates_misrecorded_transfers_from_real_loss()
    {
        var at = _bed.Clock.GetUtcNow();
        _bed.Db.InventoryIssues.AddRange(
            new InventoryIssue { BranchId = _bed.Branch.Id, ItemId = _bed.Coffee.Id, Kind = InventoryIssueKind.TransferMisrecorded, InnerVar = -10, OuterVar = 10, Total = 0, PeriodFrom = at.AddDays(-7), PeriodTo = at, InnerCountDocumentId = Guid.NewGuid(), CreatedAt = at },
            new InventoryIssue { BranchId = _bed.Branch.Id, ItemId = _bed.Cups.Id, Kind = InventoryIssueKind.RealLoss, InnerVar = -10, OuterVar = 0, Total = -10, PeriodFrom = at.AddDays(-7), PeriodTo = at, InnerCountDocumentId = Guid.NewGuid(), CreatedAt = at },
            new InventoryIssue { BranchId = _bed.Branch.Id, ItemId = _bed.Cups.Id, Kind = InventoryIssueKind.Normal, PeriodFrom = at.AddDays(-7), PeriodTo = at, InnerCountDocumentId = Guid.NewGuid(), CreatedAt = at, Status = InventoryIssueStatus.Resolved });
        _bed.Db.SaveChanges();
        await _bed.Documents.CreateReceiptAsync(_bed.ManagerUser, new CreateReceiptRequest(_bed.Branch.Id, null, null,
            [new ReceiptLineInput(_bed.Cups.Id, null, 100, 500, null)], false, null, null, null), default);
        var day = _bed.Shift.Date;

        var report = (await _bed.Reports.IssuesAsync(_bed.ManagerUser, _bed.Branch.Id, day.AddDays(-1), day.AddDays(1), default)).Value!;

        Assert.Equal(2, report.Issues.Count); // Normal is not an issue
        Assert.Equal(10, report.Kinds.Single(k => k.Kind == InventoryIssueKind.TransferMisrecorded).Units);
        var cups = report.Items.Single(i => i.ItemId == _bed.Cups.Id);
        Assert.Equal((10, 5_000m), (cups.RealLoss, cups.LossValue));
        Assert.Equal(10, report.Items.Single(i => i.ItemId == _bed.Coffee.Id).Misrecorded);
    }
}
