using System.Security.Claims;
using ChonRieng.Api.Authorization;
using ChonRieng.Api.Features.Common;

namespace ChonRieng.Api.Features.Catalog;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var branches = app.MapGroup("/api/branches").WithTags("Chi nhánh & chỗ ngồi");

        branches.MapGet("/", (double? lat, double? lng, CatalogService service, CancellationToken ct) =>
                service.ListBranchesAsync(lat, lng, ct))
            .AllowAnonymous()
            .WithName("ListPublicBranches")
            .WithSummary("Danh sách chi nhánh đang hoạt động")
            .WithDescription("Công khai. Trả về chi nhánh đang hoạt động, trạng thái mở cửa theo giờ Việt Nam và số chỗ trống theo loại. Gửi lat/lng để sắp xếp theo khoảng cách.");

        branches.MapGet("/{branchId:guid}/availability", async (Guid branchId, CatalogService service, CancellationToken ct) =>
                (await service.GetAvailabilityAsync(branchId, ct)).ToHttp())
            .AllowAnonymous()
            .WithName("GetBranchAvailability")
            .WithSummary("Số chỗ trống của chi nhánh")
            .WithDescription("Công khai. Chỉ trả về số lượng theo loại chỗ; không lộ mã chỗ hay thông tin khách.");

        branches.MapGet("/{branchId:guid}/price-plans", (Guid branchId, CatalogService service, CancellationToken ct) =>
                service.GetActivePlansAsync(branchId, ct))
            .AllowAnonymous()
            .WithName("GetActivePricePlans")
            .WithSummary("Bảng giá đang áp dụng")
            .WithDescription("Công khai. Bảng giá còn hiệu lực; bảng giá riêng của chi nhánh thay thế bảng giá toàn chuỗi cùng thời lượng.");

        var scoped = app.MapGroup("/api/branches/{branchId:guid}").WithTags("Chi nhánh & chỗ ngồi").RequireBranchScope();

        scoped.MapGet("/floor", async (Guid branchId, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.GetFloorAsync(CurrentUser.From(user), branchId, ct)).ToHttp())
            .WithName("GetBranchFloor")
            .WithSummary("Sơ đồ chỗ ngồi realtime")
            .WithDescription("Trạng thái từng chỗ: Available (trống), Occupied, EndingSoon (còn ≤ 15 phút), Overdue (quá giờ), Maintenance. Dùng cho màn hình Quầy của POS.");

        scoped.MapGet("/price-plans/all", async (Guid branchId, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.ListManagedPlansAsync(CurrentUser.From(user), branchId, ct)).ToHttp())
            .WithName("ListManagedPricePlans")
            .WithSummary("Tất cả bảng giá của chi nhánh (kể cả hết hạn)");

        scoped.MapPost("/zones", async (Guid branchId, ZoneRequest request, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.CreateZoneAsync(CurrentUser.From(user), branchId, request, ct)).ToHttp(z => TypedResults.Created($"/api/zones/{z.Id}", z)))
            .RequireManagement()
            .WithName("CreateZone")
            .WithSummary("Tạo khu vực");

        var manage = app.MapGroup("/api").WithTags("Chi nhánh & chỗ ngồi").RequireManagement();

        manage.MapPut("/zones/{zoneId:guid}", async (Guid zoneId, ZoneRequest request, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.UpdateZoneAsync(CurrentUser.From(user), zoneId, request, ct)).ToHttp())
            .WithName("UpdateZone").WithSummary("Đổi tên khu vực");

        manage.MapDelete("/zones/{zoneId:guid}", async (Guid zoneId, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.DeleteZoneAsync(CurrentUser.From(user), zoneId, ct)).ToHttp(_ => TypedResults.NoContent()))
            .WithName("DeleteZone").WithSummary("Xóa khu vực trống");

        manage.MapPost("/zones/{zoneId:guid}/seats", async (Guid zoneId, SeatRequest request, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.CreateSeatAsync(CurrentUser.From(user), zoneId, request, ct)).ToHttp(s => TypedResults.Created($"/api/seats/{s.Id}", s)))
            .WithName("CreateSeat").WithSummary("Thêm chỗ ngồi");

        manage.MapPut("/seats/{seatId:guid}", async (Guid seatId, SeatRequest request, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.UpdateSeatAsync(CurrentUser.From(user), seatId, request, ct)).ToHttp())
            .WithName("UpdateSeat").WithSummary("Sửa chỗ ngồi");

        manage.MapDelete("/seats/{seatId:guid}", async (Guid seatId, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.DeleteSeatAsync(CurrentUser.From(user), seatId, ct)).ToHttp(_ => TypedResults.NoContent()))
            .WithName("DeleteSeat").WithSummary("Xóa chỗ chưa từng sử dụng");

        manage.MapPut("/seats/{seatId:guid}/maintenance", async (Guid seatId, MaintenanceRequest request, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.SetMaintenanceAsync(CurrentUser.From(user), seatId, request.Maintenance, ct)).ToHttp())
            .WithName("SetSeatMaintenance")
            .WithSummary("Bật/tắt bảo trì chỗ ngồi")
            .WithDescription("Trả 409 nếu chỗ đang có khách.");

        manage.MapPost("/price-plans", async (PricePlanRequest request, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.CreatePlanAsync(CurrentUser.From(user), request, ct)).ToHttp(p => TypedResults.Created($"/api/price-plans/{p.Id}", p)))
            .WithName("CreatePricePlan")
            .WithSummary("Tạo bảng giá")
            .WithDescription("BranchManager tạo bảng giá cho chi nhánh mình; bảng giá toàn chuỗi (branchId = null) chỉ Admin.");

        manage.MapPut("/price-plans/{planId:guid}", async (Guid planId, PricePlanRequest request, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.UpdatePlanAsync(CurrentUser.From(user), planId, request, ct)).ToHttp())
            .WithName("UpdatePricePlan").WithSummary("Sửa bảng giá");
    }
}
