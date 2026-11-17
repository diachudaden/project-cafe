using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Engagement;
using ChonRieng.Domain.Entities;
using ChonRieng.Domain.Inventory;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>TASK-027 §6 jobs; each run is idempotent and is called by <see cref="InventoryJobsWorker"/> (or tests).</summary>
public sealed class InventoryJobs(
    AppDbContext db, OuterStockCalculator calculator, InventoryEvents events, IOptions<InventoryOptions> options, ILogger<InventoryJobs> logger)
{
    public const int RemindAfterMinutes = 30;
    public const int EscalateAfterMinutes = 60;
    public const int NormWindowDays = 14;
    public const int MinNormSamples = 3;
    private static readonly StockDocumentStatus[] Locked = [StockDocumentStatus.Confirmed, StockDocumentStatus.PendingApproval, StockDocumentStatus.Approved];

    /// <summary>Every 15′: shifts over without an outer count, and handovers nobody has received (30′ → POS, 60′ → manager).</summary>
    public async Task<int> RemindUnconfirmedCountsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var sent = 0;
        var since = now.AddHours(-24);
        var remindBefore = now.AddMinutes(-RemindAfterMinutes);
        var shifts = await db.Shifts.AsNoTracking()
            .Where(s => s.Status == ShiftStatus.Published && s.EndAt > since && s.EndAt <= remindBefore
                && s.Attendances.Any(a => a.CheckInAt != null)
                && db.ItemBranchSettings.Any(x => x.BranchId == s.BranchId && x.Item.IsActive)
                && !db.StockDocuments.Any(d => d.ShiftId == s.Id && d.Type == StockDocumentType.Count && Locked.Contains(d.Status)))
            .ToListAsync(ct);
        foreach (var shift in shifts)
        {
            var minutes = (int)(now - shift.EndAt).TotalMinutes;
            var staffLink = $"{InventoryEvents.PosLink}?count-shift={shift.Id}";
            var staff = await db.Attendances.AsNoTracking().Where(a => a.ShiftId == shift.Id).Select(a => a.Employee.UserId).Distinct().ToListAsync(ct);
            foreach (var userId in staff)
            {
                if (!await db.Notifications.AnyAsync(n => n.UserId == userId && n.Type == InventoryEvents.CountReminder && n.Link == staffLink, ct))
                {
                    db.Notify(userId, InventoryEvents.CountReminder, "Chưa đếm kho ngoài kết ca", "Ca của bạn đã kết thúc nhưng chưa xác nhận số đếm kho ngoài.", staffLink, now);
                    sent++;
                }
            }

            var managerLink = InventoryEvents.CountMissingLink(shift.Id);
            if (minutes >= EscalateAfterMinutes && !await db.Notifications.AnyAsync(n => n.Type == InventoryEvents.CountReminder && n.Link == managerLink, ct))
            {
                await events.CountMissingAsync(shift, minutes, now, ct);
                sent++;
            }
        }

        var handovers = await db.ShiftCountHandovers.Include(h => h.CountDocument)
            .Where(h => h.Status == HandoverStatus.Waiting && h.CreatedAt <= remindBefore && (h.RemindedAt == null || h.EscalatedAt == null))
            .ToListAsync(ct);
        foreach (var h in handovers)
        {
            var minutes = (int)(now - h.CreatedAt).TotalMinutes;
            if (h.RemindedAt is null)
            {
                await events.BranchStaffAsync(h.CountDocument.BranchId, InventoryEvents.HandoverOverdue, "Có bàn giao kho ngoài chưa nhận",
                    $"{h.CountDocument.Code}: hãy xác nhận nhận hoặc báo không khớp.", now, ct);
                h.RemindedAt = now;
                sent++;
            }

            if (h.EscalatedAt is null && minutes >= EscalateAfterMinutes)
            {
                await events.HandoverOverdueAsync(h.CountDocument, minutes, now, ct);
                h.EscalatedAt = now;
                sent++;
            }
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }

    /// <summary>Every 10′: items expected below OuterMin in a running shift → one notice per staff member per hour.</summary>
    public async Task<int> SuggestRestockAsync(DateTimeOffset now, CancellationToken ct)
    {
        var sent = 0;
        var running = await db.Shifts.AsNoTracking()
            .Where(s => s.Status == ShiftStatus.Published && s.StartAt <= now && now < s.EndAt
                && s.Attendances.Any(a => a.CheckInAt != null && a.CheckOutAt == null))
            .ToListAsync(ct);
        foreach (var shift in running.GroupBy(s => s.BranchId).Select(g => g.OrderBy(s => s.StartAt).First()))
        {
            if (await InventoryShifts.CountInProgressAsync(db, shift.Id, ct))
            {
                continue;
            }

            var outer = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.BranchId == shift.BranchId && w.Type == WarehouseType.Outer, ct);
            if (outer is null)
            {
                continue;
            }

            var figures = await calculator.ComputeAsync(shift.BranchId, outer.Id, shift, now, ct);
            var low = await db.ItemBranchSettings.AsNoTracking().Where(s => s.BranchId == shift.BranchId && s.Item.IsActive)
                .Select(s => new { s.ItemId, s.Item.Name, s.OuterMin, s.OuterPar }).ToListAsync(ct);
            var items = low.Where(s => figures.TryGetValue(s.ItemId, out var f) && f.Expected < s.OuterMin)
                .Select(s => $"{s.Name} (gợi ý lôi {InventoryMath.SuggestedTransfer(s.OuterPar, figures[s.ItemId].Expected)})").ToList();
            if (items.Count == 0)
            {
                continue;
            }

            var hourAgo = now.AddHours(-1);
            var userIds = await db.Attendances.AsNoTracking()
                .Where(a => a.Shift.BranchId == shift.BranchId && a.CheckInAt != null && a.CheckOutAt == null)
                .Select(a => a.Employee.UserId).Distinct().ToListAsync(ct);
            foreach (var userId in userIds)
            {
                if (await db.Notifications.AnyAsync(n => n.UserId == userId && n.Type == InventoryEvents.RestockSuggested && n.CreatedAt > hourAgo, ct))
                {
                    continue;
                }

                db.Notify(userId, InventoryEvents.RestockSuggested, "Nên lôi hàng ra kho ngoài", string.Join(", ", items), InventoryEvents.PosLink, now);
                sent++;
            }
        }

        await db.SaveChangesAsync(ct);
        return sent;
    }

    /// <summary>
    /// 00:45: reconciles each item between two consecutive locked inner counts (§3). One issue per (inner count, item).
    /// TransferMisrecorded proposes a draft DC that only records the misrecorded transfer: both books were already set
    /// right by the counts, so it has no warehouse and moves no stock; the transfer's author is reminded.
    /// </summary>
    public async Task<int> ReconcileAsync(DateTimeOffset now, CancellationToken ct)
    {
        var created = 0;
        var settings = options.Value;
        var inner = await db.StockDocuments.AsNoTracking().Include(d => d.Lines)
            .Where(d => d.Type == StockDocumentType.Count && d.ShiftId == null && d.ToWarehouse!.Type == WarehouseType.Inner
                && (d.Status == StockDocumentStatus.Confirmed || d.Status == StockDocumentStatus.Approved) && d.ConfirmedAt != null)
            .OrderBy(d => d.ConfirmedAt)
            .ToListAsync(ct);
        foreach (var branch in inner.GroupBy(d => d.BranchId))
        {
            var ordered = branch.ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var (from, to) = (ordered[i - 1], ordered[i]);
                if (await db.InventoryIssues.AnyAsync(x => x.InnerCountDocumentId == to.Id, ct))
                {
                    continue;
                }

                var outerLines = await db.StockDocumentLines.AsNoTracking()
                    .Where(l => l.Document.BranchId == branch.Key && l.Document.Type == StockDocumentType.Count && l.Document.ShiftId != null
                        && Locked.Contains(l.Document.Status) && l.Document.ConfirmedAt > from.ConfirmedAt && l.Document.ConfirmedAt <= to.ConfirmedAt)
                    .Select(l => new { l.ItemId, l.Variance, l.Usage })
                    .ToListAsync(ct);
                foreach (var line in to.Lines)
                {
                    var outer = outerLines.Where(l => l.ItemId == line.ItemId).ToList();
                    var result = InventoryMath.Reconcile(line.Variance ?? 0, outer.Sum(l => l.Variance ?? 0), outer.Sum(l => l.Usage ?? 0),
                        settings.ToleranceMinUnits, settings.TolerancePercent);
                    var issue = new InventoryIssue
                    {
                        BranchId = branch.Key, ItemId = line.ItemId, PeriodFrom = from.ConfirmedAt!.Value, PeriodTo = to.ConfirmedAt!.Value,
                        Kind = result.Kind, InnerVar = result.InnerVar, OuterVar = result.OuterVar, Total = result.Total, Tolerance = result.Tolerance,
                        InnerCountDocumentId = to.Id, CreatedAt = now,
                        Status = result.Kind is InventoryIssueKind.Normal or InventoryIssueKind.WithinTolerance ? InventoryIssueStatus.Resolved : InventoryIssueStatus.Open,
                    };
                    db.InventoryIssues.Add(issue);
                    created++;
                    await FollowUpAsync(issue, from.ConfirmedAt!.Value, to.ConfirmedAt!.Value, now, ct);
                }

                await db.SaveChangesAsync(ct);
            }
        }

        return created;
    }

    private async Task FollowUpAsync(InventoryIssue issue, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, CancellationToken ct)
    {
        var item = await db.InventoryItems.AsNoTracking().Where(i => i.Id == issue.ItemId).Select(i => new { i.Name, i.BaseUnit }).SingleAsync(ct);
        switch (issue.Kind)
        {
            case InventoryIssueKind.TransferMisrecorded:
                var transfer = await db.StockDocuments.AsNoTracking()
                    .Where(d => d.BranchId == issue.BranchId && d.Type == StockDocumentType.Transfer && d.CreatedAt > from && d.CreatedAt <= to
                        && d.Lines.Any(l => l.ItemId == issue.ItemId))
                    .OrderByDescending(d => d.CreatedAt).Select(d => new { d.Id, d.Code, d.CreatedBy }).FirstOrDefaultAsync(ct);
                var code = await DocumentCodes.NextAsync(db, StockDocumentType.Adjustment, issue.BranchId, now, ct);
                var dc = new StockDocument
                {
                    Code = code, Type = StockDocumentType.Adjustment, BranchId = issue.BranchId, Status = StockDocumentStatus.Draft,
                    AdjustsDocumentId = transfer?.Id, CreatedBy = transfer?.CreatedBy ?? Guid.Empty, CreatedAt = now,
                    Reason = $"Đối soát: phiếu chuyển {transfer?.Code ?? "trong kỳ"} có thể ghi thiếu {-issue.InnerVar} {item.BaseUnit} {item.Name}.",
                    Note = "Chỉ ghi nhận: tồn hai kho đã đúng theo số kiểm kê, phiếu này không đổi tồn.",
                };
                dc.Lines.Add(new StockDocumentLine { ItemId = issue.ItemId, Qty = -issue.InnerVar });
                db.StockDocuments.Add(dc);
                issue.AdjustmentDocumentId = dc.Id;
                if (transfer is not null)
                {
                    db.Notify(transfer.CreatedBy, InventoryEvents.IssueFound, "Phiếu chuyển có thể ghi sai số",
                        $"{transfer.Code}: {item.Name} lệch {-issue.InnerVar} {item.BaseUnit} giữa hai kho. Lần sau hãy ghi đúng số lôi ra.", InventoryEvents.PosLink, now);
                }

                await events.IssueAsync(issue.BranchId, "Đối soát: ghi phiếu chuyển sai",
                    $"{item.Name}: kho trong {issue.InnerVar}, kho ngoài +{issue.OuterVar}. Đã tạo đề xuất {code}.", now, ct);
                break;
            case InventoryIssueKind.RealLoss:
                await events.IssueAsync(issue.BranchId, "Đối soát: hao hụt thật",
                    $"{item.Name}: thiếu {-issue.Total} {item.BaseUnit} (kho trong {issue.InnerVar}, kho ngoài {issue.OuterVar}, ngưỡng ±{issue.Tolerance}).", now, ct);
                break;
            case InventoryIssueKind.UnexplainedSurplus:
                await events.IssueAsync(issue.BranchId, "Đối soát: dư không rõ",
                    $"{item.Name}: dư {issue.Total} {item.BaseUnit}. Kiểm tra phiếu nhập / phiếu huỷ.", now, ct);
                break;
        }
    }

    /// <summary>01:00: norm = median actual usage per session over 14 days of locked counts (±30 %); par = avg usage/shift × 1.2.</summary>
    public async Task<int> RecalcNormsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now.ToVietnam().DateTime);
        var since = now.AddDays(-NormWindowDays);
        var samples = await db.StockDocumentLines.AsNoTracking()
            .Where(l => l.Document.Type == StockDocumentType.Count && l.Document.ShiftId != null && l.Document.SessionCount > 0
                && (l.Document.Status == StockDocumentStatus.Confirmed || l.Document.Status == StockDocumentStatus.Approved)
                && l.Document.ConfirmedAt > since && l.CountedQty != null)
            .Select(l => new { l.Document.BranchId, l.ItemId, Sessions = l.Document.SessionCount!.Value, l.OpeningQty, l.TransferInQty, l.WasteQty, l.CountedQty })
            .ToListAsync(ct);
        var updated = 0;
        foreach (var group in samples.GroupBy(s => (s.BranchId, s.ItemId)))
        {
            var setting = await db.ItemBranchSettings.SingleOrDefaultAsync(s => s.BranchId == group.Key.BranchId && s.ItemId == group.Key.ItemId, ct);
            // Once per business day: a restart must not compound the ±30 % clamp.
            if (setting is null || group.Count() < MinNormSamples
                || (setting.NormLearnedAt is { } learned && DateOnly.FromDateTime(learned.ToVietnam().DateTime) == today))
            {
                continue;
            }

            var usage = group.Select(s => (decimal)InventoryMath.ActualUsage(s.OpeningQty ?? 0, s.TransferInQty ?? 0, s.WasteQty ?? 0, s.CountedQty!.Value)).ToList();
            var perSession = group.Zip(usage, (s, u) => u / s.Sessions);
            setting.NormPerSession = InventoryMath.RecalcNorm(perSession, setting.NormPerSession);
            if (InventoryMath.RecalcPar(usage) is { } par && par > 0)
            {
                setting.OuterPar = Math.Max(par, setting.OuterMin);
            }

            setting.NormLearnedAt = now;
            updated++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("RecalcNorms updated {Count} item settings.", updated);
        return updated;
    }
}

