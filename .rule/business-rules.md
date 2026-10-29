# Business rules

Canonical product behavior is in [../spec.md](../spec.md). Do not invent screens, roles, or auth flows that are not recorded there.

## First slice

- Three apps: `admin` (Admin, BranchManager), `pos` (Staff), `customer` (Customer).
- Business screens in the first slice use isolated sample data. Sample data is not a system of record and is not written to the API.
- Authentication is real: identifier (email or Vietnamese phone) + password against the backend. Do not fake a successful sign-in.

## Open

Rules for pricing, seating sessions, shifts, payroll, loyalty, and payments are not approved for implementation until they are added to `spec.md`.
