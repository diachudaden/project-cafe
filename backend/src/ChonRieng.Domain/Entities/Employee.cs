using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Employee : Entity
{
    public Guid UserId { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal NightAllowance { get; set; }
    public string PinHash { get; set; } = "";

    public User User { get; set; } = null!;
    public ICollection<EmployeeBranch> Branches { get; } = new List<EmployeeBranch>();
    public ICollection<ShiftAssignment> ShiftAssignments { get; } = new List<ShiftAssignment>();
    public ICollection<Attendance> Attendances { get; } = new List<Attendance>();
    public ICollection<Payroll> Payrolls { get; } = new List<Payroll>();
}
