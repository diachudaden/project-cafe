using ChonRieng.Application.Payments;
using ChonRieng.Infrastructure.Payments;
using Hangfire;

namespace ChonRieng.Api.Payments;

/// <summary>
/// Điểm cắm duy nhất của module vào <c>Program.cs</c>:
/// <code>
/// builder.Services.AddPayments(builder.Configuration);
/// ...
/// app.MapPaymentEndpoints();
/// app.UsePaymentJobs();   // sau khi Hangfire đã được cấu hình
/// </code>
/// </summary>
public static class PaymentsModule
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPaymentsCore(configuration);
        services.AddScoped<IPaymentNotifier, SignalRPaymentNotifier>();
        return services;
    }

    public static WebApplication UsePaymentJobs(this WebApplication app)
    {
        var jobs = app.Services.GetRequiredService<IRecurringJobManager>();

        jobs.AddOrUpdate<ReleaseExpiredSeatHoldsJob>(
            "payments:release-seat-holds",
            job => job.RunAsync(CancellationToken.None),
            Cron.Minutely());

        jobs.AddOrUpdate<ReconcileVietQrJob>(
            "payments:reconcile-vietqr",
            job => job.RunAsync(CancellationToken.None),
            "30 0 * * *",
            new RecurringJobOptions { TimeZone = VietnamTimeZone });

        return app;
    }
}
