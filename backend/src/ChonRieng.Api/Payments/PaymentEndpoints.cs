using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ChonRieng.Application.Payments;
using ChonRieng.Application.Payments.Sepay;
using ChonRieng.Domain.Payments;
using Microsoft.AspNetCore.Mvc;

namespace ChonRieng.Api.Payments;

public static class PaymentEndpoints
{
    private const int MaxWebhookBytes = 64 * 1024;

    // INTEGRATE: đổi tên role cho khớp dự án.
    private static class Roles
    {
        public const string Manager = "Manager";
        public const string ShiftLead = "ShiftLead";
        public const string Staff = "Staff";
        public const string Customer = "Customer";
    }

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- Webhook SePay (không đăng nhập, xác thực bằng HMAC) ----
        app.MapPost("/api/webhooks/sepay", HandleSepayWebhook)
            .AllowAnonymous()
            .WithName("SepayWebhook")
            .WithTags("Payments");

        // ---- Thanh toán ----
        var pay = app.MapGroup("/api/payments").WithTags("Payments").RequireAuthorization();

        pay.MapPost("/bills/{billId:int}/qr", async (int billId, [FromServices] PaymentService svc, CancellationToken ct) =>
                // INTEGRATE: nếu là khách gọi, kiểm tra bill thuộc về khách đang đăng nhập.
                ToHttp(await svc.CreateQrAsync(billId, ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Staff, Roles.ShiftLead, Roles.Manager, Roles.Customer));

        pay.MapPost("/bills/{billId:int}/cash", async (int billId, ClaimsPrincipal user, [FromServices] PaymentService svc, CancellationToken ct) =>
                ToHttp(await svc.MarkPaidCashAsync(billId, UserId(user), ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Staff, Roles.ShiftLead, Roles.Manager));

        pay.MapGet("/transactions/attention", async ([FromServices] PaymentService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListNeedingAttentionAsync(ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.ShiftLead, Roles.Manager));

        pay.MapPost("/transactions/{id:long}/assign", async (long id, AssignTransactionRequest req, ClaimsPrincipal user, [FromServices] PaymentService svc, CancellationToken ct) =>
                ToHttp(await svc.AssignTransactionAsync(id, req.BillId, UserId(user), req.Note, ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.ShiftLead, Roles.Manager));

        pay.MapPost("/transactions/{id:long}/dismiss", async (long id, DismissTransactionRequest req, ClaimsPrincipal user, [FromServices] PaymentService svc, CancellationToken ct) =>
                ToHttp(await svc.DismissTransactionAsync(id, UserId(user), req.Note, ct)))
            .RequireAuthorization(p => p.RequireRole(Roles.Manager));

        // ---- Sổ thu chi ----
        var ledger = app.MapGroup("/api/ledger").WithTags("Ledger")
            .RequireAuthorization(p => p.RequireRole(Roles.Manager));

        ledger.MapGet("/", async (
                [FromQuery] int? branchId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
                [FromQuery] LedgerDirection? direction, [FromQuery] LedgerCategory? category, [FromQuery] PaymentMethod? method,
                [FromQuery] int? page, [FromQuery] int? pageSize, [FromServices] LedgerService svc, CancellationToken ct) =>
            {
                var filter = new LedgerFilter(
                    branchId,
                    from is { } f ? VietnamTime.DayRangeUtc(f).FromUtc : null,
                    to is { } t ? VietnamTime.DayRangeUtc(t).ToUtc : null,
                    direction, category, method, page ?? 1, pageSize ?? 50);
                return Results.Ok(await svc.ListAsync(filter, ct));
            });

        ledger.MapPost("/expenses", async (AddExpenseRequest req, ClaimsPrincipal user, [FromServices] LedgerService svc, CancellationToken ct) =>
            ToHttp(await svc.AddExpenseAsync(req, UserId(user), ct)));

        ledger.MapPost("/{id:long}/reverse", async (long id, ReverseLedgerRequest req, ClaimsPrincipal user, [FromServices] LedgerService svc, CancellationToken ct) =>
            ToHttp(await svc.ReverseAsync(id, req.Reason, UserId(user), ct)));

        ledger.MapGet("/summary", async (
                [FromQuery] int? branchId, [FromQuery] SummaryPeriod? period, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
                [FromServices] LedgerService svc, CancellationToken ct) =>
            Results.Ok(await svc.SummaryAsync(branchId, period ?? SummaryPeriod.Month, from, to, ct)));

        return app;
    }

    private static async Task<IResult> HandleSepayWebhook(
        HttpRequest request,
        [FromServices] SepaySignatureVerifier verifier,
        [FromServices] PaymentService payments,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Payments.SepayWebhook");

        if (request.ContentLength is > MaxWebhookBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        // 1. Đọc raw body TRƯỚC khi làm bất cứ gì — chữ ký tính trên bytes gốc.
        byte[] raw;
        using (var buffer = new MemoryStream())
        {
            await request.Body.CopyToAsync(buffer, ct);
            if (buffer.Length > MaxWebhookBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            raw = buffer.ToArray();
        }

        // 2. Xác thực HMAC.
        var check = verifier.Verify(
            request.Headers[SepaySignatureVerifier.SignatureHeader].ToString(),
            request.Headers[SepaySignatureVerifier.TimestampHeader].ToString(),
            raw);

        if (check != SignatureCheck.Valid)
        {
            logger.LogWarning("Từ chối webhook SePay: {Reason}, ip={Ip}", check, request.HttpContext.Connection.RemoteIpAddress);
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status401Unauthorized);
        }

        // 3. Parse sau khi đã xác thực.
        SepayWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SepayWebhookPayload>(raw, JsonSerializerOptions.Web);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Webhook SePay đúng chữ ký nhưng JSON hỏng");
            return Success(); // trả lỗi chỉ khiến SePay gửi lại đúng payload hỏng đó
        }

        if (payload is null) return Success();

        // 4. Nghiệp vụ. Lỗi hạ tầng sẽ ném lên → 500 → SePay retry (đúng ý muốn).
        var outcome = await payments.HandleSepayAsync(payload, Encoding.UTF8.GetString(raw), ct);
        logger.LogInformation("Webhook SePay {SepayId}: {Outcome}, {Amount}đ", payload.Id, outcome, payload.TransferAmount);

        return Success();
    }

    /// <summary>SePay chỉ coi là thành công khi HTTP 200 + đúng body {"success": true}.</summary>
    private static IResult Success() => Results.Ok(new { success = true });

    private static IResult ToHttp<T>(PayResult<T> result)
    {
        if (result.IsSuccess) return Results.Ok(result.Value);

        var e = result.Error!;
        var body = new { code = e.Code, message = e.Message };
        return e.Kind switch
        {
            PayErrorKind.NotFound => Results.NotFound(body),
            PayErrorKind.Conflict => Results.Conflict(body),
            PayErrorKind.Validation => Results.BadRequest(body),
            PayErrorKind.Forbidden => Results.Json(body, statusCode: StatusCodes.Status403Forbidden),
            _ => Results.BadRequest(body),
        };
    }

    // INTEGRATE: claim chứa id người dùng của dự án (NameIdentifier hoặc "sub").
    private static int UserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return int.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Không đọc được id người dùng từ token.");
    }
}
