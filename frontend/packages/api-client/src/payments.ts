// Client + kiểu dữ liệu cho module thanh toán. Khớp với backend ChonRieng.*.Payments.
// Enum gửi dạng số (mặc định của System.Text.Json), nên giữ đúng giá trị như C#.

export const PaymentMethod = { VietQr: 1, Cash: 2, ManualTransfer: 3 } as const;
export type PaymentMethod = (typeof PaymentMethod)[keyof typeof PaymentMethod];

export const PaymentMatchStatus = {
  Matched: 1,
  Overpaid: 2,
  Underpaid: 3,
  Unmatched: 4,
  Duplicate: 5,
  ManuallyAssigned: 6,
} as const;
export type PaymentMatchStatus = (typeof PaymentMatchStatus)[keyof typeof PaymentMatchStatus];

export const LedgerDirection = { In: 1, Out: 2 } as const;
export type LedgerDirection = (typeof LedgerDirection)[keyof typeof LedgerDirection];

export const LedgerCategory = {
  SeatSale: 1,
  DrinkExtra: 2,
  Ingredients: 10,
  Payroll: 11,
  Utilities: 12,
  Rent: 13,
  Refund: 20,
  Other: 99,
} as const;
export type LedgerCategory = (typeof LedgerCategory)[keyof typeof LedgerCategory];

export const SummaryPeriod = { Day: 0, Week: 1, Month: 2, Quarter: 3, Year: 4 } as const;
export type SummaryPeriod = (typeof SummaryPeriod)[keyof typeof SummaryPeriod];

export const paymentMethodLabels: Record<PaymentMethod, string> = {
  1: 'VietQR',
  2: 'Tiền mặt',
  3: 'Chuyển khoản tay',
};

export const ledgerCategoryLabels: Record<LedgerCategory, string> = {
  1: 'Bán chỗ ngồi',
  2: 'Đồ uống thêm',
  10: 'Nguyên liệu',
  11: 'Lương',
  12: 'Điện nước, internet',
  13: 'Mặt bằng',
  20: 'Hoàn tiền',
  99: 'Khác',
};

export const matchStatusLabels: Record<PaymentMatchStatus, string> = {
  1: 'Đã khớp',
  2: 'Chuyển dư',
  3: 'Chuyển thiếu',
  4: 'Không tìm thấy bill',
  5: 'Chuyển 2 lần',
  6: 'Đã gán tay',
};

/** Lý do trong sự kiện PaymentNeedsAttention → câu tiếng Việt cho nhân viên. */
export function attentionReasonLabel(reason: string): string {
  if (reason === 'NO_SEAT') return 'Đã nhận tiền nhưng hết chỗ trống — cần xếp chỗ tay';
  if (reason === 'Underpaid') return 'Khách chuyển thiếu tiền';
  if (reason === 'Unmatched') return 'Có tiền vào nhưng không khớp bill nào';
  if (reason === 'Duplicate') return 'Khách chuyển 2 lần cho cùng bill — cần hoàn tiền';
  if (reason.startsWith('RECONCILE:')) return 'Đối soát cuối ngày có chênh lệch';
  return reason;
}

// ---------------------------------------------------------------- DTO

export interface PaymentQrDto {
  billId: number;
  code: string;
  amount: number;
  qrUrl: string;
  expiresAtUtc: string;
  bank: string;
  accountNumber: string;
  accountHolder: string;
}

export interface PaymentResultDto {
  billId: number;
  amount: number;
  method: PaymentMethod;
  seatId: number | null;
  seatCode: string | null;
  seatIsDefault: boolean;
  needsAttention: boolean;
}

export interface PaymentTransactionDto {
  id: number;
  amount: number;
  gateway: string;
  content: string;
  code: string | null;
  transactionAtUtc: string;
  matchStatus: PaymentMatchStatus;
  billId: number | null;
  note: string | null;
  needsAttention: boolean;
}

