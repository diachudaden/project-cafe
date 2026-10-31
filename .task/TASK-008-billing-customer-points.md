# TASK-008: Implement billing and customer points

- Status: Planned
- Goal: Produce a consistent bill for a session and let customers claim it and receive the approved points outcome.

## User value

Staff can complete a session's simulated-tender record; customers can claim an eligible bill and see their points balance/history.

## References

- [../spec.md](../spec.md)
- [../docs/payments/reference/project.md](../docs/payments/reference/project.md), bill, extension, and points requirements
- [TASK-007](./TASK-007-pos-session-lifecycle.md)

## Data involved

`Bill`, `Session`, `SessionExtension`, `Shift`, `Branch`, `Customer`, and `PointTransaction`.

## Scope

- Implement session-linked bill creation/finalization and retrieval with safe DTOs.
- Support only the simulated tender methods already in the schema; do not add a payment gateway.
- Support customer bill claiming using the approved claim-code workflow.
- Calculate/record points and expose a customer's own points balance and transaction history.
- Ensure bill/points writes are atomic and idempotent where requests may be retried.
- Document roles, error codes, business rules, OpenAPI examples, and `.http` scenarios.

## Swagger language and contract

- Write summaries, descriptions, field explanations, calculation/claim rules, role requirements, response/error descriptions, and examples in Vietnamese for the frontend team and demo.
- Examples must use synthetic data and show safe success/failure outcomes; never include real tokens, claim secrets, or credentials.
- Keep route paths, JSON property names, enum wire values, and operation IDs stable/English for frontend integration. Do not translate wire-contract identifiers solely for display.

## Decisions required before implementation

- Confirm claim-code generation, lifetime, uniqueness, attempt limits, and whether a bill can be claimed only once.
- Confirm points conversion, rounding, eligibility, expiration, tier rules, reversals, and refund/correction handling.
- Confirm whether extension charges are included in the original bill total and how a completed checkout initiates bill creation.
- Confirm which roles can record tender details and whether VietQr in this schema means simulated confirmation only.

No real payment processor, QR verification, gateway webhook, or financial ledger is in scope.

## Acceptance criteria

- A session has at most one bill, and bill totals reconcile with confirmed plan/extension rules.
- Claiming a bill is authenticated, customer-scoped, time-limited/one-time as approved, and resistant to guessing/replay.
- Points and customer balance updates are transactional and cannot be duplicated by retrying the same operation.
- Customers can read only their own bill/points data; staff/admin access follows the approved role/branch matrix.
- Invalid, duplicate, expired, or out-of-scope actions return documented Problem Details responses.
- Vietnamese Swagger and `.http` examples include successful bill completion/claim plus failure/retry cases and expected balances.
- Routes and JSON contract identifiers remain stable for frontend integration.
- Unit/integration tests verify calculations, transaction boundaries, access control, and idempotency.

## Dependencies

- TASK-007 session lifecycle and finalized extension/checkout handoff.
- TASK-004 authorization matrix.
- TASK-005 test identities and sample scenarios.

## Verification

Record actual calculation rules, tests, OpenAPI review, and any migration status when implemented.
