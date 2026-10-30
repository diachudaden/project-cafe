# Security rules

## Secret handling

- Never commit secrets, keys, or environment-specific values.
- Keep secrets in local user secrets or deployment environment variables.
- Do not add a webhook secret or other credentials to the repository.

## Logging

- Avoid logging full sensitive payloads at info level.
- Keep logs minimal and safe for production monitoring systems.

## Approved API authorization baseline

- Business role identifiers are `Admin`, `BranchManager`, `Staff`, and `Customer`; an account may have multiple roles.
- `Admin` has global branch scope. `BranchManager` and `Staff` are scoped to branch IDs assigned through `EmployeeBranch`; `IsHome` does not grant or limit access.
- Branch IDs are included as signed `branch_id` claims when an access token is issued. A role or branch-scope change does not alter an already issued access token; it remains effective until its 15-minute expiry. Refreshing re-reads current roles and branch assignments.
- `Customer` access to customer-owned resources is limited to the authenticated user's own user/customer ID. Admin retains global management access.
- The current generic `/api/admin/*` CRUD and user-management endpoints remain Admin-only. Assigning `BranchManager` or `Staff` does not grant generic CRUD.
- A new branch-scoped endpoint must require the `BranchScope` policy and filter its database query with the authenticated principal's `BranchAccessScope`; checking only a client-supplied branch ID is insufficient.
- A new customer-owned endpoint must require `CustomerOwnResource` using a `userId` or `customerId` route value. Do not rely on client-supplied owner IDs in request bodies.
- Feature-specific permissions (current shift, payroll visibility, manager approvals, etc.) must be defined in the relevant task before access is granted.
- Swagger security descriptions and examples are written in Vietnamese; route paths, JSON property names, enum wire values, and operation IDs stay stable.
