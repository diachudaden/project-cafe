using System.Security.Claims;
using ChonRieng.Api.Features.Common;

namespace ChonRieng.Api.Features.Sales;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var payments = app.MapGroup("/api/payments").WithTags("Thanh toán").RequireWorkforce();

        payments.MapPost("/requests", async (CreateCheckInPaymentRequest request, ClaimsPrincipal user, PaymentRequestService service, CancellationToken ct) =>
                (await service.CreateCheckInAsync(CurrentUser.From(user), request, ct)).ToHttp())
            .WithName("CreatePaymentRequest")
            .WithSummary("Tạo yêu cầu thanh toán VietQR")
            .WithDescription("Tạo yêu cầu thanh toán VietQR cho check-in. Số tiền, chi nhánh và ca được xác định từ dữ liệu máy chủ; trả về mã, số tiền, URL QR và thời hạn.");

        app.MapPost("/api/sessions/{sessionId:guid}/extend/payment-request", async (Guid sessionId, ClaimsPrincipal user, PaymentRequestService service, CancellationToken ct) =>
                (await service.CreateExtensionAsync(CurrentUser.From(user), sessionId, ct)).ToHttp())
            .RequireWorkforce()
            .WithTags("Thanh toán")
            .WithName("CreateExtensionPaymentRequest")
            .WithSummary("Tạo yêu cầu thanh toán VietQR khi gia hạn")
            .WithDescription("Tạo yêu cầu thanh toán theo giá gia hạn của lượt đang hoạt động; không nhận số tiền từ client.");

        payments.MapDelete("/requests/{code}", async (string code, ClaimsPrincipal user, PaymentRequestService service, CancellationToken ct) =>
                (await service.CancelAsync(CurrentUser.From(user), code, ct)).ToHttp(_ => TypedResults.NoContent()))
            .WithName("CancelPaymentRequest")
            .WithSummary("Hủy yêu cầu thanh toán đang chờ")
            .WithDescription("Người tạo yêu cầu hoặc quản lý/Admin đúng chi nhánh có thể hủy yêu cầu chưa hết hạn và chưa được thanh toán.");

        app.MapGet("/api/payments/{code}", async (string code, PaymentRequestService service, CancellationToken ct) =>
                (await service.GetStatusAsync(code, ct)).ToHttp())
            .AllowAnonymous()
            .WithTags("Thanh toán")
            .WithName("GetPaymentRequestStatus")
            .WithSummary("Tra cứu trạng thái yêu cầu thanh toán")
            .WithDescription("Công khai theo mã ngẫu nhiên; yêu cầu hết hạn được trả về trạng thái Expired.");

        app.MapPost("/api/hooks/sepay-payment", async (HttpRequest request, PaymentRequestService service, CancellationToken ct) =>
                await service.HandleWebhookAsync(request, ct))
            .AllowAnonymous()
            .WithName("SePayPaymentWebhook")
            .WithSummary("Webhook xác nhận thanh toán SePay")
            .WithDescription("Xác thực HMAC-SHA256 bằng X-SePay-Signature và X-SePay-Timestamp, lưu PaymentTransaction và cập nhật trạng thái PaymentRequest.");
    }
}
