using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class ShiftAssignment : Entity
{
    public Guid ShiftId { get; set; }
    public Guid EmployeeId { get; set; }
    public ShiftAssignmentStatus Status { get; set; } = ShiftAssignmentStatus.Offered;
    public DateTimeOffset? RespondedAt { get; set; }

    public Shift Shift { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
