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
| D7 | Overdue sessions | Shown red on the POS; staff decide to extend or close. No automatic charge, no background job | Simplest correct behaviour | |
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

## Questions without a default

- Q1. Should a BranchManager also appear in the staff schedule (work shifts themselves), or are managers off the rota?
- Q2. Is there a deadline or demo date? It decides whether phase 5 is in or out.
- Q3. Do you work alone, or should phases be split between team members (e.g. backend vs frontend)?
