using ChonRieng.Domain.Common;

namespace ChonRieng.Domain.Entities;

public sealed class Shift : Entity
{
    public Guid BranchId { get; set; }
    public DateOnly Date { get; set; }
    public ShiftType Type { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public int RequiredStaff { get; set; }
    public bool IsOpen { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Draft;
    public byte[] RowVersion { get; set; } = [];

    public Branch Branch { get; set; } = null!;
    public ICollection<ShiftAssignment> Assignments { get; } = new List<ShiftAssignment>();
    public ICollection<Attendance> Attendances { get; } = new List<Attendance>();
    public ShiftClosing? Closing { get; set; }
    public ICollection<Bill> Bills { get; } = new List<Bill>();
}
