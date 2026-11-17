# 02 — API design

## Conventions (keep what exists)

- Prefix stays **`/api/...`** (current code), not `/api/v1` from the reference doc, so the admin app and Swagger don't break.
- Minimal API endpoint groups per feature (`Endpoints/SessionEndpoints.cs`, …); logic in application services (`Features/Sessions/SessionService`, …) per [.rule/architecture.md](../../../.rule/architecture.md).
- Errors: `ProblemDetails`. Business rule violations return **409** with a list of violations: `{ rule, message, canOverride }`. Validation errors return **400**; out-of-scope resources return **404** (do not reveal that another branch's record exists).
- Swagger summaries/descriptions in Vietnamese; routes, JSON names and enum values in English.
- Paging `?page=&pageSize=` as today. Lists that the UI needs to filter take explicit query params (`branchId`, `from`, `to`, `status`).
- Times are `DateTimeOffset`; business-day logic uses **Asia/Ho_Chi_Minh** (UTC+7).
- Money is VND, `decimal`, no fractional đồng.

## 0. Auth additions

| Method | Route | Role | Notes |
| --- | --- | --- | --- |
| GET | `/api/auth/me` | any | **Add** `branchIds: Guid[]` (additive). Same field in the token response `user`. |
| PUT | `/api/me/password` | any | Change own password. |
| POST | `/api/auth/pin-login` | — | *Later phase*, see decision D2. |

## 1. Reference data & availability (Phase 1, TASK-006)

| Method | Route | Role | Behaviour |
| --- | --- | --- | --- |
| GET | `/api/branches` | public | Active branches; optional `lat`,`lng` → sorted by distance; includes `openNow` and free seats per type. |
| GET | `/api/branches/{id}/availability` | public | Per seat type: total, free, maintenance. No seat codes or session data for anonymous users. |
| GET | `/api/branches/{id}/price-plans` | public | Plans valid **now** for the branch (resolution rule below). |
| GET | `/api/branches/{id}/floor` | Admin, Manager, Staff 🔸 | Zones → seats with live state: `available`, `occupied` (session end time), `endingSoon` (≤15 min), `overdue`, `maintenance`. Drives the POS seat map. |
| POST/PUT/DELETE | `/api/branches/{id}/zones`, `/api/zones/{id}/seats` | Admin, Manager 🔸 | Feature-level zone/seat management. |
| PUT | `/api/seats/{id}/maintenance` | Admin, Manager 🔸 | Toggle maintenance; rejected (409) if the seat has an active session. |
| POST/PUT | `/api/branches/{id}/price-plans` | Admin, Manager 🔸 | Branch plans. Global plans (`branchId = null`) Admin only. |

**Price-plan resolution:** valid if `ValidFrom ≤ now < ValidTo` (nulls = open). The branch-specific plan overrides a global plan with the same `DurationMinutes`. Day/night/weekend pricing needs new columns → decision D4.

**Seat state is derived, not stored:** `occupied` = an `Active` session exists. `Seat.Status` keeps only `Available`/`Maintenance` as the administrative state, so seat and session can never disagree. `Occupied` in the enum stays for compatibility but is no longer written.

## 2. Shifts & attendance (Phase 2 core, Phase 5 extras — TASK-009)

| Method | Route | Role | Behaviour |
| --- | --- | --- | --- |
| GET | `/api/shifts?branchId=&from=&to=` | Admin, Manager 🔸 | Calendar data: shift + assignments (name, status). |
| POST | `/api/shifts` · PUT `/api/shifts/{id}` · DELETE | Admin, Manager 🔸 | Create/edit Draft shifts. Uses `RowVersion` (If-Match / body field) → 409 "Ca vừa được người khác cập nhật". |
| POST | `/api/shifts/validate` | Admin, Manager 🔸 | Dry-run the rules for an assignment (for drag-and-drop warnings). |
| POST | `/api/shifts/{id}/assignments` | Admin, Manager 🔸 | Offer shift to employee(s); runs rules; optional `overrideReason`. |
| POST | `/api/shifts/publish?branchId=&weekStart=` | Admin, Manager 🔸 | Draft → Published; creates notifications. |
| POST | `/api/shifts/copy-week` | Admin, Manager 🔸 | *Phase 5.* Copy shifts (and optionally assignments); returns conflicts. |
| GET | `/api/me/shifts?from=&to=` | Staff, Manager | My assignments + colleagues on the same shift. |
| POST | `/api/assignments/{id}/accept` · `/decline` | Staff 🙋 | Decline requires a reason. Re-checks rules in the same transaction. |
| GET | `/api/shifts/open` · POST `/api/shifts/{id}/claim` | Staff 🙋 | *Phase 5.* First claim wins (RowVersion). |
| POST | `/api/attendances/check-in` | Staff 🙋 | `{ shiftId }`. Allowed from 15 min before `StartAt` until `EndAt`; must be an accepted assignment. Late (>10 min) is computed from `CheckInAt` vs `StartAt`. |
| POST | `/api/attendances/check-out` | Staff 🙋 | Normally done by the shift-closing wizard. |
| GET | `/api/attendances?branchId=&from=&to=` · POST `/{id}/approve` | Admin, Manager 🔸 | Review: planned vs actual, late, early leave, overtime. |
| GET/PUT | `/api/me/availability` | Staff 🙋 | *Phase 5.* Recurring weekly or specific-date slots. |
| POST | `/api/shift-requests` · POST `/{id}/approve` · `/reject` | Staff create, Manager 🔸 review | *Phase 5.* Swap/Leave/Drop; approval updates assignments in one transaction. Status values: `Pending`, `Approved`, `Rejected`, `Cancelled`. |
| GET | `/api/scheduling/assistant?branchId=&from=&to=` | Manager 🔸 | *Phase 5.* Free/busy grid from availability + existing assignments. |

**Conflict rules** (from the reference doc), implemented as `IShiftRule` classes run by a `ShiftConflictChecker`:

| Rule | Result | Phase |
| --- | --- | --- |
| No overlap, also across branches | Block | 2 |
| Employee assigned to that branch | Block | 2 |
| Approved leave on that shift | Block | 5 |
| ≥ 8 h rest between shifts | Block, manager may override with reason | 5 |
| ≥ 60 min between consecutive shifts at different branches | Block | 5 |
| ≤ 48 h per week | Warning | 5 |
| Fewer than `RequiredStaff` accepted | Warning on calendar | 2 |

Shift state shown in the UI is derived: `Draft`/`Published` (stored) + assignments + attendance + closing → *Nháp → Công bố → Chờ phản hồi → Đã nhận → Đang diễn ra → Đã kết ca → Đã duyệt*. No new enum values needed.

## 3. POS sessions, bills, points (Phase 3 — TASK-007 + 008)

| Method | Route | Role | Behaviour |
| --- | --- | --- | --- |
| POST | `/api/sessions` (check-in) | Staff 🔸 in open shift, Manager, Admin | `{ seatId, pricePlanId, paymentMethod, customerId? }` → creates Session + Bill (8-char code) in one transaction. 409 if the seat is taken (unique index). |
| POST | `/api/sessions/{id}/extend` | same | Adds `SessionExtension` (plan's `ExtendPrice`, plan duration), moves `EndAt`, **adds to the same bill**. |
| POST | `/api/sessions/{id}/close` | same | Checkout → `Closed`, seat free again. |
| GET | `/api/branches/{id}/sessions/active` | Staff, Manager 🔸 | Active sessions with remaining minutes. |
| GET | `/api/bills/{code}` | Staff, Manager 🔸 | Bill detail for reprint/lookup. |
| GET | `/api/customers/lookup?q=` | Staff 🔸 | Find a member by phone/email/member code at check-in (returns name + tier only). |
| POST | `/api/me/bills/claim` | Customer | `{ code }`. One claim per bill, within 7 days, row-locked transaction; returns points earned. |
| GET | `/api/me/points` · `/api/me/bills` | Customer | Balance, tier, transaction history, claimed bills. |
| GET | `/api/me/session` | Customer | My current active session + time left (if attached at check-in). |

**Rules (proposed defaults, see decisions D5–D8):**

- Payment is taken **at check-in** ("mua lượt 4 tiếng"), so the bill is created then; extension adds to the same bill total.
- Walk-in allowed: `customerId` optional. If a customer is attached at check-in, points are added automatically when the bill is created.
- Points: `floor(total / 1000)` (1.000đ = 1 point). Tier from `TotalSpent`: Đồng < 500.000đ ≤ Bạc < 2.000.000đ ≤ Vàng.
- Session past `EndAt` without checkout is shown as **overdue** (red) on the POS. The status becomes `Expired` when the staff closes it or on next read. No background job in MVP.
- Bill code: 8 characters from an unambiguous alphabet (no 0/O/1/I), unique index already exists.

## 4. Shift closing & finance (Phase 4 — TASK-010)

| Method | Route | Role | Behaviour |
| --- | --- | --- | --- |
| GET | `/api/shifts/{id}/closing-preview` | Staff/Manager in that shift | Expected cash = sum of **Cash** bill totals of the shift; VietQR totals listed separately. |
| POST | `/api/shifts/{id}/closing` | same | `{ denominations: { "500000": 2, … }, note }` → counted, difference; checks out the employee. Draft save allowed. |
| POST | `/api/shift-closings/{id}/approve` · `/reject` | Admin, Manager 🔸 | Review with note; approved closings are immutable. |
| GET/POST | `/api/expenses?branchId=&from=&to=` | Admin, Manager 🔸 | Append-only; corrections are reversal entries (negative amount + reference). No PUT/DELETE. |
| POST | `/api/payrolls/generate?branchId=&month=` | Admin, Manager 🔸 | Draft payroll from **approved** attendance: hours × `HourlyRate` + night hours (22:00–06:00) × `NightAllowance` + bonus − deduction. |
| PUT | `/api/payrolls/{id}` · POST `/{id}/finalize` | Admin, Manager 🔸 | Adjust bonus/deduction while Draft; Finalized is immutable. |
| GET | `/api/me/payroll?month=` | Staff, Manager 🙋 | Per-shift hours, allowance, provisional vs finalized. |

## 5. Reports & notifications (Phase 5)

| Method | Route | Role |
| --- | --- | --- |
| GET | `/api/reports/revenue?branchId=&period=week\|month\|quarter\|year&date=` | Admin, Manager 🔸 |
| GET | `/api/reports/profit-loss` · `/api/reports/occupancy` · `/api/reports/branches` (comparison) | Admin (all), Manager 🔸 |
| GET | `/api/dashboard/today?branchId=` | Admin, Manager 🔸 — revenue today, occupancy %, who is on shift, alerts |
| GET | `/api/me/notifications` · POST `/api/me/notifications/read` | any |

Reports query `Bills`/`Expenses`/`Sessions` directly (fine at thesis data size); no nightly aggregate table in MVP.

## Migrations

All additive; one migration per phase.

| Phase | Change | Why |
| --- | --- | --- |
| 0 | Filtered unique index `Sessions(SeatId) WHERE Status = Active` | Two tablets cannot sell the same seat |
| 2 | `ShiftAssignments.DeclineReason`, `ShiftAssignments.OverrideReason` (nullable) | "Từ chối kèm lý do"; manager override audit |
| 3 | `Customers.MemberCode` (unique, nullable) | Member lookup / QR at check-in |
| 3 | `PointTransactions.CreatedAt`, `Bills.CreatedAt` (if not derivable) | History ordering, 7-day claim window |
| 4 | `ShiftClosings.Status`, `ReviewedByEmployeeId`, `ReviewedAt`, `ReviewNote` | Review/approve flow |
| 4 | `Expenses.ReversesExpenseId` (nullable) | Append-only ledger corrections |
| 4 | `Attendances.ApprovedByEmployeeId`, `ApprovedAt` | Audit of who approved |
| D4 | `PricePlans.DaysOfWeek`, `StartTime`, `EndTime` | Only if day/night/weekend pricing is approved |

## Changes to existing behaviour

- `/api/admin/{sessions,session-extensions,bills,point-transactions,shift-closings,payrolls}`: becomes **read-only** once the feature endpoints exist (writes go through the workflow).
- `Seat.Status = Occupied` no longer written (derived from sessions).
- `AuthUserResponse` gains `branchIds` (additive).

## Testing per phase

- Unit tests for every rule/calculation (pricing, points, tiers, payroll, conflict rules, closing difference).
- Authorization tests per endpoint: the right role passes, the wrong role gets 403, another branch gets 404.
- Concurrency tests: double check-in on one seat, double bill claim, two staff claiming one open shift.
- `.http` scenarios in Vietnamese using seeded accounts.
