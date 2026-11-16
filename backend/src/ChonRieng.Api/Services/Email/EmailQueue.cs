using System.Threading.Channels;

namespace ChonRieng.Api.Services.Email;

/// <summary>
/// One email to deliver. <paramref name="Purpose"/> and <paramref name="UserId"/> are the only values that are logged;
/// the bodies may contain a reset link and must never be written to logs.
/// </summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody, string Purpose, Guid? UserId);

public interface IEmailQueue
{
    /// <summary>Queues the message without waiting for SMTP; returns false when the queue is full.</summary>
    bool Enqueue(EmailMessage message);
}

/// <summary>
/// In-process queue drained by <see cref="EmailDispatchWorker"/>. Not durable: a message still queued when the API
/// stops is lost (the user can ask again). Bounded so a dead SMTP server cannot exhaust memory.
/// </summary>
public sealed class EmailQueue(ILogger<EmailQueue> logger) : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(500) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public bool Enqueue(EmailMessage message)
    {
        if (_channel.Writer.TryWrite(message))
        {
            return true;
        }

        logger.LogError("Email queue full; dropped {Purpose} email for user {UserId}.", message.Purpose, message.UserId);
        return false;
    }
}

/// <summary>Delivers queued emails; each message gets up to 3 attempts (after 5 s and 30 s).</summary>
public sealed class EmailDispatchWorker(
    EmailQueue queue,
    IEmailSender sender,
    ILogger<EmailDispatchWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await DeliverAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task DeliverAsync(EmailMessage message, CancellationToken stoppingToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await sender.SendAsync(message.To, message.Subject, message.HtmlBody, message.TextBody, stoppingToken);
                logger.LogInformation("Sent {Purpose} email for user {UserId} (attempt {Attempt}).", message.Purpose, message.UserId, attempt);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Only the exception type/message: SMTP errors do not echo the message body.
                if (attempt > RetryDelays.Length)
                {
                    logger.LogError("Giving up {Purpose} email for user {UserId} after {Attempt} attempts: {Error}",
                        message.Purpose, message.UserId, attempt, exception.GetType().Name + ": " + exception.Message);
                    return;
                }

                logger.LogWarning("Sending {Purpose} email for user {UserId} failed (attempt {Attempt}): {Error}",
                    message.Purpose, message.UserId, attempt, exception.GetType().Name + ": " + exception.Message);
                await Task.Delay(RetryDelays[attempt - 1], stoppingToken);
            }
        }
    }
}
