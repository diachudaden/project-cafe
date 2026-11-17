# TASK-015: Secure SePay webhook verification and ingestion

## Status

Implemented; SQL Server concurrent-delivery verification remains pending.

## Goal

Make the SePay webhook a safe, retry-tolerant ingress for provider events by validating the documented HMAC request format, parsing the payload, and recording each provider transaction idempotently.

## Rationale

The existing endpoint has a verification scaffold, but it does not yet verify the exact raw body bytes using a constant-time comparison or safely coordinate duplicate deliveries and payment settlement. SePay should not be configured to drive real app behavior until this ingress contract is implemented and tested.

## User value

Only authenticated SePay events enter payment processing, and provider retries do not create duplicate transaction records or trigger payment work more than once.

## References

- [../spec.md](../spec.md)
- [TASK-013](./TASK-013-vietqr-sepay.md)
- [TASK-014](./TASK-014-payment-requests-vietqr.md)
- [../docs/proposals/roles/05-decisions.md](../docs/proposals/roles/05-decisions.md), D25-D26
- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/security.md](../.rule/security.md)
- [../.rule/testing.md](../.rule/testing.md)
- [../.docs/sepay/sepay-document.txt](../.docs/sepay/sepay-document.txt) (provider's HMAC header/signing contract; do not copy its secret into source or task documentation)

## Data involved

`PaymentTransaction`, `PaymentRequest`, webhook headers, and the SePay transfer payload.

## Scope

- Read and authenticate the exact raw request body bytes using `X-SePay-Timestamp`, `X-SePay-Signature`, and HMAC-SHA256 over `{timestamp}.{raw_body}`.
- Enforce timestamp tolerance and compare the expected signature using `CryptographicOperations.FixedTimeEquals`.
- Parse and validate the documented SePay payload, including required provider transaction ID, account, transfer direction, amount, code/content, and Vietnam-local transaction time conversion.
- Persist provider events idempotently using the unique provider transaction identity; handle simultaneous duplicate deliveries safely.
- Classify ignored, unmatched, underpaid, overpaid, and potentially matched inbound events without claiming that a bill/session was completed. Successful payment settlement is TASK-016.
- Return 401 for missing/invalid authentication; after a valid signature, follow the approved acknowledgement behavior for ignored or recorded events. Infrastructure failures remain visible as server errors so SePay can retry.
- Avoid logging raw webhook bodies or customer-identifying transfer descriptions.
- Keep the public route `POST /api/hooks/sepay-payment`.

## Swagger language and contract

- Keep the route and header names stable in English.
- Document the webhook purpose and authentication behavior in Vietnamese.
- Do not expose the configured secret or return sensitive provider payload details.

## Acceptance criteria

- Valid signature is accepted; invalid, missing, malformed, or stale signature/timestamp is rejected.
- Verification uses the exact raw body bytes, and signature comparison is constant-time.
- Valid signed payloads parse and store amounts/times correctly; transaction timestamps in Vietnam local time are converted to UTC.
- Replaying the same provider transaction, including concurrent delivery, results in one durable transaction record and no duplicate downstream work.
- Non-inbound transfers and wrong-account events cannot settle a payment.
- Valid signed but unmatched/ignored events receive the approved success acknowledgement; infrastructure failures are not hidden as success.
- Tests cover valid/invalid/missing headers, timestamp tolerance, raw-body tampering, malformed payloads, wrong account/direction, and repeated delivery.
- No payment secret is added to source control, task documentation, scripts, or test fixtures.

## Dependencies

- TASK-013 approved payment scope.
- TASK-014 payment request entities and migration.

## Out of scope

- Creating a session, bill, extension, or points award; see TASK-016.
- SignalR, customer notification, and POS/customer UI work.
- Production deployment or enabling real transfers.

## Verification

- Run focused webhook unit/integration tests and the complete .NET test suite.
- Verify the endpoint through the real ASP.NET request pipeline using synthetic signed requests.
- Review logs and responses to ensure secrets and raw transfer payloads are not disclosed.

## Implementation and verification results

- Added `SePayWebhookService` and wired `POST /api/hooks/sepay-payment` to verify the exact bounded raw body using the timestamped HMAC-SHA256 contract and constant-time comparison.
- Valid signed events are parsed with numeric/string compatibility, validated, timestamped in UTC from Vietnam-local provider time, and stored with duplicate-provider-ID protection inside a relational transaction. Outbound, wrong-account, and unmatched events are recorded without settlement.
- Added focused tests for missing/invalid/stale signatures, raw-body tampering, malformed payloads, number/string values, UTC conversion, wrong account/direction, and repeated delivery. A real ASP.NET HTTP-pipeline test exercises a signed event and replay.
- Verification: full solution tests passed (84 unit tests, 8 integration tests); frontend typecheck passed; `git diff --check` passed.
- The database unique index and duplicate-key handling protect concurrent duplicate deliveries, but the available automated database is EF InMemory; SQL Server concurrency has not been exercised. No database was updated.
