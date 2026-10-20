using ChonRieng.Application.Payments;
using ChonRieng.Application.Payments.Sepay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChonRieng.Infrastructure.Payments;

public static class PaymentInfrastructureExtensions
{
    /// <summary>Đăng ký mọi thứ của module trừ phần cần tầng Api (SignalR notifier).</summary>
    public static IServiceCollection AddPaymentsCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PaymentOptions>()
            .Bind(configuration.GetSection(PaymentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<SepaySignatureVerifier>();
        services.AddSingleton<IPaymentProvider, VietQrProvider>();
        services.TryAddSingleton<IDefaultSeatStrategy, LowestSortOrderSeatStrategy>();

        services.AddScoped<IPaymentStore, EfPaymentStore>();
        services.AddScoped<ILedgerStore, EfLedgerStore>();

        services.AddScoped<SeatAllocator>();
        services.AddScoped<PaymentService>();
        services.AddScoped<LedgerService>();
        services.AddScoped<ReleaseExpiredSeatHoldsJob>();
        services.AddScoped<ReconcileVietQrJob>();

        return services;
    }
}
