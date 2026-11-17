using System.Net;
using ChonRieng.Api.Features.Common;

namespace ChonRieng.Api.Services.Email;

/// <summary>Builds password reset links and the two password emails (HTML with inline styles + plain text).</summary>
public static class PasswordEmailTemplates
{
    public const string ResetPurpose = "password-reset";
    public const string ChangedPurpose = "password-changed";
    public const string ResetSubject = "Đặt lại mật khẩu:";
    public const string ChangedSubject = "Mật khẩu của bạn vừa được thay đổi";

    /// <summary>App wire values accepted by <c>POST /api/auth/forgot-password</c> and the path each one resets on.</summary>
    public static readonly IReadOnlyDictionary<string, string> ResetPaths = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["customer"] = "/reset-password",
        ["admin"] = "/admin/reset-password",
        ["pos"] = "/pos/reset-password",
    };

    /// <summary>
    /// Server-side link: configured origin + whitelisted path. The request host and any client URL are never used,
    /// so a reset email cannot be pointed at another site.
    /// </summary>
    public static string BuildResetLink(string publicBaseUrl, string app, string email, string encodedToken)
    {
        if (!ResetPaths.TryGetValue(app, out var path))
        {
            throw new ArgumentOutOfRangeException(nameof(app), app, "Unknown app.");
        }

        return $"{publicBaseUrl.TrimEnd('/')}{path}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(encodedToken)}";
    }

    public static EmailMessage Reset(Guid userId, string to, string fullName, string link, int tokenMinutes)
    {
        const string subject = ResetSubject;
        var name = DisplayName(fullName);
        var text = $"""
            Xin chào {name},

            Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản Chọn Riêng Cafe của bạn.
            Mở liên kết sau để đặt mật khẩu mới (hiệu lực trong {tokenMinutes} phút):

            {link}

            Nếu bạn không yêu cầu, hãy bỏ qua email này. Mật khẩu hiện tại của bạn vẫn giữ nguyên.

            Chọn Riêng Cafe
            """;
        var html = Layout(
            $"""
            <p style="margin:0 0 16px">Xin chào <strong>{Html(name)}</strong>,</p>
            <p style="margin:0 0 16px">Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản Chọn Riêng Cafe của bạn.</p>
            <p style="margin:24px 0;text-align:center">
              <a href="{Html(link)}" style="display:inline-block;background:#8b5a2b;color:#ffffff;text-decoration:none;font-weight:600;padding:12px 28px;border-radius:8px">Đặt lại mật khẩu</a>
            </p>
            <p style="margin:0 0 8px">Liên kết có hiệu lực trong <strong>{tokenMinutes} phút</strong>. Nếu nút không bấm được, hãy sao chép liên kết sau vào trình duyệt:</p>
            <p style="margin:0 0 16px;word-break:break-all;font-size:13px"><a href="{Html(link)}" style="color:#8b5a2b">{Html(link)}</a></p>
            <p style="margin:0;color:#6b6b6b">Nếu bạn không yêu cầu, hãy bỏ qua email này. Mật khẩu hiện tại của bạn vẫn giữ nguyên.</p>
            """);
        return new EmailMessage(to, subject, html, text, ResetPurpose, userId);
    }

    public static EmailMessage Changed(Guid userId, string to, string fullName, DateTimeOffset changedAt)
    {
        const string subject = ChangedSubject;
        var name = DisplayName(fullName);
        var when = changedAt.ToVietnam().ToString("HH:mm 'ngày' dd/MM/yyyy") + " (giờ Việt Nam)";
        var text = $"""
            Xin chào {name},

            Mật khẩu tài khoản Chọn Riêng Cafe của bạn vừa được thay đổi lúc {when}.
            Các phiên đăng nhập khác đã được đăng xuất.

            Nếu không phải bạn, hãy liên hệ quản trị viên ngay.

            Chọn Riêng Cafe
            """;
        var html = Layout(
            $"""
            <p style="margin:0 0 16px">Xin chào <strong>{Html(name)}</strong>,</p>
            <p style="margin:0 0 16px">Mật khẩu của bạn vừa được thay đổi lúc <strong>{Html(when)}</strong>. Các phiên đăng nhập khác đã được đăng xuất.</p>
            <p style="margin:0;color:#b42318;font-weight:600">Nếu không phải bạn, hãy liên hệ quản trị viên ngay.</p>
            """);
        return new EmailMessage(to, subject, html, text, ChangedPurpose, userId);
    }

    private static string DisplayName(string fullName) => string.IsNullOrWhiteSpace(fullName) ? "bạn" : fullName.Trim();

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string Layout(string body) => $"""
        <!doctype html>
        <html lang="vi">
        <body style="margin:0;padding:24px;background:#f5f1ec;font-family:Arial,Helvetica,sans-serif;color:#1f1f1f;font-size:15px;line-height:1.5">
          <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:12px;padding:28px">
            <div style="font-size:18px;font-weight:700;color:#8b5a2b;margin-bottom:20px">Chọn Riêng Cafe</div>
            {body}
          </div>
        </body>
        </html>
        """;
}
