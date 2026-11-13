import type { SeatStatus, SeatType } from "./admin";
import { query, type Http, type RuleViolation } from "./http";

// Types mirror backend/src/ChonRieng.Api/Features/**/…Contracts.cs. Dates: DateOnly = "YYYY-MM-DD",
// TimeOnly = "HH:mm:ss", DateTimeOffset = ISO 8601. Enums are strings.

export type ShiftType = "Morning" | "Afternoon" | "Evening" | "Night";
export type ShiftStatus = "Draft" | "Published";
export type ShiftPhase = "Draft" | "Published" | "InProgress" | "Closed" | "Approved";
export type AssignmentStatus = "Offered" | "Accepted" | "Declined";
export type SessionStatus = "Active" | "Expired" | "Closed";
export type PaymentMethod = "Cash" | "VietQr" | "Mixed";
export type PaymentRequestStatus = "Pending" | "Paid" | "Expired" | "Cancelled" | "NeedsAttention";
export type ShiftRequestType = "Swap" | "Leave" | "Drop";
export type ClosingStatus = "Draft" | "Submitted" | "Approved" | "Rejected";
export type SeatLiveState = "Available" | "Occupied" | "Held" | "EndingSoon" | "Overdue" | "Maintenance";
export type ReportPeriod = "Week" | "Month" | "Quarter" | "Year";

// ---- Catalog ----
export interface SeatTypeAvailability { type: SeatType; total: number; free: number; maintenance: number }
export interface PublicBranch {
  id: string; name: string; address: string; latitude: number | null; longitude: number | null;
  is24h: boolean; openTime: string; closeTime: string; phone: string | null;
  openNow: boolean; distanceKm: number | null; availability: SeatTypeAvailability[];
}
export interface PricePlanView {
  id: string; branchId: string | null; name: string; durationMinutes: number; price: number; extendPrice: number;
  validFrom: string | null; validTo: string | null; isActive: boolean;
}
export interface SeatSessionInfo { sessionId: string; startAt: string; endAt: string; minutesLeft: number; customerName: string | null; billCode: string; billTotal: number }
export interface FloorSeat { id: string; code: string; type: SeatType; state: SeatLiveState; session: SeatSessionInfo | null }
export interface FloorZone { id: string; name: string; seats: FloorSeat[] }
export interface Floor { branchId: string; branchName: string; zones: FloorZone[] }
export interface PlanInput {
  branchId: string | null; name: string; durationMinutes: number; price: number; extendPrice: number;
  validFrom: string | null; validTo: string | null;
}
export interface SeatEditInput { code: string; type: SeatType; zoneId?: string | null }
export interface ZoneView { id: string; branchId: string; name: string }
export interface SeatView { id: string; zoneId: string; code: string; type: SeatType; status: SeatStatus }

