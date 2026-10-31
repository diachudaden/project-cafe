# 04 — Seed data plan

Requested accounts: **3 Admin, 10 BranchManager, 20 Staff, 20 Customer.**

> Note: the earlier seed commit on `frontend-v2` (5 admin / 10 staff / 20 customer, `2bfa256`) is **not in `development`**. This plan replaces it; that commit should not be merged as is.

## Principles (unchanged from TASK-005)

- Runs only with `--seed-development-data` in `Development`/`Testing`; never on normal startup.
- Idempotent: stable lookup by reserved `@cafecr.invalid` emails and `[DEMO DATA]` markers; inserts missing records only; never updates or deletes non-seed data.
- One shared password from `DevelopmentSeed__Password` (ignored `.env`). Staff PINs (later phase) from `DevelopmentSeed__Pin`. Nothing secret in git.
- Names are synthetic Vietnamese names; prices are labelled demo values, not approved business pricing.
- Deterministic: a fixed random seed, so every run on an empty database produces the same data.

## Accounts

The existing `manager.demo`, `staff.demo`, `customer.demo` accounts and the bootstrap admin stay as they are.

| Role | Emails | Count | Assignment |
| --- | --- | --- | --- |
| Admin | `admin01…03.demo@cafecr.invalid` | 3 | whole chain |
| BranchManager | `manager01…10.demo@cafecr.invalid` | 10 | 2 per branch (A–E); `manager10` also covers branch E + A (multi-branch test case) |
| Staff | `staff01…20.demo@cafecr.invalid` | 20 | 4 per branch; `staff04`, `staff08` also assigned to a second branch (cross-branch travel-rule test) |
| Customer | `customer01…20.demo@cafecr.invalid` | 20 | profile + member code; spread across Đồng/Bạc/Vàng |

Every Manager and Staff gets an `Employee` row (demo hourly rate, night allowance) and `EmployeeBranch` rows with exactly one `IsHome`.

## Branches and seats

The 2 existing demo branches (A, B) stay; add C, D, E.

| Branch | Hours | Zones | Seats |
| --- | --- | --- | --- |
| A, B (existing) | 08:00–23:00 | existing zone + "Khu nhóm" | existing 2 + ~8 more |
| C | 24/7 | Khu yên tĩnh, Khu nhóm, Góc ngủ | ~12 |
| D | 24/7 | Khu yên tĩnh, Khu nhóm | ~10 |
| E | 07:00–22:00 | Khu yên tĩnh, Khu nhóm | ~8 |

About 50 seats in total, mixing `Single`/`Group`/`Nap`, with 1–2 seats in maintenance.

Price plans (demo values): global "4 tiếng" 45.000đ, extension 40.000đ; one branch override (e.g. C: 40.000đ); one expired plan, to test the validity window.

## Business data (added phase by phase, matching what each phase implements)

| Phase | Data | Volume (approx.) | Purpose |
| --- | --- | --- | --- |
| 0 | Accounts, employees, branches, zones, seats, price plans | above | login and scope testing |
| 2 | Shifts for the previous 4 weeks + current week + next week | ~300 | calendar, attendance, payroll input |
| 2 | Assignments: Accepted / Offered / Declined (with reason); a few under-staffed shifts | ~600 | accept/decline, warnings |
| 2 | Attendance for past shifts: on time, a few late (>10 min), a few missing | ~550 | approval screen, late flags |
| 3 | Closed sessions + bills over the last 30 days, some with extensions | ~400 | reports, bill lookup |
| 3 | Some bills claimed by customers + matching point transactions; customer `Points`/`TotalSpent`/`Tier` consistent with them | ~150 claims | points history, tiers |
| 3 | 2–3 active sessions per branch (one ending soon, one overdue), consistent with seat state | ~12 | POS seat-map colours |
| 3 | Unclaimed recent bills (codes printed in the seed log) | ~10 | testing "Quét bill" |
| 4 | Shift closings for past shifts: most exact, some ± difference, some pending review | ~250 | closing review |
| 4 | Expenses per branch (rent, ingredients, utilities) + one reversal entry | ~60 | ledger, P&L |
| 4 | Payroll for last month: Finalized for some branches, Draft for others | ~30 | payroll screens |
| 5 | Availability slots, swap/leave requests (Pending/Approved/Rejected), open shifts, notifications | ~100 | requests queue, open shifts, activity feed |

Consistency rules the seeder must respect (the same rules as the API):

- Every bill belongs to a shift at the same branch during which a staff member was checked in.
- No staff member has overlapping shifts; one active session per seat.
- Customer totals = sum of their claimed bills; points = sum of point transactions.
- Historical data is created once (marker check). Re-running the seed doesn't duplicate it or move dates.

## Where to run it

Your current `.env` points to the **hosted SQL Server (site4now)**. The seed never deletes anything, but it adds about 2,500 rows and has no reset. Recommendation (decision D13): add a local `sqlserver` service to `compose.yaml` (SQL Server 2025 container with a volume, as the reference doc describes) for development and demos, and keep the hosted database for shared testing only.

## Documentation

The README account table is updated with all ranges and which branch each account belongs to, so testers can pick, for example, "staff05 = branch B, Staff".
