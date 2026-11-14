# TASK-026: Add-ons at check-in, POS menu 500 fix, points history → bill, printable receipt PDF

## Status

Step 0 + menu fix approved ("ok") and merged into `development` from `feat/checkin-addons-receipt` (from `89da238`).
Parts A–C pending. No migrations.

## Step 0 — POS menu 500: root cause

**Reproduced** with the SQL Server provider (`SqlTranslationTests`, new case `menu`; `managed-products` passes):

```
System.InvalidOperationException : The LINQ expression 'DbSet<Product>()
    .Where(p => p.BranchId == null || p.BranchId == @branchId)
    .OrderBy(p => p.Category).ThenBy(p => p.SortOrder).ThenBy(p => p.Name)
    .Where(p => new ProductView(p.Id, p.BranchId, p.Name, p.Category, p.Price, p.IsActive, p.SortOrder).IsActive)'
could not be translated.
```

- `AddOnService.MenuAsync` runs `Products(branchId).Where(p => p.IsActive)`. `Products()` already ends in
  `.Select(p => new ProductView(...))`, so the filter runs on a **positional record built by its constructor**. EF Core
  cannot map `ProductView.IsActive` back to a column (it can only do that for anonymous types or `new X { A = … }`), so
  translation throws → unhandled → `UseExceptionHandler` + `AddProblemDetails` → 500 with the default title
  "An error occurred while processing your request.", which is the text production shows.
- `ManagedAsync` (Admin "Bảng giá", `/products/all`) uses the same query **without** the extra `Where`, so it works.
- **Not role-dependent:** Staff, BranchManager and Admin all reach the same query once `CanAccessBranch` passes; a user
  without branch access gets 404 before the query. `BranchScope` handler, `ProductView` serialization, nullable
  `BranchId`/`SortOrder` ordering and pending migrations are not involved (the exception is thrown during translation,
  before any SQL is sent).
- **Why tests missed it:** `Menu_lists_active_branch_and_chain_products_only` uses the EF in-memory provider, which
  evaluates the predicate in memory; `SqlTranslationTests` had no `menu` case.

**Fix (done):** `IsActive` is applied before the projection (`Products(branchId, activeOnly: true)`). Regression tests:
`SqlTranslationTests` cases `menu` + `managed-products` (SQL Server provider) and `Menu_works_for_every_pos_role`
(Staff / BranchManager / Admin; chain + branch products listed, inactive and other-branch ones excluded).

**Diagnosability (done):** `ApiError.traceId` is read from ProblemDetails; the POS "Không tải được thực đơn" alert shows
`HTTP 500 · traceId 00-…` (copyable). Tests: `addOnMenu.test.ts`, `api-client/http.test.ts`.

## Plan (remaining)

- **Part A (`feat(checkin)`)**: `CheckInRequest.items` (≤30 lines, qty 1–99, active branch/chain products, server
  prices via `AddOnService.PriceLinesAsync`). Cash: same transaction creates session + bill + `BillItem`s, one points
  award on plan + items. VietQR: check-in `PaymentRequest.Amount` = plan + items, lines snapshot in `Items`; settlement
  creates session, bill and items atomically from the snapshot. POS check-in screen embeds the add-on grid
  (extracted `AddOnGrid` component) with plan / add-ons / total footer. Receipts, POS bill, customer `/bills/:code` and
  payment logs show the lines.
- **Part B (`feat(customer)`)**: PointsPage and MePage bill rows with `billCode` → `/bills/{code}` (chevron + press state).
- **Part C (`feat(receipt)`)**: `GET /api/bills/{code}/receipt.pdf` (QuestPDF Community, bundled OFL Vietnamese font,
  80 mm), claim QR for unclaimed bills (`ClaimPage ?code=` prefill), POS "In bill" buttons (fetch + blob download).

## Verification

Step 0:
- `dotnet test --filter SqlTranslationTests` → `Failed: 1, Passed: 29` (the `menu` case — expected until the fix).
- `npm run typecheck` → OK; `vitest` pos `addOnMenu.test.ts` 7 passed; api-client 8 passed.

Fix:
- `dotnet build CafeCR.slnx` → 0 errors.
- `dotnet test CafeCR.slnx` → UnitTests `Passed: 115, Failed: 0`; IntegrationTests `Passed: 12, Failed: 0`.
- `frontend`: `npm run typecheck` OK; `npm test` → 9 + 8 + 16 passed (3 workspaces); `npm run build` → 3 apps built.
