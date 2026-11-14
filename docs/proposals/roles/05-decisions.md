# 05 — Decisions to confirm

The task files (TASK-006…010) say not to guess these product rules. Each has a **recommended default**, based on the project reference where possible. Mark each one ✅ accept or ✏️ change.

| # | Topic | Recommended default | Source / reason | Your answer |
| --- | --- | --- | --- | --- |
| D1 | Build order | Phases 0→5 as in the README: **shifts & attendance before POS sessions** | `Bill.ShiftId` is required | |
| D2 | POS login | MVP: email/phone + password. PIN login (choose name + 6-digit PIN, 5-min auto-lock, one POS session per user) in a later phase | PIN exists in the schema but needs an endpoint and rate limits | |
| D3 | Attendance proof | MVP: "Tham gia ca" button by the logged-in staff member, branch checked from the shift. Dynamic QR at the counter later | Reference says QR; it adds a device/display flow | |
| D4 | Pricing | MVP: one 4-hour plan + extension price per branch (branch overrides global). Day/night/weekend pricing **later** (needs migration + pricing strategy) | Reference wants time-band pricing; the schema has no columns for it | |
| D5 | When the bill is created | At **check-in** (payment upfront); extension adds to the **same bill** | Reference: "mua lượt", extension "cộng vào cùng một bill" | |
| D6 | Walk-in customers | Allowed (customer optional); attaching a member at check-in gives points automatically | Reference | |
| D7 | Overdue sessions | Shown red on the POS; staff decide to extend or close. No automatic charge. **Superseded in part by D30** (automatic release after a grace period) | Simplest correct behaviour | ✏️ D30 |
| D8 | Points & tiers | 1.000đ = 1 point (floor); claim once, within 7 days; tiers by TotalSpent: Đồng < 500.000đ ≤ Bạc < 2.000.000đ ≤ Vàng | Reference gives the ratio and the 7 days, but not the thresholds | |
| D9 | Point redemption ("Đổi điểm") | **Later** (needs a rewards table and rules) | Not in the schema | |
| D10 | Who finalizes payroll | Manager generates and adjusts for their branch; **only Admin finalizes** | Money + legal sensitivity | |
| D11 | Customer login | Email + password (exists). Phone + simulated OTP later | Reference mentions simulated OTP | |
| D12 | UI libraries | Ant Design for all three apps (customer with a mobile layout) + react-big-calendar + Leaflet | Already in use; the reference suggests Fluent/shadcn | |
| D13 | Dev database | Add a local SQL Server container to compose for seeding and demos; hosted DB only for shared tests | Seed adds ~2.5k rows with no reset | |
| D14 | Generic admin CRUD | Make workflow tables read-only in `/api/admin` once feature APIs exist | Prevent inconsistent bills/sessions | |
| D15 | Realtime | Polling every 10 s in MVP; SignalR later | Less infrastructure for the demo | |
| D16 | Manager scope on accounts | Managers can assign/unassign **Staff** to their branches and edit staff rate; cannot create users or change roles | Prevent privilege escalation | |
| D17 | Shift rules in MVP | Phase 2: overlap + assigned-branch + understaffed warning. Phase 5: rest 8 h (override with reason), travel 60 min, 48 h/week warning, approved leave | Rules are all from the reference; this only decides the order | |
| D18 | Demo prices | 45.000đ per 4 h, 40.000đ extension (demo-labelled) | Reference examples use 45.000đ | |
| D19 | Payment flow | Keep `Bill` as paid-only and add `PaymentRequest` for pending bank transfers | Business reporting still reads `Bills` only; pending transfer state belongs in a separate request record | ✅ |
| D20 | Payment code format | `Payment__CodePrefix` default `TT` + 8 chars from the `Loyalty` alphabet, case-insensitive matching, unique code required | Avoid collisions with `Customer.MemberCode` (`CR` prefix) and keep SePay `code`/`content` parsing deterministic | ✅ |
| D21 | Seat hold semantics | Keep `Seat` status unchanged; use a pending `PaymentRequest` to block a seat temporarily while the transfer is pending | The seat is considered occupied while the pending request is valid; expiry releases it automatically | ✅ |
| D22 | Cash flow | Keep the existing cash check-in/extend flow unchanged and add the payment event/realtime layer after the persisted record is committed | No change to the existing cash accounting logic or reporting model | ✅ |
| D23 | Transfer completion | POS creates a VietQR request; webhook verifies the correct amount and then executes the same check-in/extend logic with `PaymentMethod.VietQr` | Reuse the core workflow instead of duplicating the existing business rules | ✅ |
| D24 | QR payload | Use `Payment__QrImageBaseUrl` with `acc`, `bank`, `amount`, `des` parameters and escape all values before generating the QR URL | Matches SePay Test mode and avoids malformed QR image links | ✅ |
| D25 | Webhook security | Secure `POST /api/hooks/sepay-payment` with HMAC-SHA256 using `X-SePay-Signature` and `X-SePay-Timestamp` over `{timestamp}.{raw_body}` | Required by the approved payment flow and must reject tampered or stale payloads | ✅ |
| D26 | Transaction states | Store `PaymentTransaction` rows for every provider event; resolve by matching code/amount and set `MatchStatus` such as `Matched`, `Underpaid`, `Overpaid`, `Duplicate`, `Unmatched` | Keep the payment ledger audit trail without inventing a full accounting module | ✅ |
| D27 | Realtime contract | Emit `PaymentReceived`, `PaymentNeedsAttention`, `PaymentExpired`, `PrintReceipt`, and `PaymentStatusChanged` over SignalR `/hubs/pos` with camelCase payloads | Required for immediate POS and customer feedback without changing the `Bill` model | ✅ |
| D28 | Customer notification | When a member bill is paid, send a `payment.received` notification through the existing `Notifier` pipeline. **TASK-025:** applies to every member payment (cash and VietQR: check-in, extension, add-on) plus `points.earned` for `/claim`; body has seat, amount, method, points and balance; link `/bills/{code}`; saved in the same transaction as the payment; customer web polls (10 s visible / 60 s hidden). SignalR push stays a follow-up | The member experience should update as soon as the payment is finalized | ✅ |
| D29 | Configuration | Require `Payment__*` settings with fail-fast validation on startup and include them in Docker Compose environment values | Prevents silent misconfiguration and keeps the demo environment runnable in Testing mode | ✅ |
| D30 | Auto-release overdue seats (TASK-024) | A background job closes a session overdue by more than `Sessions__AutoReleaseGraceMinutes` (default 10, `0` = off, max 240) as `Expired` with `CloseReason = AutoReleased` (staff "Trả chỗ" records `Manual` + the staff member). **Skipped** while the session has a Pending, unexpired VietQR request (extension or add-on). No charge for the overdue time; bill and payments unchanged. Each release is a conditional update (`Status = Active AND EndAt <= now − grace`); `Status`/`EndAt` are concurrency tokens, so a concurrent extension, close or add-on wins or gets a 409. A VietQR payment that arrives after release goes to NeedsAttention ("Lượt ngồi đã được hệ thống tự trả"). Members get a `session.auto_released` notification. POS shows a countdown and a "Vừa trả chỗ" list | Staff no longer free every overdue seat by hand; the grace period and the pending-payment skip protect customers who are extending | ✅ |
| D31 | Add-ons at check-in & printed receipt (TASK-026) | Check-in (cash or VietQR) may include add-on lines: server prices them from the branch menu (≤ 30 lines, qty 1–99), one bill and **one payment** for plan + items, points awarded once on that total; VietQR stores the lines snapshot on the request and settles from it. `GET /api/bills/{code}/receipt.pdf` prints an 80 mm receipt (QuestPDF Community, bundled Noto Sans OFL) for workforce in the bill's branch; unclaimed bills carry a QR to `/claim?code=…`; the staff name printed is the user who prints | Fewer QR scans at the counter; a paper bill lets walk-ins claim points later | ✅ |

## Questions without a default

- Q1. Should a BranchManager also appear in the staff schedule (work shifts themselves), or are managers off the rota?
- Q2. Is there a deadline or demo date? It decides whether phase 5 is in or out.
- Q3. Do you work alone, or should phases be split between team members (e.g. backend vs frontend)?
