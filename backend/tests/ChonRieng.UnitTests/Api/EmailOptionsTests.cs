using ChonRieng.Api.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ChonRieng.UnitTests.Api;

public sealed class EmailOptionsTests
{
    [Theory]
    [InlineData("https://cafe.example", true)]
    [InlineData("http://localhost", true)]
    [InlineData("http://localhost:5173", true)]
    [InlineData("cafe.example", false)]
    [InlineData("/reset", false)]
    [InlineData("ftp://cafe.example", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://cafe.example?x=1", false)]
    [InlineData("", false)]
    public void Public_base_url_must_be_absolute_http_or_https(string value, bool valid)
    {
        Assert.Equal(valid, AppOptions.IsValidBaseUrl(value));
    }

    [Fact]
    public void Defaults_target_mailpit_and_pass_validation()
    {
        using var provider = Build(new Dictionary<string, string?>());

        var email = provider.GetRequiredService<IOptions<EmailOptions>>().Value;
        Assert.Equal(1025, email.Port);
        Assert.False(email.UseStartTls);
        Assert.Equal(15, provider.GetRequiredService<IOptions<PasswordResetOptions>>().Value.TokenMinutes);
    }

    [Theory]
    [InlineData("Email:Host", "")]
    [InlineData("Email:Port", "0")]
    [InlineData("Email:FromAddress", "")]
    [InlineData("Email:FromAddress", "not-an-email")]
    [InlineData("App:PublicBaseUrl", "cafe.example")]
    [InlineData("PasswordReset:TokenMinutes", "0")]
    public void Invalid_settings_are_rejected(string key, string value)
    {
        using var provider = Build(new Dictionary<string, string?> { [key] = value });

        var section = key[..key.IndexOf(':')];
        Assert.Throws<OptionsValidationException>(() => section switch
        {
            "Email" => (object)provider.GetRequiredService<IOptions<EmailOptions>>().Value,
            "App" => provider.GetRequiredService<IOptions<AppOptions>>().Value,
            _ => provider.GetRequiredService<IOptions<PasswordResetOptions>>().Value,
        });
    }

    private static ServiceProvider Build(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.AddEmailDelivery();
        return services.BuildServiceProvider();
    }
}
