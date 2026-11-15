using ChonRieng.Domain.Entities;
using ChonRieng.Domain.Inventory;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>Live outer-warehouse figures of one item (TASK-027 §3).</summary>
public sealed record OuterFigures(Guid ItemId, int Opening, int TransfersIn, int Waste, int Sessions, decimal Norm, decimal Usage, int Expected);

/// <summary>
/// Opening = book stock of the outer warehouse right after the previous shift count was confirmed
/// (which equals its counted quantity) plus any later adjustment of that count (dispute resolution, DC);
/// with no previous count, the book stock at the shift start.
/// TransfersIn/Waste = outer movements after that point (adjustments of other documents count as transfers).
/// Sessions = sessions started + extensions sold in the shift up to <c>until</c>.
/// </summary>
public sealed class OuterStockCalculator(AppDbContext db)
{
    public async Task<IReadOnlyDictionary<Guid, OuterFigures>> ComputeAsync(
        Guid branchId, Guid outerWarehouseId, Shift? shift, DateTimeOffset until, CancellationToken ct)
    {
        var shiftId = shift?.Id;
        var previous = await db.StockDocuments.AsNoTracking()
            .Where(d => d.Type == StockDocumentType.Count && d.ToWarehouseId == outerWarehouseId && d.ShiftId != null
                && d.ShiftId != shiftId && d.ConfirmedAt != null && d.ConfirmedAt <= until
                && (d.Status == StockDocumentStatus.Confirmed || d.Status == StockDocumentStatus.PendingApproval
                    || d.Status == StockDocumentStatus.Approved))
            .OrderByDescending(d => d.ConfirmedAt)
            .Select(d => new { d.Id, ConfirmedAt = d.ConfirmedAt!.Value })
            .FirstOrDefaultAsync(ct);
        var from = previous?.ConfirmedAt ?? shift?.StartAt ?? until;
        var previousId = previous?.Id;

        var book = await db.StockMovements.AsNoTracking()
            .Where(m => m.WarehouseId == outerWarehouseId && m.OccurredAt <= from)
            .GroupBy(m => m.ItemId)
            .Select(g => new { ItemId = g.Key, Qty = g.Sum(m => m.Qty) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Qty, ct);
        var window = await db.StockMovements.AsNoTracking()
            .Where(m => m.WarehouseId == outerWarehouseId && m.OccurredAt > from && m.OccurredAt <= until)
            .Select(m => new { m.ItemId, m.Qty, m.Document.Type, m.Document.AdjustsDocumentId })
            .ToListAsync(ct);

        var sessionsFrom = shift?.StartAt ?? from;
        var sessionsTo = shift is null || until < shift.EndAt ? until : shift.EndAt;
        var sessions = await CountSessionsAsync(branchId, sessionsFrom, sessionsTo, ct);

        var settings = await db.ItemBranchSettings.AsNoTracking()
            .Where(s => s.BranchId == branchId && s.Item.IsActive)
            .Select(s => new { s.ItemId, s.NormPerSession, s.NormOverride })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, OuterFigures>();
        foreach (var setting in settings)
        {
            var moves = window.Where(m => m.ItemId == setting.ItemId).ToList();
            var correction = moves.Where(m => previousId != null && m.AdjustsDocumentId == previousId).Sum(m => m.Qty);
            var others = moves.Where(m => previousId == null || m.AdjustsDocumentId != previousId).ToList();
            var opening = book.GetValueOrDefault(setting.ItemId) + correction;
            var transfersIn = others.Where(m => m.Type is StockDocumentType.Transfer or StockDocumentType.Adjustment or StockDocumentType.Receipt).Sum(m => m.Qty);
            var waste = -others.Where(m => m.Type == StockDocumentType.Waste).Sum(m => m.Qty);
            var norm = setting.NormOverride ?? setting.NormPerSession;
            var usage = InventoryMath.Usage(sessions, norm);
            result[setting.ItemId] = new OuterFigures(setting.ItemId, opening, transfersIn, waste, sessions, norm, usage,
                InventoryMath.Expected(opening, transfersIn, waste, usage));
        }

        return result;
    }

    /// <summary>Sessions started + extensions sold at the branch in [from, to): each includes one drink.</summary>
    public async Task<int> CountSessionsAsync(Guid branchId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (to <= from)
        {
            return 0;
        }

        var started = await db.Sessions.CountAsync(s => s.Seat.Zone.BranchId == branchId && s.StartAt >= from && s.StartAt < to, ct);
        var extended = await db.SessionExtensions.CountAsync(e => e.Session.Seat.Zone.BranchId == branchId && e.CreatedAt >= from && e.CreatedAt < to, ct);
        return started + extended;
    }
}
