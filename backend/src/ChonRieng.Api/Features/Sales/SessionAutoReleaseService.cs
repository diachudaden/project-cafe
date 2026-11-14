using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Engagement;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChonRieng.Api.Features.Sales;

public sealed record AutoReleaseCandidate(Guid SessionId, DateTimeOffset EndAt, string SeatCode, string BranchName, Guid? CustomerUserId);

/// <summary>
/// Closes sessions that are overdue by more than the grace period and were not extended (TASK-024, decision D30).
/// Each release is one conditional update, so an extension or manual close that commits first always wins.
/// </summary>
public sealed class SessionAutoReleaseService(
    AppDbContext db,
    TimeProvider clock,
    IOptions<SessionReleaseOptions> options,
    ILogger<SessionAutoReleaseService> logger)
{
    private const int BatchSize = 200;
    private SessionReleaseOptions Options => options.Value;

    /// <summary>Releases every due session; one failing row never stops the others. Returns how many were released.</summary>
    public async Task<int> ReleaseDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var released = 0;
        foreach (var candidate in await FindCandidatesAsync(now, ct))
        {
            try
            {
                if (await TryReleaseAsync(candidate, now, ct))
                {
                    released++;
                }
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                logger.LogError(exception, "Auto-release failed for session {SessionId} (seat {SeatCode}).", candidate.SessionId, candidate.SeatCode);
                db.ChangeTracker.Clear();
            }
        }

        return released;
    }

    /// <summary>
    /// Active sessions past <c>EndAt + grace</c>, except those with a pending, unexpired VietQR request
    /// (extension or add-on): the customer may be paying right now.
    /// </summary>
    public async Task<IReadOnlyList<AutoReleaseCandidate>> FindCandidatesAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (!Options.AutoReleaseEnabled)
        {
            return [];
        }

        var threshold = Threshold(now);
        return await db.Sessions.AsNoTracking()
            .Where(s => s.Status == SessionStatus.Active && s.EndAt <= threshold)
            .Where(s => !db.PaymentRequests.Any(r => r.SessionId == s.Id && r.Status == PaymentRequestStatus.Pending && r.ExpiresAt > now))
            .OrderBy(s => s.EndAt)
            .Take(BatchSize)
            .Select(s => new AutoReleaseCandidate(
                s.Id, s.EndAt, s.Seat.Code, s.Seat.Zone.Branch.Name, s.Customer != null ? s.Customer.UserId : null))
            .ToListAsync(ct);
    }

    /// <summary>Closes the session only if it is still active and still past the threshold; false when someone else won.</summary>
    public async Task<bool> TryReleaseAsync(AutoReleaseCandidate candidate, DateTimeOffset now, CancellationToken ct)
    {
        var threshold = Threshold(now);
        int rows;
        if (db.Database.IsRelational())
        {
            rows = await db.Sessions
                .Where(s => s.Id == candidate.SessionId && s.Status == SessionStatus.Active && s.EndAt <= threshold)
                .ExecuteUpdateAsync(u =>
                {
                    u.SetProperty(s => s.Status, SessionStatus.Expired);
                    u.SetProperty(s => s.ClosedAt, now);
                    u.SetProperty(s => s.CloseReason, SessionCloseReason.AutoReleased);
                    u.SetProperty(s => s.ClosedByUserId, (Guid?)null);
                }, ct);
        }
        else
        {
            // Providers without ExecuteUpdate (in-memory tests): same predicate, guarded by the Status/EndAt tokens.
            rows = await ReleaseTrackedAsync(candidate.SessionId, threshold, now, ct);
        }

        if (rows == 0)
        {
            logger.LogDebug("Session {SessionId} was extended or closed before auto-release; skipped.", candidate.SessionId);
            return false;
        }

        var minutesOverdue = (int)Math.Floor((now - candidate.EndAt).TotalMinutes);
        logger.LogInformation(
            "Auto-released seat {SeatCode} at {Branch}: {MinutesOverdue} minutes overdue (session {SessionId}).",
            candidate.SeatCode, candidate.BranchName, minutesOverdue, candidate.SessionId);

        if (candidate.CustomerUserId is { } userId)
        {
            try
            {
                db.Notify(userId, Notifier.SessionAutoReleased, $"Đã tự trả chỗ {candidate.SeatCode}",
                    $"Lượt ngồi tại {candidate.BranchName} đã kết thúc lúc {now.ToVietnam():HH:mm dd/MM} do quá giờ {minutesOverdue} phút mà không gia hạn.",
                    "/", now);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                // The seat is already free; a lost notification must not undo or retry the release.
                logger.LogWarning(exception, "Could not notify the customer of auto-released session {SessionId}.", candidate.SessionId);
                db.ChangeTracker.Clear();
            }
        }

        return true;
    }

    private async Task<int> ReleaseTrackedAsync(Guid sessionId, DateTimeOffset threshold, DateTimeOffset now, CancellationToken ct)
    {
        var session = await db.Sessions.SingleOrDefaultAsync(
            s => s.Id == sessionId && s.Status == SessionStatus.Active && s.EndAt <= threshold, ct);
        if (session is null)
        {
            return 0;
        }

        session.Status = SessionStatus.Expired;
        session.ClosedAt = now;
        session.CloseReason = SessionCloseReason.AutoReleased;
        session.ClosedByUserId = null;
        try
        {
            await db.SaveChangesAsync(ct);
            return 1;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return 0;
        }
    }

    private DateTimeOffset Threshold(DateTimeOffset now) => now.AddMinutes(-Options.AutoReleaseGraceMinutes);
}
