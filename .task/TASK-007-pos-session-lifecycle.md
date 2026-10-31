# TASK-007: Implement the POS session lifecycle

- Status: Planned
- Goal: Let authorized staff start a customer session, extend it, and check it out while keeping seat, session, and bill state consistent.

## User value

POS staff can manage the core cafe service lifecycle through a small number of documented API operations rather than coordinating generic table CRUD calls.

## References

- [../spec.md](../spec.md)
- [../docs/payments/reference/project.md](../docs/payments/reference/project.md), session lifecycle and check-in use case
- [TASK-004](./TASK-004-role-authorization.md)
- [TASK-005](./TASK-005-development-seed.md)
- [TASK-006](./TASK-006-reference-data-availability.md)

## Data involved

`Session`, `SessionExtension`, `Seat`, `PricePlan`, `Customer`, and the active `Shift` required by the approved workflow. Bill creation is coordinated with TASK-008; avoid duplicating its finalized billing rules.

## Scope

- Start/check in a session for an eligible seat, plan, customer (optional only if approved), and authorized staff/branch context.
- Extend an eligible active session.
- Check out/end a session and update seat availability consistently.
- Use transactions/concurrency safeguards so competing check-ins cannot occupy the same seat.
- Provide an operation/history response appropriate for the POS without exposing persistence navigation graphs.
- Document Swagger behavior, roles, request/response examples, state transitions, and error responses; include runnable `.http` scenarios.

## Swagger language and contract

- Write operation summaries/descriptions, DTO field explanations, state-transition descriptions, role/branch requirements, response/error details, and examples in Vietnamese for the frontend team and demo.
- Explain expected workflow and recoverable failure cases in Vietnamese; keep examples synthetic and reproducible from the development seed.
- Keep route paths, JSON property names, enum wire values, and operation IDs stable/English for frontend integration. Do not translate wire-contract identifiers solely for display.

## Decisions required before implementation

- Confirm whether the initial purchase requires a customer account and whether walk-in sessions are allowed.
- Confirm start/end-time calculation, price calculation, rounding, grace periods, and whether overnight sessions are allowed.
- Confirm extension eligibility, duration/price calculation, and behavior after the scheduled end time.
- Confirm whether checkout creates/finalizes the bill in the same transaction or invokes TASK-008's billing operation.
- Confirm session expiration behavior and who can cancel or manually correct a session.

These are product decisions; do not infer them from the schema defaults or generic CRUD.

## Acceptance criteria

- Only approved Staff/Manager/Admin users can operate sessions within their allowed branch and shift scope.
- Check-in rejects unavailable seats and invalid plans with documented outcomes; concurrent attempts cannot create two active sessions for one seat.
- Extension and checkout reject invalid states and preserve the confirmed time/price rules.
- Database updates to session and seat state are atomic; tests exercise rollback/conflict behavior.
- Responses show the resulting session and seat state with safe, explicit DTOs.
- Vietnamese Swagger and `.http` examples cover success, invalid state, unavailable seat, unauthorized role, and cross-branch denial using TASK-005 data.
- Route paths and JSON contract identifiers remain stable for the frontend.
- Automated tests and integration tests verify persistence and concurrency behavior; no sensitive data is returned.

## Dependencies

- TASK-004 role/scope decisions.
- TASK-005 seed scenarios and test accounts.
- TASK-006 reference data and availability semantics.

## Verification

Record commands, results, OpenAPI review, and any required unapplied migrations when implemented.
