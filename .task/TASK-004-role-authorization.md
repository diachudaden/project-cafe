# TASK-004: Define roles and branch-scoped authorization

- Status: Completed
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

- Role identifiers: `Admin`, `BranchManager`, `Staff`, and `Customer`, matching the project-reference roles.
- Permission scope for this task: keep all existing generic CRUD Admin-only; add reusable branch-scope and customer-self policies without granting Manager/Staff CRUD. Feature tasks will define the specific operations each role can perform.
- Branch scope: `EmployeeBranch` assignments determine branch access for BranchManager and Staff. `Admin` has global scope. `IsHome` does not change authorization. Customer-owned access is restricted to the authenticated user's own `userId`/`customerId`.
- Role assignment: a user may hold multiple roles; each endpoint must declare an explicit policy and scope.
- Token changes: role and branch claims are snapshots. Existing access tokens remain effective until the current 15-minute expiry; refresh reads current roles and branch assignments.

## Acceptance criteria

- The approved permission matrix is recorded in the canonical business/security documentation.
- Tests prove allowed and denied access for each role on representative global, branch-scoped, and personal resources.
- A user cannot bypass branch scope by supplying another branch ID, resource ID, or filter.
- Admin retains explicitly approved cross-branch access; managers cannot access unassigned branches.
- Responses and logs do not disclose secrets or unrelated users' sensitive fields.
- Swagger identifies required authentication and describes role/scope restrictions for each protected operation.
- Swagger text and examples are Vietnamese; routes and JSON contract identifiers remain stable for frontend integration.
- Developer handoff includes policy implementation notes; tester handoff includes accounts/branches, reproducible requests, and expected status/data visibility.
- Build and focused authorization tests pass; migrations, if needed, are generated but not applied to an unapproved database.

## Implementation notes

- Role bootstrap now provisions `Admin`, `BranchManager`, `Staff`, and `Customer`. Admin user management accepts any of these role identifiers, including multiple roles.
- Existing generic CRUD and user-management APIs remain Admin-only. Assigning BranchManager/Staff does not create an employee or branch assignment.
- BranchManager/Staff access tokens receive signed `branch_id` claims from current `EmployeeBranch` assignments. The `BranchScope` policy requires one of Admin/BranchManager/Staff and checks the requested `{branchId}` against the authenticated token. Admin has global scope.
- `BranchAccessScope.Apply` filters EF/LINQ queries by signed branch scope; feature endpoints must use it in addition to the route policy.
- `CustomerOwnResource` allows Admin or the Customer whose authenticated user ID matches the `userId` route, or owns the `customerId` profile.
- Access-token role/branch claims last until token expiry (15 minutes by default); refresh re-reads current Identity roles and EmployeeBranch assignments. No token-version database migration was needed.
- Swagger role/security guidance and `.http` request descriptions are in Vietnamese; paths and JSON field names remain unchanged.
- No branch-scoped feature route exists yet; its implementation must apply the provided policy and query-scope helper. The HTTP examples verify that non-Admin roles remain denied from existing generic CRUD.

## Dependencies

- TASK-003 completed.

## Verification

- `dotnet build CafeCR.slnx`: succeeded with 0 warnings and 0 errors.
- `dotnet test CafeCR.slnx --no-build`: passed all 18 tests (1 integration test and 17 unit tests).
- Tests cover Admin global branch scope, assigned/unassigned BranchManager and Staff access, Customer denial from branch policy, customer self-ownership, Admin bypass, branch-token snapshots, and filtered EF queries including related branch IDs.
- `git diff --check`: passed.
- No migration was generated or applied.
