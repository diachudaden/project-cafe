using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Services;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChonRieng.Api.Features.Sales;

public sealed class PaymentRequestService(
    AppDbContext db,
    IOptions<PaymentOptions> options,
    TimeProvider clock,
    SessionService sessions)
{
    private readonly PaymentOptions _options = options.Value;

    public async Task<Result<CreatePaymentRequestResponse>> CreateCheckInAsync(
        CurrentUser user,
        CreateCheckInPaymentRequest request,
        CancellationToken ct)
    {
        var context = await sessions.PrepareCheckInPaymentAsync(user, request.SeatId, request.PricePlanId, request.CustomerId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        await ExpirePendingForSeatAsync(request.SeatId, ct);
        context = await sessions.PrepareCheckInPaymentAsync(user, request.SeatId, request.PricePlanId, request.CustomerId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        return await CreateAsync(user, context.Value!, ct);
    }

    public async Task<Result<CreatePaymentRequestResponse>> CreateExtensionAsync(CurrentUser user, Guid sessionId, CancellationToken ct)
    {
        var context = await sessions.PrepareExtensionPaymentAsync(user, sessionId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        await ExpirePendingForSeatAsync(context.Value!.SeatId, ct);
        context = await sessions.PrepareExtensionPaymentAsync(user, sessionId, ct);
        if (context.Error is not null)
        {
            return context.Error;
        }

        return await CreateAsync(user, context.Value!, ct);
    }

    public async Task<Result<PaymentRequestStatusResponse>> GetStatusAsync(string code, CancellationToken ct)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var paymentRequest = await db.PaymentRequests.SingleOrDefaultAsync(p => p.Code == normalizedCode, ct);
        if (paymentRequest is null)
        {
            return ServiceError.NotFound("Không tìm thấy yêu cầu thanh toán.");
        }

        ExpireIfOverdue(paymentRequest);
        await db.SaveChangesAsync(ct);
        return new PaymentRequestStatusResponse(
            paymentRequest.Code,
            paymentRequest.Amount,
            paymentRequest.Status,
            paymentRequest.ExpiresAt,
            paymentRequest.PaidAt);
    }

    public async Task<Result<Done>> CancelAsync(CurrentUser user, string code, CancellationToken ct)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var paymentRequest = await db.PaymentRequests.SingleOrDefaultAsync(p => p.Code == normalizedCode, ct);
        if (paymentRequest is null || !user.CanAccessBranch(paymentRequest.BranchId))
        {
            return ServiceError.NotFound("Không tìm thấy yêu cầu thanh toán.");
        }

        if (paymentRequest.CreatedByUserId != user.UserId && !user.CanManageBranch(paymentRequest.BranchId))
        {
            return ServiceError.NotFound("Không tìm thấy yêu cầu thanh toán.");
        }

        ExpireIfOverdue(paymentRequest);
        if (paymentRequest.Status != PaymentRequestStatus.Pending)
        {
            return ServiceError.Conflict("Yêu cầu không còn chờ thanh toán", "Chỉ hủy được yêu cầu đang chờ.");
        }

        paymentRequest.Status = PaymentRequestStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return new Done();
    }

    public async Task<int> ExpirePendingAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var expiredRequests = await db.PaymentRequests
            .Where(p => p.Status == PaymentRequestStatus.Pending && p.ExpiresAt <= now)
            .ToListAsync(ct);

        foreach (var paymentRequest in expiredRequests)
        {
            paymentRequest.Status = PaymentRequestStatus.Expired;
        }

        if (expiredRequests.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return expiredRequests.Count;
    }

    private async Task<Result<CreatePaymentRequestResponse>> CreateAsync(
        CurrentUser user,
        PaymentRequestContext context,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var paymentRequest = new PaymentRequest
        {
            Code = await GenerateUniqueCodeAsync(ct),
            Kind = context.Kind,
            BranchId = context.BranchId,
            SeatId = context.SeatId,
            PricePlanId = context.PricePlanId,
            CustomerId = context.CustomerId,
            SessionId = context.SessionId,
            ShiftId = context.ShiftId,
            Amount = context.Amount,
            Status = PaymentRequestStatus.Pending,
            ExpiresAt = now.AddMinutes(_options.HoldMinutes),
            CreatedByUserId = user.UserId,
            CreatedAt = now,
        };

        db.PaymentRequests.Add(paymentRequest);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            db.Entry(paymentRequest).State = EntityState.Detached;
            return ServiceError.Conflict("Chỗ đã được giữ", "Một thiết bị khác vừa tạo yêu cầu thanh toán cho chỗ này.");
        }

        return new CreatePaymentRequestResponse(
            paymentRequest.Code,
            paymentRequest.Amount,
            BuildQrUrl(paymentRequest.Amount, paymentRequest.Code),
            paymentRequest.ExpiresAt);
    }

    private async Task ExpirePendingForSeatAsync(Guid seatId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var expired = await db.PaymentRequests
            .Where(p => p.SeatId == seatId && p.Status == PaymentRequestStatus.Pending && p.ExpiresAt <= now)
            .ToListAsync(ct);
        if (expired.Count == 0)
        {
            return;
        }

        foreach (var paymentRequest in expired)
        {
            paymentRequest.Status = PaymentRequestStatus.Expired;
        }

        await db.SaveChangesAsync(ct);
    }

    private void ExpireIfOverdue(PaymentRequest paymentRequest)
    {
        if (paymentRequest.Status == PaymentRequestStatus.Pending && paymentRequest.ExpiresAt <= clock.GetUtcNow())
        {
            paymentRequest.Status = PaymentRequestStatus.Expired;
        }
    }

    public async Task<IResult> HandleWebhookAsync(HttpRequest httpRequest, CancellationToken ct)
    {
        httpRequest.EnableBuffering();
        var rawBody = await ReadBodyAsync(httpRequest);
        var timestamp = httpRequest.Headers["X-SePay-Timestamp"].FirstOrDefault();
        var signature = httpRequest.Headers["X-SePay-Signature"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(signature))
        {
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!long.TryParse(timestamp, out var unixTimestamp))
        {
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - unixTimestamp) > _options.TimestampToleranceSeconds)
        {
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var expectedSignature = ComputeSignature(timestamp, rawBody);
        if (!string.Equals(signature.Trim(), expectedSignature, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var payload = JsonSerializer.Deserialize<SePayWebhookPayload>(rawBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (payload is null)
        {
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            await RecordTransactionAsync(payload, null, PaymentMatchStatus.Unmatched, "Bỏ qua giao dịch ra/ngoài.", ct);
            return Results.Ok(new { success = true });
        }

        if (!string.Equals(payload.AccountNumber, _options.AccountNumber, StringComparison.OrdinalIgnoreCase))
        {
            await RecordTransactionAsync(payload, null, PaymentMatchStatus.Unmatched, "SePay account không khớp cấu hình.", ct);
            return Results.Ok(new { success = true });
        }

        var existing = await db.PaymentTransactions.AsNoTracking().SingleOrDefaultAsync(t => t.ProviderTxId == payload.Id, ct);
        if (existing is not null)
        {
            return Results.Ok(new { success = true });
        }

        var matchedRequest = await MatchRequestAsync(payload, ct);
        var matchStatus = PaymentMatchStatus.Unmatched;
        var note = "Không tìm thấy mã thanh toán phù hợp.";

        if (matchedRequest is not null)
        {
            var amount = ParseAmount(payload.TransferAmount);
            if (amount == matchedRequest.Amount)
            {
                matchStatus = PaymentMatchStatus.Matched;
                note = "Khớp đúng số tiền.";
                matchedRequest.Status = PaymentRequestStatus.Paid;
                matchedRequest.PaidAt = clock.GetUtcNow();
            }
            else if (amount < matchedRequest.Amount)
            {
                matchStatus = PaymentMatchStatus.Underpaid;
                note = $"Khớp mã nhưng thanh toán thiếu {matchedRequest.Amount - amount:0} VND.";
                matchedRequest.Status = PaymentRequestStatus.NeedsAttention;
            }
            else
            {
                matchStatus = PaymentMatchStatus.Overpaid;
                note = $"Khớp mã nhưng thanh toán thừa {amount - matchedRequest.Amount:0} VND.";
                matchedRequest.Status = PaymentRequestStatus.NeedsAttention;
            }
        }

        await RecordTransactionAsync(payload, matchedRequest?.Id, matchStatus, note, ct);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { success = true });
    }

    private async Task<PaymentRequest?> MatchRequestAsync(SePayWebhookPayload payload, CancellationToken ct)
    {
        var pending = await db.PaymentRequests
            .Where(r => r.Status == PaymentRequestStatus.Pending && r.ExpiresAt > clock.GetUtcNow())
            .ToListAsync(ct);

        var candidates = pending
            .Where(r => string.Equals(r.Code, payload.Code, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(payload.Content) && r.Code.Contains(payload.Content.Trim(), StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(payload.Content) && payload.Content.Contains(r.Code, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        return null;
    }

    private async Task RecordTransactionAsync(SePayWebhookPayload payload, Guid? paymentRequestId, PaymentMatchStatus matchStatus, string note, CancellationToken ct)
    {
        var transaction = new PaymentTransaction
        {
            Provider = "SePay",
            ProviderTxId = payload.Id,
            AccountNumber = payload.AccountNumber,
            Amount = ParseLongAmount(payload.TransferAmount),
            Code = payload.Code,
            Content = payload.Content,
            ReferenceCode = payload.ReferenceCode,
            OccurredAt = ParseOccurredAt(payload.TransactionDate),
            ReceivedAt = clock.GetUtcNow(),
            MatchStatus = matchStatus,
            PaymentRequestId = paymentRequestId,
            Note = note,
        };

        if (paymentRequestId is not null)
        {
            var request = await db.PaymentRequests.SingleOrDefaultAsync(r => r.Id == paymentRequestId, ct);
            if (request is not null)
            {
                request.Status = request.Status == PaymentRequestStatus.Pending ? PaymentRequestStatus.Paid : request.Status;
            }
        }

        db.PaymentTransactions.Add(transaction);
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private string ComputeSignature(string timestamp, string rawBody)
    {
        var payload = $"{timestamp}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var candidate = Loyalty.NewPaymentCode(_options.CodePrefix);
            if (!await db.PaymentRequests.AnyAsync(r => r.Code == candidate, ct))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Không thể tạo mã thanh toán duy nhất.");
    }

    private string BuildQrUrl(decimal amount, string code)
    {
        var query = new Dictionary<string, string?>
        {
            ["acc"] = _options.AccountNumber,
            ["bank"] = _options.Bank,
            ["amount"] = amount.ToString("0", CultureInfo.InvariantCulture),
            ["des"] = code,
        };

        return QueryHelpers.AddQueryString(_options.QrImageBaseUrl, query);
    }

    private static decimal ParseAmount(string? value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return amount;
        }

        return 0m;
    }

    private static long ParseLongAmount(string? value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return (long)Math.Round(amount, MidpointRounding.AwayFromZero);
        }

        return 0L;
    }

    private static DateTimeOffset ParseOccurredAt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTimeOffset.UtcNow;
        }

        if (DateTimeOffset.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var fallback)
            ? fallback
            : DateTimeOffset.UtcNow;
    }
}

public sealed record CreateCheckInPaymentRequest(
    Guid SeatId,
    Guid PricePlanId,
    Guid? CustomerId);

public sealed record CreatePaymentRequestResponse(string Code, decimal Amount, string QrUrl, DateTimeOffset ExpiresAt);

public sealed record PaymentRequestStatusResponse(
    string Code,
    decimal Amount,
    PaymentRequestStatus Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? PaidAt);

public sealed record SePayWebhookPayload(
    string Id,
    string Gateway,
    string? TransactionDate,
    string AccountNumber,
    string? SubAccount,
    string? Code,
    string? Content,
    string TransferType,
    string? Description,
    string? TransferAmount,
    string? Accumulated,
    string? ReferenceCode);
