using ChonRieng.Api.Features.Sales;

namespace ChonRieng.Api.Features.Sales;

public sealed class PaymentRequestExpiryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<PaymentRequestExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<PaymentRequestService>();
                await service.ExpirePendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Payment request expiry run failed.");
            }
        }
    }
}
