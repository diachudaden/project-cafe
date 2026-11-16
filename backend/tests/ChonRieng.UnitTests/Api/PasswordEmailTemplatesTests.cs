using ChonRieng.Api.Services.Email;
using Xunit;

namespace ChonRieng.UnitTests.Api;

public sealed class PasswordEmailTemplatesTests
{
    [Theory]
    [InlineData("customer", "https://cafe.example/reset-password?")]
    [InlineData("admin", "https://cafe.example/admin/reset-password?")]
    [InlineData("pos", "https://cafe.example/pos/reset-password?")]
    public void Link_is_built_from_the_configured_origin_and_a_whitelisted_path(string app, string prefix)
    {
        var link = PasswordEmailTemplates.BuildResetLink("https://cafe.example/", app, "a+b@x.vn", "tok_en-");

        Assert.StartsWith(prefix, link);
        Assert.EndsWith("email=a%2Bb%40x.vn&token=tok_en-", link);
    }

    [Fact]
    public void Unknown_app_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PasswordEmailTemplates.BuildResetLink("https://cafe.example", "//evil.example", "a@x.vn", "t"));
    }

    [Fact]
    public void Reset_email_has_button_text_link_lifetime_and_ignore_notice_and_encodes_the_name()
    {
        var link = "https://cafe.example/reset-password?email=a%40x.vn&token=abc";
        var mail = PasswordEmailTemplates.Reset(Guid.NewGuid(), "a@x.vn", "<b>Lan</b>", link, 15);

        Assert.Equal(PasswordEmailTemplates.ResetSubject, mail.Subject);
        Assert.Contains("Đặt lại mật khẩu</a>", mail.HtmlBody);
        Assert.Contains("&lt;b&gt;Lan&lt;/b&gt;", mail.HtmlBody);
        Assert.DoesNotContain("<b>Lan</b>", mail.HtmlBody);
        Assert.Contains("15 phút", mail.HtmlBody);
        Assert.Contains("Nếu bạn không yêu cầu, hãy bỏ qua email này", mail.HtmlBody);
        Assert.Contains("Xin chào <b>Lan</b>", mail.TextBody);
        Assert.Contains(link, mail.TextBody);
        Assert.Contains("15 phút", mail.TextBody);
    }

    [Fact]
    public void Changed_email_shows_vietnam_time_and_contact_admin_notice()
    {
        var mail = PasswordEmailTemplates.Changed(Guid.NewGuid(), "a@x.vn", "", new DateTimeOffset(2026, 11, 16, 3, 5, 0, TimeSpan.Zero));

        Assert.Contains("10:05 ngày 16/11/2026 (giờ Việt Nam)", mail.TextBody);
        Assert.Contains("Xin chào bạn", mail.TextBody);
        Assert.Contains("Nếu không phải bạn, hãy liên hệ quản trị viên ngay", mail.HtmlBody);
    }
}
