# TASK-013: VietQR + SePay payment and realtime bill flow

## Status

Planned.

## Goal

Reintroduce real payment processing for check-in and extension flows with VietQR and SePay in Test mode, while preserving the paid-only `Bill` model and enabling realtime POS/customer updates.

## Scope

- Add the approved payment request and transaction flow for pending bank transfers.
- Support VietQR generation, SePay webhook verification, and branch-scoped transaction resolution.
- Keep cash payment behavior unchanged and reuse the existing check-in/extend core logic for successful webhook completion.
- Emit realtime status updates for the POS and customer web experience.
- Add fail-fast configuration and demo-safe environment values for the payment module.

## Decisions resolved before implementation

- `Bill` stays paid-only; a new `PaymentRequest` handles pending transfer requests and link resolution to the final bill.
- `PaymentRequest` stores the branch, seat, price plan, session context, status, expiry, amount, and the final `BillId` after settlement.
- Seat holds are expressed through a pending `PaymentRequest` rather than a new `Seat` state.
- Payment expiry follows `Payment__HoldMinutes` with a periodic background cleanup loop.
- SePay Test mode uses HMAC-SHA256 verification over the raw request body and timestamps.
- QR generation uses `Payment__QrImageBaseUrl` with `acc`, `bank`, `amount`, and `des` query parameters.
- Payment success and failure events are emitted over SignalR `/hubs/pos` and customer payment status events.
- `PaymentTransaction` records provider events and the match status without introducing a full accounting ledger.
- Customer milestone notifications continue through the existing `Notifier` service and `Notification` model.

## Dependencies

- TASK-007: POS session lifecycle and checkout/extend logic.
- TASK-008: existing bill and customer points outcomes.
- TASK-011/TASK-012: Admin/POS/customer apps consuming the payment status updates.
- .rule/business-rules.md: approved branch and customer handling rules.

## Out of scope

- Production gateway integration or real bank settlement.
- Full ledger accounting or tax reporting.
- A separate payment-only admin screen beyond the branch-scoped transaction review workflow.
- Replacing the existing cash flow or changing the `Bill` accounting semantics.

## Acceptance criteria

- POS can create a VietQR payment request for check-in or extension and receive a `{ code, amount, qrUrl, expiresAt }` payload.
- A SePay webhook is accepted only when the signature, timestamp tolerance, and destination account are valid.
- Pending payment requests expire after the configured hold period and release the seat to other customers.
- Successful webhook completion invokes the same underlying check-in/extend business logic used for cash flows.
- Unmatched, underpaid, overpaid, duplicate, and expired payment intents are resolved with documented branch-manager/admin handling.
- Realtime events are emitted after commit and do not block or rollback successful payment completion.
- Customer-facing payment status and receipt messages remain consistent with the accepted session and bill rules.
- All payment configuration values are fail-fast validated on startup and documented in the sample environment configuration.

## Verification

- API tests cover SePay signature validation, timestamp tolerance, duplicate transaction handling, and request expiry.
- Service tests cover `PaymentRequest` resolution and seat hold behavior.
- Manual demo verification against the local stack and EC2 environment checks the QR image URL, webhook route, and realtime notification flow.
- The implementation must keep route and JSON contracts stable for the frontend integrations while adding the payment-specific path.
