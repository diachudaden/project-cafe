# TASK-024: Auto-release overdue seats (no extension within grace period)

## Status

Implemented on branch `feat/auto-release-seats` (from `development`, which already contains TASK-023); automated checks pass. **Pending on the server:** apply migration `SessionCloseReason`.

> Numbering: the request named this TASK-022, but TASK-022 (schedule grid) and TASK-023 (POS add-on menu) already exist, so this is TASK-024.

## Goal

Staff no longer have to press "Trả chỗ" for every overdue seat. A session past `EndAt` by more than a configurable grace period (default 10 min) that was not extended is closed by the system and the seat becomes free.

This **changes decision D7** ("Overdue … staff decide … no background job"); D7 is updated and a new decision D30 is recorded in `docs/proposals/roles/05-decisions.md`.

## Rules

- Candidate: `Status == Active && EndAt <= now − grace`. Grace `0` = feature off.
- Skip while the customer may be paying: the session has a `PaymentRequest` (`SessionId` = session, any kind: Extension or AddOn) with `Status == Pending && ExpiresAt > now`. It becomes a candidate again on the next tick after the request expires (≤ `Payment__HoldMinutes`).
- Release = `Status = Expired` (still "stayed past the paid time", as D7), `ClosedAt = now`, `CloseReason = AutoReleased`, `ClosedByUserId = null`. Bill, pricing and payments untouched; no charge for the overdue time.
- Manual "Trả chỗ" sets `CloseReason = Manual`, `ClosedByUserId = current user`.
- Member customer → notification `session.auto_released`: "Đã tự trả chỗ {seat}" / "Lượt ngồi tại {branch} đã kết thúc lúc {time} do quá giờ {N} phút mà không gia hạn."
- A late VietQR extension/add-on payment for an auto-released session stays on the existing NeedsAttention path, with the note "Lượt ngồi đã được hệ thống tự trả" (instead of the generic "Lượt ngồi đã kết thúc…").

## Files

### Backend — add
- `backend/src/ChonRieng.Api/Features/Sales/SessionOptions.cs` — section `Sessions`: `AutoReleaseGraceMinutes` (`[Range(0, 240)]`, default 10), `AutoReleaseIntervalSeconds` (`[Range(10, 3600)]`, default 60). Bound with `AddOptions<>().Bind().ValidateDataAnnotations().ValidateOnStart()`.
- `backend/src/ChonRieng.Api/Features/Sales/SessionAutoReleaseService.cs` (scoped): `FindCandidatesAsync(now)` (query incl. the pending-payment skip) and `TryReleaseAsync(candidate, now)` (conditional update + notification + log); `ReleaseDueAsync(ct)` runs both, one try/catch per row.
- `backend/src/ChonRieng.Api/Features/Sales/SessionAutoReleaseWorker.cs` — `BackgroundService` in the `PaymentRequestExpiryWorker` style (`PeriodicTimer` with `TimeProvider`, new scope per tick, error logged, tick never throws). Does nothing when grace = 0.
- Migration `SessionCloseReason`: nullable `CloseReason tinyint`, nullable `ClosedByUserId uniqueidentifier`; backfill `CloseReason = 1 (Manual)` for already closed rows (`Status <> 1`) since all past closes were manual.
- Tests (see below).

### Backend — change
- `Domain/Entities/Enums.cs`: `SessionCloseReason { Manual = 1, AutoReleased = 2 }`. `Domain/Entities/Session.cs`: `CloseReason?`, `ClosedByUserId?`.
- `Infrastructure/.../SalesConfiguration.cs`: map the 2 columns; mark `Status` and `EndAt` as **concurrency tokens** (no schema change; see Races).
- `SessionService.CloseAsync`: set `Manual` + user; catch `DbUpdateConcurrencyException` → 409 "Chỗ này đã được trả." `ExtendAsync` (cash): catch → 409 "Lượt ngồi đã kết thúc (hệ thống tự trả chỗ)".
- `SessionService` extension settlement + `AddOnService` add-on settlement: when `CloseReason == AutoReleased`, NeedsAttention reason "Lượt ngồi đã được hệ thống tự trả". **Message only**, no settlement logic change.
- `AddOnService.SellCashAsync`: include the session row in the save (mark `Status` as modified, same value) so a cash add-on can't land on a session released a millisecond earlier.
- `SalesContracts.SessionView`: `CloseReason?` (+ Vietnamese Swagger description). `CatalogContracts.SeatSessionInfo` + `CatalogService.GetFloorAsync`: `AutoReleaseAt?` (`EndAt + grace`, `null` when disabled). Floor endpoint description mentions it.
- `Engagement/Notifier.cs`: `SessionAutoReleased = "session.auto_released"`.
- `FeatureRegistration.cs`: options, service, hosted worker. `compose.yaml`: `Sessions__AutoReleaseGraceMinutes: ${…:-10}`, `Sessions__AutoReleaseIntervalSeconds: ${…:-60}`. `.env.example`: both keys.

