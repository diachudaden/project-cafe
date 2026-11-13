using ChonRieng.Api.Features.Sales;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChonRieng.UnitTests.Features;

/// <summary>A real <see cref="PaymentLogService"/> writing to the same in-memory database as the test's DbContext.</summary>
internal static class TestPaymentLog
{
    public static PaymentLogService For(AppDbContext db, TimeProvider clock)
    {
        var options = db.GetService<IDbContextOptions>();
        var services = new ServiceCollection();
        services.AddScoped(_ => new AppDbContext((DbContextOptions<AppDbContext>)options));
        return new PaymentLogService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new HttpContextAccessor(),
            clock,
            NullLogger<PaymentLogService>.Instance);
    }
}
