using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Features.Inventory;

/// <summary>Document codes <c>{PN|PC|PH|KK|DC}-{branch}-{yyMMdd}-{seq:000}</c>; the branch tag is the last 4 hex digits of its id.</summary>
public static class DocumentCodes
{
    public static string Prefix(StockDocumentType type) => type switch
    {
        StockDocumentType.Receipt => "PN",
        StockDocumentType.Transfer => "PC",
        StockDocumentType.Waste => "PH",
        StockDocumentType.Count => "KK",
        StockDocumentType.Adjustment => "DC",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static string BranchTag(Guid branchId) => branchId.ToString("N")[^4..].ToUpperInvariant();

    /// <summary>Next free code for the Vietnam business day of <paramref name="now"/>. The unique index on Code guards races.</summary>
    public static async Task<string> NextAsync(AppDbContext db, StockDocumentType type, Guid branchId, DateTimeOffset now, CancellationToken ct)
    {
        var stem = $"{Prefix(type)}-{BranchTag(branchId)}-{now.ToOffset(TimeSpan.FromHours(7)):yyMMdd}-";
        var pending = db.StockDocuments.Local.Count(d => d.Code.StartsWith(stem, StringComparison.Ordinal) && db.Entry(d).State == EntityState.Added);
        var saved = await db.StockDocuments.CountAsync(d => d.Code.StartsWith(stem), ct);
        return $"{stem}{saved + pending + 1:000}";
    }
}
