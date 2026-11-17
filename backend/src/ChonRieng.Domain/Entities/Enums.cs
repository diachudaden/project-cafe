namespace ChonRieng.Domain.Entities;

public enum SeatType : byte
{
    Single = 1,
    Group = 2,
    Nap = 3,
}

public enum SeatStatus : byte
{
    Available = 1,
    Occupied = 2,
    Maintenance = 3,
}

public enum ShiftType : byte
{
    Morning = 1,
    Afternoon = 2,
    Evening = 3,
    Night = 4,
}

public enum ShiftStatus : byte
{
    Draft = 1,
    Published = 2,
}

public enum ShiftAssignmentStatus : byte
{
    Offered = 1,
    Accepted = 2,
    Declined = 3,
}

public enum SessionStatus : byte
{
    Active = 1,
    Expired = 2,
    Closed = 3,
}

/// <summary>Who ended a session: staff pressing "Trả chỗ", or the system after the grace period (TASK-024).</summary>
public enum SessionCloseReason : byte
{
    Manual = 1,
    AutoReleased = 2,
}

public enum PaymentMethod : byte
{
    Cash = 1,
    VietQr = 2,
    Mixed = 3,
}

/// <summary>Session = the seat bill (plan, add-ons bought at check-in, extensions); AddOn = one add-on sale after check-in (D34).</summary>
public enum BillKind : byte
{
    Session = 1,
    AddOn = 2,
}

public enum PaymentRequestKind : byte
{
    CheckIn = 1,
    Extension = 2,
    AddOn = 3,
}

public enum PaymentRequestStatus : byte
{
    Pending = 1,
    Paid = 2,
    Expired = 3,
    Cancelled = 4,
    NeedsAttention = 5,
}

public enum PaymentMatchStatus : byte
{
    Unmatched = 1,
    Matched = 2,
    Overpaid = 3,
    Underpaid = 4,
    Duplicate = 5,
}

public enum ShiftRequestType : byte
{
    Swap = 1,
    Leave = 2,
    Drop = 3,
}

public enum ClosingStatus : byte
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
}

public enum WarehouseType : byte
{
    Inner = 1,
    Outer = 2,
}

public enum StockDocumentType : byte
{
    Receipt = 1,
    Transfer = 2,
    Waste = 3,
    Count = 4,
    Adjustment = 5,
}

public enum StockDocumentStatus : byte
{
    Draft = 1,
    Confirmed = 2,
    PendingApproval = 3,
    Approved = 4,
    Rejected = 5,
}

public enum HandoverStatus : byte
{
    Waiting = 1,
    Accepted = 2,
    Disputed = 3,
    Resolved = 4,
}

public enum InventoryIssueKind : byte
{
    Normal = 1,
    WithinTolerance = 2,
    TransferMisrecorded = 3,
    RealLoss = 4,
    UnexplainedSurplus = 5,
}

public enum InventoryIssueStatus : byte
{
    Open = 1,
    Resolved = 2,
}
