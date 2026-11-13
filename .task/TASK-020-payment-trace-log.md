# TASK-020: Payment trace log (create bill → done)

## Status

Implemented; automated checks pass. Pending: apply migration `PaymentLogs` on the server (deploy does not run migrations) and watch a real SePay Test-mode payment in the new page.

## Goal

Record every step of a VietQR payment in a database table and show it in the admin app, so when a customer has paid (money is in the bank) but the POS screen does not turn to "Thanh toán thành công", we can see exactly where the flow stopped.

## Problem

Today the flow leaves almost no trace:

- `POST /api/hooks/sepay-payment` returns 401/400/413 for a bad signature, stale timestamp, malformed or oversized body **without writing anything**. A rejected SePay callback is invisible; SePay retries and gives up.
- A recorded `PaymentTransaction` has one `Note`, overwritten at each step. There is no history of when the request was created, polled, matched, settled, expired or cancelled.
- If SePay sends a non-empty `code` that does not match a request (different code pattern configured on SePay), the transfer `content` is never searched, so a correct code inside the content is ignored and the request stays Pending until it expires.
- POS stops polling forever if the very first status call fails (no data → `refetchInterval` returns `false`).

## Flow and logged stages

| Stage | Where | Level |
| --- | --- | --- |
| `RequestCreated` | Check-in / extension QR created | Info |
| `StatusPolled` | POS polls status (first poll, status change, then at most once per 30 s) | Info |
| `WebhookReceived` | Callback reached the API (body size, headers present, IP) | Info |
| `WebhookRejected` | Too large, missing/invalid signature, stale timestamp (with clock skew), bad JSON/payload | Error |
| `WebhookDuplicate` | Same provider transaction delivered again | Info |
| `TransactionRecorded` | `PaymentTransaction` stored | Info |
| `TransactionIgnored` | Outgoing transfer or wrong receiving account | Warning |
| `TransactionUnmatched` | No / ambiguous payment code | Warning |
| `CodeMatched` | Transaction linked to a request (and how: `code` field or content) | Info |
| `AmountMismatch` | Underpaid / overpaid | Warning |
| `LatePayment` | Paid after expiry or cancellation | Warning |
| `AlreadyPaid` | Second payment for a paid request | Warning |
| `SettlementFailed` | Bill/session could not be created (seat taken, plan changed, ...) | Error |
| `Settled` | Bill created / session extended (bill code) | Info |
| `Expired` | Expiry worker or poll marked it expired | Warning |
| `Cancelled` | Staff cancelled | Info |
| `Exception` | Unhandled error while processing a callback | Error |

Each row: time, stage, level, payment request (id + code), provider transaction id, bill id/code, branch, amount, message (Vietnamese), small JSON detail, HTTP trace id, elapsed ms since the request was created.

## Scope

- Domain: `PaymentLog` entity + EF configuration + migration `PaymentLogs` (indexes on time, code, request, branch).
- `PaymentLogService`: writes through its own DbContext scope so a log survives a rolled-back payment transaction; never throws into the payment flow.
- Instrument `PaymentRequestService`, `SePayWebhookService`, `PaymentRequestExpiryWorker`.
- Fix: fall back to searching `content` when `code` does not match a request.
- API (management): `GET /api/payment-logs` (filters: branch, code/search, stage, level, from, to; paged) and `GET /api/payment-logs/{code}` (request + bank transactions + full timeline). Admin sees all including callbacks without a branch; managers see their branches.
- Admin page "Nhật ký thanh toán": filterable table, warning/error highlight, click a code → timeline drawer.
- POS: keep polling when the first status call fails.
- Do not log the webhook secret, signature value, raw body or transfer content (may contain customer names).

## Acceptance criteria

- A rejected callback (bad signature, stale timestamp) creates a `WebhookRejected` row with the reason.
- A successful payment produces `RequestCreated → … → WebhookReceived → TransactionRecorded → CodeMatched → Settled` for its code.
- Underpaid / late / unmatched transfers produce the matching warning rows.
- Logging failures do not break payments.
- Admin can filter and open a timeline per code.

## Verification

- Unit: full trace of a paid request (`RequestCreated → StatusPolled → TransactionRecorded → CodeMatched → Settled → StatusPolled`, poll throttling, elapsed ms), rejected callbacks logged with `stale_timestamp` / `bad_signature` / `missing_headers` and no secret in the detail, wrong `code` field falling back to the content code.
- Integration: rejected webhook visible in `GET /api/payment-logs` for Admin, 403 for Staff, 404 for an unknown trace code.
- `dotnet test CafeCR.slnx`: unit 88, integration 11 passed. `npm run typecheck`, `npm test`, `npm run build` passed.

## Deploy

```bash
dotnet ef database update --project backend/src/ChonRieng.Infrastructure --startup-project backend/src/ChonRieng.Api
```

Additive only (new table `PaymentLogs`, no FKs). Until it is applied, log writes are skipped with a warning and the status poll keeps working; the "Nhật ký thanh toán" page returns an error.

## Follow-ups

- Retention: no cleanup yet; roughly 10–20 rows per payment. Add a periodic purge (e.g. > 90 days) if the table grows.
