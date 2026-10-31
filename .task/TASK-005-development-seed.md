# TASK-005: Add repeatable development seed data

- Status: Completed
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

- Approved: two clearly marked synthetic branches (A/B), one zone and two available seats per branch, demo-only 240-minute price plans, BranchManager and Staff accounts assigned only to branch A, and one Customer account. The existing environment-provisioned Admin is the Admin test account.
- Approved: a disposable local SQL Server database is available for any manual clean-database run. Automated tests use an isolated EF Core in-memory database and never touch it.

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

- Added the explicit `--seed-development-data` command. It fails unless `ASPNETCORE_ENVIRONMENT` is `Development` or `Testing`; normal API startup never invokes the business-data seeder.
- The command requires an existing migrated database, the configured bootstrap Admin, and local `DevelopmentSeed__Password`. It does not migrate or reset the database.
- Stable lookup uses marked branch/zone/seat/price-plan names and reserved `.invalid` demo emails. The seeder only inserts missing records; it does not overwrite existing records. Conflicting demo-account identity data causes an explicit error.
- Seeded records include two branches, two zones, four available seats, two clearly labeled sample price plans (1,000 VND / 240 minutes, 250 VND extension), BranchManager and Staff identities scoped only to branch A, and a Customer identity/profile.
- Admin is the account from `BootstrapAdmin__Email`; no extra admin password is stored in the repository. Demo account passwords come from ignored local `DevelopmentSeed__Password`.
- Employee PIN hashes use random, discarded values; no PIN is seeded or documented. Shifts, sessions, occupied seats, and transactional records are deferred until their feature workflows exist.
- Updated `.env.example`, Compose environment forwarding, README host/container run instructions, and Vietnamese `.http` login scenarios. No Swagger operation was added because seeding is a local CLI command, not an API endpoint.
- `dotnet test backend/tests/ChonRieng.UnitTests/ChonRieng.UnitTests.csproj --filter FullyQualifiedName~DevelopmentDataSeederTests`: passed 3 tests with an isolated EF Core in-memory database.
- `dotnet build CafeCR.slnx`: succeeded with 0 warnings and 0 errors.
- `dotnet test CafeCR.slnx --no-build`: passed 21 tests (1 integration and 20 unit tests).
- `git diff --check`: passed.
- `ASPNETCORE_ENVIRONMENT=Production dotnet run --project backend/src/ChonRieng.Api --no-build --no-launch-profile -- --seed-development-data`: rejected by the environment guard before database access, as expected.
- Manual SQL Server seed execution: not run; a disposable local SQL Server is available to the developer. No local or shared database was modified.
