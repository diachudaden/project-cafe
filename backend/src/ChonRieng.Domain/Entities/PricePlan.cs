using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class PricePlan : Entity
{
    public Guid? BranchId { get; set; }
    public string Name { get; set; } = "";
    public int DurationMinutes { get; set; } = 240;
    public decimal Price { get; set; }
    public decimal ExtendPrice { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }

    public Branch? Branch { get; set; }
    public ICollection<Session> Sessions { get; } = new List<Session>();
}
