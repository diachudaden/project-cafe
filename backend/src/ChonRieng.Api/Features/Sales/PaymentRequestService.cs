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
    TimeProvider clock)
{
    private readonly PaymentOptions _options = options.Value;

    public async Task<Result<CreatePaymentRequestResponse>> CreateAsync(CurrentUser user, CreatePaymentRequestRequest request, CancellationToken ct)
    {
        var seat = await db.Seats.Include(s => s.Zone).SingleOrDefaultAsync(s => s.Id == request.SeatId, ct);
        if (seat is null || !user.CanAccessBranch(seat.Zone.BranchId))
        {
            return ServiceError.NotFound("Không tìm thấy chỗ ngồi hoặc không thuộc chi nhánh được phép.");
        }

        var branchId = seat.Zone.BranchId;
        var pricePlan = await db.PricePlans.SingleOrDefaultAsync(
            p => p.Id == request.PricePlanId && (p.BranchId == null || p.BranchId == branchId), ct);
        if (pricePlan is null)
        {
            return ServiceError.Invalid("PricePlanId", "Bảng giá không áp dụng cho chi nhánh này.");
        }

        if (request.Amount <= 0)
        {
            request = request with { Amount = request.Kind == PaymentRequestKind.Extension ? pricePlan.ExtendPrice : pricePlan.Price };
        }

        var shiftId = request.ShiftId ?? await ResolveCurrentShiftAsync(branchId, ct);
        if (shiftId is null)
        {
            return ServiceError.Conflict("ShiftNotFound", "Chưa có ca đang mở cho chi nhánh này.");
        }

        if (request.CustomerId is Guid customerId && !await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            return ServiceError.Invalid("CustomerId", "Không tìm thấy khách hàng.");
        }

        var now = clock.GetUtcNow();
        var code = await GenerateUniqueCodeAsync(ct);
        var expiresAt = now.AddMinutes(_options.HoldMinutes);

        var paymentRequest = new PaymentRequest
        {
            Code = code,
            Kind = request.Kind,
            BranchId = branchId,
            SeatId = request.SeatId,
            PricePlanId = request.PricePlanId,
            CustomerId = request.CustomerId,
            SessionId = request.SessionId,
            ShiftId = shiftId,
            Amount = request.Amount,
            Status = PaymentRequestStatus.Pending,
            ExpiresAt = expiresAt,
            CreatedByUserId = user.UserId,
            CreatedAt = now,
        };

        db.PaymentRequests.Add(paymentRequest);
        await db.SaveChangesAsync(ct);

        var qrUrl = BuildQrUrl(paymentRequest.Amount, paymentRequest.Code);
        return new CreatePaymentRequestResponse(paymentRequest.Id, paymentRequest.Code, paymentRequest.Amount, qrUrl, paymentRequest.ExpiresAt);
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
            var candidate = _options.CodePrefix + RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 8);
            if (!await db.PaymentRequests.AnyAsync(r => r.Code == candidate, ct))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Không thể tạo mã thanh toán duy nhất.");
    }

    private async Task<Guid?> ResolveCurrentShiftAsync(Guid branchId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var shift = await db.Shifts
            .Where(s => s.BranchId == branchId && s.StartAt <= now && s.EndAt >= now)
            .OrderByDescending(s => s.StartAt)
            .FirstOrDefaultAsync(ct);

        return shift?.Id;
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

public sealed record CreatePaymentRequestRequest(
    Guid SeatId,
    Guid PricePlanId,
    PaymentRequestKind Kind,
    Guid? CustomerId,
    Guid? SessionId,
    Guid? ShiftId,
    decimal Amount);

public sealed record CreatePaymentRequestResponse(Guid PaymentRequestId, string Code, decimal Amount, string QrUrl, DateTimeOffset ExpiresAt);

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
