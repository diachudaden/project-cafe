using System.Net.Http.Json;
using System.Text.Json;
using ChonRieng.Api.Services.Email;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;

namespace ChonRieng.IntegrationTests;

/// <summary>Sends a real SMTP message through MailKit to a Mailpit container (plain SMTP, no auth). Requires Docker.</summary>
public sealed class MailpitSmtpTests : IAsyncLifetime
{
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:latest")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/livez")))
        .Build();

    public Task InitializeAsync() => _mailpit.StartAsync();

    public Task DisposeAsync() => _mailpit.DisposeAsync().AsTask();

    [Fact]
    public async Task Smtp_sender_delivers_html_and_text_with_vietnamese_subject()
    {
        var sender = new SmtpEmailSender(Options.Create(new EmailOptions
        {
            Host = _mailpit.Hostname,
            Port = _mailpit.GetMappedPublicPort(1025),
            FromAddress = "no-reply@chonrieng.local",
            FromName = "Chọn Riêng Cafe",
        }));

        await sender.SendAsync("khach@cafecr.invalid", "Đặt lại mật khẩu", "<p>Xin chào</p>", "Xin chào", default);

        using var http = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}") };
        var list = await http.GetFromJsonAsync<JsonElement>("/api/v1/messages");
        var message = Assert.Single(list.GetProperty("messages").EnumerateArray());
        Assert.Equal("Đặt lại mật khẩu", message.GetProperty("Subject").GetString());
        Assert.Equal("khach@cafecr.invalid", message.GetProperty("To")[0].GetProperty("Address").GetString());
        Assert.Equal("Chọn Riêng Cafe", message.GetProperty("From").GetProperty("Name").GetString());

        var detail = await http.GetFromJsonAsync<JsonElement>($"/api/v1/message/{message.GetProperty("ID").GetString()}");
        Assert.Contains("Xin chào", detail.GetProperty("HTML").GetString());
        Assert.Contains("Xin chào", detail.GetProperty("Text").GetString());
    }
}
