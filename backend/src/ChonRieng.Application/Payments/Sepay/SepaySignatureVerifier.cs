using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ChonRieng.Application.Payments.Sepay;

public enum SignatureCheck
{
    Valid,
    MissingHeaders,
    BadTimestamp,
    Expired,
    Mismatch,
    NotConfigured,
}

/// <summary>
/// Xác thực webhook SePay kiểu HMAC-SHA256.
/// SePay ký chuỗi <c>"{timestamp}.{raw_body}"</c> và gửi <c>X-SePay-Signature: sha256={hex}</c>
/// kèm <c>X-SePay-Timestamp: {unix seconds}</c>.
/// </summary>
public sealed class SepaySignatureVerifier(IOptions<PaymentOptions> options, TimeProvider clock)
{
    public const string SignatureHeader = "X-SePay-Signature";
    public const string TimestampHeader = "X-SePay-Timestamp";
    private const string Scheme = "sha256=";

    /// <param name="rawBody">Bytes gốc của body. Không được parse rồi serialize lại.</param>
    public SignatureCheck Verify(string? signatureHeader, string? timestampHeader, ReadOnlySpan<byte> rawBody)
    {
        var secret = options.Value.WebhookSecret;
        if (string.IsNullOrEmpty(secret)) return SignatureCheck.NotConfigured;

        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(timestampHeader))
            return SignatureCheck.MissingHeaders;

        var timestamp = timestampHeader.Trim();
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var ts))
            return SignatureCheck.BadTimestamp;

        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(now - ts) > options.Value.TimestampToleranceSeconds)
            return SignatureCheck.Expired;

        var expected = Encoding.ASCII.GetBytes(Scheme + Compute(secret, timestamp, rawBody));
        var actual = Encoding.ASCII.GetBytes(signatureHeader.Trim().ToLowerInvariant());

        return CryptographicOperations.FixedTimeEquals(expected, actual)
            ? SignatureCheck.Valid
            : SignatureCheck.Mismatch;
    }

    /// <summary>Hex thường của HMAC-SHA256(secret, "{timestamp}.{body}"). Dùng chung cho test và tool gửi webhook giả.</summary>
    public static string Compute(string secret, string timestamp, ReadOnlySpan<byte> rawBody)
    {
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret));
        hmac.AppendData(Encoding.ASCII.GetBytes(timestamp + "."));
        hmac.AppendData(rawBody);
        return Convert.ToHexStringLower(hmac.GetHashAndReset());
    }
}
