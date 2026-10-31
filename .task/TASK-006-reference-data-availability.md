# TASK-006: Manage branches, pricing, and seat availability

- Status: Completed (API + frontend; real-database verification pending)
- Goal: Let authorized staff maintain cafe reference data and let customers discover available seats and applicable plans.

## User value

Staff can configure branches, seating areas, seats, and price plans; a customer or tester can identify a suitable branch/seat/plan before starting a session.

## References

- [../spec.md](../spec.md)
- [../docs/payments/reference/project.md](../docs/payments/reference/project.md), branch, pricing, and customer/POS requirements
- [TASK-004](./TASK-004-role-authorization.md)
- [TASK-005](./TASK-005-development-seed.md)

## Data involved

`Branch`, `Zone`, `Seat`, and `PricePlan`.

## Scope

- Replace generic CRUD usage for these resources with feature-oriented, validated API operations where needed.
- Provide branch and price-plan administration under the approved role/branch policies.
- Provide customer-safe branch/seat availability and applicable-plan queries.
- Keep availability derived from persisted state/session rules; prevent exposing internal or unrelated operational data.
- Document requests, response schemas, role requirements, filters, and expected errors in Swagger; provide `.http` examples using the development seed.

## Swagger language and contract

- Write summaries, descriptions, field explanations, response/error descriptions, and request/response examples in Vietnamese for the frontend team and live demo.
- Explain role/branch requirements and availability/pricing semantics in Vietnamese.
- Keep route paths, JSON property names, enum wire values, and operation IDs stable/English; do not translate wire-contract identifiers solely for display.

## Decisions required before implementation

- Confirm price-plan selection rules: branch-specific versus global plan precedence, validity windows, and time/day pricing. The project reference marks sample pricing as unresolved.
- Confirm whether availability lookup is real-time only, date/time-aware, or includes reservations. Do not invent a reservation feature.
- Define exact seat availability semantics when a session expires, is checked out, or is administratively changed.

## Acceptance criteria

- Admin/Manager reference-data operations enforce the approved branch scope and input constraints.
- Customer-facing lookup returns only active/valid branches, seats, and plans according to the confirmed rules.
- Seat availability cannot be changed into an inconsistent state through concurrent requests or generic CRUD.
- Invalid/missing references produce documented 400/404/409 Problem Details responses without internal details.
- Vietnamese Swagger provides useful summaries, field descriptions, request/response examples, errors, authentication/role requirements, and paging/filter parameters where applicable.
- API routes, JSON property names, enum wire values, and operation IDs remain stable for frontend integration.
- Tests cover available/unavailable seats, inactive/out-of-date plans, branch scope, and invalid input.
- Developer and tester handoff includes seed prerequisites, reproducible requests, and expected results.

## Dependencies

- TASK-004 role/scope decisions.
- TASK-005 repeatable development dataset.

## Verification

Record tests, build, OpenAPI review, and any migration/database status when implemented.

## Implementation

Implemented on branch `Finish_Roles_Configuration` (2026-10-31) using the approved defaults in [../docs/proposals/roles/05-decisions.md](../docs/proposals/roles/05-decisions.md). Schema changes are in migration `RoleWorkflows` (not yet applied to any shared database).

- Public `GET /api/branches` (distance sort, open-now, free seats per type), `/availability`, `/price-plans`; branch-scoped `/floor` with derived seat state (Available/Occupied/EndingSoon ≤ 15 min/Overdue/Maintenance).
- Manager/Admin zone, seat, maintenance and price-plan management; chain-wide plans Admin-only.
- Decisions: branch plan overrides a chain plan with the same duration; validity window `ValidFrom ≤ now < ValidTo`; no day/night/weekend pricing yet (D4). Occupancy is derived from active sessions; `Seat.Status` stores only Available/Maintenance.

## Verification results

- `dotnet test CafeCR.slnx`: 64 unit tests (rules, calculators, workflow services on EF in-memory, SQL Server translation of feature queries via a command interceptor) and 7 HTTP integration tests (real API pipeline with JWT, policies, rate limiting, Swagger and the demo seed on an in-memory database) passed; `dotnet build CafeCR.slnx`: 0 warnings, 0 errors.
- Not yet run: `dotnet ef database update` and the seed against a real SQL Server (Docker was unavailable in the implementation session).
