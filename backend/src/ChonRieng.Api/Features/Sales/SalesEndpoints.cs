using System.Security.Claims;
using ChonRieng.Api.Features.Common;

namespace ChonRieng.Api.Features.Sales;

public static class SalesEndpoints
{
    public const string ClaimRateLimit = "bill-claim";

    public static void MapSalesEndpoints(this WebApplication app)
    {
        var sessions = app.MapGroup("/api/sessions").WithTags("Lượt ngồi (POS)").RequireWorkforce();

        sessions.MapPost("/", async (CheckInSessionRequest request, ClaimsPrincipal user, SessionService service, CancellationToken ct) =>
                (await service.CheckInAsync(CurrentUser.From(user), request, ct)).ToHttp(s => TypedResults.Created($"/api/sessions/{s.Id}", s)))
            .WithName("CheckInSession")
            .WithSummary("Check-in khách (thu tiền và tạo bill)")
            .WithDescription("Tạo lượt ngồi và hóa đơn trong một giao dịch. Nhân viên phải đang tham gia ca tại chi nhánh. 409 nếu chỗ vừa có người (khóa duy nhất một lượt đang diễn ra mỗi chỗ).");

        sessions.MapPost("/{sessionId:guid}/extend", async (Guid sessionId, ClaimsPrincipal user, SessionService service, CancellationToken ct) =>
                (await service.ExtendAsync(CurrentUser.From(user), sessionId, ct)).ToHttp())
            .WithName("ExtendSession")
            .WithSummary("Gia hạn thêm một lượt")
            .WithDescription("Cộng thời lượng của bảng giá vào giờ kết thúc và cộng giá gia hạn vào cùng hóa đơn.");

        sessions.MapPost("/{sessionId:guid}/items", async (Guid sessionId, AddOnSaleRequest request, ClaimsPrincipal user, AddOnService service, CancellationToken ct) =>
                (await service.SellCashAsync(CurrentUser.From(user), sessionId, request, ct)).ToHttp())
            .WithName("SellAddOnItems")
            .WithSummary("Bán đồ thêm (tiền mặt)")
            .WithDescription("Thêm các món (mì, trứng, bánh...) vào hóa đơn của lượt đang diễn ra; giá lấy từ thực đơn hiện tại và được lưu lại trên từng dòng. Không ảnh hưởng thời gian ngồi.");

        sessions.MapPost("/{sessionId:guid}/items/payment-request", async (Guid sessionId, AddOnSaleRequest request, ClaimsPrincipal user, PaymentRequestService service, CancellationToken ct) =>
                (await service.CreateAddOnAsync(CurrentUser.From(user), sessionId, request, ct)).ToHttp())
            .WithTags("Thanh toán")
            .WithName("CreateAddOnPaymentRequest")
            .WithSummary("Tạo QR VietQR cho đồ thêm")
            .WithDescription("Số tiền do máy chủ tính từ thực đơn; các món được thêm vào hóa đơn khi SePay xác nhận thanh toán.");

        sessions.MapPost("/{sessionId:guid}/close", async (Guid sessionId, ClaimsPrincipal user, SessionService service, CancellationToken ct) =>
                (await service.CloseAsync(CurrentUser.From(user), sessionId, ct)).ToHttp())
            .WithName("CloseSession").WithSummary("Trả chỗ (check-out)");

        app.MapGet("/api/branches/{branchId:guid}/sessions/active", async (Guid branchId, ClaimsPrincipal user, SessionService service, CancellationToken ct) =>
                (await service.ActiveAsync(CurrentUser.From(user), branchId, ct)).ToHttp())
            .WithTags("Lượt ngồi (POS)")
            .RequireAuthorization("BranchScope")
            .WithName("ListActiveSessions").WithSummary("Lượt ngồi đang diễn ra của chi nhánh");

        var pos = app.MapGroup("/api").WithTags("Lượt ngồi (POS)").RequireWorkforce();

        pos.MapGet("/bills/{code}", async (string code, ClaimsPrincipal user, SessionService service, CancellationToken ct) =>
                (await service.GetByBillCodeAsync(CurrentUser.From(user), code, ct)).ToHttp())
            .WithName("GetBillByCode").WithSummary("Tra cứu hóa đơn theo mã");

        pos.MapGet("/customers/lookup", (string q, SessionService service, CancellationToken ct) =>
                service.LookupCustomersAsync(q, ct))
            .WithName("LookupCustomer")
            .WithSummary("Tìm thành viên khi check-in")
            .WithDescription("Khớp chính xác mã thành viên, số điện thoại hoặc email; chỉ trả tên, hạng và điểm.");

        var me = app.MapGroup("/api/me").WithTags("Khách hàng").RequireCustomer();

        me.MapPost("/bills/claim", async (ClaimBillRequest request, ClaimsPrincipal user, LoyaltyService service, CancellationToken ct) =>
                (await service.ClaimAsync(CurrentUser.From(user), request.Code, ct)).ToHttp())
            .RequireRateLimiting(ClaimRateLimit)
            .WithName("ClaimBill")
            .WithSummary("Nhập bill tích điểm")
            .WithDescription("Mỗi bill chỉ tích một lần, trong 7 ngày. 1.000đ = 1 điểm. Giới hạn 10 lần/phút để chống dò mã.");

        me.MapGet("/points", async (ClaimsPrincipal user, LoyaltyService service, CancellationToken ct) =>
                (await service.PointsAsync(CurrentUser.From(user), ct)).ToHttp())
            .WithName("MyPoints").WithSummary("Điểm, hạng thành viên và lịch sử");

        me.MapGet("/bills", (ClaimsPrincipal user, LoyaltyService service, CancellationToken ct) =>
                service.BillsAsync(CurrentUser.From(user), ct))
            .WithName("MyBills").WithSummary("Hóa đơn đã tích điểm");

        me.MapGet("/session", async (ClaimsPrincipal user, LoyaltyService service, CancellationToken ct) =>
                await service.CurrentSessionAsync(CurrentUser.From(user), ct) is { } s ? Results.Ok(s) : Results.NoContent())
            .WithName("MyActiveSession").WithSummary("Lượt ngồi hiện tại (204 nếu không có)");
    }
}
