# TASK-025: Payment completion → POS "done" state, customer notification, live points/bill

## Status

Implemented on branch `feat/payment-completion-customer` (from `development` `77a4390`); automated checks pass. **Pending on the server:** apply migration `NotificationPaymentLinks` (data only).

> Numbering: the request named this TASK-023; 023 (add-on menu) and 024 (auto-release) exist, so this is TASK-025.

## Current state (re-checked at `77a4390`)

| Claim in the request | On the latest code |
|---|---|
| Points via `Loyalty.Award` on every member path | ✔ `SessionService.CheckInAsync` (cash, l.98), `ExtendAsync` (cash, l.249), VietQR settlement check-in (l.332) + extension (l.382), `AddOnService.AddToBillAsync` (cash + VietQR, l.252), `LoyaltyService.ClaimAsync` (l.38). "Member" everywhere = `Bill.ClaimedByCustomerId` (set at check-in when a member is attached, or later by `/claim`). Unchanged by this task. |
| `PaymentReceived` notifications only on VietQR | ✔ 3 call sites (check-in, extension, add-on when `method == VietQr`), all with link `"/pay"`; body lacks bill code, points and balance. Cash paths and `/claim` create none. |
| `/pay` doesn't exist in the customer app | ✔ Routes `/`, `/login`, `/claim`, `/points`, `/me`, `*` → `/`. (TASK-024's `session.auto_released` uses link `/`, fine.) |
| Customer app has no notification UI | ✔ Bell exists only in `PosLayout.tsx`, `AdminLayout.tsx`, POS `ActivityPage.tsx`. |
| `GET /api/me/notifications` | ✔ group uses `RequireAuthorization()` (any signed-in role, Customer included), returns `{ unread, items[{…, link}] }`. No change needed beyond a test. |
| MePage / PointsPage fetch once | ✔ `["points"]`, `["my-bills"]` no refetch; `["my-session"]` every 30 s. No bill detail page / endpoint (`GET /api/me/bills` is a list). |
| POS: VietQR shows Paid result, cash only a toast | **Partly wrong:** cash **check-in** already shows a `Result` modal (bill code, "Đã cộng N điểm cho …", Xong / Thêm đồ). Cash **extension** and cash **add-on** only toast. Also the VietQR Paid result never shows points: it loads the bill via `GET /api/bills/{code}`, whose `pointsAwarded` is always 0. |
| Add-on cash API returns points | ✘ `POST /api/sessions/{id}/items` returns only `BillItemView[]` (no bill code/total/points/member). |
| Frontend test runners | `pos`, `ui`, `api-client` have `vitest`; **`customer` has no `test` script**. |

## Plan

### Backend
1. **`Features/Engagement/PaymentNotifications.cs`** (one helper, called right after `Loyalty.Award` in the same `SaveChanges`, so a rollback drops it):
   - `PaymentCompleted(db, customer, bill, seatCode, kind, amount, method, pointsEarned, now)` → type `payment.received`, title e.g. "Thanh toán gia hạn thành công · A-01", body "Đã nhận 40.000đ (Tiền mặt) cho gia hạn chỗ A-01, bill QW12ER34. +40 điểm · số dư 1.250 điểm.", link `/bills/{code}`. `kind` ∈ check-in / extension / add-on (Vietnamese label).
   - `PointsClaimed(db, customer, bill, seatCode?, pointsEarned, now)` → type `points.earned`, "Tích điểm bill … +N điểm · số dư …".
   - New balance = `customer.Points` after `Loyalty.Award` (it updates the tracked entity). Only called when `ClaimedByCustomerId` is set → walk-ins never notified.
   - Call sites: replace the 3 VietQR `db.Notify` calls; add to cash check-in, cash extension, cash add-on (in `AddToBillAsync`, so both methods use it) and `LoyaltyService.ClaimAsync`. `Notifier.PointsEarned = "points.earned"`.
2. **`GET /api/me/bills/{code}`** (`RequireCustomer`, `LoyaltyService.BillDetailAsync`): 404 unless `bill.ClaimedByCustomer.UserId == me`. Returns code, branch, seat, plan, start/end, session status/close reason, extensions (time, minutes, price), add-on items, cash/VietQR/total, payment method, point transactions of this bill (points, reason, time), created/claimed time. Vietnamese Swagger.
3. **Points in POS responses** (needed for the "+N điểm" done state):
   - `POST /api/sessions/{id}/items` → `AddOnSaleResponse { items, billCode, billTotal, pointsAwarded, customerName }` (**contract change**; only the POS calls it, updated together).
   - `GET /api/payments/requests/{code}` status → add `pointsAwarded` and `customerName` (points of this bill created at `PaidAt`), so the VietQR Paid screen can show "+N điểm" too.
