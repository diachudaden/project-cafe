using System.ComponentModel.DataAnnotations;

namespace ChonRieng.Api.Services.Email;

/// <summary>SMTP settings, section <c>Email</c> (env: <c>Email__Host</c>, ...). Defaults target a local Mailpit.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 1025;

    /// <summary>SMTP login; empty = no authentication (Mailpit).</summary>
    public string? Username { get; set; }

    /// <summary>SMTP password (Gmail: a 16-character App Password). Never logged.</summary>
    public string? Password { get; set; }

    [Required, EmailAddress]
    public string FromAddress { get; set; } = "no-reply@chonrieng.local";

    [Required]
    public string FromName { get; set; } = "Chọn Riêng Cafe";

    /// <summary>true = STARTTLS (port 587); false = plain SMTP (Mailpit). Port 465 always uses implicit TLS.</summary>
    public bool UseStartTls { get; set; }
}

/// <summary>Section <c>App</c>: where the web apps are served, used to build links in emails.</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Absolute http/https origin without a trailing slash, e.g. <c>https://cafe.example</c>.</summary>
    [Required]
    public string PublicBaseUrl { get; set; } = "http://localhost";

    public static bool IsValidBaseUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}

/// <summary>Section <c>PasswordReset</c>.</summary>
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>Lifetime of a reset link (Identity <c>DataProtectionTokenProviderOptions.TokenLifespan</c>).</summary>
    [Range(5, 1440)]
    public int TokenMinutes { get; set; } = 15;
}