### Frontend
- `packages/api-client/src/features.ts`: `SessionCloseReason`, `closeReason` on `SessionView`, `autoReleaseAt` on `SeatSessionInfo`.
- `packages/ui/src/labels.ts`: `closeReasonLabels` (`AutoReleased` → "Hệ thống tự trả chỗ", `Manual` → "Nhân viên trả chỗ").
- POS `CounterPage.tsx` (seat tile) and `SeatActionsSheet.tsx`: for `Overdue` seats "Tự trả chỗ sau X phút" from `autoReleaseAt` (or "Sắp tự trả chỗ" once reached); hidden when `null`. The floor is already polled, so a released seat turns Available on the next poll.
- POS `SeatActionsSheet`: a 409 on extend/close after auto-release shows the server message and refreshes the floor.

### Docs
- `docs/proposals/roles/05-decisions.md`: D7 updated, D30 added (grace, skip rule, audit, late payment).
- `README.md`: config keys + "run migration `SessionCloseReason`".

## Races

All writers of a session row become conditional on the state they read:

| Situation | What happens |
|---|---|
| Extension (cash or VietQR) commits **before** the worker's update | Worker `ExecuteUpdateAsync … WHERE Id = @id AND Status = Active AND EndAt <= @threshold` affects 0 rows (EndAt moved) → skipped, logged at Debug. |
| Worker commits **before** a cash extension that already loaded the session | `Status`/`EndAt` concurrency tokens → the extension's `UPDATE … WHERE Status = 1 AND EndAt = @old` affects 0 rows → `DbUpdateConcurrencyException` → 409, nothing charged (bill change is in the same `SaveChanges`). |
| Worker vs VietQR extension/add-on webhook | Skipped while the request is Pending and unexpired. If the webhook commits concurrently anyway, the token makes its save fail; `SePayWebhookService` already rolls back and SePay retries; the retry sees the closed session → NeedsAttention "Lượt ngồi đã được hệ thống tự trả". |
| Worker vs manual "Trả chỗ" | Whichever commits second gets 0 rows / a concurrency error → only one close, one reason. |
| Two concurrent extensions (existing lost-update bug) | Also fixed by the `EndAt` token: the second one gets 409 instead of silently overwriting. |
| Cash add-on vs worker | Session row touched in the same save → concurrency error → 409. |
| Several API instances | The conditional update is idempotent; the notification is written only when 1 row was affected, in the same transaction. |

`ExecuteUpdateAsync` is SQL-only; the in-memory test provider doesn't support it, so `TryReleaseAsync` uses a tracked update with the same predicate re-checked (still guarded by the tokens) when `!Database.IsRelational()`. The SQL predicate is covered by adding the candidate query + conditional update to `SqlTranslationTests`.

## Tests (unit, fake `TimeProvider`)

- Overdue > grace → released, `Expired` + `AutoReleased` + `ClosedAt`, `ClosedByUserId` null; member gets `session.auto_released`.
- Overdue < grace → untouched. Grace = 0 → nothing released.
- Pending unexpired Extension / AddOn request → skipped; expired pending request → released.
- Extension between query and update (change `EndAt` after `FindCandidatesAsync`) → not released.
- Cash extend after release → 409, bill unchanged. Manual close → `Manual` + user id.
- Late VietQR extension webhook after release → NeedsAttention with the explicit note, session stays closed.
- Floor: `autoReleaseAt = EndAt + grace` before release (null when disabled); seat `Available` after.
- Options validation: 241 minutes rejected.

## History screen (decided)