4. **Data fix:** migration `NotificationPaymentLinks` (SQL only, no schema change): `UPDATE Notifications SET Link = '/me' WHERE Link = '/pay'`. Down is a no-op.
5. No change to pricing, points rules, sessions, webhook verification, or settlement flow (only the notification line in it).

### Customer web (`frontend/apps/customer`)
6. `NotificationBell` next to the theme toggle (top-right): `Badge` with unread, `Drawer` list; click → `markRead([id])` + `navigate(link)` (unknown links fall back to `/me`). Feed query `["my-notifications"]`, `refetchInterval: () => document.visibilityState === "visible" ? 10_000 : 60_000`, `refetchIntervalInBackground: true`, refetch on `visibilitychange`.
7. `useNotificationWatcher`: compares the previous and new feed (`newPaymentNotifications(prev, next)` → unread `payment.received` / `points.earned` ids not seen before; the first load never toasts). For each: `notification.success` toast with the body; browser `Notification` if `Notification.permission === "granted"` and the tab is hidden; invalidate `["points"]`, `["my-bills"]`, `["my-session"]`.
8. MePage: "Bật thông báo" button (only when `Notification` exists and permission is `default`; shows state when granted/denied) — never requested on load.
9. Points animation: `PointsDelta` component on PointsPage and MePage — when `points.data.points` grows, show "+N điểm" (fade/slide, ~2.5 s) and count up the number.
10. Route `/bills/:code` (`RequireCustomer`) → `BillPage` with the detail from (2); MePage bill rows link to it. Bottom tab bar unchanged.
11. Pure helpers + tests in `packages/ui` (`notifications.ts`: `newPaymentNotifications`, `pointsDeltaText`) — the customer app has no test runner; adding one is not needed if the logic lives in `ui`.

### POS (`frontend/apps/pos`)
12. `PaymentDoneModal` (shared): `Result` "Đã thanh toán", what was paid (check-in / gia hạn / đồ thêm), amount + method, bill code (copyable), member name + "+N điểm" when attached, "Xong" (check-in keeps "Thêm đồ ăn / uống"). Used by cash check-in (replaces the current inline modal), cash extension (`SessionView.pointsAwarded`), cash add-on (new response), and the VietQR Paid screen (adds "+N điểm").
13. After success: invalidate `["floor", branchId]`, `["current-shift"]`, `["seat-bill"]`, `["recent-closed", branchId]`.

### Tests
- Backend (in-memory, existing fixtures): for a member, each path — cash check-in, cash extension, cash add-on, VietQR check-in, VietQR extension, VietQR add-on — creates **exactly one** `payment.received` with amount, points, balance and link `/bills/{code}`; walk-in check-in/extension/add-on create none; `/claim` creates one `points.earned`; `BillDetailAsync` returns the bill to its owner and 404 for another customer / unclaimed bill; add-on response carries `pointsAwarded`; SQL translation test for the bill-detail query; migration count 9.
- Frontend (vitest, `ui`): `newPaymentNotifications` (first load, new unread, already seen, read ones, other types), `pointsDeltaText`.
- Verification commands as required.

### Not in scope / follow-up
- SignalR push (replace polling; D27 already names the contract).
- Web Push when the browser tab is closed (needs a service worker + VAPID keys).

## Questions (answered "ok" → defaults)
1. Contract change of `POST /api/sessions/{id}/items` (array → object) — OK? *(default: yes)*
2. Add `pointsAwarded`/`customerName` to the VietQR status response so the QR "done" screen matches cash — OK? *(default: yes)*
3. Also notify a member when an add-on/extension lands on a bill they **claimed later** via `/claim` (they earn those points today)? *(default: yes — "member" = whoever earns the points)*

## Implementation notes

