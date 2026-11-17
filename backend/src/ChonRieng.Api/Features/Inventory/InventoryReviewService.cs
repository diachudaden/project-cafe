using ChonRieng.Api.Features.Common;
using ChonRieng.Api.Features.Engagement;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>Manager side: approve/reject, adjustments (the only way to correct a locked document) and the "Cần xử lý" box.</summary>
public sealed class InventoryReviewService(AppDbContext db, StockDocumentService documents, TimeProvider clock)
{
    public const int HandoverOverdueMinutes = 60;

    public async Task<Result<AttentionView>> AttentionAsync(CurrentUser user, Guid branchId, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var now = clock.GetUtcNow();
        var pending = await db.StockDocuments.AsNoTracking()
            .Where(d => d.BranchId == branchId && d.Type == StockDocumentType.Count && d.Status == StockDocumentStatus.PendingApproval)
            .OrderBy(d => d.ConfirmedAt)
            .Select(d => new
            {
                d.Id, d.Code, ShiftType = (ShiftType?)d.Shift!.Type, ShiftStart = (DateTimeOffset?)d.Shift.StartAt, ShiftEnd = (DateTimeOffset?)d.Shift.EndAt,
                d.ConfirmedBy, ConfirmedAt = d.ConfirmedAt!.Value, d.Reason,
                Over = d.Lines.Count(l => l.Variance != null && (l.Variance > l.Tolerance || -l.Variance > l.Tolerance)),
            })
            .ToListAsync(ct);
        var overdueBefore = now.AddMinutes(-HandoverOverdueMinutes);
        var handovers = await db.ShiftCountHandovers.AsNoTracking()
            .Where(h => h.CountDocument.BranchId == branchId
                && (h.Status == HandoverStatus.Disputed || (h.Status == HandoverStatus.Waiting && h.CreatedAt <= overdueBefore)))
            .OrderBy(h => h.CreatedAt)
            .Select(h => new
            {
                h.CountDocumentId, h.CountDocument.Code, h.Status, ShiftType = (ShiftType?)h.CountDocument.Shift!.Type,
                ShiftStart = (DateTimeOffset?)h.CountDocument.Shift.StartAt, ShiftEnd = (DateTimeOffset?)h.CountDocument.Shift.EndAt,
                h.CreatedAt, h.ReceivedBy, h.DisputeNote,
            })
            .ToListAsync(ct);
        var draftIds = await db.StockDocuments.AsNoTracking()
            .Where(d => d.BranchId == branchId && d.Type == StockDocumentType.Adjustment && d.Status == StockDocumentStatus.Draft)
            .OrderBy(d => d.CreatedAt).Select(d => d.Id).ToListAsync(ct);
        var issues = await IssuesAsync(db.InventoryIssues.Where(i => i.BranchId == branchId && i.Status == InventoryIssueStatus.Open
            && i.Kind != InventoryIssueKind.Normal && i.Kind != InventoryIssueKind.WithinTolerance), ct);

        var userIds = pending.Select(p => p.ConfirmedBy).Concat(handovers.Select(h => h.ReceivedBy)).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string Name(Guid? id) => id is { } x ? names.GetValueOrDefault(x) ?? "" : "";

        var counts = pending.Select(p => new AttentionCount(p.Id, p.Code, p.ShiftType, p.ShiftStart, p.ShiftEnd, Name(p.ConfirmedBy), p.ConfirmedAt, p.Reason, p.Over)).ToList();
        var handoverViews = handovers.Select(h => new AttentionHandover(h.CountDocumentId, h.Code, h.Status, h.ShiftType, h.ShiftStart, h.ShiftEnd,
            h.CreatedAt, (int)(now - h.CreatedAt).TotalMinutes, h.ReceivedBy is null ? null : Name(h.ReceivedBy), h.DisputeNote)).ToList();
        var drafts = await documents.ViewsAsync(draftIds, ct);
        var misrecorded = issues.Where(i => i.Kind == InventoryIssueKind.TransferMisrecorded).ToList();
        var losses = issues.Where(i => i.Kind != InventoryIssueKind.TransferMisrecorded).ToList();
        return new AttentionView(counts, handoverViews, drafts, misrecorded, losses,
            counts.Count + handoverViews.Count + drafts.Count + misrecorded.Count + losses.Count);
    }

    /// <summary>PendingApproval count → Approved; Draft adjustment → posted and Approved.</summary>
    public async Task<Result<StockDocumentView>> ApproveAsync(CurrentUser user, Guid documentId, ReviewDocumentRequest request, CancellationToken ct)
    {
        var doc = await db.StockDocuments.Include(d => d.Lines).SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null || !user.CanManageBranch(doc.BranchId))
        {
            return ServiceError.NotFound();
        }

