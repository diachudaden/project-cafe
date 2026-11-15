using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

/// <summary>A stock item counted in base units (gói, túi, lon, cái). Separate from <see cref="Product"/> (items sold at the counter).</summary>
public sealed class InventoryItem : Entity
{
    public string Name { get; set; } = "";
    public string BaseUnit { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsActive { get; set; } = true;
    /// <summary>True: counted at every shift close in the outer warehouse; false: counted on a rotation.</summary>
    public bool CountDaily { get; set; } = true;
    /// <summary>Shelf order used by the count screen.</summary>
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<ItemUnit> Units { get; } = new List<ItemUnit>();
    public ICollection<ItemBranchSetting> BranchSettings { get; } = new List<ItemBranchSetting>();
}
