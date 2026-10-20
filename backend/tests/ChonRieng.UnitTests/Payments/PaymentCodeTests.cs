using ChonRieng.Domain.Payments;
using Xunit;

namespace ChonRieng.UnitTests.Payments;

public class PaymentCodeTests
{
    [Fact]
    public void For_builds_uppercase_code() => Assert.Equal("CR1024", PaymentCode.For("cr", 1024));

    [Theory]
    [InlineData("CR1024", 1024)]
    [InlineData("cr1024", 1024)]
    [InlineData(" CR7 ", 7)]
    public void TryParse_accepts_valid_codes(string code, int expected)
    {
        Assert.True(PaymentCode.TryParse(code, "CR", out var id));
        Assert.Equal(expected, id);
    }

    [Fact]
    public void TryParse_rejects_null() => Assert.False(PaymentCode.TryParse(null, "CR", out _));

    [Theory]
    [InlineData("")]
    [InlineData("CR")]
    [InlineData("CRabc")]
    [InlineData("CR0")]
    [InlineData("DH1024")]
    [InlineData("CR12a")]
    [InlineData("CR1234567890")]
    public void TryParse_rejects_invalid_codes(string code) =>
        Assert.False(PaymentCode.TryParse(code, "CR", out _));

    [Theory]
    [InlineData("CR1024 chuyen tien", 1024)]
    [InlineData("NGUYEN VAN A chuyen khoan cr55", 55)]
    [InlineData("SEVQR CR9 thanh toan", 9)]
    public void TryFindInContent_finds_single_code(string content, int expected)
    {
        Assert.True(PaymentCode.TryFindInContent(content, "CR", out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData("chuyen tien")]
    [InlineData("CR1 CR2")]          // 2 mã khác nhau → không đoán
    [InlineData("ABCR1024")]         // dính chữ phía trước
    [InlineData("SCR1024")]
    public void TryFindInContent_rejects_ambiguous_or_missing(string content) =>
        Assert.False(PaymentCode.TryFindInContent(content, "CR", out _));

    [Fact]
    public void TryFindInContent_accepts_same_code_repeated() =>
        Assert.True(PaymentCode.TryFindInContent("CR8 CR8", "CR", out var id) && id == 8);
}
