using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Customer : Entity
{
    public Guid UserId { get; set; }
    public int Points { get; set; }
    public string Tier { get; set; } = "";
    public decimal TotalSpent { get; set; }
    public string? MemberCode { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Session> Sessions { get; } = new List<Session>();
    public ICollection<Bill> ClaimedBills { get; } = new List<Bill>();
    public ICollection<PointTransaction> PointTransactions { get; } = new List<PointTransaction>();
}
