# TASK-002: Create the Cafe CR domain schema with EF Core Code First

- Status: Planned
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

## Dependencies

- None. This task must settle the schema decisions below before implementation; do not infer unspecified constraints or business behavior.

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

- Do not reintroduce the removed VietQR/SePay payment module, webhook, payment ledger, or payment-specific persistence.
- `PaymentMethod` in the reference schema may remain a simple simulated tender type on `Bill`; it is not a request to add a payment gateway.
- Do not implement the application use cases, controllers, authorization, seed data, or automatic migration-on-startup in this task.
- Do not add the full frontend or six-service Docker Compose architecture described elsewhere in the reference document.
- Do not apply migrations to an existing or persistent database.

## Decisions required before implementation

The reference describes the table names and high-level columns, but leaves schema details open. Resolve and record these in the task before coding:

- Whether `User` will be an ASP.NET Core Identity user with `Guid` keys, and how Identity-managed tables/roles relate to the 22 business tables. Do not store or validate passwords with custom code.
- Required/optional fields, maximum string lengths, delete behavior, and unique constraints for each relationship.
- Exact enums and allowed transitions for seats, shifts, assignments, sessions, bills, attendance, payroll, and shift requests.
- Whether `ShiftClosing` is one-to-one with `Shift` and how cross-midnight shifts are represented.
- How recurring versus date-specific `Availability` records are represented.
- Whether the omitted audit-log requirement needs an additional table or is deferred; the reference asks that monetary operations be audited but does not include `AuditLog` in its 22-table list.
- Whether `Guid.CreateVersion7()` is the selected key-generation mechanism and which layer generates IDs.

If these decisions cannot be confirmed from project requirements, keep implementation blocked and request clarification instead of inventing business rules.

## Acceptance criteria

- The Domain project contains the agreed 22 entities, with no EF Core dependency in Domain.
- The Infrastructure project owns EF Core configuration and references the proper lower layers without circular project references.
- Every agreed entity is mapped to the intended SQL table, with explicit relationships, nullability, keys, indexes, precision, timestamps, enum storage, and concurrency configuration.
- Unique constraints cover at minimum `Bills.Code`, the agreed user identity fields, and the active POS session rule (`UserSessions(UserId)` filtered to non-revoked POS sessions), if confirmed by the schema decisions.
- EF Core can generate and apply the migration to a disposable SQL Server database; no persistent database is modified.
- Unit/integration tests verify the agreed model and migration behavior.
- `dotnet build CafeCR.slnx` succeeds without warnings, and focused tests pass.
- The migration and model contain only the agreed Cafe CR schema; no removed payment gateway tables or webhook data are introduced.

## Verification

Run after implementation:

```powershell
dotnet build .\CafeCR.slnx
dotnet test .\CafeCR.slnx
```

Also apply the migration in an isolated disposable SQL Server test environment and verify the resulting table, key, and index definitions. Never point this check at a persistent developer or production database.

## Implementation notes

- The reference describes 17 core tables plus five supplementary tables, totaling 22 business tables.
- `vw_BranchAvailability` is a database view, not one of the 22 entities. `DailyRevenue` is a suggested reporting aggregate, not part of the 22-table baseline; defer both unless separately approved.
- EF Core Identity may add framework-managed tables beyond the 22 business tables. Record that distinction in the schema preview and migration review.
