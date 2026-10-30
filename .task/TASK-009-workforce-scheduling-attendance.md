# TASK-009: Implement workforce scheduling and attendance

- Status: Planned
- Goal: Let authorized managers schedule staff and let assigned employees respond to shifts and record attendance.

## User value

Managers can create and staff branch shifts; employees can view and respond to assignments; attendance records can be recorded and reviewed.

## References

- [../spec.md](../spec.md)
- [../docs/payments/reference/project.md](../docs/payments/reference/project.md), workforce roles and “Luồng ca làm của nhân viên”
- [TASK-004](./TASK-004-role-authorization.md)
- [TASK-005](./TASK-005-development-seed.md)

## Data involved

`Employee`, `EmployeeBranch`, `Availability`, `Shift`, `ShiftAssignment`, `ShiftRequest`, and `Attendance`.

## Scope

- Create/list/update/publish shifts within authorized branch scope.
- Assign employees, expose assignments to the intended employee, and support accept/decline.
- Support approved leave/drop/swap request operations and manager review.
- Record attendance check-in/out using the approved PIN/QR mechanism and review/approval workflow.
- Enforce scheduling conflicts on the server and use concurrency protection for assignments and state changes.
- Add Swagger state/action descriptions and `.http` examples with seeded staff/branches.

## Swagger language and contract

- Write summaries, descriptions, DTO field explanations, state transitions, role/branch requirements, scheduling rules, response/error descriptions, and examples in Vietnamese.
- Explain warnings versus blocking errors and manager override behavior in Vietnamese once those rules are approved.
- Keep route paths, JSON property names, enum wire values, and operation IDs stable/English for frontend integration. Do not translate wire-contract identifiers solely for display.
- Use synthetic seed examples only; never show real PINs, QR secrets, or credentials in Swagger.

## Decisions required before implementation

- Confirm rules from the reference: 8-hour rest, 60-minute cross-branch travel, 48-hour weekly warning, overlap blocking, leave conflicts, and which constraints managers may override with reasons.
- Confirm shift state transitions, publication/notification timing, who may approve attendance, and late-arrival handling.
- Confirm PIN setup/reset/rate limits or QR generation/expiry; do not accept raw PINs in responses/logs.
- Confirm whether managers can view payroll or only schedules/attendance (payroll operations are TASK-010).

## Acceptance criteria

- Manager/staff operations respect approved role and branch scope, including employees assigned to multiple branches.
- Server-side checks enforce confirmed overlap/rest/travel/availability/leave rules; client-side checks are not relied upon.
- Assignment and attendance transitions reject invalid/replayed requests with documented status codes.
- Attendance credentials are stored/checked safely and never returned or logged.
- Vietnamese Swagger documents each operation, state transition, role, branch scope, request/response, and failure.
- Route paths, JSON contract identifiers, and enum wire values remain stable for frontend integration.
- `.http` scenarios and automated tests cover publish, accept/decline, conflicts, branch denial, check-in/out, and approval.
- Tester handoff lists seeded accounts and expected shift/attendance state after each scenario.

## Dependencies

- TASK-004 role/scope matrix and approval rules.
- TASK-005 seeded branches, employees, and test accounts.

## Verification

Record the final conflict rules, test/build results, OpenAPI review, and unapplied migrations when implemented.
