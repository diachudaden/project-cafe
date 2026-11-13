using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

/// <summary>An add-on item sold at the counter (mì, trứng, bánh, nước...). <c>BranchId</c> null = whole chain.</summary>
public sealed class Product : Entity
{
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Branch? Branch { get; set; }
}
