using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class EmployeeBranch : Entity
{
    public Guid EmployeeId { get; set; }
    public Guid BranchId { get; set; }
    public bool IsHome { get; set; }

    public Employee Employee { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
}
