using System.ComponentModel;
using System.Security.Claims;
using ChonRieng.Api.Features.Common;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Engagement;

public sealed record NotificationView(Guid Id, string Type, string Title, string Body, string? Link, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public sealed record NotificationFeed(int Unread, IReadOnlyList<NotificationView> Items);

public sealed record MarkReadRequest([property: Description("Danh sách id cần đánh dấu đã đọc; để trống = đánh dấu tất cả.")] IReadOnlyList<Guid>? Ids);

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var me = app.MapGroup("/api/me/notifications").WithTags("Thông báo").RequireAuthorization();

        me.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
            {
                var userId = CurrentUser.From(principal).UserId;
                var items = await db.Notifications.AsNoTracking()
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(50)
                    .Select(n => new NotificationView(n.Id, n.Type, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt))
                    .ToListAsync(ct);
                var unread = await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
                return new NotificationFeed(unread, items);
            })
            .WithName("MyNotifications").WithSummary("Feed Hoạt động của tôi (50 mục mới nhất)");

        me.MapPost("/read", async (MarkReadRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
            {
                var userId = CurrentUser.From(principal).UserId;
                var query = db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null);
                if (request.Ids is { Count: > 0 } ids)
                {
                    query = query.Where(n => ids.Contains(n.Id));
                }

                var now = clock.GetUtcNow();
                foreach (var n in await query.ToListAsync(ct))
                {
                    n.ReadAt = now;
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("MarkNotificationsRead").WithSummary("Đánh dấu đã đọc");
    }
}
