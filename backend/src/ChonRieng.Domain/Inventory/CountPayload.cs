using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChonRieng.Domain.Inventory;

/// <summary>Tamper evidence for a locked count: SHA-256 (hex) of its lines + who confirmed + when.</summary>
public static class CountPayload
{
    public sealed record Line(Guid ItemId, int? CountedQty, int? ExpectedQty);

    public static string Hash(IEnumerable<Line> lines, Guid confirmedBy, DateTimeOffset confirmedAt)
    {
        var payload = new
        {
            lines = lines.OrderBy(l => l.ItemId).Select(l => new { itemId = l.ItemId, countedQty = l.CountedQty, expectedQty = l.ExpectedQty }),
            confirmedBy,
            confirmedAt = confirmedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        };
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        return Convert.ToHexStringLower(bytes);
    }
}
