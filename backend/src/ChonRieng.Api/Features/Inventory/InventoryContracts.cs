using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Features.Inventory;

// ---- Items & settings ----
public sealed record ItemUnitView(string UnitName, int Factor);

public sealed record InventoryItemView(
    Guid Id, string Name, string BaseUnit, string Category, bool IsActive, bool CountDaily, int SortOrder,
    IReadOnlyList<ItemUnitView> Units);

public sealed record ItemUnitInput(
    [property: Required, MaxLength(30)] string UnitName,
    [property: Range(2, 100_000), Description("Số đơn vị gốc trong 1 đơn vị này, ví dụ Thùng = 480 gói.")] int Factor);

public sealed record SaveInventoryItemRequest(
    [property: Required, MaxLength(100)] string Name,
    [property: Required, MaxLength(30), Description("Đơn vị gốc: gói, túi, lon, cái…")] string BaseUnit,
    [property: Required, MaxLength(50)] string Category,
    bool CountDaily,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<ItemUnitInput>? Units);

public sealed record BranchSettingView(
    Guid ItemId, string ItemName, string BaseUnit, bool IsActive,
    [property: Description("Định mức tự học (job RecalcNorms).")] decimal NormPerSession,
    [property: Description("Định mức do quản lý đặt; ưu tiên hơn định mức tự học.")] decimal? NormOverride,
    decimal EffectiveNorm, int OuterMin, int OuterPar, int InnerMin, DateTimeOffset? NormLearnedAt);

public sealed record SaveBranchSettingRequest(
    [property: Range(0, 99999), Description("Định mức ban đầu; job đêm sẽ học lại.")] decimal? NormPerSession,
    [property: Range(0, 99999)] decimal? NormOverride,
    [property: Range(0, 1_000_000)] int OuterMin,
    [property: Range(0, 1_000_000)] int OuterPar,
    [property: Range(0, 1_000_000)] int InnerMin);

public sealed record BalanceView(
    Guid ItemId, string ItemName, string BaseUnit, int Qty,
    [property: Description("Kho trong: InnerMin; kho ngoài: OuterMin.")] int Min,
    int? Par, bool BelowMin,
    [property: Description("Chỉ kho ngoài: tồn dự kiến lúc này (đầu ca + chuyển vào − huỷ − lượt × định mức).")] int? ExpectedNow);

// ---- Documents ----
public sealed record StockLineView(
    Guid ItemId, string ItemName, string BaseUnit, int Qty, string? UnitName, int? UnitQty, decimal? UnitCost,
    DateOnly? ExpiryDate, int? SuggestedQty);

public sealed record StockDocumentView(
    Guid Id, string Code, StockDocumentType Type, StockDocumentStatus Status, Guid BranchId,
    WarehouseType? FromWarehouse, WarehouseType? ToWarehouse, Guid? ShiftId,
    string? Note, string? Reason, string? Supplier, string? InvoiceNo, string? PhotoUrl,
    string CreatedByName, DateTimeOffset CreatedAt, DateTimeOffset? ConfirmedAt, Guid? AdjustsDocumentId,
    [property: Description("Phiếu nhập đã thanh toán: khoản chi đã ghi.")] Guid? ExpenseId,
    decimal? Total,
    IReadOnlyList<StockLineView> Lines);

public sealed record ReceiptLineInput(
    Guid ItemId,
    [property: MaxLength(30), Description("Null = đơn vị gốc.")] string? UnitName,
    [property: Range(1, 1_000_000)] int UnitQty,
    [property: Range(0, 1_000_000_000), Description("Đơn giá theo đơn vị đã chọn (VND).")] decimal UnitCost,
    DateOnly? ExpiryDate);

public sealed record CreateReceiptRequest(
    Guid BranchId,
    [property: MaxLength(200)] string? Supplier,
    [property: MaxLength(100)] string? InvoiceNo,
    [property: Required] IReadOnlyList<ReceiptLineInput> Lines,
    [property: Description("true: ghi 1 khoản chi 'Nguyên liệu' vào sổ chi.")] bool Paid,
    PaymentMethod? Method,
    [property: MaxLength(500)] string? PhotoUrl,
    [property: MaxLength(1000)] string? Note);

public sealed record QtyLineInput(Guid ItemId, [property: Range(1, 1_000_000)] int Qty);

public sealed record CreateTransferRequest(
    Guid BranchId,
    [property: Required] IReadOnlyList<QtyLineInput> Lines,
    [property: MaxLength(1000), Description("Bắt buộc khi vượt gợi ý hoặc vượt mức tối đa kho ngoài (không chặn).")] string? Reason);

public sealed record CreateWasteRequest(
    Guid BranchId,
    WarehouseType Warehouse,
    [property: Required] IReadOnlyList<QtyLineInput> Lines,
    [property: Required, MaxLength(1000)] string Reason,
    [property: MaxLength(500)] string? PhotoUrl);

public sealed record TransferSuggestion(
    Guid ItemId, string ItemName, string BaseUnit, IReadOnlyList<ItemUnitView> Units,
    int ExpectedNow, int Par, int Min, int Suggested, int InnerQty);
