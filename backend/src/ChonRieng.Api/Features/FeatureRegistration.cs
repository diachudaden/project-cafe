using System.Security.Claims;
using System.Threading.RateLimiting;
using ChonRieng.Api.Features.Catalog;
using ChonRieng.Api.Features.Engagement;
using ChonRieng.Api.Features.Finance;
using ChonRieng.Api.Features.Reports;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Api.Features.Scheduling;

namespace ChonRieng.Api.Features;

public static class FeatureRegistration
{
    public static IServiceCollection AddFeatureServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CatalogService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<WorkforceService>();
        services.AddScoped<SessionService>();
        services.AddScoped<PaymentRequestService>();
        services.AddHostedService<PaymentRequestExpiryWorker>();
        services.AddScoped<LoyaltyService>();
        services.AddScoped<FinanceService>();
        services.AddScoped<ReportService>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Bill codes are short, so claim attempts are limited per user to stop guessing.
            options.AddPolicy(SalesEndpoints.ClaimRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });
        return services;
    }

    public static void MapFeatureEndpoints(this WebApplication app)
    {
        app.MapCatalogEndpoints();
        app.MapSchedulingEndpoints();
        app.MapPaymentEndpoints();
        app.MapSalesEndpoints();
        app.MapFinanceEndpoints();
        app.MapReportEndpoints();
        app.MapNotificationEndpoints();
    }
}
