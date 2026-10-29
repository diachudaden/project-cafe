using ChonRieng.Domain.Auth;

namespace ChonRieng.UnitTests.Auth;

public class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("0901234567", "0901234567")]
    [InlineData("+84901234567", "0901234567")]
    [InlineData("84901234567", "0901234567")]
    [InlineData("090 123 4567", "0901234567")]
    public void Normalize_VietnameseNumbers_ToLocalZeroPrefix(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumberNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-phone")]
    public void Normalize_InvalidInput_ReturnsNull(string? input)
    {
        Assert.Null(PhoneNumberNormalizer.Normalize(input));
    }
}
