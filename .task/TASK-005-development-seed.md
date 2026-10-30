# TASK-005: Add repeatable development seed data

- Status: Planned
- Goal: Give developers and testers a safe, predictable dataset for exercising the API without manually creating every prerequisite record.

## User value

A developer can initialize an isolated local database with documented test accounts and related cafe records; a tester can reset/recreate the same scenarios and know what each account is allowed to do.

## References

- [../spec.md](../spec.md)
- [../.rule/security.md](../.rule/security.md)
- [TASK-004](./TASK-004-role-authorization.md)
- [TASK-003](./TASK-003-api-auth-crud.md)

## Scope

- Add an explicit, opt-in seed operation for Development/test environments only.
- Seed a minimal connected dataset: branches, zones, available and occupied seats as appropriate, price plans, customer accounts/profiles, staff/admin accounts, employee-branch assignments, and shifts only where needed for the planned scenarios.
- Use deterministic identifiers or another documented stable lookup mechanism.
- Source all seeded passwords from local environment/user secrets; never commit usable passwords or credentials.
- Document initial setup, rerun/idempotency behavior, and safe database reset instructions.
- Provide Vietnamese descriptions for seed-dependent Swagger examples and tester scenarios; keep endpoint paths and JSON field names unchanged.

## Safety requirements

- Normal API startup must not silently add/reset demo business data.
- Seed execution must fail outside Development/test and must never delete or overwrite non-seed records.
- Re-running the seed operation must be idempotent.
- Test accounts and sample data are never production defaults.

## Decisions required before implementation

- Confirm which sample branches, price plans, staff assignments, and test roles best support the first demos.
- Confirm whether a disposable local SQL Server database is available for reset-based test runs.

Use clearly labeled synthetic records if no real sample values are specified; do not imply they represent approved business pricing.

## Acceptance criteria

- A documented command creates the seed data only when explicitly requested in Development/test.
- Re-running it causes no duplicate records and preserves unrelated data.
- The seed includes test identities for every approved role and branch-scope case from TASK-004.
- No seeded password or secret appears in tracked files or logs.
- A tester can recreate the documented starting state and locate records using documented IDs/names.
- Automated tests verify idempotency and environment guarding without modifying a developer/shared/production database.
- Swagger and `.http` examples that rely on seeded records identify the required seed scenario and expected results.
- Swagger-facing descriptions/examples and tester guidance for seed scenarios are in Vietnamese; test credentials remain outside tracked files and Swagger.

## Dependencies

- TASK-004 approval of roles and branch-scoped access.

## Verification

Record the seed command, test database/environment, results, and reset procedure when implemented.
