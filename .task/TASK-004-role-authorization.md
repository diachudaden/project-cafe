# TASK-004: Define roles and branch-scoped authorization

- Status: Planned
- Goal: Replace the current Admin-only feature access baseline with the approved Cafe CR role and branch authorization model.

## User value

Administrators, branch managers, employees, and customers can use only the API operations and records appropriate to their role and assigned branch.

## References

- [../spec.md](../spec.md)
- [../.rule/security.md](../.rule/security.md)
- [../docs/payments/reference/project.md](../docs/payments/reference/project.md), “Vai trò & phân quyền”
- [TASK-003](./TASK-003-api-auth-crud.md)

## Scope

- Confirm and document a role × resource × operation permission matrix for Admin, BranchManager, Staff, and Customer.
- Define how branch scope is represented and evaluated, including users assigned to multiple branches and the home-branch relationship.
- Add policy/handler-based authorization and safe branch-scoped query enforcement.
- Restrict existing generic Admin CRUD and future feature endpoints consistently; keep Identity and refresh-token storage private.
- Add safe admin workflows for managing roles and employee/branch assignments as required by the approved matrix.
- Document OpenAPI security/role requirements and update relevant `.http` examples.

## Swagger language and contract

- Write all Swagger summaries, descriptions, parameter/DTO field summaries, response/error descriptions, and examples in Vietnamese so the Vietnamese frontend team and demo audience can understand them.
- Explain the required role and branch scope in Vietnamese for every protected operation, including why a request is denied.
- Keep route paths, JSON property names, enum wire values, and operation IDs stable/English for frontend integration. Do not translate or rename wire-contract identifiers solely for display.
- Use realistic synthetic examples and clearly label development-only credentials/data; never put secrets in OpenAPI examples.

## Decisions required before implementation

- Confirm whether the reference role names map to `Admin`, `Manager`, `Staff`, and `Customer`, or to different identifiers.
- Confirm resource-level permissions, including who may view/edit payroll, attendance, bills, expenses, and customer data.
- Confirm whether branch managers can access only assigned branches and how Admin exceptions/role combinations work.
- Define access-token behavior when a user's role or branch assignment changes.

If these decisions are not resolved, keep the task blocked rather than inventing access rules.

## Acceptance criteria

- The approved permission matrix is recorded in the canonical business/security documentation.
- Tests prove allowed and denied access for each role on representative global, branch-scoped, and personal records.
- A user cannot bypass branch scope by supplying another branch ID, resource ID, or filter.
- Admin retains explicitly approved cross-branch access; managers cannot access unassigned branches.
- Responses and logs do not disclose secrets or unrelated users' sensitive fields.
- Swagger identifies required authentication and describes role/scope restrictions for each protected operation.
- Swagger text and examples are Vietnamese; routes and JSON contract identifiers remain stable for frontend integration.
- Developer handoff includes policy implementation notes; tester handoff includes accounts/branches, reproducible requests, and expected status/data visibility.
- Build and focused authorization tests pass; migrations, if needed, are generated but not applied to an unapproved database.

## Dependencies

- TASK-003 completed.

## Verification

Record commands, results, unresolved blockers, and migration status when implemented.
