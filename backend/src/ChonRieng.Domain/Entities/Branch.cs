using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Branch : Entity
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool Is24h { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Zone> Zones { get; } = new List<Zone>();
    public ICollection<PricePlan> PricePlans { get; } = new List<PricePlan>();
    public ICollection<Shift> Shifts { get; } = new List<Shift>();
}
