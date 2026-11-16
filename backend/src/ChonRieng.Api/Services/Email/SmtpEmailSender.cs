using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ChonRieng.Api.Services.Email;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken cancellationToken);
}

/// <summary>Sends one message per SMTP connection with MailKit. Callers go through <see cref="IEmailQueue"/>.</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 30_000 };
        var security = settings.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);
        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password ?? "", cancellationToken);
        }

        await client.SendAsync(new SendRequest(message), cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
