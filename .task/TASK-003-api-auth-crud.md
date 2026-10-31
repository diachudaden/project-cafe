# TASK-003: Add authentication, authorization, and admin CRUD API

- Status: Completed
- Goal: Provide a secure API foundation for customer authentication and administrator management of the agreed business entities.

## Specification and rule references

- [../spec.md](../spec.md)
- [../README.md](../README.md)
- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/business-rules.md](../.rule/business-rules.md)
- [../.rule/security.md](../.rule/security.md)
- [../.github/instructions/backend.instructions.md](../.github/instructions/backend.instructions.md)
- [../.github/instructions/tests.instructions.md](../.github/instructions/tests.instructions.md)

## Decisions

- Use ASP.NET Core Identity for user/password management and JWT bearer authentication for API requests.
- Use short-lived access tokens and rotating, revocable refresh tokens persisted as hashes in `UserSessions`. Keep refresh-token sessions internal to authentication endpoints, not generic CRUD.
- Public registration creates a `Customer` and assigns the `Customer` role. Email verification is deferred because there is no email delivery service; immediate sign-in is allowed for this prototype baseline.
- Provision the first administrator from environment configuration. Never seed a hard-coded password or signing key.
- Only the `Admin` role can access business-resource CRUD. The `Customer` role is assigned at registration; no broader role or branch-scope matrix is established.
- Use explicit API request/response DTOs. Never expose Identity persistence fields, password hashes, employee PIN hashes, or refresh-token hashes.
- Keep EF migration application manual; do not migrate automatically at API startup.

## Scope

1. Configure runtime SQL Server persistence, Identity, authentication, authorization, validation, Problem Details, and Swagger bearer-token support.
2. Add registration, login, refresh-token rotation, logout/revocation, and safe authenticated user information endpoints.
3. Add Admin-only paginated CRUD for business entities, using explicit DTOs and handling missing resources, database constraint conflicts, and concurrency conflicts.
4. Extend `UserSessions` as needed to support refresh-token expiry and generate a migration; do not apply it to a persistent database.
5. Document local configuration, bootstrap requirements, endpoints, and migration steps; add focused tests and HTTP request examples.

## Acceptance criteria

- Missing or invalid JWT configuration is reported explicitly; no signing secret is committed.
- Only valid bearer tokens authenticate protected routes; all business CRUD routes require `Admin`.
- Registration cannot assign a privileged role. Login issues access and refresh tokens; refresh rotates the persisted token; logout revokes it.
- User-facing responses do not contain password hashes, PIN hashes, or refresh-token hashes.
- Create/read/update/delete use proper HTTP status codes and OpenAPI metadata; list endpoints are bounded and paginated.
- Database constraint and concurrency failures return safe Problem Details responses, not internal exception details.
- Swagger documents bearer authentication and the available API endpoints.
- A migration captures any schema changes; applying it is a separate operator action.
- Focused tests and the backend build pass.

## Verification

- `dotnet build CafeCR.slnx`: succeeded with 0 warnings and 0 errors.
- `dotnet test CafeCR.slnx`: passed; 1 integration-project test and 8 unit/model/token tests.
- `dotnet ef migrations add AddRefreshTokenExpiry`: succeeded. The idempotent SQL preview was regenerated and includes the `UserSessions.ExpiresAt` change.
- `docker compose config --quiet`: succeeded.
- `docker compose build --quiet`: not verified because the Docker daemon is unavailable in this environment.
- The new migration was not applied to any database. The operator must review and apply it to the intended database before using refresh-token authentication.
- End-to-end authentication and CRUD were not exercised against a live database in this environment.
