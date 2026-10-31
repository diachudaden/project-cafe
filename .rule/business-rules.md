# Business rules

## Authorization baseline

- The supported role identifiers are `Admin`, `BranchManager`, `Staff`, and `Customer`; accounts may hold multiple roles.
- `Admin` has global branch scope. `BranchManager` and `Staff` use branch assignments represented by `EmployeeBranch`; the home-branch marker does not itself grant access.
- `Customer` is limited to their own customer resources. Generic CRUD remains restricted to `Admin` until a feature task specifies and implements narrower operations.
- Branch and role claims in a JWT are snapshots for at most 15 minutes. Refresh tokens generate a new access token from current roles and branch assignments.

## Approved workflow rules (TASK-006…010)

Approved defaults from [docs/proposals/roles/05-decisions.md](../docs/proposals/roles/05-decisions.md); change them there first.

- Pricing: a branch plan overrides a chain plan with the same duration; plans are valid when `ValidFrom ≤ now < ValidTo`. Time-band pricing is not implemented.
- Sessions: payment at check-in creates the bill; each extension adds the plan duration (from the scheduled end) and the extend price to the same bill. Walk-ins are allowed. One active session per seat. Seat occupancy is derived from active sessions.
- Every bill belongs to a shift: Staff sell only while checked in to a shift at that branch; Admin/Manager use the branch's running shift.
- Points: 1.000đ = 1 point (floor). A bill is claimed once, within 7 days; members identified at check-in earn points automatically. Tiers by total spent: Bronze < 500.000đ ≤ Silver < 2.000.000đ ≤ Gold.
- Scheduling: no overlapping shifts (any branch), only assigned branches, no shift with an approved leave, ≥ 8 h rest (manager may override with a reason), ≥ 60 min between consecutive shifts at different branches, > 48 h/week is a warning. Check-in opens 15 minutes before start; > 10 minutes late is flagged.
- Closing: expected cash = Cash bill totals of the shift; a difference requires a note; approved closings are immutable.
- Finance: the expense ledger is append-only (corrections are reversal entries). Payroll = approved hours × rate + night hours (22:00–06:00) × allowance + bonus − deduction; Managers draft and adjust, only Admin finalizes; finalized payroll is immutable.
- Managers manage Staff of their branches only (rates, branch assignment); role changes remain Admin-only.