        var now = clock.GetUtcNow();
        switch (doc)
        {
            case { Type: StockDocumentType.Count, Status: StockDocumentStatus.PendingApproval }:
                break;
            case { Type: StockDocumentType.Adjustment, Status: StockDocumentStatus.Draft }:
                if (await StockLedger.PostAdjustmentAsync(db, doc, await WarehouseTypesAsync(doc.BranchId, ct), now, ct) is { } shortfall)
                {
                    db.ChangeTracker.Clear();
                    return Insufficient(shortfall);
                }

                doc.ConfirmedBy = user.UserId;
                doc.ConfirmedAt = now;
                break;
            default:
                return InventoryErrors.Conflict(InventoryErrors.InvalidState, "Không thể duyệt", "Chỉ duyệt được phiếu đếm chờ duyệt hoặc phiếu điều chỉnh nháp.");
        }

        doc.Status = StockDocumentStatus.Approved;
        doc.ApprovedBy = user.UserId;
        doc.ApprovedAt = now;
        doc.ApprovalNote = request.Note?.Trim();
        // A draft adjustment proposed by reconciliation closes its issue.
        foreach (var issue in await db.InventoryIssues.Where(i => i.AdjustmentDocumentId == doc.Id && i.Status == InventoryIssueStatus.Open).ToListAsync(ct))
        {
            issue.Status = InventoryIssueStatus.Resolved;
            issue.ResolvedBy = user.UserId;
            issue.ResolvedAt = now;
            issue.Note = $"Đã duyệt phiếu điều chỉnh {doc.Code}.";
        }

