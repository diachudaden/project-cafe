namespace ChonRieng.Domain.Payments;

public enum PaymentMethod : byte
{
    VietQr = 1,
    Cash = 2,
    ManualTransfer = 3,
}

/// <summary>Kết quả khớp một giao dịch SePay với bill.</summary>
public enum PaymentMatchStatus : byte
{
    Matched = 1,          // đúng tiền, bill đã Paid
    Overpaid = 2,         // dư tiền, vẫn coi là đã trả
    Underpaid = 3,        // thiếu tiền, bill vẫn Pending
    Unmatched = 4,        // không tìm được bill (khách xoá mã, mã sai)
    Duplicate = 5,        // bill đã Paid trước đó (khách chuyển 2 lần)
    ManuallyAssigned = 6, // nhân viên gán tay cho bill
}

public enum LedgerDirection : byte
{
    In = 1,   // Thu
    Out = 2,  // Chi
}

public enum LedgerCategory : byte
{
    SeatSale = 1,     // Bán chỗ ngồi (gói 4 tiếng + ly tự pha)
    DrinkExtra = 2,   // Gọi thêm đồ uống
    Ingredients = 10, // Nhập nguyên liệu
    Payroll = 11,     // Lương
    Utilities = 12,   // Điện nước, internet
    Rent = 13,        // Mặt bằng
    Refund = 20,      // Hoàn tiền cho khách
    Other = 99,
}

public enum ReconciliationIssueKind : byte
{
    TotalMismatch = 1,
    UnresolvedTransaction = 2,
}
