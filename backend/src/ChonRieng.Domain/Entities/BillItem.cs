using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

/// <summary>One add-on line on a bill. Name and unit price are copied at sale time so menu edits never change past bills.</summary>
public sealed class BillItem : Entity
{
    public Guid BillId { get; set; }
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public Guid? PaymentRequestId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Bill Bill { get; set; } = null!;
    public Product? Product { get; set; }
}
