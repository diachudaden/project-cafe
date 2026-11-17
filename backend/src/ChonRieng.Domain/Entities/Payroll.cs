using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Payroll : Entity
{
    public Guid EmployeeId { get; set; }
    public DateOnly Month { get; set; }
    public decimal TotalHours { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal Allowance { get; set; }
    public decimal Bonus { get; set; }
    public decimal Deduction { get; set; }
    public decimal NetAmount { get; set; }
    public string Status { get; set; } = "";

    public Employee Employee { get; set; } = null!;
}
