using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class ShiftClosing : Entity
{
    public Guid ShiftId { get; set; }
    public Guid ClosedByEmployeeId { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public decimal Difference { get; set; }
    public string? Note { get; set; }
    public string Denominations { get; set; } = "{}";
    public ClosingStatus Status { get; set; } = ClosingStatus.Draft;
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public Shift Shift { get; set; } = null!;
    public Employee ClosedByEmployee { get; set; } = null!;
}