/// <summary>
/// Runs <see cref="InventoryJobs"/> on Vietnam time (same timer pattern as the other workers, no Hangfire):
/// reminders every 15′, restock every 10′, reconciliation at 00:45 and norm learning at 01:00.
/// </summary>
public sealed class InventoryJobsWorker(
    IServiceScopeFactory scopeFactory, TimeProvider clock, IOptions<InventoryOptions> options, ILogger<InventoryJobsWorker> logger) : BackgroundService
{
    private DateTimeOffset _lastRemind = DateTimeOffset.MinValue;
    private DateTimeOffset _lastRestock = DateTimeOffset.MinValue;
    private DateOnly _reconciled;
    private DateOnly _recalculated;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.JobsEnabled)
        {
            logger.LogInformation("Inventory jobs are disabled (Inventory:JobsEnabled = false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = clock.GetUtcNow();
            var local = now.ToVietnam();
            var today = DateOnly.FromDateTime(local.DateTime);
            await RunAsync("RemindUnconfirmedCounts", now - _lastRemind >= TimeSpan.FromMinutes(15), j => j.RemindUnconfirmedCountsAsync(now, stoppingToken), () => _lastRemind = now, stoppingToken);
            await RunAsync("SuggestRestock", now - _lastRestock >= TimeSpan.FromMinutes(10), j => j.SuggestRestockAsync(now, stoppingToken), () => _lastRestock = now, stoppingToken);
            await RunAsync("ReconcileInventory", _reconciled != today && local.TimeOfDay >= new TimeSpan(0, 45, 0), j => j.ReconcileAsync(now, stoppingToken), () => _reconciled = today, stoppingToken);
            await RunAsync("RecalcNorms", _recalculated != today && local.TimeOfDay >= new TimeSpan(1, 0, 0), j => j.RecalcNormsAsync(now, stoppingToken), () => _recalculated = today, stoppingToken);
        }
    }

    private async Task RunAsync(string name, bool due, Func<InventoryJobs, Task<int>> job, Action done, CancellationToken ct)
    {
        if (!due)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await job(scope.ServiceProvider.GetRequiredService<InventoryJobs>());
            done();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Inventory job {Job} failed.", name);
        }
    }
}
