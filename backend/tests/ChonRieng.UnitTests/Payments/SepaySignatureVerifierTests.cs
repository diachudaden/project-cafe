using System.Text;
using ChonRieng.Application.Payments;
using ChonRieng.Application.Payments.Sepay;
using Microsoft.Extensions.Options;
using Xunit;

namespace ChonRieng.UnitTests.Payments;

public class SepaySignatureVerifierTests
{
    private const string Secret = "test-secret";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_760_000_000);
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"id":1,"transferAmount":25000,"content":"CR1 Nguyễn"}""");

    private static SepaySignatureVerifier Create(string secret = Secret) =>
        new(Options.Create(new PaymentOptions { WebhookSecret = secret, TimestampToleranceSeconds = 300 }), new FixedTime(Now));

    private static string Sign(string ts, byte[] body, string secret = Secret) =>
        "sha256=" + SepaySignatureVerifier.Compute(secret, ts, body);

    [Fact]
    public void Valid_signature_passes()
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        Assert.Equal(SignatureCheck.Valid, Create().Verify(Sign(ts, Body), ts, Body));
    }

    [Fact]
    public void Uppercase_hex_is_accepted()
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        var sig = "sha256=" + SepaySignatureVerifier.Compute(Secret, ts, Body).ToUpperInvariant();
        Assert.Equal(SignatureCheck.Valid, Create().Verify(sig, ts, Body));
    }

    [Fact]
    public void Tampered_body_fails()
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        var sig = Sign(ts, Body);
        var tampered = Encoding.UTF8.GetBytes("""{"id":1,"transferAmount":99000000,"content":"CR1 Nguyễn"}""");
        Assert.Equal(SignatureCheck.Mismatch, Create().Verify(sig, ts, tampered));
    }

    [Fact]
    public void Wrong_secret_fails()
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        Assert.Equal(SignatureCheck.Mismatch, Create().Verify(Sign(ts, Body, "other"), ts, Body));
    }

    [Fact]
    public void Old_timestamp_is_rejected()
    {
        var ts = (Now.ToUnixTimeSeconds() - 301).ToString();
        Assert.Equal(SignatureCheck.Expired, Create().Verify(Sign(ts, Body), ts, Body));
    }

    [Fact]
    public void Timestamp_signed_with_different_value_fails()
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        var otherTs = (Now.ToUnixTimeSeconds() + 1).ToString();
        Assert.Equal(SignatureCheck.Mismatch, Create().Verify(Sign(ts, Body), otherTs, Body));
    }

    [Theory]
    [InlineData(null, "1")]
    [InlineData("sha256=abc", null)]
    [InlineData("", "")]
    public void Missing_headers_fail(string? sig, string? ts) =>
        Assert.Equal(SignatureCheck.MissingHeaders, Create().Verify(sig, ts, Body));

    [Fact]
    public void Non_numeric_timestamp_fails() =>
        Assert.Equal(SignatureCheck.BadTimestamp, Create().Verify("sha256=abc", "abc", Body));

    [Fact]
    public void Empty_secret_is_never_valid()
    {
        var ts = Now.ToUnixTimeSeconds().ToString();
        Assert.Equal(SignatureCheck.NotConfigured, Create(secret: "").Verify(Sign(ts, Body, ""), ts, Body));
    }

    /// <summary>
    /// Vector cố định, tính bằng Node (crypto.createHmac) và Python (hmac) cho ra cùng kết quả.
    /// Khớp với tools/payments/send-fake-webhook.mjs.
    /// </summary>
    [Fact]
    public void Known_vector_matches_node_and_python()
    {
        var body = Encoding.UTF8.GetBytes("""{"id":1}""");
        Assert.Equal(
            "3dd1b9aef568d75f6790a84bd2e5dfa1f44409eef3cbdbd3f10b837376100c11",
            SepaySignatureVerifier.Compute("secret", "1700000000", body));
    }
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
