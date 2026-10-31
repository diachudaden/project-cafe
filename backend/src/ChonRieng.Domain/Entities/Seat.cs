using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Seat : Entity
{
    public Guid ZoneId { get; set; }
    public string Code { get; set; } = "";
    public SeatType Type { get; set; }
    public SeatStatus Status { get; set; } = SeatStatus.Available;

    public Zone Zone { get; set; } = null!;
    public ICollection<Session> Sessions { get; } = new List<Session>();
}
