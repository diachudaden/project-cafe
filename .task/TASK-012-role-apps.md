# TASK-012: Role apps (Web Quản lý, POS, Web Khách)

- Status: Completed (frontend; real-database verification pending)
- Goal: Give every role a working frontend on the role APIs from TASK-006…010.

## Scope

- `frontend/apps/admin` (`/admin/`): Admin and BranchManager. Branch switcher, dashboard, Teams-style week schedule (create, assign with live rule check and override reason, publish, copy week), requests, attendance approval, closing review, expense ledger, payroll, reports, seat map, pricing, employees; Admin-only branches and accounts.
- `frontend/apps/pos` (`/pos/`): Staff. "Tham gia ca", live seat map (10 s refresh, reference colours), 3-tap check-in with member lookup, extend/checkout, 3-step closing wizard with draft, my schedule (accept/decline, open shifts, swap/leave/drop, availability), payroll, activity feed.
- `frontend/apps/customer` (`/`): Customer, mobile-first with 4 bottom tabs: Tìm quán, Quét bill, Điểm, Tôi; email registration.
- `frontend/packages/ui`: formatting (45.000đ, UTC+7), Vietnamese labels, shared auth provider. One nginx image serves all apps and proxies `/api`.

## Not included (later)

PIN login and auto-lock (D2), dynamic QR attendance and camera bill scanning, drag-and-drop on the calendar, Leaflet map (links to OpenStreetMap instead), SignalR (polling, D15), point redemption, Excel/PDF export, charts.

## Verification

- `npm run typecheck`, `npm test` (api-client 5, ui 3) and production builds of all three apps passed.
- Manual browser run and `docker compose up --build` not yet performed.
