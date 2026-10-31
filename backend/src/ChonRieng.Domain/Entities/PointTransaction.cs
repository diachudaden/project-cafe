using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class PointTransaction : Entity
{
    public Guid CustomerId { get; set; }
    public Guid? BillId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = "";

    public Customer Customer { get; set; } = null!;
    public Bill? Bill { get; set; }
}