Option 1 approved: POS counter "Vừa trả chỗ (2 giờ qua)" list from `GET /api/branches/{branchId}/sessions/recent-closed?hours=2` (Staff/Manager/Admin of the branch; max 24 h, 50 rows) with the reason tag. No Admin history page (neither app had one); `closeReason` is also on `SessionView` (bill lookup).

## Implementation notes

- Options class is `SessionReleaseOptions` (section `Sessions`): `SessionOptions` clashes with ASP.NET Core's session middleware type.
- Migration `20261114075346_SessionCloseReason`: `CloseReason tinyint NULL`, `ClosedByUserId uniqueidentifier NULL`, index `IX_Sessions_Status_EndAt`, backfill `CloseReason = 1` where `Status <> 1`.
- Release SQL (SQL Server): `UPDATE [Sessions] SET … WHERE [Id] = @id AND [Status] = 1 AND [EndAt] <= @threshold`, checked by `SqlTranslationTests` ("auto-release").
- The notification is written after the release in its own save; if it fails, the release stays and a warning is logged.
- A VietQR **add-on** settlement does not touch the session row (settlement logic unchanged by request), so it is protected only by the pending-request skip rule; a race there adds the paid items to the bill of a just-released session (money still recorded correctly).
- In-memory tests can't run `ExecuteUpdate` or transactions; the service falls back to a tracked update with the same predicate, and the race test asserts the session row only (on SQL Server the bill change in the same `SaveChanges` rolls back too).

## Files changed

- Domain/Infrastructure: `Enums.cs` (`SessionCloseReason`), `Session.cs`, `SalesConfiguration.cs`, migration `SessionCloseReason` + snapshot.
- API: `Sales/SessionReleaseOptions.cs`, `Sales/SessionAutoReleaseService.cs`, `Sales/SessionAutoReleaseWorker.cs`, `Sales/SessionService.cs` (manual reason, 409 on concurrent change, `RecentClosedAsync`, late-payment note), `Sales/AddOnService.cs`, `Sales/SalesContracts.cs`, `Sales/SalesEndpoints.cs`, `Catalog/CatalogService.cs` + `CatalogContracts.cs` + `CatalogEndpoints.cs` (`autoReleaseAt`), `Engagement/Notifier.cs`, `FeatureRegistration.cs`.
- Config/docs: `compose.yaml`, `.env.example`, `README.md`, `docs/proposals/roles/05-decisions.md` (D7, D30).
- Frontend: `api-client/features.ts`, `ui/format.ts` (`autoReleaseText`) + test, `ui/labels.ts` (`closeReasonLabels`), POS `CounterPage.tsx` (countdown, "Vừa trả chỗ"), `SeatActionsSheet.tsx` (countdown, 409 → refresh floor).
- Tests: `SePayWebhookTests.cs` (+11), `SqlTranslationTests.cs` (+2 and NonQuery support), `DomainSchemaModelTests.cs` (8 migrations).

## Verification

- `dotnet build CafeCR.slnx`: Build succeeded.
- `dotnet test CafeCR.slnx`: unit **105 passed** (94 + 11: release after grace with audit/notification/floor/recent list; within grace; grace 0; pending Extension and AddOn request ×2; extension between query and update; stale writer vs release → concurrency error and 409 on extend/add-on/close; manual close reason + user; late VietQR extension → NeedsAttention with explicit note; options validation; + SQL translation "auto-release", "recent-closed"), integration **12 passed**.
- `frontend/`: `npm run typecheck` 0 errors; `npm test` pos 8, customer/admin 7, ui 10 passed; `npm run build` 3 apps built.
- POS rendered in headless Chrome against a mocked API: overdue tile "Tự trả chỗ sau 6 phút", seat sheet "(10:06) nếu không gia hạn", "Vừa trả chỗ" with both reason tags.
- Not done: run against a real SQL Server (no database in this session).

## Manual steps on the server (after release)

1. Back up, then `dotnet ef database update --project backend/src/ChonRieng.Infrastructure --startup-project backend/src/ChonRieng.Api` (applies `SessionCloseReason`; `/api/health` from TASK-023 shows it as pending until then).
2. Optional `.env`: `Sessions__AutoReleaseGraceMinutes=10` (0 disables), `Sessions__AutoReleaseIntervalSeconds=60`; then `docker compose up -d`.
