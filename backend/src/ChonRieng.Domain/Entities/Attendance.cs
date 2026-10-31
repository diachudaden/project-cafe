using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Attendance : Entity
{
    public Guid EmployeeId { get; set; }
    public Guid ShiftId { get; set; }
    public DateTimeOffset? CheckInAt { get; set; }
    public DateTimeOffset? CheckOutAt { get; set; }
    public string Method { get; set; } = "";
    public bool Approved { get; set; }

    public Employee Employee { get; set; } = null!;
    public Shift Shift { get; set; } = null!;
}
