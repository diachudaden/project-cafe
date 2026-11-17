# 03 — Frontend apps and workflows

## App layout

| App | Users | Device | URL (nginx) | Status |
| --- | --- | --- | --- | --- |
| `apps/admin` → "Web Quản lý" | Admin, BranchManager | desktop/laptop | `/admin` | exists (Admin only) → extend with roles |
| `apps/pos` → "POS Nhân viên" | Staff (and managers working a shift) | tablet, landscape | `/pos` | new |
| `apps/customer` → "Web Khách" | Customer | phone, mobile-first | `/` | new |

- All three reuse `packages/api-client` (one module per feature: `branches`, `shifts`, `sessions`, `bills`, `me`, …).
- Add `packages/ui` for shared bits: VND formatter (`45.000đ`), status colours, empty states, error handling.
- One nginx image builds and serves all three apps (simpler than three containers): `/admin/*`, `/pos/*`, `/*` → static; `/api/*` → API. Vite `base` set per app.
- UI library: keep **Ant Design** for admin and POS (already used, fast CRUD/forms); customer app uses Ant Design Mobile or plain AntD with a mobile layout → decision D12. The reference doc suggests Fluent UI/shadcn; switching now would cost time without changing features.
- Calendar: `react-big-calendar` (week/day/month, drag-and-drop addon) for the Teams-style shift calendar.
- Colour meaning everywhere: green = free, orange = ending soon (≤15 min), red = overdue, grey = maintenance.
- Live data: TanStack Query `refetchInterval` (10 s on the POS seat map and dashboard). SignalR is a later upgrade.

## Web Quản lý (Admin + BranchManager)

Top bar: **branch switcher** (Admin: "Tất cả" + every branch; Manager: only assigned branches) — every screen filters by it. Left menu shows only what the role can use.

| Menu | Admin | Manager | Screens |
| --- | --- | --- | --- |
| Tổng quan | ✅ | ✅ | Today: revenue, occupancy %, who is on shift, alerts (understaffed shift, closing difference) |
| Lịch ca | ✅ | ✅ | Week calendar per branch; drag to create/move; conflict warnings while dragging; publish week; copy last week |
| Đơn từ | ✅ | ✅ | Queue of swap/leave/drop requests → approve/reject |
| Chấm công | ✅ | ✅ | Planned vs actual per shift, late/early flags, approve |
| Kết ca | ✅ | ✅ | List of closings with difference; open → denominations, note → approve/reject |
| Thu chi | ✅ | ✅ | Expense ledger, add expense, reversal entry |
| Lương | ✅ | ✅ | Generate month, edit bonus/deduction, finalize |
| Báo cáo | ✅ | ✅ (own branches) | Revenue/P&L/occupancy by week/month/quarter/year; branch comparison (Admin) |
| Chi nhánh & chỗ ngồi | ✅ | ✅ own branch (no create/delete branch) | Branch profile, zones, seats, maintenance toggle |
| Bảng giá | ✅ | ✅ own branch plans | Plans, validity |
| Nhân viên | ✅ | ✅ own staff | Profile, hourly rate, branches, lock account |
| Tài khoản & vai trò | ✅ | — | Existing users screen |

### Workflow: manager plans a week

1. Lịch ca → choose branch + week → "Sao chép tuần trước" or drag to create shifts (type, time, required staff).
2. Drag an employee onto a shift → API validate → warnings shown inline (red = blocked, orange = warning, with "Bỏ qua kèm lý do" when allowed).
3. "Công bố tuần" → staff get a notification; cards turn dashed until accepted.
4. Declined shifts come back with the reason; manager reassigns or marks as open shift.

### Workflow: manager closes the day

Kết ca list → open a closing with a difference → read note → approve/reject. Chấm công → approve today's attendance. Both feed payroll.

## POS Nhân viên (Staff)

Layout: top bar shows **current shift, branch, cash held**, "Kết ca" button. Tabs: **Quầy** (seat map) · **Lịch của tôi** · **Lương** · **Hoạt động**. Auto-lock after 5 minutes idle (later, with PIN — decision D2).

### Workflow: start of shift

1. Log in (email/phone + password in MVP).
2. Lịch của tôi shows today's shift card; the **"Tham gia ca"** button is enabled from 15 min before start.
3. Tap → attendance check-in → Quầy opens. Without an open shift, Quầy shows "Bạn chưa tham gia ca nào" and selling is disabled.

### Workflow: check-in a customer (3 taps)

1. Tap a **green** seat on the map.
2. Sheet: plan preselected (4 tiếng – 45.000đ), optional member search/scan, payment method (Tiền mặt / VietQR simulated).
3. "Thu tiền" → confirm → bill code shown large (+ QR) for the customer to claim points.

### Workflow: during the shift

- Orange seat (≤15 min left) → tap → **"Gia hạn thêm 4 tiếng"** (one confirm) or **"Trả chỗ"**.
- Red seat (overdue) → same actions, highlighted.
- Grey seat → maintenance, not selectable.

### Workflow: end of shift ("Rời ca" wizard)

1. **Đếm tiền**: enter count per denomination (500k … 1k), total updates live; save as draft any time.
2. **Đối chiếu**: expected cash vs counted, difference shown; note required if difference ≠ 0.
3. **Bàn giao**: confirm → closing submitted, attendance check-out, auto logout.

### Other staff screens

Lịch của tôi: week view, accept/decline offered shifts (reason required), open shifts to claim, availability, swap/leave requests. Lương: hours per shift, night allowance, provisional vs finalized.

## Web Khách (Customer, mobile-first)

Bottom tabs: **Tìm quán · Quét bill · Điểm · Tôi**.

| Tab | Content |
| --- | --- |
| Tìm quán | Branch list sorted by distance (browser location, optional) with free seats per type and open hours; map view (Leaflet + OpenStreetMap) as a toggle |
| Quét bill | Enter the 8-character code or scan the QR → points added; clear messages for used/expired/invalid codes |
| Điểm | Balance, tier progress bar (Đồng → Bạc → Vàng), history |
| Tôi | Profile, my member QR (for check-in), current session countdown, claimed bills, logout |

### Workflow: earn points

Visit → (optional) show member QR at the counter → if attached, points are automatic. Otherwise: Quét bill → enter code → "+45 điểm".

Register/login: email + password (exists). Phone + simulated OTP → decision D11.

## Verification per phase

- Typecheck, unit tests for api-client modules, build.
- Manual run through each workflow with the seeded accounts (listed in [04-seed-data.md](04-seed-data.md)).
- Optional later: Playwright E2E for check-in and shift closing (mentioned in the reference doc).
