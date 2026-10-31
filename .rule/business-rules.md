# Business rules

## Authorization baseline

- The supported role identifiers are `Admin`, `BranchManager`, `Staff`, and `Customer`; accounts may hold multiple roles.
- `Admin` has global branch scope. `BranchManager` and `Staff` use branch assignments represented by `EmployeeBranch`; the home-branch marker does not itself grant access.
- `Customer` is limited to their own customer resources. Generic CRUD remains restricted to `Admin` until a feature task specifies and implements narrower operations.
- Branch and role claims in a JWT are snapshots for at most 15 minutes. Refresh tokens generate a new access token from current roles and branch assignments.

Feature-specific business behavior and permissions remain undefined until established by their task acceptance criteria.

Add rules here when the corresponding product behavior and acceptance criteria are defined.
