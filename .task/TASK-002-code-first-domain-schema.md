# TASK-002: Create the Cafe CR domain schema with EF Core Code First

- Status: Completed
- Goal: Implement the 22 business entities/tables described in the project reference, configure their SQL Server mappings, and generate an EF Core Code First migration without applying it to a persistent database.
- Rationale: The API repository is currently a minimal scaffold with empty Domain and Infrastructure projects. The schema must be agreed and represented in code before feature implementation can safely build on it.

## Specification references

- [../spec.md](../spec.md)
- [../README.md](../README.md)
- [../docs/payments/reference/project.md](../docs/payments/reference/project.md), sections “Thiết kế CSDL” and “Kiến trúc kỹ thuật”

## Rule references

- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/business-rules.md](../.rule/business-rules.md)
- [../.rule/coding-style.md](../.rule/coding-style.md)
- [../.rule/testing.md](../.rule/testing.md)
- [../.github/instructions/backend.instructions.md](../.github/instructions/backend.instructions.md)
- [../.github/instructions/tests.instructions.md](../.github/instructions/tests.instructions.md)

## Decisions resolved before implementation

- `User` extends `IdentityUser<Guid>` and maps to `Users`; the six Identity support tables are additional framework tables. Email and non-null phone numbers are unique.
- All business primary keys use `Guid.CreateVersion7()`. Identity roles are supplied by ASP.NET Core Identity rather than a custom `Role` property.
- VND amounts use `decimal(12,0)`; elapsed payroll hours use `decimal(8,2)`. Timestamps use `DateTimeOffset`; shift calendar dates use `DateOnly`; branch and availability wall-clock times use `TimeOnly`.
- Status/type values explicitly listed in the reference are byte-backed enums: seat type/status, shift type/status, assignment status, session status, simulated bill payment method, and shift request type. Status/category/method vocabularies not defined by the reference remain required strings rather than invented enums or transitions.
- Relationships use restrictive deletes except employee-branch link rows and session-extension child rows, which cascade with their owner. The `ShiftClosing` relationship is one-to-one.
- Shifts store full `StartAt`/`EndAt` timestamps, so an overnight shift ends on the following date; `EndAt` must be later than `StartAt`.
- Availability supports recurring weekdays and specific dates using `IsRecurring`, `DayOfWeek`, and `SpecificDate`, with a database check constraint requiring exactly one matching schedule mode.
- Audit log is deferred to a separate task, preserving the 22 business-table baseline. `Denominations` is stored as serialized JSON text in `nvarchar(max)` for SQL Server provider compatibility.

## Scope

1. Add Domain entities for the 17 core tables:
   - Branch: `Branch`
   - Zones and seating: `Zone`, `Seat`
   - Pricing: `PricePlan`
   - Users and profiles: `User`, `Employee`, `Customer`
   - Scheduling: `Shift`, `ShiftAssignment`, `Attendance`, `ShiftClosing`
   - Sales: `Session`, `SessionExtension`, `Bill`, `PointTransaction`
   - Finance: `Expense`, `Payroll`
2. Add the five supplementary tables:
   - `EmployeeBranch`
   - `Availability`
   - `ShiftRequest`
   - `Notification`
   - `UserSession`
3. Configure `AppDbContext`, entity relationships, keys, indexes, enum conversions, concurrency fields, and SQL column types in Infrastructure using EF Core Code First.
4. Use `Guid` primary keys and the specified VND precision (`decimal(12,0)`) where the design defines monetary values. Store timestamps as `DateTimeOffset`.
5. Add the SQL Server EF Core provider and design-time migration support required by the existing .NET 10 projects.
6. Generate an initial migration for the schema. Do not apply it to a developer, shared, staging, or production database.
7. Add focused tests for the EF model and migration/schema behavior.

## Explicit exclusions

- Do not reintroduce the removed VietQR/SePay payment module, webhook, payment ledger, or payment-specific persistence; the approved payment scope is replaced by TASK-013 and must be implemented there instead.
- `PaymentMethod` in the reference schema may remain a simple simulated tender type on `Bill`; it is not a request to add a payment gateway.
- Do not implement the application use cases, controllers, authorization, seed data, or automatic migration-on-startup in this task.
- Do not add the full frontend or six-service Docker Compose architecture described elsewhere in the reference document.
- Do not apply migrations to an existing or persistent database.

## Acceptance criteria

- The Domain project contains the agreed 22 entities, with no EF Core dependency in Domain.
- The Infrastructure project owns EF Core configuration and references the proper lower layers without circular project references.
- Every agreed entity is mapped to the intended SQL table, with explicit relationships, nullability, keys, indexes, precision, timestamps, enum storage, and concurrency configuration.
- Unique constraints cover `Bills.Code`, normalized user email, non-null phone numbers, one employee/customer profile per user, unique shift assignments per employee, one bill per session, one home branch per employee, one payroll per employee-month, and at most one active POS session per user.
- EF Core can generate and apply the migration to a disposable SQL Server database; no persistent database is modified.
- Unit/integration tests verify the agreed model and migration behavior.
- `dotnet build CafeCR.slnx` succeeds without warnings, and focused tests pass.
- The migration and model contain only the agreed Cafe CR schema; no removed payment gateway tables or webhook data are introduced.

## Verification

Executed after implementation:

```powershell
dotnet build .\CafeCR.slnx
dotnet test .\CafeCR.slnx
dotnet ef migrations script --idempotent --project .\backend\src\ChonRieng.Infrastructure --startup-project .\backend\src\ChonRieng.Api --output .\backend\src\ChonRieng.Infrastructure\Persistence\Migrations\InitialCafeCrSchema.sql
docker compose build --quiet
```

- Build: succeeded with 0 warnings and 0 errors.
- Tests: 6 unit/model tests and 1 existing integration-project test passed.
- Docker Compose image build: succeeded.
- The initial migration was applied to an isolated disposable SQL Server 2025 container; SQL Server reported 28 application tables excluding `__EFMigrationsHistory`. Key tables `Users`, `Branches`, `Bills`, `Shifts`, and `UserSessions` were present.
- Package vulnerability check: no vulnerable packages reported for the API.
- No developer, shared, staging, or production database was modified.

## Implementation notes

- The reference describes 17 core tables plus five supplementary tables, totaling 22 business tables.
- `vw_BranchAvailability` is a database view, not one of the 22 entities. `DailyRevenue` is a suggested reporting aggregate, not part of the 22-table baseline; defer both unless separately approved.
- EF Core Identity adds six supporting tables; the generated schema has 28 tables total (22 business tables including `Users`, plus six Identity tables).
- Migration `20261028050512_InitialCafeCrSchema` and its idempotent SQL preview are under `backend/src/ChonRieng.Infrastructure/Persistence/Migrations`. Migrations are not applied automatically at API startup.
- The SQL preview contains no gateway tables. Exact maximum string lengths and non-explicit unique indexes are conservative implementation choices in the Infrastructure configurations; review them before using this initial migration against a persistent database.
