# TASK-010: Implement shift closing and payroll

- Status: Completed (API + frontend; real-database verification pending)
- Goal: Let approved staff reconcile a shift's cash and let authorized managers review expenses and calculate/approve payroll.

## User value

Staff can hand over a reconciled shift; managers can review the cash closing and branch expenses; employees and managers can access the correct payroll views.

## References

- [../spec.md](../spec.md)
- [../docs/payments/reference/project.md](../docs/payments/reference/project.md), shift closing, expense, and payroll requirements
- [TASK-008](./TASK-008-billing-customer-points.md)
- [TASK-009](./TASK-009-workforce-scheduling-attendance.md)

## Data involved

`ShiftClosing`, `Shift`, `Bill`, `Expense`, `Payroll`, `Employee`, and `EmployeeBranch`.

## Scope

- Record closing denominations and calculate expected/count/difference from eligible bills.
- Submit, review, and approve/reject shift closings using the approved state flow.
- Record and query branch-scoped expenses.
- Calculate payroll from approved attendance, rates, night allowances, bonuses, and deductions; expose employee-self and authorized manager views.
- Provide audit-appropriate history for corrections/approvals; do not silently overwrite approved financial data.
- Document operations, calculations, permissions, examples, and tester scenarios in Swagger and `.http`.

## Swagger language and contract

- Write summaries, descriptions, DTO field explanations, formulas, state transitions, role/branch requirements, response/error details, and examples in Vietnamese for the frontend team and demo.
- Explain financial and payroll values using clear Vietnamese labels and units/currency, once formulas are confirmed.
- Keep route paths, JSON property names, enum wire values, and operation IDs stable/English for frontend integration. Do not translate wire-contract identifiers solely for display.
- Use synthetic financial examples and never expose real payroll or personal data in Swagger.

## Decisions required before implementation

- Confirm which bill statuses/tender types count as expected cash and how refunds/voids/corrections affect closing.
- Confirm denomination format/currency, rounding, reconciliation tolerance, and reviewer/approval rules.
- Confirm payroll period boundaries, night-hour calculation, breaks/overtime, bonuses/deductions, rounding, and approval/finalization rules.
- Confirm which roles can create/review expenses and view each employee's payroll.
- Confirm correction and re-open policy after a closing or payroll is approved.

Do not treat schema columns as complete accounting or legal payroll rules.

## Acceptance criteria

- A shift can have only the permitted closing record(s); expected cash and difference are calculated from defined bill rules on the server.
- Submit/review/approve transitions enforce role, branch, and state restrictions and preserve history for approved records.
- Payroll calculations use only approved attendance and confirmed formula/period rules; results are reproducible and tested at boundary cases.
- Employees can view only their own permitted payroll data; managers/Admin access follows the approved matrix.
- Expenses and closings cannot be read or changed across unauthorized branches.
- Vietnamese Swagger and `.http` examples show a full closing/review and payroll scenario, including mismatch and denied-access cases.
- Route paths and JSON contract identifiers remain stable for frontend integration.
- Automated tests verify money calculations, time boundaries, authorization, duplicates, correction rules, and persistence consistency.
- Developer handoff records formulas and migration status; tester handoff includes seed prerequisites, expected totals, requests, and reproducible reset steps.

## Dependencies

- TASK-008 billing semantics and records available for reconciliation.
- TASK-009 approved attendance, shifts, employees, and branch scope.
- TASK-004 role matrix.
- TASK-005 repeatable test dataset.

## Verification

Record confirmed calculations, test/build results, OpenAPI review, and migration/database status when implemented.

## Implementation

Implemented on branch `Finish_Roles_Configuration` (2026-10-31) using the approved defaults in [../docs/proposals/roles/05-decisions.md](../docs/proposals/roles/05-decisions.md). Schema changes are in migration `RoleWorkflows` (not yet applied to any shared database).

- Closing preview (expected cash = Cash bill totals of the shift), submit with denominations (draft or submit; difference requires a note; submitting checks the employee out), review approve/reject (approved closings immutable).
- Expense ledger is append-only with reversal entries. Payroll draft from approved attendance: hours × rate + night hours (22:00–06:00) × allowance + bonus − deduction; only Admin finalizes (D10); finalized payroll and its month's attendance are immutable.
- Reports: revenue by period vs previous period, profit/loss, occupancy, branch comparison, today's dashboard with alerts. Notifications feed for assignment, request and closing events.

## Verification results

- `dotnet test CafeCR.slnx`: 64 unit tests (rules, calculators, workflow services on EF in-memory, SQL Server translation of feature queries via a command interceptor) and 7 HTTP integration tests (real API pipeline with JWT, policies, rate limiting, Swagger and the demo seed on an in-memory database) passed; `dotnet build CafeCR.slnx`: 0 warnings, 0 errors.
- Not yet run: `dotnet ef database update` and the seed against a real SQL Server (Docker was unavailable in the implementation session).
