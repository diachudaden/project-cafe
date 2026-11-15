import type { PaymentMethod } from "./features";
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
  };
}

export type InventoryApi = ReturnType<typeof createInventoryApi>;
