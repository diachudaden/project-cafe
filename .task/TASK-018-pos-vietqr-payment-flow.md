# TASK-018: Add VietQR payment flow to POS

## Status

Planned.

## Goal

Let a cashier create and monitor VietQR payment requests from the existing POS check-in and extension workflows, then automatically show the completed session/bill after the verified SePay callback settles the request.

## Rationale

The backend already creates payment requests and settles exact, verified SePay payments, but the POS still treats VietQR like an already-collected tender. A focused QR-and-status flow is the smallest useful step toward a continuous Test-mode demo without introducing SignalR or unrelated customer-app work.

## User value

The cashier can create a payment request, show the QR and amount to the customer, and wait on one clear screen until the payment succeeds, expires, or needs attention.

## References

- [../spec.md](../spec.md)
- [TASK-013](./TASK-013-vietqr-sepay.md)
- [TASK-012](./TASK-012-role-apps.md)
- [TASK-014](./TASK-014-payment-requests-vietqr.md)
- [TASK-015](./TASK-015-sepay-webhook-ingestion.md)
- [TASK-016](./TASK-016-sepay-payment-settlement.md)
- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/business-rules.md](../.rule/business-rules.md)
- [../.rule/testing.md](../.rule/testing.md)

## Scope

- Add typed payment-request create, status, and cancel calls to the shared API client using the existing backend routes and response contracts.
- Update the POS check-in flow so Cash preserves its current behavior and VietQR creates a pending request instead of immediately checking in.
- Show the request QR image, VND amount, transfer code, seat/plan summary, expiry countdown, and a clear pending-payment state in a cashier-friendly modal or drawer.
- Poll the existing public payment-status endpoint while the payment screen is open, at a modest interval (about 2–3 seconds); stop polling on terminal states and refresh the floor/current shift after settlement.
- On settlement, show a clear success state with the final session and bill details; do not claim payment success based only on an HTTP 200 webhook acknowledgement.
- Add the same QR-request and status flow for session extension, reusing the existing extension payment-request endpoint and refreshing the active bill/session after settlement.
- Support expiry and an explicit cashier cancel action; handle a cancel-versus-payment race by refreshing status and displaying the authoritative server result.
- Keep polling as the demo-first update mechanism. SignalR, customer-facing payment UI, receipt printing, and admin transaction-resolution UI are out of scope.
- Keep user-visible copy in Vietnamese and do not display or log webhook secrets or raw provider payloads.

## UX flow

1. Cashier chooses a seat, plan, optional member, and payment method.
2. Cash continues through the current immediate check-in flow.
3. VietQR creates a pending request and opens the QR waiting screen.
4. The screen polls status until Paid, Expired, Cancelled, or NeedsAttention.
5. Paid closes the waiting state, refreshes the POS data, and shows the actual active session and final bill. Other terminal states explain the outcome and the cashier's next action.
6. Extension follows the same sequence from the active seat's action sheet.

## Acceptance criteria

- Cash check-in, cash extension, seat holds, and existing session behavior remain unchanged.
- Choosing VietQR does not create an immediate paid session/bill; it creates a pending request and displays its server-provided QR, amount, code, and expiry.
- A successful request status causes the POS to show the settled session/bill and refreshed seat map without requiring a manual browser reload.
- Extension payment updates the existing session and bill exactly once and the POS shows the new total/end time.
- Expired, cancelled, and NeedsAttention requests are visibly distinct from successful payment; polling stops for all terminal states.
- Closing/cancelling the payment screen does not silently leave a misleading success state; cancellation races are resolved using the server status.
- Polling requests are scoped to the currently displayed payment request, do not continue after unmount/terminal status, and avoid overlapping requests.
- SePay callbacks without a matching payment code remain unmatched; the UI must not infer a match from amount alone.
- Add focused frontend tests for request creation, pending-to-paid transition, terminal failures, cancellation, and regressions to cash flows.
- `npm run typecheck`, relevant frontend tests, and POS production build pass.
- Manual demo evidence covers QR display, callback-driven completion, expiry/cancellation, and a retry without duplicate settlement. If SePay Test mode does not return the generated code, record that provider constraint and use only an explicitly labeled synthetic callback for a software demonstration.

## Dependencies

- TASK-014 payment-request create/status/cancel API.
- TASK-015 authenticated and idempotent SePay webhook.
- TASK-016 payment settlement into the session and bill.
- Existing POS flows from TASK-012.

## Out of scope

- Adding SignalR or changing the backend settlement protocol.
- Customer web payment UI, realtime customer updates, receipts/printers, and manual unmatched-payment resolution.
- Production banking certification or claiming a real provider payment when using a synthetic callback.
- Database schema changes unless implementation discovers a necessary contract gap; any such change requires a documented migration.

## Verification

- Run focused POS/API-client tests, workspace typecheck, and POS build.
- Run the complete frontend test suite if focused coverage is unavailable.
- Verify API request shapes against the backend contracts and test at least one request through the ASP.NET pipeline.
- Do not apply migrations to an unintended database or include credentials in fixtures, screenshots, logs, or task evidence.

## Implementation and verification results

Not started.
