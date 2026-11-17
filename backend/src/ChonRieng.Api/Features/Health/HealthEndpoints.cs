using System.ComponentModel;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Health;

public sealed record HealthResponse(
    [property: Description("Healthy = sẵn sàng; Degraded = còn migration chưa áp dụng; Unhealthy = không kết nối được CSDL.")] string Status,
    [property: Description("API kết nối được cơ sở dữ liệu.")] bool DatabaseReachable,
    [property: Description("Các migration EF Core chưa áp dụng trên cơ sở dữ liệu.")] IReadOnlyList<string> PendingMigrations);

/// <summary>
/// Database readiness: reachable and fully migrated. Migrations are never applied here (see spec):
/// a pending one only produces an Error log at startup and a "Degraded" health status.
/// </summary>
public static class DatabaseHealth
{
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
    public const string Unhealthy = "Unhealthy";

    public static async Task<HealthResponse> CheckAsync(AppDbContext db, CancellationToken ct)
    {
        // Test providers (InMemory) have no migrations.
        if (!db.Database.IsRelational())
        {
            return new HealthResponse(Healthy, true, []);
        }

        try
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            return new HealthResponse(pending.Count == 0 ? Healthy : Degraded, true, pending);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return new HealthResponse(Unhealthy, false, []);
        }
    }

    /// <summary>Logs an Error when the database is behind the code, so a forgotten migration is visible in the API logs.</summary>
    public static async Task LogStartupStatusAsync(IServiceProvider services, ILogger logger, CancellationToken ct)
    {
        var health = await CheckAsync(services.GetRequiredService<AppDbContext>(), ct);
        if (health.Status == Degraded)
        {
            logger.LogError(
                "Database has {Count} pending EF Core migration(s): {Migrations}. They are NOT applied automatically; run 'dotnet ef database update --project backend/src/ChonRieng.Infrastructure --startup-project backend/src/ChonRieng.Api'. Features using the new schema will fail until then.",
                health.PendingMigrations.Count,
                string.Join(", ", health.PendingMigrations));
        }
        else if (health.Status == Unhealthy)
        {
            logger.LogError("Could not check EF Core migrations: the database is not reachable.");
        }
    }
}

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", async (AppDbContext db, CancellationToken ct) =>
            {
                var health = await DatabaseHealth.CheckAsync(db, ct);
                return Results.Json(health, statusCode: health.Status == DatabaseHealth.Unhealthy
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status200OK);
            })
            .AllowAnonymous()
            .WithTags("Hệ thống")
            .WithName("GetHealth")
            .WithSummary("Kiểm tra tình trạng API và cơ sở dữ liệu")
            .WithDescription("Công khai. Trả về Healthy khi CSDL kết nối được và đã áp dụng đủ migration; Degraded kèm danh sách migration còn thiếu (API không tự áp dụng migration); 503 Unhealthy khi không kết nối được CSDL.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);
    }
}
