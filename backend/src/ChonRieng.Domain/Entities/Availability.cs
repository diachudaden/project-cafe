using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Availability : Entity
{
    public Guid EmployeeId { get; set; }
    public bool IsRecurring { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public DateOnly? SpecificDate { get; set; }
    public TimeOnly From { get; set; }
    public TimeOnly To { get; set; }

    public Employee Employee { get; set; } = null!;
}
