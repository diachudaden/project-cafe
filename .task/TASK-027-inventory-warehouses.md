# TASK-027: Kho trong / kho ngoài, đếm kho ngoài kết ca, đối soát 2 kho

## Status

**Implemented** on `feat/inventory` (7 commits, one per slice). **Server: apply migration `InventoryModule`** (it also
creates the two warehouses for every existing branch). New optional settings `Inventory__ToleranceMinUnits` (2),
`Inventory__TolerancePercent` (0.08), `Inventory__RotationDays` (3), `Inventory__JobsEnabled` (true).
Decision D32 records the deviations from the original prompt.

## Deviations from the prompt (approved before coding)

| Prompt | Implemented | Why |
| --- | --- | --- |
| `SPEC-thanh-toan-vietqr.md` | D25–D31 + TASK-013…018 | The file does not exist in the repo |
| `bigint` / Guid keys, `long` money | Guid v7 (`Entity`) for all new tables, `bigint` identity for append-only `StockMovements`; money `decimal(12,0)` | Repo convention |
| `LedgerEntries` "Chi · Ingredients" | One `Expense` with `Category = "Nguyên liệu"` and `Expense.StockDocumentId` (unique) | No ledger table; `Expense` is already append-only |
| Hangfire | `InventoryJobsWorker` (`BackgroundService` + `PeriodicTimer`, Vietnam time) | Same pattern as the existing workers; no new framework |
| SignalR `/hubs/pos` events | `InventoryEvents` → `Notifications` in the same unit of work; POS/Web poll (D28) | No hub exists yet; the events are isolated so a hub can be added later |
| Role `ShiftLead` | `BranchManager` (`Management` policy) | Role does not exist |
| Photo upload | `PhotoUrl` (link) on receipts and waste | No file storage in the repo |

## Implementation

### Data (`InventoryModule`)

`Warehouses` (UNIQUE BranchId+Type), `InventoryItems` (+`SortOrder` = shelf order), `ItemUnits`, `ItemBranchSettings`
(PK ItemId+BranchId), `StockDocuments` (filtered UNIQUE `(ShiftId, ToWarehouseId) WHERE Type = 4 AND ShiftId IS NOT NULL
AND Status <> 5`, RowVersion), `StockDocumentLines` (count snapshot columns), `StockMovements` (bigint, append-only,
`Qty <> 0`), `StockBalances` (PK WarehouseId+ItemId, RowVersion), `ShiftCountHandovers` (dispute lines as JSON),
`InventoryIssues` (UNIQUE InnerCountDocumentId+ItemId), `Expenses.StockDocumentId`.

`StockLedger` is the only writer of movements/balances. Dispute and resolve data live on the handover; a locked count
document is never written again.

### Formulas — `ChonRieng.Domain/Inventory/InventoryMath.cs`

Expected/Variance/Tolerance, restock suggestion + reason rule, reconciliation (5 kinds), median norm ±30 %, par,
rotation day, unit conversion. `CountPayload.Hash` = SHA-256 of lines + ConfirmedBy + ConfirmedAt.

**Opening** (`OuterStockCalculator`): outer book right after the previous locked shift count (= its counted quantity)
plus later adjustments of that count (dispute resolution / DC); with no previous count, the book at the shift start.
TransfersIn/Waste = outer movements after that point; Sessions = sessions started + extensions sold in the shift.

### API (`/api/inventory`, Swagger tag "Kho")

Items, branch settings, balances (management only), documents; receipts (PN), transfer suggestion + transfers (PC),
waste (PH); `shift-counts/open`, `inner-counts/open`, `{shift|inner}-counts/{id}/lines|summary|confirm`,
`handovers/pending`, `shift-counts/{id}/accept|dispute|resolve`; `attention`, `documents/{id}/approve|reject`,
`adjustments`, `issues`, `issues/{id}/resolve`; `reports/usage|issues|branches`.

Business error codes come back as `violations[0].rule` of a 409 ProblemDetails: `SHIFT_NOT_OPEN`, `NOT_YOUR_SHIFT`,
`COUNT_ALREADY_CONFIRMED`, `COUNT_INCOMPLETE`, `COUNT_IN_PROGRESS`, `REASON_REQUIRED`, `INSUFFICIENT_STOCK`,
`STOCK_CHANGED`, `INVALID_STATE`.

