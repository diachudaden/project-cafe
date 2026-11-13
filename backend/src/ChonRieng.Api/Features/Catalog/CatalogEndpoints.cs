using System.Security.Claims;
using ChonRieng.Api.Authorization;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Sales;

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
            .WithDescription("Công khai. Tất cả bảng giá còn hiệu lực của chi nhánh: bảng giá riêng của chi nhánh trước, sau đó bảng giá toàn chuỗi.");

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

        scoped.MapGet("/products", async (Guid branchId, ClaimsPrincipal user, AddOnService service, CancellationToken ct) =>
                (await service.MenuAsync(CurrentUser.From(user), branchId, ct)).ToHttp())
            .WithName("ListBranchProducts")
            .WithSummary("Thực đơn đồ thêm đang bán")
            .WithDescription("Món đang bán tại chi nhánh (gồm món toàn chuỗi), nhóm theo Category; dùng cho POS.");

        scoped.MapGet("/products/all", async (Guid branchId, ClaimsPrincipal user, AddOnService service, CancellationToken ct) =>
                (await service.ManagedAsync(CurrentUser.From(user), branchId, ct)).ToHttp())
            .RequireManagement()
            .WithName("ListManagedProducts")
            .WithSummary("Tất cả món đồ thêm (kể cả đã ngừng bán)");

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

        manage.MapPost("/products", async (ProductRequest request, ClaimsPrincipal user, AddOnService service, CancellationToken ct) =>
                (await service.CreateAsync(CurrentUser.From(user), request, ct)).ToHttp(p => TypedResults.Created($"/api/products/{p.Id}", p)))
            .WithName("CreateProduct")
            .WithSummary("Thêm món đồ thêm")
            .WithDescription("BranchManager thêm món cho chi nhánh mình; món toàn chuỗi (branchId = null) chỉ Admin.");

        manage.MapPut("/products/{productId:guid}", async (Guid productId, ProductRequest request, ClaimsPrincipal user, AddOnService service, CancellationToken ct) =>
                (await service.UpdateAsync(CurrentUser.From(user), productId, request, ct)).ToHttp())
            .WithName("UpdateProduct").WithSummary("Sửa món / ngừng bán");

        manage.MapDelete("/products/{productId:guid}", async (Guid productId, ClaimsPrincipal user, AddOnService service, CancellationToken ct) =>
                (await service.DeleteAsync(CurrentUser.From(user), productId, ct)).ToHttp(_ => TypedResults.NoContent()))
            .WithName("DeleteProduct")
            .WithSummary("Xóa món")
            .WithDescription("Món đã từng bán chỉ bị chuyển sang ngừng bán để giữ lịch sử hóa đơn.");

        manage.MapPut("/price-plans/{planId:guid}", async (Guid planId, PricePlanRequest request, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
                (await service.UpdatePlanAsync(CurrentUser.From(user), planId, request, ct)).ToHttp())
            .WithName("UpdatePricePlan").WithSummary("Sửa bảng giá");
    }
}
