using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

/// <summary>Each branch has exactly one <see cref="WarehouseType.Inner"/> (storeroom) and one <see cref="WarehouseType.Outer"/> (self-serve bar).</summary>
public sealed class Warehouse : Entity
{
    public Guid BranchId { get; set; }
    public WarehouseType Type { get; set; }
    public string Name { get; set; } = "";

    public Branch Branch { get; set; } = null!;
}
