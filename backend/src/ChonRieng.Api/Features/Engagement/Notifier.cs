using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;

namespace ChonRieng.Api.Features.Engagement;

/// <summary>Adds activity-feed entries; they are saved with the caller's SaveChanges.</summary>
public static class Notifier
{
    public const string ShiftOffered = "shift.offered";
    public const string ShiftChanged = "shift.changed";
    public const string ShiftDeclined = "shift.declined";
    public const string RequestCreated = "request.created";
    public const string RequestReviewed = "request.reviewed";
    public const string ClosingSubmitted = "closing.submitted";
    public const string ClosingReviewed = "closing.reviewed";
    public const string PayrollFinalized = "payroll.finalized";
    public const string PaymentReceived = "payment.received";
    public const string SessionAutoReleased = "session.auto_released";

    public static void Notify(this AppDbContext db, Guid userId, string type, string title, string body, string? link, DateTimeOffset now)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title.Length > 200 ? title[..200] : title,
            Body = body.Length > 2000 ? body[..2000] : body,
            Link = link,
            CreatedAt = now,
        });
    }
}
