import type { PaymentMethod, ShiftType } from "./features";
import { query, type Http } from "./http";

// Types mirror backend/src/ChonRieng.Api/Features/Inventory/*Contracts.cs (TASK-027). Quantities are base units.

export type WarehouseType = "Inner" | "Outer";
export type StockDocumentType = "Receipt" | "Transfer" | "Waste" | "Count" | "Adjustment";
export type StockDocumentStatus = "Draft" | "Confirmed" | "PendingApproval" | "Approved" | "Rejected";

/** Business error codes in `ApiError.violations[0].rule`. */
export const inventoryErrors = {
  shiftNotOpen: "SHIFT_NOT_OPEN",
  countAlreadyConfirmed: "COUNT_ALREADY_CONFIRMED",
  reasonRequired: "REASON_REQUIRED",
  insufficientStock: "INSUFFICIENT_STOCK",
  notYourShift: "NOT_YOUR_SHIFT",
  countInProgress: "COUNT_IN_PROGRESS",
  countIncomplete: "COUNT_INCOMPLETE",
} as const;

export interface ItemUnit { unitName: string; factor: number }
export interface InventoryItem {
  id: string; name: string; baseUnit: string; category: string; isActive: boolean; countDaily: boolean; sortOrder: number; units: ItemUnit[];
}
export interface InventoryItemInput {
  name: string; baseUnit: string; category: string; countDaily: boolean; sortOrder: number; isActive: boolean; units: ItemUnit[];
}
export interface BranchSetting {
  itemId: string; itemName: string; baseUnit: string; isActive: boolean;
  normPerSession: number; normOverride: number | null; effectiveNorm: number;
  outerMin: number; outerPar: number; innerMin: number; normLearnedAt: string | null;
}
export interface BranchSettingInput { normPerSession?: number | null; normOverride: number | null; outerMin: number; outerPar: number; innerMin: number }
export interface StockBalance {
  itemId: string; itemName: string; baseUnit: string; qty: number; min: number; par: number | null; belowMin: boolean; expectedNow: number | null;
}
export interface StockLine {
  itemId: string; itemName: string; baseUnit: string; qty: number; unitName: string | null; unitQty: number | null;
  unitCost: number | null; expiryDate: string | null; suggestedQty: number | null;
}
export interface StockDocument {
  id: string; code: string; type: StockDocumentType; status: StockDocumentStatus; branchId: string;
  fromWarehouse: WarehouseType | null; toWarehouse: WarehouseType | null; shiftId: string | null;
  note: string | null; reason: string | null; supplier: string | null; invoiceNo: string | null; photoUrl: string | null;
  createdByName: string; createdAt: string; confirmedAt: string | null; adjustsDocumentId: string | null;
  expenseId: string | null; total: number | null; lines: StockLine[];
}
export interface ReceiptLineInput { itemId: string; unitName: string | null; unitQty: number; unitCost: number; expiryDate?: string | null }
export interface ReceiptInput {
  branchId: string; supplier: string | null; invoiceNo: string | null; lines: ReceiptLineInput[];
  paid: boolean; method: PaymentMethod | null; photoUrl?: string | null; note?: string | null;
}
export interface QtyLine { itemId: string; qty: number }
export interface TransferSuggestion {
  itemId: string; itemName: string; baseUnit: string; units: ItemUnit[];
  expectedNow: number; par: number; min: number; suggested: number; innerQty: number;
}

export type HandoverStatus = "Waiting" | "Accepted" | "Disputed" | "Resolved";

/** Blind count sheet: never carries an expected quantity. */
export interface CountItem { itemId: string; name: string; baseUnit: string; units: ItemUnit[]; sortOrder: number; countedQty: number | null }
export interface CountSheet { countId: string; code: string; warehouse: WarehouseType; shiftId: string | null; status: StockDocumentStatus; items: CountItem[] }
export interface CountLine { itemId: string; countedQty: number }
export interface CountSummaryRow {
  itemId: string; name: string; baseUnit: string; units: ItemUnit[];
  opening: number; transfersIn: number; waste: number; sessions: number; norm: number; usage: number;
  expected: number; firstCounted: number | null; counted: number | null; variance: number | null; tolerance: number; withinTolerance: boolean | null;
}
export interface HandoverInfo {
  id: string; status: HandoverStatus; receivedByName: string | null; receivedAt: string | null; disputeNote: string | null;
  disputeLines: CountLine[]; resolvedByName: string | null; resolvedAt: string | null; resolveNote: string | null;
}
export interface CountSummary {
  countId: string; code: string; warehouse: WarehouseType; status: StockDocumentStatus;
  branchId: string; branchName: string; shiftId: string | null; shiftType: ShiftType | null; shiftStart: string | null; shiftEnd: string | null;
  countedByName: string; confirmedByName: string | null; confirmedAt: string | null; sessions: number;
  reason: string | null; approvalNote: string | null; withinCount: number; itemCount: number;
  rows: CountSummaryRow[]; handover: HandoverInfo | null; payloadHash: string | null;
}
export interface OutOfTolerance { itemId: string; name: string; baseUnit: string; expected: number; counted: number; variance: number; tolerance: number }
export interface ConfirmCountResult { countId: string; status: StockDocumentStatus; alreadyConfirmed: boolean; outOfTolerance: OutOfTolerance[] }
export interface PendingHandover {
  countId: string; code: string; fromShiftId: string; shiftType: ShiftType; shiftStart: string; shiftEnd: string;
  status: HandoverStatus; countStatus: StockDocumentStatus; countedByName: string; confirmedAt: string;
}

