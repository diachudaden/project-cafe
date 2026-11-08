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

public enum PaymentMethod : byte
{
    Cash = 1,
    VietQr = 2,
}

public enum PaymentRequestKind : byte
{
    CheckIn = 1,
    Extension = 2,
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