- Notification text example: title "Gia hạn thành công · A-01", body "Đã nhận 40.000đ (Tiền mặt) cho gia hạn chỗ A-01, bill AB12CD34. +40 điểm · số dư 85 điểm.". Numbers are grouped by hand ("1.250.000"), not via `vi-VN` culture data, so the text is identical in every container.
- "Member" = `Bill.ClaimedByCustomerId` (who earns the points), so a bill claimed later via `/claim` also notifies its claimer for later extensions/add-ons (Q3).
- VietQR points on the POS: points of the bill with `CreatedAt == PaidAt`. The add-on settlement now uses one timestamp for the lines, the points and `PaidAt` (previously two `GetUtcNow()` calls); no other settlement change.
- `PaymentRequestModal` takes a `what` prop ("Check-in" / "Gia hạn" / "Đồ thêm") because the create-request response has no kind.
- Customer bell uses an emoji button (the customer app does not depend on `@ant-design/icons`); browser `Notification` may be unavailable on some mobile browsers without a service worker; the in-app toast still shows.

## Files changed

- Backend: `Engagement/PaymentNotifications.cs` (new), `Engagement/Notifier.cs` (`points.earned`), `Sales/SessionService.cs`, `Sales/AddOnService.cs` (`AddOnSaleResponse`), `Sales/LoyaltyService.cs` (claim notification, `BillDetailAsync`), `Sales/SalesContracts.cs` (`MyBillDetail`), `Sales/SalesEndpoints.cs` (`GET /api/me/bills/{code}`), `Sales/PaymentRequestService.cs` (status `pointsAwarded`, `customerName`), migration `20261114081638_NotificationPaymentLinks`.
- Tests: `SePayWebhookTests.cs` (+4), `SqlTranslationTests.cs` (+1 "my-bill-detail"), `DomainSchemaModelTests.cs` (9 migrations).
- Frontend: `api-client/features.ts`; `ui/notifications.ts` (+ test); customer `App.tsx`, `components/NotificationBell.tsx`, `usePageVisible.ts`, `PointsDelta.tsx`, `EnableNotifications.tsx`, `pages/BillPage.tsx`, `MePage.tsx`, `PointsPage.tsx`; POS `PaymentDoneModal.tsx` (new), `CheckInSheet.tsx`, `SeatActionsSheet.tsx`, `AddOnSheet.tsx`, `PaymentRequestModal.tsx`.
- Docs: decision D28 updated.

## Verification

- `dotnet build CafeCR.slnx`: Build succeeded.
- `dotnet test CafeCR.slnx`: unit **110 passed** (+4 behaviour tests: cash ×3 paths and VietQR ×3 paths each create exactly one notification with amount/points/balance/link and the POS gets the points; walk-in cash + VietQR paths create none; claim notifies `points.earned`, later extension notifies the claimer, bill detail owner-only/404 for others and for unclaimed bills; +1 SQL translation), integration **12 passed**.
- `frontend/`: `npm run typecheck` 0 errors; `npm test` pos 8, api-client 7, ui 16 passed; `npm run build` 3 apps built.
- Headless Chrome against a mocked API: POS cash extension → "Đã thanh toán · Gia hạn A-01 · 40.000đ · Tiền mặt · +40 điểm cho Nguyễn An"; customer `/me` → after the feed changes, toast + badge 1 + count-up with "+40 điểm" within one poll; bell → item → `/bills/AB12CD34` detail.
- Not done: real SQL Server / real SePay run.

## Follow-ups

- SignalR push instead of polling (D27 contract).
- Web Push (service worker + VAPID) for closed tabs.

## Manual demo script

1. Deploy, then run `dotnet ef database update --project backend/src/ChonRieng.Infrastructure --startup-project backend/src/ChonRieng.Api` (applies `NotificationPaymentLinks`).
2. Phone: open the customer web, sign in as a member, go to **Tôi**, tap **Bật thông báo** (optional), note the points.
3. POS (staff in a shift): free seat → Check-in → look up the member's code → Tiền mặt → **Đã thanh toán … +45 điểm cho …** → Xong.
4. Within ~10 s the phone shows a toast "Thanh toán check-in thành công · …", the bell badge goes to 1, points animate "+45 điểm". Tap the notification → bill detail.
5. POS: same seat → VietQR → Gia hạn → pay the QR (or the SePay test script). POS shows **Đã thanh toán · Gia hạn … +N điểm**; the phone shows "Gia hạn thành công" within ~10 s and the bill page lists the extension.
6. Walk-in check-in (no member) → POS shows "Khách lẻ…"; no notification anywhere.

