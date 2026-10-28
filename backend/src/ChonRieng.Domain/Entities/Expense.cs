using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Expense : Entity
{
    public Guid BranchId { get; set; }
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTimeOffset Date { get; set; }
    public string? Note { get; set; }
    public Guid CreatedByEmployeeId { get; set; }

    public Branch Branch { get; set; } = null!;
    public Employee CreatedByEmployee { get; set; } = null!;
}