export type InventoryIssueKind = "Normal" | "WithinTolerance" | "TransferMisrecorded" | "RealLoss" | "UnexplainedSurplus";
export type InventoryIssueStatus = "Open" | "Resolved";
export interface InventoryIssue {
  id: string; itemId: string; itemName: string; baseUnit: string; kind: InventoryIssueKind; periodFrom: string; periodTo: string;
  innerVar: number; outerVar: number; total: number; tolerance: number; status: InventoryIssueStatus;
  adjustmentDocumentId: string | null; createdAt: string; note: string | null;
}
export interface AttentionCount {
  countId: string; code: string; shiftType: ShiftType | null; shiftStart: string | null; shiftEnd: string | null;
  confirmedByName: string; confirmedAt: string; reason: string | null; outOfTolerance: number;
}
export interface AttentionHandover {
  countId: string; code: string; status: HandoverStatus; shiftType: ShiftType | null; shiftStart: string | null; shiftEnd: string | null;
  createdAt: string; minutesWaiting: number; receivedByName: string | null; disputeNote: string | null;
}
export interface Attention {
  pendingCounts: AttentionCount[]; handovers: AttentionHandover[]; draftAdjustments: StockDocument[];
  misrecordedIssues: InventoryIssue[]; lossIssues: InventoryIssue[]; total: number;
}

export function createInventoryApi(http: Http) {
  const get = <T>(path: string) => http.request<T>("GET", path);
  const post = <T>(path: string, body?: unknown) => http.request<T>("POST", path, body ?? {});
  const put = <T>(path: string, body: unknown) => http.request<T>("PUT", path, body);
  const base = "/api/inventory";

  return {
    items: (includeInactive = false) => get<InventoryItem[]>(`${base}/items${query({ includeInactive })}`),
    createItem: (input: InventoryItemInput) => post<InventoryItem>(`${base}/items`, input),
    updateItem: (id: string, input: InventoryItemInput) => put<InventoryItem>(`${base}/items/${id}`, input),
    settings: (branchId: string) => get<BranchSetting[]>(`${base}/branch-settings${query({ branchId })}`),
    saveSetting: (itemId: string, branchId: string, input: BranchSettingInput) =>
      put<BranchSetting>(`${base}/items/${itemId}/branch-settings/${branchId}`, input),
    balances: (branchId: string, warehouse: WarehouseType) => get<StockBalance[]>(`${base}/balances${query({ branchId, warehouse })}`),
    documents: (branchId: string, from: string, to: string, type?: StockDocumentType) =>
      get<StockDocument[]>(`${base}/documents${query({ branchId, from, to, type })}`),
    document: (id: string) => get<StockDocument>(`${base}/documents/${id}`),
    createReceipt: (input: ReceiptInput) => post<StockDocument>(`${base}/receipts`, input),
    transferSuggestion: (branchId: string) => get<TransferSuggestion[]>(`${base}/transfers/suggestion${query({ branchId })}`),
    createTransfer: (branchId: string, lines: QtyLine[], reason: string | null) => post<StockDocument>(`${base}/transfers`, { branchId, lines, reason }),
    createWaste: (branchId: string, warehouse: WarehouseType, lines: QtyLine[], reason: string, photoUrl?: string | null) =>
      post<StockDocument>(`${base}/waste`, { branchId, warehouse, lines, reason, photoUrl }),

    // Counts: shift close (outer) and weekly inner share lines → summary → confirm.
    openShiftCount: (shiftId: string) => post<CountSheet>(`${base}/shift-counts/open`, { shiftId }),
    openInnerCount: (branchId: string) => post<CountSheet>(`${base}/inner-counts/open`, { branchId }),
    saveCountLines: (warehouse: WarehouseType, countId: string, lines: CountLine[]) =>
      put<CountSheet>(`${base}/${countPath(warehouse)}/${countId}/lines`, { lines }),
    countSummary: (warehouse: WarehouseType, countId: string) => get<CountSummary>(`${base}/${countPath(warehouse)}/${countId}/summary`),
    confirmCount: (warehouse: WarehouseType, countId: string, reason: string | null) =>
      post<ConfirmCountResult>(`${base}/${countPath(warehouse)}/${countId}/confirm`, { reason }),
    pendingHandovers: (branchId: string) => get<PendingHandover[]>(`${base}/handovers/pending${query({ branchId })}`),
    acceptHandover: (countId: string) => post<CountSummary>(`${base}/shift-counts/${countId}/accept`),
    disputeHandover: (countId: string, lines: CountLine[], note: string) => post<CountSummary>(`${base}/shift-counts/${countId}/dispute`, { lines, note }),
    resolveHandover: (countId: string, lines: { itemId: string; finalQty: number }[], note: string) =>
      post<CountSummary>(`${base}/shift-counts/${countId}/resolve`, { lines, note }),

    // Manager review (TASK-027 slice 5).
    attention: (branchId: string) => get<Attention>(`${base}/attention${query({ branchId })}`),
    approve: (documentId: string, note: string | null) => post<StockDocument>(`${base}/documents/${documentId}/approve`, { note }),
    reject: (documentId: string, note: string) => post<StockDocument>(`${base}/documents/${documentId}/reject`, { note }),
    createAdjustment: (adjustsDocumentId: string, lines: { itemId: string; qtyDelta: number }[], reason: string) =>
      post<StockDocument>(`${base}/adjustments`, { adjustsDocumentId, lines, reason }),
    issues: (branchId: string, from: string, to: string) => get<InventoryIssue[]>(`${base}/issues${query({ branchId, from, to })}`),
    resolveIssue: (id: string, note: string) => post<InventoryIssue>(`${base}/issues/${id}/resolve`, { note }),
  };
}

const countPath = (warehouse: WarehouseType) => (warehouse === "Outer" ? "shift-counts" : "inner-counts");

export type InventoryApi = ReturnType<typeof createInventoryApi>;
