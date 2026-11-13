using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Services;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace ChonRieng.Api.Features.Sales;

public sealed class SePayWebhookService(
    AppDbContext db,
    IOptions<PaymentOptions> options,
    TimeProvider clock,
    SessionService sessions,
    PaymentLogService paymentLog,
    ILogger<SePayWebhookService> logger)
{
    private const int MaximumBodyBytes = 256 * 1024;
    private readonly PaymentOptions _options = options.Value;

    /// <summary>Trace rows of the current callback; written after commit so "Settled" is never logged for a rolled-back payment.</summary>
    private readonly List<PaymentLogEntry> _pending = [];

    private static readonly JsonSerializerOptions PayloadJson = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new StringOrNumberJsonConverter() },
    };

    public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken ct)
    {
        var body = await ReadBodyAsync(request, ct);
        var timestamp = request.Headers["X-SePay-Timestamp"].FirstOrDefault();
        var signature = request.Headers["X-SePay-Signature"].FirstOrDefault();
        await paymentLog.WriteAsync(new PaymentLogEntry(PaymentLogStage.WebhookReceived, "SePay gọi webhook.")
        {
            Detail = new
            {
                bodyBytes = body?.Length ?? request.ContentLength,
                hasTimestamp = !string.IsNullOrWhiteSpace(timestamp),
                hasSignature = !string.IsNullOrWhiteSpace(signature),
                hasAuthorization = request.Headers.Authorization.Count > 0,
                contentType = request.ContentType,
                remoteIp = request.HttpContext.Connection.RemoteIpAddress?.ToString(),
                forwardedFor = request.Headers["X-Forwarded-For"].FirstOrDefault(),
                userAgent = request.Headers.UserAgent.FirstOrDefault(),
            },
        });

        if (body is null)
        {
            await RejectAsync("Nội dung webhook quá lớn (> 256 KB).", new { reason = "too_large", request.ContentLength });
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var signatureError = CheckSignature(timestamp, signature, body);
        if (signatureError is not null)
        {
            await RejectAsync(signatureError.Message, signatureError.Detail);
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status401Unauthorized);
        }

        SePayWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SePayWebhookPayload>(body, PayloadJson);
        }
        catch (JsonException exception)
        {
            await RejectAsync("JSON webhook không đọc được.", new { reason = "bad_json", error = exception.Message, exception.Path });
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status400BadRequest);
        }

        var payloadError = PayloadError(payload);
        if (payloadError is not null)
        {
            await RejectAsync($"Dữ liệu webhook không hợp lệ: {payloadError}", new
            {
                reason = "bad_payload",
                field = payloadError,
                id = payload?.Id,
                transferType = payload?.TransferType,
                amount = payload?.TransferAmount,
                transactionDate = payload?.TransactionDate,
            }, payload?.Id);
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            return await ProcessAsync(payload!, ct);
        }
        catch (Exception exception)
        {
            // Rows collected so far show how far processing got; the database work itself was rolled back.
            await FlushAsync();
            await paymentLog.WriteAsync(new PaymentLogEntry(PaymentLogStage.Exception,
                "Lỗi khi xử lý webhook; giao dịch đã hoàn tác, SePay sẽ gửi lại.")
            {
                ProviderTxId = payload!.Id,
                Amount = payload.TransferAmount,
                PaymentCode = payload.Code,
                Detail = new { type = exception.GetType().Name, exception.Message, inner = exception.InnerException?.Message },
            });
            throw;
        }
    }

    private Task RejectAsync(string message, object detail, string? providerTxId = null) =>
        paymentLog.WriteAsync(new PaymentLogEntry(PaymentLogStage.WebhookRejected, message) { Detail = detail, ProviderTxId = providerTxId });

    private async Task FlushAsync()
    {
        foreach (var entry in _pending)
        {
            await paymentLog.WriteAsync(entry);
        }

        _pending.Clear();
    }

    private sealed record SignatureError(string Message, object Detail);

    private SignatureError? CheckSignature(string? timestamp, string? signature, byte[] rawBody)
    {
        if (string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(signature))
        {
            return new("Thiếu header X-SePay-Timestamp hoặc X-SePay-Signature.", new
            {
                reason = "missing_headers",
                hasTimestamp = !string.IsNullOrWhiteSpace(timestamp),
                hasSignature = !string.IsNullOrWhiteSpace(signature),
            });
        }

        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unixTimestamp))
        {
            return new("X-SePay-Timestamp không phải số giây Unix.", new { reason = "bad_timestamp", timestampLength = timestamp.Length });
        }

        var skew = clock.GetUtcNow().ToUnixTimeSeconds() - (decimal)unixTimestamp;
        if (Math.Abs(skew) > _options.TimestampToleranceSeconds)
        {
            return new($"Timestamp lệch {skew:0} giây so với máy chủ (cho phép ±{_options.TimestampToleranceSeconds}).", new
            {
                reason = "stale_timestamp",
                skewSeconds = skew,
                toleranceSeconds = _options.TimestampToleranceSeconds,
            });
        }

        return IsValidSignature(timestamp, signature, rawBody)
            ? null
            : new("Chữ ký HMAC không khớp (sai WebhookSecret hoặc nội dung bị thay đổi).", new
            {
                reason = "bad_signature",
                startsWithSha256 = signature.Trim().StartsWith("sha256=", StringComparison.OrdinalIgnoreCase),
                signatureLength = signature.Trim().Length,
            });
    }

    private bool IsValidSignature(string? timestamp, string? signature, byte[] rawBody)
    {
        if (string.IsNullOrWhiteSpace(timestamp)
            || string.IsNullOrWhiteSpace(signature)
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unixTimestamp))
        {
            return false;
        }

        var currentTimestamp = clock.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs((decimal)currentTimestamp - unixTimestamp) > _options.TimestampToleranceSeconds)
        {
            return false;
        }

        var suppliedHex = signature.Trim();
        if (!suppliedHex.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] suppliedBytes;
        try
        {
            suppliedBytes = Convert.FromHexString(suppliedHex["sha256=".Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (suppliedBytes.Length != 32)
        {
            return false;
        }

        var timestampBytes = Encoding.UTF8.GetBytes(timestamp);
        var signedBytes = new byte[timestampBytes.Length + 1 + rawBody.Length];
        timestampBytes.CopyTo(signedBytes, 0);
        signedBytes[timestampBytes.Length] = (byte)'.';
        rawBody.CopyTo(signedBytes, timestampBytes.Length + 1);
        var expectedBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.WebhookSecret), signedBytes);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private async Task<IResult> ProcessAsync(SePayWebhookPayload payload, CancellationToken ct)
    {
        if (await db.PaymentTransactions.AsNoTracking().AnyAsync(t => t.ProviderTxId == payload.Id, ct))
        {
            await LogDuplicateAsync(payload);
            return Results.Ok(new { success = true });
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var providerTransaction = new PaymentTransaction
        {
            Provider = "SePay",
            ProviderTxId = payload.Id,
            AccountNumber = payload.AccountNumber,
            Amount = decimal.ToInt64(payload.TransferAmount),
            Code = Limit(payload.Code, 32),
            Content = Limit(payload.Content, 256),
            ReferenceCode = Limit(payload.ReferenceCode, 64),
            OccurredAt = ParseOccurredAt(payload.TransactionDate!),
            ReceivedAt = clock.GetUtcNow(),
            MatchStatus = PaymentMatchStatus.Unmatched,
            Note = "Giao dịch đã nhận, chưa đối soát.",
        };
        db.PaymentTransactions.Add(providerTransaction);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsSqlServerDuplicateKey(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }

            db.ChangeTracker.Clear();
            if (await db.PaymentTransactions.AsNoTracking().AnyAsync(t => t.ProviderTxId == payload.Id, ct))
            {
                await LogDuplicateAsync(payload);
                return Results.Ok(new { success = true });
            }

            throw;
        }

        _pending.Add(new PaymentLogEntry(PaymentLogStage.TransactionRecorded, "Đã lưu giao dịch ngân hàng.")
        {
            Transaction = providerTransaction,
            PaymentCode = providerTransaction.Code,
            Detail = new
            {
                gateway = payload.Gateway,
                transferType = payload.TransferType,
                providerTransaction.OccurredAt,
                providerTransaction.ReferenceCode,
                codeField = providerTransaction.Code,
                hasContent = !string.IsNullOrWhiteSpace(payload.Content),
                receivedAfterSeconds = (providerTransaction.ReceivedAt - providerTransaction.OccurredAt).TotalSeconds,
            },
        });

        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            providerTransaction.Note = "Giao dịch chuyển ra được bỏ qua.";
            _pending.Add(new PaymentLogEntry(PaymentLogStage.TransactionIgnored, providerTransaction.Note) { Transaction = providerTransaction });
        }
        else if (!string.Equals(payload.AccountNumber, _options.AccountNumber, StringComparison.OrdinalIgnoreCase))
        {
            providerTransaction.Note = "Tài khoản nhận không khớp cấu hình.";
            _pending.Add(new PaymentLogEntry(PaymentLogStage.TransactionIgnored, providerTransaction.Note)
            {
                Transaction = providerTransaction,
                Detail = new { accountLast4 = Last4(payload.AccountNumber), configuredLast4 = Last4(_options.AccountNumber) },
            });
        }
        else
        {
            var (paymentRequest, matchedBy, candidates) = await FindPaymentRequestAsync(payload, ct);
            if (paymentRequest is null)
            {
                providerTransaction.Note = "Không tìm thấy mã thanh toán hoặc mã khớp không duy nhất.";
                _pending.Add(new PaymentLogEntry(PaymentLogStage.TransactionUnmatched, providerTransaction.Note)
                {
                    Transaction = providerTransaction,
                    PaymentCode = candidates.Length == 1 ? candidates[0] : providerTransaction.Code,
                    Detail = new { codeField = payload.Code, codesInContent = candidates, prefix = _options.CodePrefix },
                });
            }
            else
            {
                providerTransaction.PaymentRequestId = paymentRequest.Id;
                _pending.Add(new PaymentLogEntry(PaymentLogStage.CodeMatched, $"Khớp mã {paymentRequest.Code} (theo {matchedBy}).")
                {
                    Request = paymentRequest,
                    Transaction = providerTransaction,
                    Amount = payload.TransferAmount,
                    Detail = new { matchedBy, requestStatus = paymentRequest.Status.ToString(), paymentRequest.ExpiresAt, expected = paymentRequest.Amount, paid = payload.TransferAmount },
                });
                await MatchPaymentAsync(payload, paymentRequest, providerTransaction, ct);
            }
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        await FlushAsync();
        return Results.Ok(new { success = true });
    }

    /// <summary>
    /// Finds the request by SePay's <c>code</c> field first, then by a single payment code found in the transfer content.
    /// The content fallback matters when SePay's own code pattern differs from ours and it sends a wrong/partial code.
    /// </summary>
    private async Task<(PaymentRequest? Request, string MatchedBy, string[] Candidates)> FindPaymentRequestAsync(
        SePayWebhookPayload payload,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(payload.Code))
        {
            var code = payload.Code.Trim().ToUpperInvariant();
            var byCode = await db.PaymentRequests.SingleOrDefaultAsync(r => r.Code == code, ct);
            if (byCode is not null)
            {
                return (byCode, "code", [code]);
            }
        }

        var matches = string.IsNullOrWhiteSpace(payload.Content)
            ? []
            : PaymentCodeRegex().Matches(payload.Content)
                .Select(match => match.Value.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        if (matches.Length != 1)
        {
            return (null, "", matches);
        }

        var byContent = await db.PaymentRequests.SingleOrDefaultAsync(r => r.Code == matches[0], ct);
        return (byContent, "content", matches);
    }

    private async Task MatchPaymentAsync(
        SePayWebhookPayload payload,
        PaymentRequest paymentRequest,
        PaymentTransaction transaction,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (paymentRequest.Status == PaymentRequestStatus.Paid)
        {
            transaction.MatchStatus = PaymentMatchStatus.Duplicate;
            transaction.Note = "Yêu cầu đã được thanh toán trước đó.";
            _pending.Add(new PaymentLogEntry(PaymentLogStage.AlreadyPaid, "Nhận thêm tiền cho yêu cầu đã thanh toán; cần hoàn tiền thủ công.")
            {
                Request = paymentRequest, Transaction = transaction, Amount = payload.TransferAmount,
            });
            return;
        }

        if (paymentRequest.Status != PaymentRequestStatus.Pending || paymentRequest.ExpiresAt <= now)
        {
            var previous = paymentRequest.Status;
            paymentRequest.Status = PaymentRequestStatus.NeedsAttention;
            transaction.Note = "Giao dịch đến sau khi yêu cầu hết hạn hoặc bị hủy.";
            _pending.Add(new PaymentLogEntry(PaymentLogStage.LatePayment, transaction.Note)
            {
                Request = paymentRequest,
                Transaction = transaction,
                Amount = payload.TransferAmount,
                Detail = new { previousStatus = previous.ToString(), paymentRequest.ExpiresAt, lateBySeconds = (now - paymentRequest.ExpiresAt).TotalSeconds },
            });
            return;
        }

        if (payload.TransferAmount != paymentRequest.Amount)
        {
            paymentRequest.Status = PaymentRequestStatus.NeedsAttention;
            transaction.MatchStatus = payload.TransferAmount < paymentRequest.Amount
                ? PaymentMatchStatus.Underpaid
                : PaymentMatchStatus.Overpaid;
            transaction.Note = payload.TransferAmount < paymentRequest.Amount
                ? $"Thanh toán thiếu {paymentRequest.Amount - payload.TransferAmount:0} VND."
                : $"Thanh toán thừa {payload.TransferAmount - paymentRequest.Amount:0} VND.";
            _pending.Add(new PaymentLogEntry(PaymentLogStage.AmountMismatch, transaction.Note)
            {
                Request = paymentRequest,
                Transaction = transaction,
                Amount = payload.TransferAmount,
                Detail = new { expected = paymentRequest.Amount, paid = payload.TransferAmount },
            });
            return;
        }

        var outcome = await sessions.SettlePaymentRequestAsync(paymentRequest, ct);
        if (!outcome.Settled)
        {
            paymentRequest.Status = PaymentRequestStatus.NeedsAttention;
            transaction.MatchStatus = PaymentMatchStatus.Unmatched;
            transaction.Note = outcome.Reason;
            logger.LogWarning("A verified SePay payment requires attention for request {PaymentRequestId}.", paymentRequest.Id);
            _pending.Add(new PaymentLogEntry(PaymentLogStage.SettlementFailed, $"Đã nhận tiền nhưng không tạo được bill: {outcome.Reason}")
            {
                Request = paymentRequest, Transaction = transaction,
            });
            return;
        }

        paymentRequest.Status = PaymentRequestStatus.Paid;
        paymentRequest.PaidAt = now;
        transaction.MatchStatus = PaymentMatchStatus.Matched;
        transaction.Note = "Thanh toán khớp và đã hoàn tất.";
        _pending.Add(new PaymentLogEntry(PaymentLogStage.Settled,
            paymentRequest.Kind == PaymentRequestKind.CheckIn
                ? $"Đã tạo bill {outcome.BillCode} cho chỗ {outcome.SeatCode}."
                : $"Đã gia hạn lượt ngồi, bill {outcome.BillCode}.")
        {
            Request = paymentRequest,
            Transaction = transaction,
            BillId = outcome.BillId,
            BillCode = outcome.BillCode,
            Detail = new { outcome.SeatCode, outcome.SessionId, seatChanged = paymentRequest.Kind == PaymentRequestKind.CheckIn && outcome.SeatCode is not null },
        });
    }

    /// <summary>Returns the first invalid field, or null when the payload is usable.</summary>
    private static string? PayloadError(SePayWebhookPayload? payload) => payload switch
    {
        null => "body",
        { Id: null or "" } or { Id.Length: > 128 } => "id",
        { AccountNumber: null or "" } or { AccountNumber.Length: > 32 } => "accountNumber",
        _ when !string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(payload.TransferType, "out", StringComparison.OrdinalIgnoreCase) => "transferType",
        _ when payload.TransferAmount <= 0 || payload.TransferAmount > long.MaxValue
            || decimal.Truncate(payload.TransferAmount) != payload.TransferAmount => "transferAmount",
        _ when string.IsNullOrWhiteSpace(payload.TransactionDate)
            || !DateTime.TryParseExact(payload.TransactionDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _) => "transactionDate",
        _ => null,
    };

    private Task LogDuplicateAsync(SePayWebhookPayload payload) =>
        paymentLog.WriteAsync(new PaymentLogEntry(PaymentLogStage.WebhookDuplicate, "SePay gửi lại giao dịch đã nhận; bỏ qua.")
        {
            ProviderTxId = payload.Id,
            Amount = payload.TransferAmount,
            PaymentCode = payload.Code,
        });

    private static string Last4(string value) => value.Length <= 4 ? value : value[^4..];

    private static DateTimeOffset ParseOccurredAt(string value)
    {
        var local = DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None);
        return new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), VietnamTime.Offset).ToUniversalTime();
    }

    private static string? Limit(string? value, int maxLength) =>
        value is { Length: > 0 } ? value[..Math.Min(value.Length, maxLength)] : null;

    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        await using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await request.Body.ReadAsync(chunk, ct);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumBodyBytes)
            {
                request.Body.Position = 0;
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }

        request.Body.Position = 0;
        return buffer.ToArray();
    }

    private static bool IsSqlServerDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };

    private Regex PaymentCodeRegex() =>
        new($@"(?<![A-Z0-9]){Regex.Escape(_options.CodePrefix)}[A-HJ-NP-Z2-9]{{8}}(?![A-Z0-9])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}

public sealed record SePayWebhookPayload(
    string Id,
    string? Gateway,
    string? TransactionDate,
    string AccountNumber,
    string? SubAccount,
    string? Code,
    string? Content,
    string TransferType,
    string? Description,
    decimal TransferAmount,
    decimal? Accumulated,
    string? ReferenceCode);

public sealed class StringOrNumberJsonConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => Encoding.UTF8.GetString(reader.ValueSpan),
            JsonTokenType.Null => null,
            _ => throw new JsonException("Expected a string or number."),
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