// ---- Scheduling ----
export interface AssignmentView {
  id: string; employeeId: string; employeeName: string; status: AssignmentStatus;
  respondedAt: string | null; declineReason: string | null; overrideReason: string | null;
}
export interface ShiftView {
  id: string; branchId: string; branchName: string; date: string; type: ShiftType; startAt: string; endAt: string;
  requiredStaff: number; isOpen: boolean; status: ShiftStatus; phase: ShiftPhase; rowVersion: string;
  acceptedCount: number; understaffed: boolean; assignments: AssignmentView[];
}
export interface CreateShiftInput { branchId: string; date: string; type: ShiftType; startAt: string; endAt: string; requiredStaff: number; isOpen: boolean }
export type UpdateShiftInput = Omit<CreateShiftInput, "branchId"> & { rowVersion: string };
export interface ValidationResult { allowed: boolean; needsOverrideReason: boolean; violations: RuleViolation[] }
export interface CopyWeekResult { created: number; skipped: number; conflicts: { date: string; type: ShiftType; employeeName: string; violations: RuleViolation[] }[] }
export interface Colleague { employeeId: string; name: string; status: AssignmentStatus }
export interface MyShift {
  assignmentId: string; assignmentStatus: AssignmentStatus; shiftId: string; branchId: string; branchName: string; branchAddress: string;
  date: string; type: ShiftType; startAt: string; endAt: string; phase: ShiftPhase; checkInAt: string | null; checkOutAt: string | null;
  canCheckIn: boolean; colleagues: Colleague[];
}
export interface OpenShift { shiftId: string; branchId: string; branchName: string; date: string; type: ShiftType; startAt: string; endAt: string; requiredStaff: number; acceptedCount: number; isMyBranch: boolean }
export interface AttendanceView { id: string; shiftId: string; employeeId: string; checkInAt: string | null; checkOutAt: string | null; lateMinutes: number; isLate: boolean; approved: boolean }
export interface AttendanceRow {
  attendanceId: string | null; shiftId: string; date: string; shiftType: ShiftType; shiftStart: string; shiftEnd: string;
  employeeId: string; employeeName: string; checkInAt: string | null; checkOutAt: string | null;
  lateMinutes: number; earlyLeaveMinutes: number; overtimeMinutes: number; missing: boolean; approved: boolean;
}
export interface CurrentShift { shiftId: string; branchId: string; branchName: string; type: ShiftType; startAt: string; endAt: string; checkInAt: string; cashHeld: number; billCount: number }
export interface EmployeeView {
  employeeId: string; userId: string; fullName: string; email: string; phoneNumber: string | null; roles: string[];
  hourlyRate: number; nightAllowance: number; branchIds: string[]; homeBranchId: string | null; locked: boolean;
}
export interface ShiftRequestView {
  id: string; type: ShiftRequestType; status: "Pending" | "Approved" | "Rejected" | "Cancelled"; shiftId: string; branchId: string; branchName: string;
  date: string; shiftType: ShiftType; startAt: string; endAt: string; requesterEmployeeId: string; requesterName: string;
  targetEmployeeId: string | null; targetName: string | null; reason: string; createdAt: string; reviewedAt: string | null;
}
export interface AvailabilitySlot { isRecurring: boolean; dayOfWeek: string | null; specificDate: string | null; from: string; to: string }
export interface AssistantEmployee { employeeId: string; fullName: string; availability: AvailabilitySlot[]; busy: { shiftId: string; branchId: string; startAt: string; endAt: string }[]; hoursThisRange: number }

// ---- Sales ----
export interface SessionView {
  id: string; branchId: string; seatId: string; seatCode: string; pricePlanId: string; planName: string; startAt: string; endAt: string;
  status: SessionStatus; minutesLeft: number; extensionCount: number;
  customer: { id: string; fullName: string; tier: string } | null;
  bill: { id: string; code: string; total: number; paymentMethod: PaymentMethod; claimed: boolean; createdAt: string };
  pointsAwarded: number;
}
export interface PaymentRequestView { code: string; amount: number; qrUrl: string; expiresAt: string }
export interface PaymentRequestStatusView {
  code: string; amount: number; status: PaymentRequestStatus; expiresAt: string; paidAt: string | null; billCode: string | null;
}
export interface CustomerLookup { customerId: string; fullName: string; tier: string; points: number; memberCode: string | null }
export interface ClaimResult { code: string; total: number; pointsEarned: number; balance: number; tier: string }
export interface MyPoints {
  points: number; tier: string; totalSpent: number; nextTier: string | null; amountToNextTier: number; memberCode: string;
  history: { id: string; points: number; reason: string; billCode: string | null; createdAt: string }[];
}
export interface MyBill { code: string; branchName: string; total: number; createdAt: string; claimedAt: string | null; points: number }
export interface MyActiveSession { sessionId: string; branchName: string; seatCode: string; startAt: string; endAt: string; minutesLeft: number }

