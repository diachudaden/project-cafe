using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

/// <summary>A larger packing unit ("Thùng", "Hộp") worth <see cref="Factor"/> base units.</summary>
public sealed class ItemUnit : Entity
{
    public Guid ItemId { get; set; }
    public string UnitName { get; set; } = "";
    public int Factor { get; set; }

    public InventoryItem Item { get; set; } = null!;
}
