using ChonRieng.Api.Features.Health;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChonRieng.UnitTests.Features;

public sealed class DatabaseHealthTests
{
    [Fact]
    public async Task Non_relational_provider_is_healthy_without_pending_migrations()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Health-{Guid.NewGuid()}").Options);

        var health = await DatabaseHealth.CheckAsync(db, default);

        Assert.Equal(DatabaseHealth.Healthy, health.Status);
        Assert.Empty(health.PendingMigrations);
    }

    [Fact]
    public async Task Unreachable_sql_server_is_unhealthy_and_nothing_is_migrated()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=none;User Id=x;Password=x;Connect Timeout=1;TrustServerCertificate=True",
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options);

        var health = await DatabaseHealth.CheckAsync(db, default);

        Assert.Equal(DatabaseHealth.Unhealthy, health.Status);
        Assert.False(health.DatabaseReachable);
    }
}
