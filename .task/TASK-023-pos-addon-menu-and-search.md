# TASK-023: POS add-on menu (đồ thêm) — empty list and broken search

## Status

Implemented on branch `fix/pos-addon-menu` (from `development`); automated checks pass. **Pending on the server:** apply migrations, then create add-on items in Bảng giá (see "Manual steps").

> Numbering: the request named this TASK-021, but TASK-021 (plans dropdown + add-on items) and TASK-022 (schedule grid) already exist, so this is TASK-023.

## Goal

Staff opening "Thêm đồ" on an occupied seat must see the branch menu (or a clear reason why not), and the search box must find items the way staff type them (without diacritics, by category, several words).

Related: [TASK-021](./TASK-021-plans-dropdown-and-add-on-items.md) introduced add-ons. Its rule is kept: add-ons go on the seat's current bill and never change the session time.

## Step 0 — Diagnosis (code at `323256d`)

### Bug 1 — "Thêm đồ" shows no items

`AddOnSheet` calls `GET /api/branches/{branchId}/products` (`["products", branchId]`) and renders `<List dataSource={visible} loading=… />`, where `visible = (products.data ?? []).filter(...)`. Every path below ends in the **same** text, "Chưa có món nào. Quản lý thêm món ở trang Bảng giá.", because the sheet never reads `products.isError`:

