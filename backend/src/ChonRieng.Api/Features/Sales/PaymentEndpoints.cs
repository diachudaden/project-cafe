using System.Security.Claims;
using ChonRieng.Api.Features.Common;

namespace ChonRieng.Api.Features.Sales;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var payments = app.MapGroup("/api/payments").WithTags("Thanh toán").RequireWorkforce();

        payments.MapPost("/requests", async (CreatePaymentRequestRequest request, ClaimsPrincipal user, PaymentRequestService service, CancellationToken ct) =>
                (await service.CreateAsync(CurrentUser.From(user), request, ct)).ToHttp())
            .WithName("CreatePaymentRequest")
            .WithSummary("Tạo yêu cầu thanh toán VietQR")
            .WithDescription("Tạo PaymentRequest chờ chuyển khoản cho check-in hoặc gia hạn; trả về mã, số tiền và URL QR để khách quét.");

        app.MapPost("/api/hooks/sepay-payment", async (HttpRequest request, PaymentRequestService service, CancellationToken ct) =>
                await service.HandleWebhookAsync(request, ct))
            .AllowAnonymous()
            .WithName("SePayPaymentWebhook")
            .WithSummary("Webhook xác nhận thanh toán SePay")
            .WithDescription("Xác thực HMAC-SHA256 bằng X-SePay-Signature và X-SePay-Timestamp, lưu PaymentTransaction và cập nhật trạng thái PaymentRequest.");
    }
}