        return await SaveAsync(doc.Id, ct);
    }

    /// <summary>
    /// PendingApproval count → Rejected: its book movements are reversed (append-only) and the shift may count again.
    /// Draft adjustment → Rejected without touching stock.
    /// </summary>
    public async Task<Result<StockDocumentView>> RejectAsync(CurrentUser user, Guid documentId, ReviewDocumentRequest request, CancellationToken ct)
    {
        var doc = await db.StockDocuments.Include(d => d.Lines).SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null || !user.CanManageBranch(doc.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return InventoryErrors.Reason("Từ chối cần ghi lý do.");
        }

        var now = clock.GetUtcNow();
        var note = request.Note.Trim();
        switch (doc)
        {
            case { Type: StockDocumentType.Count, Status: StockDocumentStatus.PendingApproval }:
                var movements = await db.StockMovements.AsNoTracking().Where(m => m.DocumentId == doc.Id).ToListAsync(ct);
                foreach (var m in movements.GroupBy(m => (m.WarehouseId, m.ItemId)))
                {
                    await StockLedger.PostAsync(db, m.Key.WarehouseId, m.Key.ItemId, -m.Sum(x => x.Qty), doc.Id, now, ct);
                }

                var handover = await db.ShiftCountHandovers.SingleOrDefaultAsync(h => h.CountDocumentId == doc.Id, ct);
                if (handover is not null && handover.Status is HandoverStatus.Waiting or HandoverStatus.Disputed)
                {
                    handover.Status = HandoverStatus.Resolved;
                    handover.ResolvedBy = user.UserId;
                    handover.ResolvedAt = now;
                    handover.ResolveNote = $"Phiếu đếm bị từ chối: {note}";
                }

                if (doc.ConfirmedBy is { } counter)
                {
                    db.Notify(counter, InventoryEvents.CountNeedsApproval, "Phiếu đếm kho ngoài bị từ chối",
                        $"{doc.Code}: {note}. Cần đếm lại.", InventoryEvents.PosLink, now);
                }

                break;
            case { Type: StockDocumentType.Adjustment, Status: StockDocumentStatus.Draft }:
                break;
            default:
                return InventoryErrors.Conflict(InventoryErrors.InvalidState, "Không thể từ chối", "Chỉ từ chối được phiếu đếm chờ duyệt hoặc phiếu điều chỉnh nháp.");
        }

        doc.Status = StockDocumentStatus.Rejected;
        doc.ApprovedBy = user.UserId;
        doc.ApprovedAt = now;
        doc.ApprovalNote = note;
        return await SaveAsync(doc.Id, ct);
    }

    /// <summary>
    /// Corrects a locked document by ± quantities. Movements follow the target: receipt → inner, waste → its warehouse (more
    /// waste = less stock), transfer → inner and outer, count → the counted warehouse. The target is never modified.
    /// </summary>
    public async Task<Result<StockDocumentView>> CreateAdjustmentAsync(CurrentUser user, CreateAdjustmentRequest request, CancellationToken ct)
    {
        var target = await db.StockDocuments.AsNoTracking().Include(d => d.Lines).SingleOrDefaultAsync(d => d.Id == request.AdjustsDocumentId, ct);
        if (target is null || !user.CanManageBranch(target.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (target.Type == StockDocumentType.Adjustment || target.Status is StockDocumentStatus.Draft or StockDocumentStatus.Rejected)
        {
            return InventoryErrors.Conflict(InventoryErrors.InvalidState, "Không thể điều chỉnh", "Chỉ điều chỉnh phiếu đã khoá (không phải phiếu điều chỉnh, nháp hoặc bị từ chối).");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return InventoryErrors.Reason("Phiếu điều chỉnh cần lý do.");
        }

        var itemIds = target.Lines.Select(l => l.ItemId).ToHashSet();
        if (request.Lines is not { Count: > 0 } || request.Lines.Any(l => l.QtyDelta == 0 || !itemIds.Contains(l.ItemId))
            || request.Lines.Select(l => l.ItemId).Distinct().Count() != request.Lines.Count)
        {
            return ServiceError.Invalid(nameof(request.Lines), "Mỗi món của phiếu gốc một dòng, số điều chỉnh khác 0.");
        }

        var (from, to) = target.Type switch
        {
            StockDocumentType.Waste => (target.FromWarehouseId, (Guid?)null),
            StockDocumentType.Transfer => (target.FromWarehouseId, target.ToWarehouseId),
            _ => ((Guid?)null, target.ToWarehouseId),
        };
        var now = clock.GetUtcNow();
        var doc = StockDocumentService.NewDocument(StockDocumentType.Adjustment, target.BranchId, from, to, target.ShiftId, user, now,
            await DocumentCodes.NextAsync(db, StockDocumentType.Adjustment, target.BranchId, now, ct));
        doc.AdjustsDocumentId = target.Id;
        doc.Reason = request.Reason.Trim();
        foreach (var line in request.Lines)
        {
            doc.Lines.Add(new StockDocumentLine { ItemId = line.ItemId, Qty = line.QtyDelta });
        }

        if (await StockLedger.PostAdjustmentAsync(db, doc, await WarehouseTypesAsync(target.BranchId, ct), now, ct) is { } shortfall)
        {
            db.ChangeTracker.Clear();
            return Insufficient(shortfall);
        }

        db.StockDocuments.Add(doc);
        return await SaveAsync(doc.Id, ct);
    }

    public async Task<Result<InventoryIssueView>> ResolveIssueAsync(CurrentUser user, Guid issueId, ResolveIssueRequest request, CancellationToken ct)
    {
        var issue = await db.InventoryIssues.SingleOrDefaultAsync(i => i.Id == issueId, ct);
        if (issue is null || !user.CanManageBranch(issue.BranchId))
        {
            return ServiceError.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return InventoryErrors.Reason("Cần ghi chú cách xử lý.");
        }

        if (issue.Status == InventoryIssueStatus.Open)
        {
            issue.Status = InventoryIssueStatus.Resolved;
            issue.ResolvedBy = user.UserId;
            issue.ResolvedAt = clock.GetUtcNow();
            issue.Note = request.Note.Trim();
            await db.SaveChangesAsync(ct);
        }

        return (await IssuesAsync(db.InventoryIssues.Where(i => i.Id == issueId), ct)).Single();
    }

    public async Task<Result<IReadOnlyList<InventoryIssueView>>> ListIssuesAsync(CurrentUser user, Guid branchId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!user.CanManageBranch(branchId))
        {
            return ServiceError.NotFound();
        }

        var start = VietnamTime.StartOfDay(from);
        var end = VietnamTime.StartOfDay(to.AddDays(1));
        return Result<IReadOnlyList<InventoryIssueView>>.From(await IssuesAsync(
            db.InventoryIssues.Where(i => i.BranchId == branchId && i.PeriodTo >= start && i.PeriodTo < end), ct));
    }

    public static async Task<List<InventoryIssueView>> IssuesAsync(IQueryable<InventoryIssue> query, CancellationToken ct) =>
        await query.AsNoTracking().OrderByDescending(i => i.PeriodTo).ThenBy(i => i.Item.SortOrder).Take(500)
            .Select(i => new InventoryIssueView(i.Id, i.ItemId, i.Item.Name, i.Item.BaseUnit, i.Kind, i.PeriodFrom, i.PeriodTo,
                i.InnerVar, i.OuterVar, i.Total, i.Tolerance, i.Status, i.AdjustmentDocumentId, i.CreatedAt, i.Note))
            .ToListAsync(ct);

    private Task<Dictionary<Guid, WarehouseType>> WarehouseTypesAsync(Guid branchId, CancellationToken ct) =>
        db.Warehouses.AsNoTracking().Where(w => w.BranchId == branchId).ToDictionaryAsync(w => w.Id, w => w.Type, ct);

    private async Task<Result<StockDocumentView>> SaveAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return InventoryErrors.Retry();
        }

        db.ChangeTracker.Clear();
        return (await documents.ViewsAsync([id], ct)).Single();
    }

    private ServiceError Insufficient((Guid ItemId, int Available, int Needed) s) =>
        InventoryErrors.Conflict(InventoryErrors.InsufficientStock, "Kho trong không đủ hàng",
            $"{db.InventoryItems.AsNoTracking().Where(i => i.Id == s.ItemId).Select(i => i.Name).FirstOrDefault()}: kho trong còn {s.Available}, cần {s.Needed}.");
}
