using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class ShiftRequest : Entity
{
    public ShiftRequestType Type { get; set; }
    public Guid ShiftId { get; set; }
    public Guid RequesterEmployeeId { get; set; }
    public Guid? TargetEmployeeId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
    public Guid? ReviewedByEmployeeId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Shift Shift { get; set; } = null!;
    public Employee RequesterEmployee { get; set; } = null!;
    public Employee? TargetEmployee { get; set; }
    public Employee? ReviewedByEmployee { get; set; }
}
