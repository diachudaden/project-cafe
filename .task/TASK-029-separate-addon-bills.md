# TASK-029: Tách bill riêng cho đồ thêm + đóng màn tính tiền sau khi thanh toán xong

## Status

**Implemented** on `feat/separate-addon-bills` (from `development`, 6 commits; slices 2 and 3 share one commit because
they share the new-bill helper). Decision D34 replaces the TASK-021 rule "đồ thêm cộng vào bill của lượt ngồi".

**Server: apply migration `SeparateAddOnBills`** before deploying the API. It makes `Bills.SessionId` nullable, adds
`Bills.Kind` (existing rows = 1 Session) and `Bills.ParentBillId`, recreates `IX_Bills_SessionId` filtered on
`IS NOT NULL` and adds `CK_Bills_Kind`. Existing bills are not changed. No new settings.

## Deviations from the prompt (approved before coding)

| Prompt | Implemented | Why |
| --- | --- | --- |
| Add-on bill keeps `SessionId` = the session; unique index on `Bills.SessionId` becomes filtered `WHERE Kind = 1` | Add-on bill has `SessionId = NULL` and `ParentBillId` = the session bill (required for `Kind = AddOn`, check constraint). The unique index on `SessionId` stays and is filtered `SessionId IS NOT NULL` | `Session.Bill` is an EF **one-to-one** on `Bills.SessionId`. A second bill with the same `SessionId` breaks that mapping, and every `s.Bill` query (floor, recent closed, bill lookup, catalog, extensions) would need rewriting. With a null `SessionId` the one-to-one, the "1 lượt = 1 bill lượt ngồi" guarantee and all existing session queries stay correct; the session of an add-on bill is `ParentBill.Session` |
| — | The add-on bill's `ShiftId` = the shift selling it (same rule as check-in), not the session bill's shift | Today an add-on sold in a later shift lands on the check-in shift's bill, so its cash is expected in the **earlier** shift's closing. Chain-wide and per-day totals do not change; per-shift totals change only for sales that cross a shift boundary |

## Goal

Every add-on sale after check-in is its own bill with its own code (cash and VietQR), so the "Đã thanh toán" screen and the
printed receipt show what was just paid. After a payment is done, closing the payment screen returns to the floor.

## Spec references

- [spec.md](../spec.md), [.rule/architecture.md](../.rule/architecture.md), [.rule/testing.md](../.rule/testing.md)
- [TASK-021](./TASK-021-plans-dropdown-and-add-on-items.md) (superseded rule), [TASK-026](./TASK-026-checkin-addons-receipt-pdf.md)
  (add-ons at check-in stay on the session bill), [TASK-024](./TASK-024-auto-release-overdue-seats.md) (concurrency token)

## Dependencies

TASK-021, TASK-025, TASK-026. TASK-027 (inventory) counts sessions from `Sessions` + `SessionExtensions` and is unaffected.

## Scope

See the slices in "Implementation order". Data: `Bill.Kind` (`Session = 1`, `AddOn = 2`, default 1), `Bill.ParentBillId`,
`Bill.SessionId` nullable, `Bill.AddOnBills`. Existing bills keep their data (no data migration).

## Exclusions

Extension pricing/billing, splitting or merging old bills, add-on refunds, add-ons without a seat.

## Places that assume "1 lượt = 1 bill" (step 0.4)

| Place | Today | After |
| --- | --- | --- |
| `SalesConfiguration` `Bill`↔`Session` one-to-one, unique `IX_Bills_SessionId` | unique | unique filtered `SessionId IS NOT NULL`; new FK `ParentBillId` |
| `AddOnService.SellCashAsync` / `SettleAsync` / `AddToBillAsync` | adds lines to `session.Bill`, `Total +=` | new `AddOn` bill |
| `AddOnService.PreparePaymentAsync` | `ShiftId = s.Bill.ShiftId` | current shift |
| `SessionService.Query` (`SessionView.Items = s.Bill.Items`) | all add-ons of the session | session bill items + `addOnBills` list |
| `SessionService.GetByBillCodeAsync` (`GET /api/bills/{code}`) | finds only session bills | resolves add-on code to its parent session, returns `kind` |
| `ReceiptService.LoadAsync`, `LoyaltyService.BillDetailAsync` | `b.Session.*` (plan, times, extensions) | add-on bill: seat via `ParentBill.Session`, no plan/times/extensions |
| `LoyaltyService.ClaimAsync` | `bill.Session.Seat.Code` | seat via parent for add-on bills |
| `FinanceService.PreviewAsync` / `SubmitClosingAsync`, `AttendanceService` (`Shift.Bills`) | sums per shift | unchanged code; bill count +1 per add-on sale |
| `ReportService.BillsAsync` (revenue, `Bills` count, dashboard `BillsToday`) | counts all bills | revenue unchanged; bill counts include add-on bills; sessions counted from `Sessions` |
| `AdminCrudEndpoints` `/api/admin/bills` (`BillResponse.SessionId`) | `Guid` | `Guid?` + `Kind` |
| `DevelopmentHistorySeeder`, unit/integration tests | set `SessionId` | unchanged for session bills |

