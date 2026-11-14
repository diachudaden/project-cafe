using Microsoft.Extensions.Options;

namespace ChonRieng.Api.Features.Sales;

/// <summary>Runs <see cref="SessionAutoReleaseService"/> on a timer (TASK-024); same pattern as <see cref="PaymentRequestExpiryWorker"/>.</summary>
public sealed class SessionAutoReleaseWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    IOptions<SessionReleaseOptions> options,
    ILogger<SessionAutoReleaseWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.AutoReleaseEnabled)
        {
            logger.LogInformation("Seat auto-release is disabled (Sessions:AutoReleaseGraceMinutes = 0).");
            return;
        }

        logger.LogInformation(
            "Seat auto-release: grace {GraceMinutes} min, every {IntervalSeconds} s.",
            settings.AutoReleaseGraceMinutes, settings.AutoReleaseIntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.AutoReleaseIntervalSeconds), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SessionAutoReleaseService>().ReleaseDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Seat auto-release run failed.");
            }
        }
    }
}
