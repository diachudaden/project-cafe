using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Zone : Entity
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = "";

    public Branch Branch { get; set; } = null!;
    public ICollection<Seat> Seats { get; } = new List<Seat>();
}
