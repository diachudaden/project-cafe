using System.Security.Claims;
using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Features.Inventory;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var inventory = app.MapGroup("/api/inventory").WithTags("Kho").RequireWorkforce();

        inventory.MapGet("/items", async (bool? includeInactive, InventoryCatalogService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListItemsAsync(includeInactive ?? false, ct)))
            .WithName("ListInventoryItems").WithSummary("Danh mục mặt hàng kho (đơn vị gốc + đơn vị quy đổi)");

        inventory.MapPost("/items", async (SaveInventoryItemRequest request, ClaimsPrincipal user, InventoryCatalogService service, CancellationToken ct) =>
                (await service.CreateItemAsync(CurrentUser.From(user), request, ct)).ToHttp(i => TypedResults.Created($"/api/inventory/items/{i.Id}", i)))
            .RequireManagement()
            .WithName("CreateInventoryItem").WithSummary("Thêm mặt hàng (quản lý)");

        inventory.MapPut("/items/{id:guid}", async (Guid id, SaveInventoryItemRequest request, ClaimsPrincipal user, InventoryCatalogService service, CancellationToken ct) =>
                (await service.UpdateItemAsync(CurrentUser.From(user), id, request, ct)).ToHttp())
            .RequireManagement()
            .WithName("UpdateInventoryItem").WithSummary("Sửa mặt hàng; danh sách đơn vị quy đổi được thay toàn bộ");

        inventory.MapGet("/branch-settings", async (Guid branchId, ClaimsPrincipal user, InventoryCatalogService service, CancellationToken ct) =>
                (await service.ListSettingsAsync(CurrentUser.From(user), branchId, ct)).ToHttp())
            .RequireManagement()
            .WithName("ListItemBranchSettings").WithSummary("Định mức / min / par của từng mặt hàng tại chi nhánh");

        inventory.MapPut("/items/{id:guid}/branch-settings/{branchId:guid}", async (Guid id, Guid branchId, SaveBranchSettingRequest request, ClaimsPrincipal user, InventoryCatalogService service, CancellationToken ct) =>
                (await service.SaveSettingAsync(CurrentUser.From(user), id, branchId, request, ct)).ToHttp())
            .RequireManagement()
            .WithName("SaveItemBranchSetting")
            .WithSummary("Cấu hình mặt hàng theo chi nhánh")
            .WithDescription("NormOverride (nếu có) ưu tiên hơn định mức tự học. OuterPar phải ≥ OuterMin.");

        inventory.MapGet("/balances", async (Guid branchId, WarehouseType warehouse, ClaimsPrincipal user, InventoryCatalogService service, CancellationToken ct) =>
                (await service.BalancesAsync(CurrentUser.From(user), branchId, warehouse, ct)).ToHttp())
            .RequireManagement()
            .WithName("ListStockBalances")
            .WithSummary("Tồn kho theo sổ (warehouse = Inner | Outer)")
            .WithDescription("Kho ngoài kèm tồn dự kiến lúc này. Chỉ quản lý được xem để giữ nguyên tắc đếm mù.");

        inventory.MapGet("/documents", async (Guid branchId, StockDocumentType? type, DateOnly from, DateOnly to, ClaimsPrincipal user, StockDocumentService service, CancellationToken ct) =>
                (await service.ListAsync(CurrentUser.From(user), branchId, type, from, to, ct)).ToHttp())
            .RequireManagement()
            .WithName("ListStockDocuments").WithSummary("Phiếu kho của chi nhánh (tối đa 300 phiếu gần nhất)");

        inventory.MapGet("/documents/{id:guid}", async (Guid id, ClaimsPrincipal user, StockDocumentService service, CancellationToken ct) =>
                (await service.GetAsync(CurrentUser.From(user), id, ct)).ToHttp())
            .RequireManagement()
            .WithName("GetStockDocument").WithSummary("Chi tiết phiếu nhập / chuyển / huỷ / điều chỉnh");

        inventory.MapPost("/receipts", async (CreateReceiptRequest request, ClaimsPrincipal user, StockDocumentService service, CancellationToken ct) =>
                (await service.CreateReceiptAsync(CurrentUser.From(user), request, ct)).ToHttp(d => TypedResults.Created($"/api/inventory/documents/{d.Id}", d)))
            .RequireManagement()
            .WithName("CreateStockReceipt")
            .WithSummary("Phiếu nhập (PN) vào kho trong")
            .WithDescription("Khoá ngay khi tạo. Số lượng nhập theo đơn vị đã chọn và quy đổi về đơn vị gốc. paid = true ghi đúng 1 khoản chi 'Nguyên liệu' vào sổ chi.");

        inventory.MapGet("/transfers/suggestion", async (Guid branchId, ClaimsPrincipal user, StockDocumentService service, CancellationToken ct) =>
                (await service.SuggestAsync(CurrentUser.From(user), branchId, ct)).ToHttp())
            .WithName("TransferSuggestion")
            .WithSummary("Gợi ý lôi hàng ra kho ngoài")
            .WithDescription("suggested = max(0, par − tồn dự kiến), không vượt tồn kho trong. Nhân viên cần đang trong ca; 409 COUNT_IN_PROGRESS khi ca đang đếm kho ngoài.");

        inventory.MapPost("/transfers", async (CreateTransferRequest request, ClaimsPrincipal user, StockDocumentService service, CancellationToken ct) =>
                (await service.CreateTransferAsync(CurrentUser.From(user), request, ct)).ToHttp(d => TypedResults.Created($"/api/inventory/documents/{d.Id}", d)))
            .WithName("CreateStockTransfer")
            .WithSummary("Phiếu chuyển (PC) kho trong → kho ngoài")
            .WithDescription("Một giao dịch: −kho trong, +kho ngoài. Vượt gợi ý hoặc vượt par cần lý do (409 REASON_REQUIRED). Kho trong không đủ: 409 INSUFFICIENT_STOCK.");

        inventory.MapPost("/waste", async (CreateWasteRequest request, ClaimsPrincipal user, StockDocumentService service, CancellationToken ct) =>
                (await service.CreateWasteAsync(CurrentUser.From(user), request, ct)).ToHttp(d => TypedResults.Created($"/api/inventory/documents/{d.Id}", d)))
            .WithName("CreateStockWaste")
            .WithSummary("Phiếu huỷ (PH) hàng hỏng / đổ vỡ")
            .WithDescription("Bắt buộc lý do. Kho trong không được âm (409 INSUFFICIENT_STOCK).");

        MapCountEndpoints(inventory.MapGroup("/shift-counts"), "Đếm kho ngoài kết ca");
        MapCountEndpoints(inventory.MapGroup("/inner-counts"), "Kiểm kê kho trong");

        inventory.MapPost("/shift-counts/open", async (OpenShiftCountRequest request, ClaimsPrincipal user, ShiftCountService service, CancellationToken ct) =>
                (await service.OpenShiftCountAsync(CurrentUser.From(user), request.ShiftId, ct)).ToHttp())
            .WithName("OpenShiftCount")
            .WithSummary("Mở (hoặc tiếp tục) phiếu đếm kho ngoài của ca")
            .WithDescription("Đếm mù: chỉ trả danh sách món (đếm mỗi ca + món xoay vòng hôm nay), KHÔNG có số dự kiến. Chỉ nhân viên đang trong ca hoặc quản lý.");

        inventory.MapPost("/inner-counts/open", async (OpenInnerCountRequest request, ClaimsPrincipal user, ShiftCountService service, CancellationToken ct) =>
                (await service.OpenInnerCountAsync(CurrentUser.From(user), request.BranchId, ct)).ToHttp())
            .RequireManagement()
            .WithName("OpenInnerCount")
            .WithSummary("Mở phiếu kiểm kê kho trong (quản lý)")
            .WithDescription("Đếm mù toàn bộ mặt hàng; xác nhận đưa tồn sổ kho trong về đúng số đếm.");

        inventory.MapGet("/handovers/pending", async (Guid branchId, ClaimsPrincipal user, ShiftCountService service, CancellationToken ct) =>
                (await service.PendingHandoversAsync(CurrentUser.From(user), branchId, ct)).ToHttp())
            .WithName("PendingHandovers")
            .WithSummary("Bàn giao kho ngoài đang chờ ca sau nhận (banner POS)");

        inventory.MapPost("/shift-counts/{id:guid}/accept", async (Guid id, ClaimsPrincipal user, ShiftCountService service, CancellationToken ct) =>
                (await service.AcceptAsync(CurrentUser.From(user), id, ct)).ToHttp())
            .WithName("AcceptHandover")
            .WithSummary("Ca sau xác nhận nhận bàn giao kho ngoài");

        inventory.MapPost("/shift-counts/{id:guid}/dispute", async (Guid id, DisputeHandoverRequest request, ClaimsPrincipal user, ShiftCountService service, CancellationToken ct) =>
                (await service.DisputeAsync(CurrentUser.From(user), id, request, ct)).ToHttp())
            .WithName("DisputeHandover")
            .WithSummary("Ca sau báo không khớp (số tự đếm + ghi chú) → báo quản lý");

        inventory.MapPost("/shift-counts/{id:guid}/resolve", async (Guid id, ResolveHandoverRequest request, ClaimsPrincipal user, ShiftCountService service, CancellationToken ct) =>
                (await service.ResolveAsync(CurrentUser.From(user), id, request, ct)).ToHttp())
            .RequireManagement()
            .WithName("ResolveHandover")
            .WithSummary("Quản lý chốt số bàn giao")
            .WithDescription("Chênh lệch với số đếm được ghi bằng phiếu điều chỉnh (DC) gắn với phiếu đếm; phiếu đếm giữ nguyên. Đầu ca sau = số chốt.");
    }

    private static void MapCountEndpoints(RouteGroupBuilder group, string area)
    {
        var tag = area.Replace(' ', '-');
        group.MapPut("/{id:guid}/lines", async (Guid id, SaveCountLinesRequest request, ClaimsPrincipal user, ShiftCountService service, CancellationToken ct) =>
                (await service.SaveLinesAsync(CurrentUser.From(user), id, request, ct)).ToHttp())
            .WithName($"SaveCountLines-{tag}")
            .WithSummary($"{area}: lưu số đếm")
            .WithDescription("Lần nhập đầu tiên của mỗi món được giữ làm FirstCountedQty (không đổi). Không trả số dự kiến. Phiếu đã khoá: 409 COUNT_ALREADY_CONFIRMED.");

        group.MapGet("/{id:guid}/summary", async (Guid id, ClaimsPrincipal user, ShiftCountService service, CancellationToken ct) =>
                (await service.SummaryAsync(CurrentUser.From(user), id, ct)).ToHttp())
            .WithName($"CountSummary-{tag}")
            .WithSummary($"{area}: bảng tổng hợp")
            .WithDescription("Đầu ca · chuyển vào · huỷ · lượt × định mức · dự kiến · đếm · lệch · ngưỡng. 409 COUNT_INCOMPLETE khi còn món chưa đếm.");

        group.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmCountRequest request, ClaimsPrincipal user, ShiftCountService service, CancellationToken ct) =>
                (await service.ConfirmAsync(CurrentUser.From(user), id, request, ct)).ToHttp())
            .WithName($"ConfirmCount-{tag}")
            .WithSummary($"{area}: xác nhận và khoá")
            .WithDescription("Mọi món trong ngưỡng → Confirmed; có món vượt → PendingApproval (bắt buộc lý do ≥ 10 ký tự, báo quản lý). Gọi lại trên phiếu đã khoá trả kết quả cũ (idempotent).");
    }
}
