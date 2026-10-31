# Proposal: complete the Admin, BranchManager, Staff and Customer roles

- Date: 2026-10-31
- Status: **Draft for review** — nothing in here is implemented yet
- Branch: `Finish_Roles_Configuration` (from `development`)
- Sources: [spec.md](../../../spec.md), [project reference](../../payments/reference/project.md), [.task/TASK-006…010](../../../.task/), current code

## Where we are today

| Role | API today | Frontend today |
| --- | --- | --- |
| Admin | Login + generic CRUD on all 21 tables (`/api/admin/*`) | `apps/admin`: branches, zones, seats, price plans, users |
| BranchManager | Login + `/me` only | none (admin app refuses non-Admin) |
| Staff | Login + `/me` only | none |
| Customer | Register + login + `/me` only | none |

The authorization building blocks exist but are **not used by any endpoint**: `BranchScope` policy, `CustomerOwnResource` policy, signed `branch_id` JWT claims, `IBranchAccessService`.

## What this proposal covers

| File | Content |
| --- | --- |
| [01-role-matrix.md](01-role-matrix.md) | What each role may do, per module, and the data scope |
| [02-api-design.md](02-api-design.md) | New endpoints, business rules, error behaviour, required migrations |
| [03-frontend-workflows.md](03-frontend-workflows.md) | Apps, screens, and step-by-step workflows per role |
| [04-seed-data.md](04-seed-data.md) | Seed plan: 3 admin, 10 managers, 20 staff, 20 customers + consistent business data |
| [05-decisions.md](05-decisions.md) | Open product decisions, each with a recommended default — **please review these first** |

## Key findings that shape the plan

1. **POS check-in depends on shifts.** `Bill.ShiftId` is required, so a staff member must be in an open shift (attendance check-in) before selling. Minimal shifts + attendance must come **before** the POS session flow, which is the reverse of the TASK numbering (007 before 009).
2. **Double check-in is not blocked by the database.** `Sessions(SeatId, Status)` is a normal index. Add a filtered unique index (one `Active` session per seat) so two tablets cannot sell the same seat.
3. **The frontend needs the user's branches.** `/api/auth/me` returns roles but not branch IDs; add `branchIds` (additive, non-breaking) so the manager/POS UI can show the right branch selector.
4. **Generic Admin CRUD can break workflow state** (e.g. editing a bill total or a session status directly). Once feature APIs exist, make `/api/admin/{sessions,bills,shift-closings,payrolls,point-transactions}` read-only.
5. **A few columns are missing for the documented workflows** (closing review status, decline reason, rule override reason). See the migration list in [02-api-design.md](02-api-design.md#migrations).

## Proposed delivery order

Each phase = backend API + tests + Swagger (Vietnamese) + frontend screens + seed data for that phase. Every phase is demo-able on its own.

| Phase | Backend | Frontend | Maps to |
| --- | --- | --- | --- |
| **0. Foundations** | `branchIds` in `/me`; filtered unique index; role seed (3/10/20/20 accounts + 5 branches) | Role-aware admin shell + branch switcher; `apps/pos` and `apps/customer` skeletons with login; nginx `/admin`, `/pos`, `/` | — |
| **1. Reference data & availability** | Manager-scoped seats/price plans; public branch list + availability; plan resolution | Manager: seat map & pricing; Customer: "Tìm quán" | TASK-006 |
| **2. Shifts & attendance (core)** | Create/publish/assign shifts, accept/decline, `me/shifts`, attendance check-in/out, overlap + branch rules | Manager: week calendar; Staff: "Lịch của tôi", "Tham gia ca" | TASK-009 (part 1) |
| **3. POS sessions, bills, points** | Check-in (bill created), extend, checkout, active sessions, bill claim, points, tiers | POS seat map + 3-tap check-in; Customer: "Quét bill", "Điểm" | TASK-007 + 008 |
| **4. Shift closing & finance** | Closing preview/submit/review, expenses ledger, payroll generate/finalize, `me/payroll` | POS: "Kết ca" wizard, "Lương"; Manager: closing review, expenses, payroll | TASK-010 |
| **5. Scheduling extras & reports** | Full conflict rules (rest/travel/48h/leave), shift requests, availability, open-shift claim, copy week, notifications, reports | Requests queue, availability grid, open shifts, dashboard & reports | TASK-009 (part 2) + reports |
| Later / optional | PIN login, dynamic QR, SignalR realtime, point redemption, Excel/PDF export, reservations | — | out of MVP |

Rough size: phases 1, 2 and 4 are medium; phase 3 is the largest (core of the product); phase 5 is large but every item is cuttable.

## How to review

1. Read [05-decisions.md](05-decisions.md) and mark each decision *accept* / *change*.
2. Skim the role matrix and the seed plan for anything that does not match how the cafe should work.
3. When approved, I will turn each phase into a `.task/TASK-0xx` file (same format as existing tasks) and implement phase by phase.
