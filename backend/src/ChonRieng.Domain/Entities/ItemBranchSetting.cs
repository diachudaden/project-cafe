namespace ChonRieng.Domain.Entities;

/// <summary>Per-branch norm and stock levels for an item. Key: (ItemId, BranchId).</summary>
public sealed class ItemBranchSetting
{
    public Guid ItemId { get; set; }
    public Guid BranchId { get; set; }
    /// <summary>Base units consumed per sold session/extension; learned nightly by RecalcNorms.</summary>
    public decimal NormPerSession { get; set; }
    /// <summary>Set by a manager; wins over <see cref="NormPerSession"/>.</summary>
    public decimal? NormOverride { get; set; }
    public int OuterMin { get; set; }
    public int OuterPar { get; set; }
    public int InnerMin { get; set; }
    public DateTimeOffset? NormLearnedAt { get; set; }

    public InventoryItem Item { get; set; } = null!;
    public Branch Branch { get; set; } = null!;

    public decimal EffectiveNorm => NormOverride ?? NormPerSession;
}
