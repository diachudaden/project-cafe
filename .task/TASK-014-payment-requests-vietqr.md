# TASK-014: VietQR payment requests, seat holds, and expiry

## Status

Completed.

## Goal

Allow authenticated POS staff to create server-priced VietQR requests for check-in or session extension, hold the associated seat while payment is pending, and release the hold when the request expires or is cancelled.

## User value

Staff can show a customer a QR code without prematurely creating a bill or session. Other POS devices and public availability lookups treat the held seat as unavailable until the request is resolved.

## References

- [../spec.md](../spec.md)
- [TASK-013](./TASK-013-vietqr-sepay.md)
- [TASK-007](./TASK-007-pos-session-lifecycle.md)
- [TASK-008](./TASK-008-billing-customer-points.md)
- [../docs/proposals/roles/05-decisions.md](../docs/proposals/roles/05-decisions.md), D19-D29
- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/business-rules.md](../.rule/business-rules.md)
- [../.rule/testing.md](../.rule/testing.md)

## Data involved

`PaymentRequest`, `PaymentTransaction`, `Seat`, `Session`, `PricePlan`, `Shift`, `Customer`, and `Branch`.

## Scope

- Add the EF migration for the existing payment request and provider transaction entities. Generate the migration only; do not apply it to a shared or production database.
- Implement `POST /api/payments/requests` for check-in and `POST /api/sessions/{id}/extend/payment-request` for extensions. Derive price, branch, shift, seat, and session details on the server; never trust client-supplied amounts or shift IDs.
- Return the approved `{ code, amount, qrUrl, expiresAt }` response using escaped VietQR query parameters.
- Implement payment request status lookup by code and cancellation by the creating staff member or an authorized manager/admin.
- Prevent cash check-in, another payment request, public availability, and the POS floor map from treating an unexpired pending payment hold as available.
- Expire pending requests using a periodic hosted service and provide a testable expiration operation.
- Keep bill creation and session finalization out of this task; these are performed only after confirmed payment in the remaining SePay settlement work under TASK-013.

## Swagger language and contract

- Document operation summaries, descriptions, authorization, validation, and response/error behavior in Vietnamese.
- Keep route paths, JSON property names, enum wire values, and operation IDs in English.
- Request bodies must not accept amount, branch, shift, status, code, or expiry values from clients.

## Acceptance criteria

- Check-in and extension payment requests use the persisted, applicable price plan and correct shift/session context.
- Invalid, inactive, out-of-scope, maintenance, occupied, or already-held seats cannot receive a check-in request.
- A pending unexpired request makes its seat unavailable to public availability, the floor map, and cash check-in.
- The database's filtered unique index prevents two concurrent pending holds for one seat.
- Expiry and authorized cancellation release the seat; an expired request cannot be reported as pending.
- QR parameters are escaped and amount formatting is invariant-culture VND.
- Focused tests cover request validation, amount derivation, occupied/held seat handling, expiry/cancellation, availability, and schema metadata.
- A named EF migration and model snapshot include the payment tables; no database update is run against shared or production data.

## Dependencies

- TASK-013 approved payment scope and decisions.
- TASK-007 session lifecycle and TASK-008 bill behavior.

## Verification

- Run the focused workflow and model test classes.
- Build the API and test project.
- Generate an EF migration without applying it to any database.
- Record implementation notes, actual commands, results, and remaining limitations below.

## Implementation

- Added the `PaymentRequestTables` migration for payment requests and provider transactions; it was generated but not applied to any database.
- Added server-priced check-in and extension payment request endpoints, public status lookup, and authorized cancellation.
- Reused session validation for branch scope, active pricing, shift, customer, maintenance, occupied-seat, and active-session checks.
- Added the filtered unique pending-seat index usage to hold a seat, returned `Held` from the POS floor map, excluded active holds from public availability, and blocked cash check-in while a hold is active.
- Added a 30-second background expiration worker and an explicit expiration operation for deterministic service tests.
- Added focused service and EF model tests. Existing SePay settlement is not completed by this task; payment confirmation/finalization remains in the overall TASK-013 scope.

## Verification results

- `dotnet test backend/tests/ChonRieng.UnitTests/ChonRieng.UnitTests.csproj --filter "FullyQualifiedName~PaymentRequestTests|FullyQualifiedName~DomainSchemaModelTests"` — passed, 13/13.
- `dotnet test CafeCR.slnx` — passed, 70 unit tests and 7 integration tests.
- `npm run typecheck` from `frontend/` — passed.
- `ConnectionStrings__DefaultConnection='Server=localhost;Database=CafeCrMigrationDesign;Integrated Security=True;TrustServerCertificate=True' dotnet ef migrations add PaymentRequestTables --project backend/src/ChonRieng.Infrastructure --startup-project backend/src/ChonRieng.Api --output-dir Persistence/Migrations` — succeeded; no database was updated.
- `ConnectionStrings__DefaultConnection='Server=localhost;Database=CafeCrMigrationDesign;Integrated Security=True;TrustServerCertificate=True' dotnet ef migrations script --idempotent --project backend/src/ChonRieng.Infrastructure --startup-project backend/src/ChonRieng.Api --output /tmp/cafecr-payment-migrations.sql` — succeeded; generated SQL includes `PaymentRequests`, `PaymentTransactions`, and the filtered unique pending-seat index.
- `git diff --check` — passed.