// ---- Finance ----
export interface ClosingSummary {
  id: string; status: ClosingStatus; expectedCash: number; countedCash: number; difference: number; note: string | null;
  denominations: Record<string, number>; closedByName: string; submittedAt: string | null; reviewedAt: string | null; reviewNote: string | null;
}
export interface ClosingPreview {
  shiftId: string; branchId: string; branchName: string; shiftType: ShiftType; startAt: string; endAt: string;
  expectedCash: number; vietQrTotal: number; billCount: number; activeSessions: number; closing: ClosingSummary | null;
}
export interface ClosingItem { id: string; shiftId: string; date: string; shiftType: ShiftType; startAt: string; endAt: string; closing: ClosingSummary }
export interface ExpenseView { id: string; branchId: string; category: string; amount: number; date: string; note: string | null; createdByName: string | null; reversesExpenseId: string | null; isReversed: boolean }
export interface PayrollView { id: string; employeeId: string; employeeName: string; month: string; totalHours: number; baseAmount: number; allowance: number; bonus: number; deduction: number; netAmount: number; status: "Draft" | "Finalized" }
export interface MyPayroll {
  month: string; hourlyRate: number; nightAllowance: number; provisionalNet: number; payroll: PayrollView | null;
  lines: { date: string; branchName: string; shiftType: ShiftType; checkInAt: string; checkOutAt: string; hours: number; nightHours: number; approved: boolean; amount: number }[];
}

// ---- Reports & notifications ----
export interface RevenueReport {
  from: string; to: string; total: number; bills: number; cash: number; vietQr: number; previousTotal: number; changePercent: number | null;
  buckets: { label: string; start: string; revenue: number; bills: number }[];
}
export interface ProfitLossReport { from: string; to: string; revenue: number; expenses: number; payroll: number; profit: number; expensesByCategory: Record<string, number> }
export interface OccupancyRow { branchId: string; branchName: string; seatType: SeatType; seats: number; soldHours: number; availableHours: number; rate: number }
export interface BranchComparisonRow { branchId: string; branchName: string; revenue: number; sessions: number; occupancy: number; payrollCost: number; payrollRatio: number | null; rank: number }
export interface Dashboard {
  revenueToday: number; billsToday: number; activeSessions: number; seats: number; occupancyNow: number;
  onShift: { name: string; branchName: string; checkInAt: string; shiftEnd: string }[];
  alerts: { type: string; message: string; link: string }[];
}
export interface NotificationFeed { unread: number; items: { id: string; type: string; title: string; body: string; link: string | null; createdAt: string; readAt: string | null }[] }

