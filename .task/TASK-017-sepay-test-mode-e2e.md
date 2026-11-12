# TASK-017: Verify SePay Test-mode end-to-end readiness

## Status

Blocked for provider/deployment verification; synthetic API verification and runbook are implemented.

## Goal

Demonstrate the complete VietQR-to-SePay-to-paid-bill flow in a controlled Test-mode environment and document the deployment/configuration and troubleshooting steps before relying on webhook delivery.

## Rationale

Passing service tests does not prove that the deployed route, reverse proxy, environment configuration, provider headers, and actual SePay payload work together. The integration must be tested end to end using a rotated, environment-only test secret.

## User value

The project team can configure SePay with confidence, distinguish delivery acknowledgement from successful settlement, and reproduce the demo without exposing credentials.

## References

- [../spec.md](../spec.md)
- [../../docs/payments/sepay-test-mode.md](../../docs/payments/sepay-test-mode.md)
- [TASK-013](./TASK-013-vietqr-sepay.md)
- [TASK-014](./TASK-014-payment-requests-vietqr.md)
- [TASK-015](./TASK-015-sepay-webhook-ingestion.md)
- [TASK-016](./TASK-016-sepay-payment-settlement.md)
- [../.rule/security.md](../.rule/security.md)
- [../.rule/testing.md](../.rule/testing.md)
- [../.docs/sepay/sepay-document.txt](../.docs/sepay/sepay-document.txt)

## Data involved

SePay Test-mode configuration, API environment variables, payment requests, webhook delivery results, sessions, and bills.

## Scope

- Add reproducible synthetic webhook test requests/scripts and document the expected signed headers and payload without embedding a secret.
- Verify SePay Test-mode's configured event payload and HMAC behavior against the implemented webhook.
- Verify the deployed public route reaches the API through nginx/reverse proxy and returns the expected status/body; use HTTPS for any externally reachable endpoint that will be used beyond isolated testing.
- Document how to set `Payment__Bank`, `Payment__AccountNumber`, `Payment__AccountHolder`, `Payment__CodePrefix`, `Payment__QrImageBaseUrl`, `Payment__HoldMinutes`, `Payment__WebhookSecret`, and `Payment__TimestampToleranceSeconds` only in environment configuration.
- Document safe migration application for the target database and how to verify applied migration/table presence.
- Run a full check-in and extension Test-mode scenario through QR creation, provider callback, final bill/session state, retry delivery, and UI/API status.
- Record unresolved limitations and provider configuration behavior; do not claim readiness if actual provider delivery cannot be exercised.

## Swagger language and contract

- Keep API route and JSON/header identifiers stable in English.
- Keep tester instructions, expected outcomes, and troubleshooting notes in Vietnamese for the project demo team.
- Examples must use synthetic values and must not contain a real webhook secret, bank account credential, access token, or personal transfer data.

## Acceptance criteria

- A deployed Test-mode inbound payment follows the configured HTTPS webhook route and creates the expected single session/bill or extension.
- Repeated delivery does not duplicate financial records.
- Invalid signature and stale timestamp requests are rejected; valid but unmatched events are distinguishable from completed payments.
- The runbook explains that HTTP 200 means the signed event was accepted/recorded, not necessarily that a bill was settled.
- Secrets are supplied only through ignored deployment environment files or a secret manager; no secret is committed or included in artifacts/logs.
- The applicable migration is confirmed on the intended database before the demo, without accidental updates to another environment.
- End-to-end evidence records environment, synthetic scenario, expected result, actual result, and any remaining blockers.

## Dependencies

- TASK-015 webhook verification and ingestion.
- TASK-016 payment settlement.
- TASK-014 database migration and payment request API.

## Out of scope

- Live bank settlement or production gateway certification.
- Frontend feature development beyond what is needed to exercise the already-planned payment flow.
- Persisting real customer or bank-transfer personal data in test evidence.

## Verification

- Run all API unit and integration tests.
- Run the documented SePay Test-mode check-in and extension scenarios against the intended test deployment.
- Inspect the repository and generated evidence for accidental secrets or personal transfer details.
- Document the actual results and explicitly mark unavailable provider/deployment checks as blocked rather than passed.

## Implementation and verification results

- Added [the Vietnamese Test-mode runbook](../../docs/payments/sepay-test-mode.md) and a standard-library Python script that signs synthetic events using environment variables only; no provider secret is embedded or printed.
- Added an ASP.NET pipeline integration test for a signed synthetic event and replay. The full solution passed (84 unit tests, 8 integration tests), frontend typecheck passed, and the migration SQL preview includes the new columns and backfill.
- Actual SePay Test-mode delivery, reverse-proxy/HTTPS behavior, target-database migration application, and full deployed check-in/extension scenarios remain unverified because no provider or deployment environment was exercised in this task. Rotate the previously disclosed secret before configuring the test environment.