export interface LedgerEntryDto {
  id: number;
  branchId: number;
  occurredAtUtc: string;
  direction: LedgerDirection;
  amount: number;
  signedAmount: number;
  category: LedgerCategory;
  method: PaymentMethod;
  billId: number | null;
  paymentTransactionId: number | null;
  shiftId: number | null;
  createdBy: number | null;
  note: string | null;
  reversalOfId: number | null;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CategoryTotal {
  category: LedgerCategory;
  direction: LedgerDirection;
  total: number;
}

export interface PeriodSummary {
  periodStart: string;
  periodEnd: string;
  label: string;
  totalIn: number;
  totalOut: number;
  net: number;
  byCategory: CategoryTotal[];
}

export interface LedgerSummaryDto {
  period: SummaryPeriod;
  from: string;
  to: string;
  totalIn: number;
  totalOut: number;
  net: number;
  periods: PeriodSummary[];
}

export interface ApiError {
  code: string;
  message: string;
}

// ---------------------------------------------------------------- Sự kiện realtime

export const PaymentEvents = {
  PaymentReceived: 'PaymentReceived',
  SeatAssigned: 'SeatAssigned',
  PaymentNeedsAttention: 'PaymentNeedsAttention',
  SeatHoldExpired: 'SeatHoldExpired',
} as const;

export interface PaymentReceivedEvent {
  billId: number;
  amount: number;
  method: PaymentMethod;
  paidAtUtc: string;
}

export interface SeatAssignedEvent {
  billId: number;
  seatId: number;
  seatCode: string;
  isDefault: boolean;
}

export interface PaymentNeedsAttentionEvent {
  transactionId: number | null;
  billId: number | null;
  reason: string;
  amount: number;
}

export interface SeatHoldExpiredEvent {
  billId: number;
  seatId: number;
}

// ---------------------------------------------------------------- HTTP

/**
 * INTEGRATE: thay bằng fetcher của api-client (có gắn JWT, baseURL).
 * Hàm phải ném `PaymentApiError` khi status không 2xx.
 */
export type Fetcher = (path: string, init?: RequestInit) => Promise<Response>;

export class PaymentApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = 'PaymentApiError';
  }
}

async function json<T>(res: Response): Promise<T> {
  if (res.ok) return (await res.json()) as T;
  let body: Partial<ApiError> = {};
  try {
    body = (await res.json()) as ApiError;
  } catch {
    /* body rỗng */
  }
  throw new PaymentApiError(res.status, body.code ?? `HTTP_${res.status}`, body.message ?? res.statusText);
}

function query(params: Record<string, string | number | undefined | null>): string {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') q.set(k, String(v));
  const s = q.toString();
  return s ? `?${s}` : '';
}

const post = (body?: unknown): RequestInit => ({
  method: 'POST',
  headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
  body: body === undefined ? undefined : JSON.stringify(body),
});

export interface LedgerQuery {
  branchId?: number;
  from?: string; // yyyy-MM-dd giờ VN
  to?: string;
  direction?: LedgerDirection;
  category?: LedgerCategory;
  method?: PaymentMethod;
  page?: number;
  pageSize?: number;
}

export function createPaymentsApi(fetcher: Fetcher) {
  return {
    createQr: (billId: number) => fetcher(`/api/payments/bills/${billId}/qr`, post()).then(json<PaymentQrDto>),

    payCash: (billId: number) => fetcher(`/api/payments/bills/${billId}/cash`, post()).then(json<PaymentResultDto>),

    listNeedingAttention: () =>
      fetcher('/api/payments/transactions/attention').then(json<PaymentTransactionDto[]>),

    assignTransaction: (transactionId: number, billId: number, note?: string) =>
      fetcher(`/api/payments/transactions/${transactionId}/assign`, post({ billId, note })).then(json<PaymentResultDto>),

    dismissTransaction: (transactionId: number, note: string) =>
      fetcher(`/api/payments/transactions/${transactionId}/dismiss`, post({ note })).then(json<PaymentTransactionDto>),

    listLedger: (q: LedgerQuery) =>
      fetcher(`/api/ledger${query({ ...q })}`).then(json<PagedResult<LedgerEntryDto>>),

    addExpense: (body: {
      branchId: number;
      amount: number;
      category: LedgerCategory;
      method: PaymentMethod;
      occurredAtUtc?: string;
      note?: string;
    }) => fetcher('/api/ledger/expenses', post(body)).then(json<LedgerEntryDto>),

    reverseLedger: (id: number, reason: string) =>
      fetcher(`/api/ledger/${id}/reverse`, post({ reason })).then(json<LedgerEntryDto>),

    ledgerSummary: (q: { branchId?: number; period: SummaryPeriod; from: string; to: string }) =>
      fetcher(`/api/ledger/summary${query(q)}`).then(json<LedgerSummaryDto>),
  };
}

export type PaymentsApi = ReturnType<typeof createPaymentsApi>;

export const paymentKeys = {
  all: ['payments'] as const,
  attention: () => [...paymentKeys.all, 'attention'] as const,
  ledger: (q: LedgerQuery) => ['ledger', 'list', q] as const,
  ledgerSummary: (q: object) => ['ledger', 'summary', q] as const,
  bill: (billId: number) => ['bills', billId] as const,
  seats: () => ['seats'] as const,
};

export const formatVnd = (n: number) => `${n.toLocaleString('vi-VN')}đ`;