| # | Path | Where | Result on screen |
|---|------|-------|------------------|
| A | **Table `Products` missing**: migration `20261113170926_AddOnItems` not applied on the server. The deploy workflow says "Database migrations are intentionally NOT run here", and the API doesn't migrate at startup (spec). EF query → SQL error "Invalid object name 'Products'" → HTTP 500. | `AddOnService.MenuAsync` | `isError`, `data` undefined → `[]` → "Chưa có món nào" |
| B | **Menu truly empty**: migration applied but no product rows. Demo products ("[DEMO] Mì ly"…) come only from the explicit dev seed (Development/Testing only), so production has none until someone adds them in Admin → Bảng giá → "Đồ thêm". | `MenuAsync` returns `[]` (200) | "Chưa có món nào" (correct, but no hint for Staff, who can't open Bảng giá) |
| C | **All products inactive** (`IsActive = false`), or created for **another branch**. `MenuAsync` returns active products where `BranchId == null || BranchId == branchId`. A product that was sold and then "deleted" is deactivated, not removed. | `AddOnService.Products()` + `.Where(p => p.IsActive)` | Same as B |
| D | **403/404**: `RequireBranchScope` / `CanAccessBranch` fails, e.g. a staff JWT whose `branch_id` claims don't include the branch (≤ 15 min snapshot after a branch change). Unlikely here: the floor uses the same scope and loads. Network error / 502 during a redeploy also lands here. | endpoint policy / `MenuAsync` | `isError` → "Chưa có món nào" |
| E | **Client filters**: category Segmented + search both apply at once. If a category is selected and the term matches an item in another category, nothing shows. The search is diacritic-sensitive and matches only the raw name (including the "[DEMO] " prefix), not the category: `mi` ≠ `Mì ly`, `uong` finds nothing in "Đồ uống", `trung op` fails (multi-word, no accents). | `AddOnSheet.visible` | List empty with the same "Chưa có món" message, so it looks like an empty menu |

**Most likely root cause for the server:** A (or B right after A is fixed). TASK-021's own status says "Pending: apply migration `AddOnItems` on the server, add real menu items in Bảng giá", and the deploy workflow does not migrate. To check on the server DB:

```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;           -- is 20261113170926_AddOnItems there?
SELECT OBJECT_ID('dbo.Products') AS ProductsTable;                             -- NULL = table missing
SELECT BranchId, Name, Category, Price, IsActive FROM Products ORDER BY Category, SortOrder;  -- only if the table exists
```

Note: if the migration is missing, the **floor and the seat sheet are also affected**: TASK-021's deploy note says the API reads `BillItems` when listing sessions/bills, so `GET /api/bills/{code}` would 500 too.

### "Not visible" = sold but not on the bill?

Checked; this is **not** the cause, apart from one small gap:

- `SeatActionsSheet` reads `["seat-bill", session.billCode]` → `GET /api/bills/{code}`, whose view includes `Items` (`SessionService` line ~525, ordered by `CreatedAt`).
- **Cash**: `AddOnSheet.sell.onSuccess` → `refresh()` invalidates the `["seat-bill"]` prefix (matches `["seat-bill", code]`) and closes; `SeatActionsSheet` also calls `bill.refetch()` when the add-on sheet closes. ✔
- **VietQR**: `PaymentRequestModal` can only be closed in a terminal state; its `onClose` calls the same `refresh()` and then `bill.refetch()`. ✔ Gap: when the status turns `Paid`, the modal invalidates `floor` and `current-shift` but not `seat-bill`. Harmless today (closing refetches), but it will be added so the bill is fresh as soon as payment is confirmed.
- **Customer app**: `MyBill` has no item lines (code, branch, total, points), so add-ons show only in the total. **Admin**: no bill-detail screen. Nothing to refresh there; showing lines there would be a new feature (out of scope).

### Bug 2 — the search box is useless

`p.name.toLowerCase().includes(term.toLowerCase())`:
- diacritic-sensitive (`mi`, `trung`, `banh` find nothing; staff on a POS keyboard rarely type accents; `đ` ≠ `d`);
- one substring for the whole term, so `trung op` (two words) must appear contiguously;
- ignores the category (`uong`, `an`);
- combined with the selected category tab, it hides matches in other categories;
- no result count and no "no results" message (shows "Chưa có món nào").
- The cart itself survives filtering (state is keyed by product id), but selected items disappear from view when filtered out.

Duplicated name cleaning: `clean()` in `AddOnSheet` strips only `[DEMO]`; `CheckInSheet` and `SeatActionsSheet` (plan name) use `/^\[DEMO( ONLY)?\]\s*/`, and `SeatActionsSheet` item lines use `/^\[DEMO\]\s*/`.

### Health / migrations

There is **no health or diagnostics endpoint** in the API (`Program.cs` only maps `/` → Swagger, auth, admin CRUD and features). The startup scope in `Program.cs` (role bootstrap) is the natural place for a pending-migrations check.

## Implementation

### Frontend
1. `packages/ui`: `normalizeVi(text)` (lowercase, NFD, strip combining marks, đ→d, collapse spaces), `displayName(name)` (strips `[DEMO]` / `[DEMO ONLY]`), `matchesSearch(term, ...fields)` (every token must appear in the normalized name or category). Replace `clean()` and the 3 regexes in `AddOnSheet`, `SeatActionsSheet`, `CheckInSheet`.
2. `AddOnSheet` states: skeleton while loading; error `Alert` with the API message + "Thử lại"; empty menu "Chi nhánh chưa có món nào đang bán." + link to admin Bảng giá for Admin/BranchManager, "Nhờ quản lý thêm món" for Staff; no result "Không tìm thấy “{term}”" + "Xóa tìm kiếm".
3. While a term is present: search all categories (Segmented disabled), show "N món". Autofocus the search on desktop; keep `allowClear`.
4. Sticky "Đã chọn" summary for items with qty > 0 (with − / + buttons) so they stay visible when filtered out; cart is kept across search/category changes.
5. `PaymentRequestModal`: also invalidate `["seat-bill"]` when status becomes `Paid`.

### Backend
6. Startup check: `Database.GetPendingMigrationsAsync()` → log **Error** listing pending migrations; never applies them. Add `GET /api/health` (anonymous; Vietnamese Swagger text) returning `{ status, pendingMigrations: [...] }` (`status = "Degraded"` when pending or DB unreachable). Approved: there was no existing endpoint, so `GET /api/health` was added (anonymous, `Features/Health/HealthEndpoints.cs`).
7. Tests: `MenuAsync` (branch + chain-wide active, excludes inactive and other branches, NotFound for staff of another branch); selling add-ons (cash and VietQR settlement) leaves `Session.EndAt` unchanged.

### Docs
8. README "Database migrations" / deploy: after deploying a commit with a migration run `dotnet ef database update --project backend/src/ChonRieng.Infrastructure --startup-project backend/src/ChonRieng.Api`; products must be created in Admin → Bảng giá (or the dev seed for demo data).

## Acceptance criteria

- An API error never shows "Chưa có món"; it shows the error message and "Thử lại".
- An empty menu explains what to do, per role.
- `mi` → "Mì ly", `trung op` → "Trứng ốp la", `uong` → items in "Đồ uống", `[DEMO]` prefixes ignored, across all categories while searching; result count shown; "Xóa tìm kiếm" on no result.
- Items with qty > 0 stay visible in "Đã chọn" whatever the filter.
- After a Cash sale or a Paid add-on QR, the seat's bill lists the new lines.
- Pending migrations are logged as an Error at startup (and reported by `/api/health` if approved); nothing is auto-applied.
- Selling add-ons never changes `Session.EndAt`.

## Files changed

- `frontend/packages/ui/src/search.ts` (+ `search.test.ts`, export in `index.ts`): `normalizeVi`, `displayName`, `matchesSearch`.
- `frontend/apps/pos/src/features/counter/addOnMenu.ts` (+ `addOnMenu.test.ts`): `filterMenu`, `menuState` (loading / error / empty / noResult / list).
- `frontend/apps/pos/src/features/counter/AddOnSheet.tsx`: states, search across categories with count, autofocus on desktop, "Đã chọn" summary with steppers, role-specific empty hint.
- `SeatActionsSheet.tsx`, `CheckInSheet.tsx`: shared `displayName`. `PaymentRequestModal.tsx`: invalidates `["seat-bill"]` when Paid.
- `frontend/apps/pos/package.json`: `test` script (the existing `paymentStatus.test.ts` was never run before).
- `backend/src/ChonRieng.Api/Features/Health/HealthEndpoints.cs`, `Program.cs`: startup Error log for pending migrations, `GET /api/health`. No migration is ever applied.
- `backend/tests/ChonRieng.UnitTests/Features/SePayWebhookTests.cs`: `Menu_lists_active_branch_and_chain_products_only`, `Add_on_sales_never_change_the_session_end` (cash + VietQR settlement). `DatabaseHealthTests.cs`: InMemory → Healthy, unreachable SQL Server → Unhealthy.
- `README.md`: migrate after deploy, `/api/health`, creating add-on items.

No change to pricing, session time, or SePay webhook code.

## Root causes

1. **Empty list:** `AddOnSheet` ignored `isError`, so a failing menu request (most likely `Products` table missing because migration `AddOnItems` was never applied — the deploy doesn't migrate) looked exactly like an empty menu. On a migrated database with no product rows (production has no seed) it was also empty, with a hint Staff can't act on.
2. **Search:** case-only, accent-sensitive substring on the raw name; no category match; one contiguous phrase; combined with the selected category tab.

## Verification

- `dotnet build CafeCR.slnx`: Build succeeded, 0 warnings.
- `dotnet test CafeCR.slnx`: unit 94 passed (90 + 4 new), integration 12 passed.
- `frontend/`: `npm run typecheck` 0 errors; `npm test` pos 8, customer/admin 7, ui 9 passed; `npm run build` all 3 apps built.
- Not done: manual run of the POS sheet against a real API (no local API/DB in this session).

## Manual steps on the server

1. Back up the database, then on a machine with the repo at the deployed commit and the server connection string:
   `dotnet ef database update --project backend/src/ChonRieng.Infrastructure --startup-project backend/src/ChonRieng.Api`
   (applies `PaymentLogs` / `AddOnItems` if missing).
2. `GET https://<host>/api/health` → expect `{"status":"Healthy",...,"pendingMigrations":[]}`.
3. Web Quản lý → Bảng giá → "Đồ thêm": create the real items (chain-wide needs Admin).
4. POS: open an occupied seat → "Thêm đồ", search `mi` / `uong`, sell one item by cash and check it appears on the seat's bill.
