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

// ---- Counts (shift close: outer; weekly: inner) ----
/// <summary>Blind count sheet: deliberately has no expected quantity.</summary>
public sealed record CountItemView(
    Guid ItemId, string Name, string BaseUnit, IReadOnlyList<ItemUnitView> Units, int SortOrder,
    [property: Description("Số đã nhập (nháp) của chính người đếm.")] int? CountedQty);

public sealed record CountSheetView(
    Guid CountId, string Code, WarehouseType Warehouse, Guid? ShiftId, StockDocumentStatus Status, IReadOnlyList<CountItemView> Items);

public sealed record OpenShiftCountRequest(Guid ShiftId);

public sealed record OpenInnerCountRequest(Guid BranchId);

public sealed record CountLineInput(Guid ItemId, [property: Range(0, 1_000_000)] int CountedQty);

public sealed record SaveCountLinesRequest([property: Required] IReadOnlyList<CountLineInput> Lines);

public sealed record CountSummaryRow(
    Guid ItemId, string Name, string BaseUnit, IReadOnlyList<ItemUnitView> Units,
    [property: Description("Đầu ca: số đếm ca trước (kho ngoài) / tồn sổ (kho trong).")] int Opening,
    int TransfersIn, int Waste, int Sessions, decimal Norm, decimal Usage,
    int Expected, int? FirstCounted, int? Counted, int? Variance, int Tolerance, bool? WithinTolerance);

public sealed record CountedOutOfTolerance(Guid ItemId, string Name, string BaseUnit, int Expected, int Counted, int Variance, int Tolerance);

public sealed record HandoverLine(Guid ItemId, int CountedQty);

public sealed record HandoverInfo(
    Guid Id, HandoverStatus Status, string? ReceivedByName, DateTimeOffset? ReceivedAt, string? DisputeNote,
    IReadOnlyList<HandoverLine> DisputeLines, string? ResolvedByName, DateTimeOffset? ResolvedAt, string? ResolveNote);

public sealed record CountSummaryView(
    Guid CountId, string Code, WarehouseType Warehouse, StockDocumentStatus Status,
    Guid BranchId, string BranchName, Guid? ShiftId, ShiftType? ShiftType, DateTimeOffset? ShiftStart, DateTimeOffset? ShiftEnd,
    string CountedByName, string? ConfirmedByName, DateTimeOffset? ConfirmedAt,
    [property: Description("Số lượt bán + gia hạn trong ca.")] int Sessions,
    string? Reason, string? ApprovalNote,
    [property: Description("Số món trong ngưỡng / tổng số món.")] int WithinCount, int ItemCount,
    IReadOnlyList<CountSummaryRow> Rows, HandoverInfo? Handover, string? PayloadHash);

public sealed record ConfirmCountRequest(
    [property: MaxLength(1000), Description("Bắt buộc (≥ 10 ký tự) khi có món vượt ngưỡng.")] string? Reason);

public sealed record ConfirmCountResult(
    Guid CountId, StockDocumentStatus Status,
    [property: Description("true khi phiếu đã được xác nhận trước đó (gọi lại không ghi thêm).")] bool AlreadyConfirmed,
    IReadOnlyList<CountedOutOfTolerance> OutOfTolerance);

public sealed record DisputeHandoverRequest(
    [property: Required] IReadOnlyList<CountLineInput> Lines,
    [property: Required, MaxLength(1000)] string Note);

public sealed record ResolveLineInput(Guid ItemId, [property: Range(0, 1_000_000)] int FinalQty);

public sealed record ResolveHandoverRequest(
    [property: Required] IReadOnlyList<ResolveLineInput> Lines,
    [property: Required, MaxLength(1000)] string Note);

public sealed record PendingHandoverView(
    Guid CountId, string Code, Guid FromShiftId, ShiftType ShiftType, DateTimeOffset ShiftStart, DateTimeOffset ShiftEnd,
    HandoverStatus Status, StockDocumentStatus CountStatus, string CountedByName, DateTimeOffset ConfirmedAt);

// ---- Review, adjustments, attention ----
public sealed record ReviewDocumentRequest([property: MaxLength(1000), Description("Bắt buộc khi từ chối.")] string? Note);

public sealed record AdjustmentLineInput(Guid ItemId, [property: Description("Số cần sửa (±) so với phiếu gốc, đơn vị gốc.")] int QtyDelta);

public sealed record CreateAdjustmentRequest(
    Guid AdjustsDocumentId,
    [property: Required] IReadOnlyList<AdjustmentLineInput> Lines,
    [property: Required, MaxLength(1000)] string Reason);

public sealed record ResolveIssueRequest([property: Required, MaxLength(1000)] string Note);

public sealed record AttentionCount(
    Guid CountId, string Code, ShiftType? ShiftType, DateTimeOffset? ShiftStart, DateTimeOffset? ShiftEnd,
    string ConfirmedByName, DateTimeOffset ConfirmedAt, string? Reason, int OutOfTolerance);

public sealed record AttentionHandover(
    Guid CountId, string Code, HandoverStatus Status, ShiftType? ShiftType, DateTimeOffset? ShiftStart, DateTimeOffset? ShiftEnd,
    DateTimeOffset CreatedAt, int MinutesWaiting, string? ReceivedByName, string? DisputeNote);

public sealed record InventoryIssueView(
    Guid Id, Guid ItemId, string ItemName, string BaseUnit, InventoryIssueKind Kind, DateTimeOffset PeriodFrom, DateTimeOffset PeriodTo,
    int InnerVar, int OuterVar, int Total, int Tolerance, InventoryIssueStatus Status, Guid? AdjustmentDocumentId,
    DateTimeOffset CreatedAt, string? Note);

public sealed record AttentionView(
    IReadOnlyList<AttentionCount> PendingCounts,
    [property: Description("Bàn giao tranh chấp hoặc chờ quá 60 phút.")] IReadOnlyList<AttentionHandover> Handovers,
    [property: Description("Phiếu điều chỉnh nháp do job đối soát đề xuất.")] IReadOnlyList<StockDocumentView> DraftAdjustments,
    [property: Description("Ghi phiếu sai (TransferMisrecorded).")] IReadOnlyList<InventoryIssueView> MisrecordedIssues,
    [property: Description("Hao hụt thật / dư không rõ (RealLoss, UnexplainedSurplus).")] IReadOnlyList<InventoryIssueView> LossIssues,
    int Total);