export function createFeatureApi(http: Http) {
  const get = <T>(path: string) => http.request<T>("GET", path);
  const post = <T>(path: string, body?: unknown) => http.request<T>("POST", path, body ?? {});
  const put = <T>(path: string, body: unknown) => http.request<T>("PUT", path, body);
  const del = (path: string) => http.request<void>("DELETE", path);

  return {
    catalog: {
      branches: (lat?: number, lng?: number) => get<PublicBranch[]>(`/api/branches${query({ lat, lng })}`),
      activePlans: (branchId: string) => get<PricePlanView[]>(`/api/branches/${branchId}/price-plans`),
      allPlans: (branchId: string) => get<PricePlanView[]>(`/api/branches/${branchId}/price-plans/all`),
      floor: (branchId: string) => get<Floor>(`/api/branches/${branchId}/floor`),
      createZone: (branchId: string, name: string) => post<ZoneView>(`/api/branches/${branchId}/zones`, { name }),
      updateZone: (zoneId: string, name: string) => put<ZoneView>(`/api/zones/${zoneId}`, { name }),
      deleteZone: (zoneId: string) => del(`/api/zones/${zoneId}`),
      createSeat: (zoneId: string, input: SeatEditInput) => post<SeatView>(`/api/zones/${zoneId}/seats`, input),
      updateSeat: (seatId: string, input: SeatEditInput) => put<SeatView>(`/api/seats/${seatId}`, input),
      deleteSeat: (seatId: string) => del(`/api/seats/${seatId}`),
      setMaintenance: (seatId: string, maintenance: boolean) => put<SeatView>(`/api/seats/${seatId}/maintenance`, { maintenance }),
      createPlan: (input: PlanInput) => post<PricePlanView>("/api/price-plans", input),
      updatePlan: (id: string, input: PlanInput) => put<PricePlanView>(`/api/price-plans/${id}`, input),
    },
    shifts: {
      list: (branchId: string, from: string, to: string) => get<ShiftView[]>(`/api/shifts${query({ branchId, from, to })}`),
      create: (input: CreateShiftInput) => post<ShiftView>("/api/shifts", input),
      update: (id: string, input: UpdateShiftInput) => put<ShiftView>(`/api/shifts/${id}`, input),
      remove: (id: string) => del(`/api/shifts/${id}`),
      validate: (shiftId: string, employeeId: string) => post<ValidationResult>("/api/shifts/validate", { shiftId, employeeId }),
      assign: (shiftId: string, employeeId: string, overrideReason?: string) => post<ShiftView>(`/api/shifts/${shiftId}/assignments`, { employeeId, overrideReason }),
      unassign: (assignmentId: string) => del(`/api/assignments/${assignmentId}`),
      publish: (branchId: string, weekStart: string) => post<{ published: number; notifiedEmployees: number }>("/api/shifts/publish", { branchId, weekStart }),
      copyWeek: (branchId: string, sourceWeekStart: string, targetWeekStart: string, includeAssignments: boolean) =>
        post<CopyWeekResult>("/api/shifts/copy-week", { branchId, sourceWeekStart, targetWeekStart, includeAssignments }),
      open: () => get<OpenShift[]>("/api/shifts/open"),
      claim: (shiftId: string) => post<void>(`/api/shifts/${shiftId}/claim`),
      accept: (assignmentId: string) => post<void>(`/api/assignments/${assignmentId}/accept`),
      decline: (assignmentId: string, reason: string) => post<void>(`/api/assignments/${assignmentId}/decline`, { reason }),
      mine: (from: string, to: string) => get<MyShift[]>(`/api/me/shifts${query({ from, to })}`),
      colleagues: () => get<Colleague[]>("/api/me/colleagues"),
    },
    attendance: {
      checkIn: (shiftId: string) => post<AttendanceView>("/api/attendances/check-in", { shiftId }),
      checkOut: (shiftId: string) => post<AttendanceView>("/api/attendances/check-out", { shiftId }),
      /** null when the employee is not in a shift (API answers 204). */
      current: () => get<CurrentShift | undefined>("/api/me/current-shift").then((v) => v ?? null),
      list: (branchId: string, from: string, to: string) => get<AttendanceRow[]>(`/api/attendances${query({ branchId, from, to })}`),
      approve: (attendanceId: string, approved: boolean) => post<AttendanceView>(`/api/attendances/${attendanceId}/approve`, { approved }),
    },
    workforce: {
      allEmployees: () => get<EmployeeView[]>("/api/employees"),
      employees: (branchId: string) => get<EmployeeView[]>(`/api/branches/${branchId}/employees`),
      updateEmployee: (id: string, hourlyRate: number, nightAllowance: number) => put<EmployeeView>(`/api/employees/${id}`, { hourlyRate, nightAllowance }),
      setBranches: (id: string, branchIds: string[], homeBranchId: string) => put<EmployeeView>(`/api/employees/${id}/branches`, { branchIds, homeBranchId }),
      assistant: (branchId: string, from: string, to: string) => get<AssistantEmployee[]>(`/api/scheduling/assistant${query({ branchId, from, to })}`),
      requests: (branchId: string, status?: string) => get<ShiftRequestView[]>(`/api/shift-requests${query({ branchId, status })}`),
      myRequests: () => get<ShiftRequestView[]>("/api/me/shift-requests"),
      createRequest: (input: { type: ShiftRequestType; shiftId: string; targetEmployeeId?: string | null; reason: string }) =>
        post<ShiftRequestView>("/api/shift-requests", input),
      approveRequest: (id: string, note?: string) => post<ShiftRequestView>(`/api/shift-requests/${id}/approve`, { note }),
      rejectRequest: (id: string, note?: string) => post<ShiftRequestView>(`/api/shift-requests/${id}/reject`, { note }),
      cancelRequest: (id: string) => post<void>(`/api/shift-requests/${id}/cancel`),
      myAvailability: () => get<AvailabilitySlot[]>("/api/me/availability"),
      setMyAvailability: (slots: AvailabilitySlot[]) => put<AvailabilitySlot[]>("/api/me/availability", slots),
    },
    sessions: {
      checkIn: (input: { seatId: string; pricePlanId: string; paymentMethod: PaymentMethod; customerId?: string | null }) => post<SessionView>("/api/sessions", input),
      extend: (id: string) => post<SessionView>(`/api/sessions/${id}/extend`),
      close: (id: string) => post<SessionView>(`/api/sessions/${id}/close`),
      active: (branchId: string) => get<SessionView[]>(`/api/branches/${branchId}/sessions/active`),
      bill: (code: string) => get<SessionView>(`/api/bills/${encodeURIComponent(code)}`),
      lookupCustomer: (q: string) => get<CustomerLookup[]>(`/api/customers/lookup${query({ q })}`),
    },
    payments: {
      createCheckIn: (input: { seatId: string; pricePlanId: string; customerId?: string | null }) =>
        post<PaymentRequestView>("/api/payments/requests", input),
      createExtension: (sessionId: string) =>
        post<PaymentRequestView>(`/api/sessions/${sessionId}/extend/payment-request`),
      status: (code: string) => get<PaymentRequestStatusView>(`/api/payments/${encodeURIComponent(code)}`),
      cancel: (code: string) => del(`/api/payments/requests/${encodeURIComponent(code)}`),
    },
    loyalty: {
      claim: (code: string) => post<ClaimResult>("/api/me/bills/claim", { code }),
      points: () => get<MyPoints>("/api/me/points"),
      bills: () => get<MyBill[]>("/api/me/bills"),
      session: () => get<MyActiveSession | undefined>("/api/me/session").then((v) => v ?? null),
    },
    finance: {
      closingPreview: (shiftId: string) => get<ClosingPreview>(`/api/shifts/${shiftId}/closing-preview`),
      submitClosing: (shiftId: string, denominations: Record<string, number>, note: string | null, submit: boolean) =>
        post<ClosingPreview>(`/api/shifts/${shiftId}/closing`, { denominations, note, submit }),
      closings: (branchId: string, from: string, to: string, status?: ClosingStatus) => get<ClosingItem[]>(`/api/shift-closings${query({ branchId, from, to, status })}`),
      reviewClosing: (id: string, approve: boolean, note?: string) => post<ClosingItem>(`/api/shift-closings/${id}/review`, { approve, note }),
      expenses: (branchId: string, from: string, to: string) => get<ExpenseView[]>(`/api/expenses${query({ branchId, from, to })}`),
      createExpense: (input: { branchId: string; category: string; amount: number; date: string; note: string | null }) => post<ExpenseView>("/api/expenses", input),
      reverseExpense: (id: string, reason: string) => post<ExpenseView>(`/api/expenses/${id}/reverse`, { reason }),
      payroll: (branchId: string, month: string) => get<PayrollView[]>(`/api/payrolls${query({ branchId, month })}`),
      generatePayroll: (branchId: string, month: string) => post<PayrollView[]>("/api/payrolls/generate", { branchId, month }),
      adjustPayroll: (id: string, bonus: number, deduction: number) => put<PayrollView>(`/api/payrolls/${id}`, { bonus, deduction }),
      finalizePayroll: (id: string) => post<PayrollView>(`/api/payrolls/${id}/finalize`),
      myPayroll: (month: string) => get<MyPayroll>(`/api/me/payroll${query({ month })}`),
    },
    reports: {
      dashboard: (branchId?: string) => get<Dashboard>(`/api/dashboard/today${query({ branchId })}`),
      revenue: (period: ReportPeriod, date: string, branchId?: string) => get<RevenueReport>(`/api/reports/revenue${query({ period, date, branchId })}`),
      profitLoss: (period: ReportPeriod, date: string, branchId?: string) => get<ProfitLossReport>(`/api/reports/profit-loss${query({ period, date, branchId })}`),
      occupancy: (period: ReportPeriod, date: string, branchId?: string) => get<OccupancyRow[]>(`/api/reports/occupancy${query({ period, date, branchId })}`),
      branches: (period: ReportPeriod, date: string) => get<BranchComparisonRow[]>(`/api/reports/branches${query({ period, date })}`),
    },
    notifications: {
      feed: () => get<NotificationFeed>("/api/me/notifications"),
      markRead: (ids?: string[]) => post<void>("/api/me/notifications/read", { ids }),
    },
  };
}

export type FeatureApi = ReturnType<typeof createFeatureApi>;
