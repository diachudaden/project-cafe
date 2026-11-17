# TASK-016: Settle SePay payments into sessions and bills

## Status

Implemented; SQL Server concurrency verification remains pending.

## Goal

Complete eligible VietQR payment requests after a verified SePay transfer by reusing the existing check-in/extension business rules and creating the corresponding paid bill exactly once.

## Rationale

A verified webhook delivery alone is not a completed sale. The current payment request flow intentionally creates no bill; a correctly matched transfer must atomically finalize the request and its existing Cafe CR workflow.

## User value

After an exact payment, staff can see the newly active session or extension and a paid VietQR bill with points handled consistently with existing cash sales.

## References

- [../spec.md](../spec.md)
- [TASK-013](./TASK-013-vietqr-sepay.md)
- [TASK-014](./TASK-014-payment-requests-vietqr.md)
- [TASK-015](./TASK-015-sepay-webhook-ingestion.md)
- [TASK-007](./TASK-007-pos-session-lifecycle.md)
- [TASK-008](./TASK-008-billing-customer-points.md)
- [../docs/proposals/roles/05-decisions.md](../docs/proposals/roles/05-decisions.md), D19, D23, D26
- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/business-rules.md](../.rule/business-rules.md)
- [../.rule/testing.md](../.rule/testing.md)

## Data involved

`PaymentRequest`, `PaymentTransaction`, `Session`, `SessionExtension`, `Bill`, `PointTransaction`, `Seat`, `PricePlan`, `Shift`, and `Customer`.

## Scope

- On a verified inbound transaction with exactly one eligible payment code match and the exact expected amount, finalize the request as paid and run the matching check-in or extension workflow with `PaymentMethod.VietQr`.
- Extract/reuse the existing check-in and extension core behavior rather than duplicating business rules.
- Use the `ShiftId`, customer, plan, and session context captured by the payment request; webhooks have no staff principal.
- For check-in, if the held seat became unavailable, choose an available seat in the same branch with the same `SeatType`; if none exists, mark the request `NeedsAttention` and retain the transaction for authorized resolution.
- For extensions, validate that the associated session is still eligible before extending it.
- Persist the transaction outcome, payment-request state, session/extension, bill, and points changes in a transaction with retry-safe idempotency.
- Record underpaid, overpaid, expired, cancelled, ambiguous-code, already-paid, and unmatched events for follow-up without silently creating a bill.
- Preserve cash check-in and extension behavior and existing reports, which continue to use paid bills.

## Swagger language and contract

- Keep route paths, request/response field names, enum wire values, and operation IDs stable in English.
- Document settlement outcomes and manual follow-up requirements in Vietnamese.
- Never return provider secrets or transfer descriptions to POS clients.

## Acceptance criteria

- Exact matched check-in transfer creates one active session and one VietQR bill, linked from the payment request.
- Exact matched extension transfer adds one extension to the existing session and bill and awards points only once when eligible.
- A provider retry or concurrent duplicate cannot create another session, extension, bill, or points transaction.
- A competing seat causes same-branch/same-type fallback; no eligible fallback marks `NeedsAttention` without a misleading paid bill/session.
- Underpaid, overpaid, expired, cancelled, already-settled, ambiguous, and unmatched transactions do not create a completed sale automatically.
- Cash workflows and their expected totals remain unchanged.
- Tests exercise the above settlement cases and verify transaction rollback/idempotency.

## Dependencies

- TASK-014 payment request lifecycle.
- TASK-015 verified, idempotent SePay webhook ingestion.
- Existing TASK-007 session lifecycle and TASK-008 billing/points behavior.

## Out of scope

- SignalR events, receipt printer UI, and customer-facing payment UI.
- Manual transaction resolution UI beyond any minimal API contract explicitly approved for a later task.
- Production payment processing.

## Verification

- Run focused service and integration tests for check-in settlement, extension settlement, seat fallback, amount mismatches, and duplicate delivery.
- Run the complete .NET test suite.
- Verify cash workflow regression tests remain green.
- Do not apply migrations to an unintended database or use live payment data in automated tests.

## Implementation and verification results

- Exact authenticated inbound payments now settle check-ins and extensions within the webhook database transaction, recording the provider transaction, request, bill/session/extension, and eligible points together.
- Check-in settlement uses an available same-branch/same-seat-type fallback; when none exists, the request becomes `NeedsAttention` and no sale is created. Underpaid, overpaid, expired, cancelled, unmatched, and already-paid cases are retained without creating another sale.
- Existing bills track cash and VietQR subtotals so a VietQR extension of a cash-started session is reported as `Mixed` without changing cash closing totals. Updated the development history seeder and generated migration `20261108085322_BillTenderTotals` with a legacy-bill backfill.
- Tests cover exact check-in settlement/replay, wrong-account and amount mismatches, expiry, seat fallback/no-seat attention, and cash-plus-VietQR extension totals.
- Verification: full solution tests passed (84 unit tests, 8 integration tests); frontend typecheck passed; migration SQL preview and `git diff --check` passed.
- The new migration has not been applied to any database. Concurrent financial-write behavior has not been exercised against SQL Server; automated tests use EF InMemory.