## Acceptance criteria

- Check-in 40k + two 7k add-on sales → three bills (40k, 7k, 7k) with different codes; the session bill stays 40k.
- Add-ons bought in the check-in payment and extensions stay on the session bill.
- VietQR add-on settles into one new bill; a resent webhook does not create a second one.
- Member session: points on the add-on bill's own total; walk-in: claim the add-on code once within 7 days.
- Add-on receipt: "HÓA ĐƠN ĐỒ THÊM", seat, time, items, payment, total, staff, print time, claim QR; no plan/times/extensions.
- Closing cash and revenue totals unchanged; bill counts +1 per add-on sale; sessions/occupancy count sessions.
- POS: paid (cash "Xong" or VietQR Paid) → floor; not paid (cancel, close, expired, cancelled QR) → previous screen.

## Implementation order (one commit per slice)

1. Migration + `BillKind` + `ParentBillId` + index; adjust the places above that only need the new columns.
2. Cash add-on sale → separate bill + points + tests.
3. VietQR add-on settle → separate bill, idempotent + tests.
4. `GET /api/bills/{code}` + receipt PDF for add-on bills + tests.
5. Closing, reports, occupancy review + tests (incl. SQL Server).
6. POS: separate bills (4.1) + close-after-payment (4.2).
7. api-client + customer app (4.3).

## Implementation notes

- `AddOnService.NewAddOnBillAsync` is the only place that creates add-on bills (cash sale and VietQR settle).
  `ShiftId` = the seller's shift (`SessionService.ResolveShiftAsync`, now shared); for VietQR it is resolved when the
  request is created and stored on `PaymentRequest.ShiftId`. Staff must be checked in to a shift to sell add-ons, as for
  check-in and extensions.
- If the session bill is claimed (member at check-in or a later claim), each add-on bill is claimed for the same member.
- `GET /api/bills/{code}` keeps returning the session view; an add-on code resolves to its parent session, with
  `kind = AddOn`. `items` = add-ons from the check-in payment; `addOnBills` = later add-on bills.
- `/api/me/bills/{code}`: `kind`; `planName`, `startAt`, `endAt` are null for add-on bills (contract change for the
  customer app, updated in the same branch).
- Report bug fixed on the way: `BranchComparisonRow.Sessions` counted bills; it now counts session bills only.
- POS close rules live in pure functions `afterPaymentClose` and `addOnSheetExit` (`paymentStatus.ts`). Extension VietQR
  now also returns to the seat sheet when the QR expires or is cancelled (paid still goes to the floor).

## Verification results (2026-11-17)

- `dotnet test CafeCR.slnx`: UnitTests 208 passed, IntegrationTests 45 passed (SQL Server 2025 via Testcontainers).
  New: separate cash bills + closing preview, check-in add-ons/extension on the session bill, walk-in claim of an
  add-on code (+ customer detail), VietQR settle with a resent webhook, reports (revenue/compare/dashboard), add-on
  receipt PDF, `bill-by-code` SQL translation; on SQL Server: one session bill per session, many add-on bills,
  `CK_Bills_Kind`, receipt and bill lookup, closing preview and reports.
- `npm run typecheck`, `npm test` (api-client 15, POS 16, ui 33), `npm run build` in `frontend/`: pass.
- Migration generated with `dotnet ef migrations add SeparateAddOnBills` and applied by the SQL Server tests.

Not covered: a browser run of the POS and customer screens (no DOM test setup in the repo) and a real SePay payment.

## Verification

`dotnet test CafeCR.slnx`; `npm run typecheck && npm test && npm run build` in `frontend/`.