Rules worth knowing:

- Transfers and outer waste are refused while the shift's outer count is still a draft (`COUNT_IN_PROGRESS`).
- Confirm is idempotent; a parallel confirm loses on RowVersion and returns the stored result.
- Reject of a pending count reverses its movements (new rows) and lets the shift count again.
- An adjustment (DC) is the only correction: receipt → inner, waste → its warehouse, transfer → both, count → the counted
  warehouse. The reconciliation's *TransferMisrecorded* draft DC is record-only (no warehouse) because both books were
  already set right by the counts.

### Jobs (`InventoryJobsWorker`, every minute, Vietnam time)

`RemindUnconfirmedCounts` 15′, `SuggestRestock` 10′ (hourly per person), `ReconcileInventory` 00:45,
`RecalcNorms` 01:00 (≥ 3 samples, once per business day). All idempotent.

### Frontend

- `@cafecr/ui`: `ConfirmDialog`, `QtyStepper`, `VarianceChip`, `CountSummaryTable`, `inventory.ts` helpers.
- POS: floating **Lôi hàng** drawer (pre-filled suggestions: open → "Lôi hàng" = 2 taps), closing wizard step
  **Kho ngoài** (count → summary → popup), `HandoverBanner` for the next shift.
- Web quản lý: **Kho · Cần xử lý**, **Kho · Tồn & phiếu** (balances, receipts, documents + DC, weekly inner count),
  **Kho · Mặt hàng**, **Kho · Báo cáo**.

## Verification (2026-11-15)

- `dotnet test CafeCR.slnx`: UnitTests 181 passed, IntegrationTests 19 passed (inventory: 58 unit + 6 SQL Server).
- SQL Server 2025 via Testcontainers (`InventorySqlTests`, needs Docker): parallel transfers → one 409; parallel opens
  share one count; parallel confirms write once; attention, jobs and reports translate to SQL.
- `npm run typecheck`, `npm test`, `npm run build` in `frontend/`: pass.
- Migrations applied to a disposable SQL Server 2025; `has-pending-model-changes` clean; dev seed run twice (idempotent).
- HTTP smoke test on SQL Server (manager): balances, suggestion, open (no `expected` in JSON), 409 COUNT_INCOMPLETE,
  summary, 409 REASON_REQUIRED, confirm ×2 (second `alreadyConfirmed: true`), 409 COUNT_ALREADY_CONFIRMED.
- `docker compose build` passes; the API image starts against SQL Server and `/api/health` is Healthy.

Not covered automatically: React component/DOM tests (the repo has no jsdom/testing-library; `ConfirmDialog`'s
enable rule is tested through `canConfirm`), and a browser run of the POS/Web screens.

Local note: Testcontainers failed to pull `testcontainers/ryuk` with stale Docker Hub credentials in
`~/.docker/config.json`; `docker pull testcontainers/ryuk:0.14.0` once fixed it.

## Manual demo script

1. Seed (`--seed-development-data`): 5 demo items, opening receipt + transfer per branch.
2. Web quản lý → **Kho · Mặt hàng → Cấu hình chi nhánh**: check norm/min/max.
3. POS (staff in a shift) → **Lôi hàng** → accept the suggestion. Raise one quantity above it → reason box appears.
4. Sell a few sessions. **Kết ca** → cash steps → **Kho ngoài**: count (no expected shown) → **Xem tổng hợp** →
   **Xác nhận số đếm** → popup. Count one item far off → the popup lists it and requires a reason (≥ 10 chars).
5. Log in as the next shift → banner **Có bàn giao kho ngoài** → **Báo không khớp** for one item.
6. Web quản lý → **Kho · Cần xử lý**: approve/reject the pending count, **Chốt số** for the dispute.
7. **Kho · Tồn & phiếu → Kiểm kê kho trong** twice (a week apart in real use) → next 00:45 run fills **Kho · Báo cáo →
   Đối soát 2 kho**.
