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
    ILogger<SePayWebhookService> logger)
{
    private const int MaximumBodyBytes = 256 * 1024;
    private readonly PaymentOptions _options = options.Value;

    private static readonly JsonSerializerOptions PayloadJson = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new StringOrNumberJsonConverter() },
    };

    public async Task<IResult> HandleAsync(HttpRequest request, CancellationToken ct)
    {
        var body = await ReadBodyAsync(request, ct);
        if (body is null)
        {
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var timestamp = request.Headers["X-SePay-Timestamp"].FirstOrDefault();
        var signature = request.Headers["X-SePay-Signature"].FirstOrDefault();
        if (!IsValidSignature(timestamp, signature, body))
        {
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status401Unauthorized);
        }

        SePayWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SePayWebhookPayload>(body, PayloadJson);
        }
        catch (JsonException)
        {
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!IsValidPayload(payload))
        {
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status400BadRequest);
        }

        return await ProcessAsync(payload!, ct);
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
                return Results.Ok(new { success = true });
            }

            throw;
        }

        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            providerTransaction.Note = "Giao dịch chuyển ra được bỏ qua.";
        }
        else if (!string.Equals(payload.AccountNumber, _options.AccountNumber, StringComparison.OrdinalIgnoreCase))
        {
            providerTransaction.Note = "Tài khoản nhận không khớp cấu hình.";
        }
        else
        {
            var paymentRequest = await FindPaymentRequestAsync(payload, ct);
            if (paymentRequest is null)
            {
                providerTransaction.Note = "Không tìm thấy mã thanh toán hoặc mã khớp không duy nhất.";
            }
            else
            {
                providerTransaction.PaymentRequestId = paymentRequest.Id;
                await MatchPaymentAsync(payload, paymentRequest, providerTransaction, ct);
            }
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return Results.Ok(new { success = true });
    }

    private async Task<PaymentRequest?> FindPaymentRequestAsync(SePayWebhookPayload payload, CancellationToken ct)
    {
        string? code = null;
        if (!string.IsNullOrWhiteSpace(payload.Code))
        {
            code = payload.Code.Trim().ToUpperInvariant();
        }
        else if (!string.IsNullOrWhiteSpace(payload.Content))
        {
            var matches = PaymentCodeRegex().Matches(payload.Content)
                .Select(match => match.Value.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (matches.Length != 1)
            {
                return null;
            }

            code = matches[0];
        }

        return code is null
            ? null
            : await db.PaymentRequests.SingleOrDefaultAsync(r => r.Code == code, ct);
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
            return;
        }

        if (paymentRequest.Status != PaymentRequestStatus.Pending || paymentRequest.ExpiresAt <= now)
        {
            paymentRequest.Status = PaymentRequestStatus.NeedsAttention;
            transaction.Note = "Giao dịch đến sau khi yêu cầu hết hạn hoặc bị hủy.";
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
            return;
        }

        var outcome = await sessions.SettlePaymentRequestAsync(paymentRequest, ct);
        if (!outcome.Settled)
        {
            paymentRequest.Status = PaymentRequestStatus.NeedsAttention;
            transaction.MatchStatus = PaymentMatchStatus.Unmatched;
            transaction.Note = outcome.Reason;
            logger.LogWarning("A verified SePay payment requires attention for request {PaymentRequestId}.", paymentRequest.Id);
            return;
        }

        paymentRequest.Status = PaymentRequestStatus.Paid;
        paymentRequest.PaidAt = now;
        transaction.MatchStatus = PaymentMatchStatus.Matched;
        transaction.Note = "Thanh toán khớp và đã hoàn tất.";
    }

    private static bool IsValidPayload(SePayWebhookPayload? payload) =>
        payload is not null
        && !string.IsNullOrWhiteSpace(payload.Id)
        && payload.Id.Length <= 128
        && !string.IsNullOrWhiteSpace(payload.AccountNumber)
        && payload.AccountNumber.Length <= 32
        && (string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase)
            || string.Equals(payload.TransferType, "out", StringComparison.OrdinalIgnoreCase))
        && payload.TransferAmount > 0
        && payload.TransferAmount <= long.MaxValue
        && decimal.Truncate(payload.TransferAmount) == payload.TransferAmount
        && !string.IsNullOrWhiteSpace(payload.TransactionDate)
        && DateTime.TryParseExact(payload.TransactionDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);

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
