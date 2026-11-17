using System.Security.Claims;
using ChonRieng.Api.Features.Common;

namespace ChonRieng.Api.Features.Reports;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var reports = app.MapGroup("/api").WithTags("Báo cáo").RequireManagement();

        reports.MapGet("/dashboard/today", async (Guid? branchId, ClaimsPrincipal user, ReportService service, CancellationToken ct) =>
                (await service.DashboardAsync(CurrentUser.From(user), branchId, ct)).ToHttp())
            .WithName("DashboardToday")
            .WithSummary("Tổng quan hôm nay")
            .WithDescription("Doanh thu hôm nay, tỷ lệ lấp đầy hiện tại, ai đang trong ca và cảnh báo (thiếu người, kết ca chờ duyệt/chênh lệch, đơn chờ duyệt). Không gửi branchId = toàn bộ phạm vi.");

        reports.MapGet("/reports/revenue", async (Guid? branchId, ReportPeriod period, DateOnly date, ClaimsPrincipal user, ReportService service, CancellationToken ct) =>
                (await service.RevenueAsync(CurrentUser.From(user), branchId, period, date, ct)).ToHttp())
            .WithName("RevenueReport").WithSummary("Doanh thu theo kỳ (Week/Month/Quarter/Year) so với kỳ trước");

        reports.MapGet("/reports/profit-loss", async (Guid? branchId, ReportPeriod period, DateOnly date, ClaimsPrincipal user, ReportService service, CancellationToken ct) =>
                (await service.ProfitLossAsync(CurrentUser.From(user), branchId, period, date, ct)).ToHttp())
            .WithName("ProfitLossReport").WithSummary("Lãi lỗ = doanh thu − chi phí − lương");

        reports.MapGet("/reports/occupancy", async (Guid? branchId, ReportPeriod period, DateOnly date, ClaimsPrincipal user, ReportService service, CancellationToken ct) =>
                (await service.OccupancyAsync(CurrentUser.From(user), branchId, period, date, ct)).ToHttp())
            .WithName("OccupancyReport").WithSummary("Tỷ lệ lấp đầy theo loại chỗ");

        reports.MapGet("/reports/branches", async (ReportPeriod period, DateOnly date, ClaimsPrincipal user, ReportService service, CancellationToken ct) =>
                (await service.CompareBranchesAsync(CurrentUser.From(user), period, date, ct)).ToHttp())
            .WithName("BranchComparison").WithSummary("So sánh chi nhánh: doanh thu, lượt khách, lấp đầy, chi phí lương / doanh thu, xếp hạng");
    }
}
