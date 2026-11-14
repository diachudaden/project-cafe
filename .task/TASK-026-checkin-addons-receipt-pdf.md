# TASK-026: Add-ons at check-in, POS menu 500 fix, points history → bill, printable receipt PDF

## Status

**Done.** Step 0 + menu fix merged first (`f2aab42`); Parts B, C, A implemented on `feat/checkin-addons-receipt`
(rebased on `f2aab42`) and merged into `development`. **No EF migration.** New optional setting
`Receipt__CustomerAppUrl` (not required; defaults to the origin of the POS request).

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

## Implementation

### Part B — points history → bill (`feat(customer)`, `76c0dd8`)
- `packages/ui/src/bills.ts`: `billLink(code)` (`/bills/{code}` or `null`), `normalizeBillCode(raw)`.
- `customer/components/BillRow.tsx`: one row for PointsPage "Lịch sử điểm" and MePage "Hóa đơn đã tích điểm".
  With a bill code: chevron, hover + pressed background, `role="link"`, Enter/Space → `/bills/{code}`; without one
  (redemption, adjustment) a plain, non-interactive row. History rows also show "· Bill {code}".

### Part C — receipt PDF (`feat(receipt)`, `c6fdbd1`)
- `GET /api/bills/{code}/receipt.pdf` (`GetBillReceiptPdf`, Swagger summary "In hóa đơn (PDF khổ 80 mm)", response
  `application/pdf`). `RequireWorkforce` + branch scope **in the query** (Admin: all; Manager/Staff: assigned branches;
  otherwise 404). The `BranchScope` policy needs a `branchId` route value, which this route has not; same pattern as
  `GET /api/bills/{code}`. `File(..., "application/pdf", "bill-{code}.pdf")` → `Content-Disposition: attachment`.
- `ReceiptService` (data query) + `ReceiptPdf` (QuestPDF 2026.9.2, `LicenseType.Community`, `ContinuousSize(80 mm)`):
  branch name/address, bill code, seat, plan, start/end, extensions, add-on lines, plan amount (= total − extensions −
  items), cash/VietQR split, total, staff = **the user printing** (bills store no cashier; adding one needs a migration),
  printed time (VN). Unclaimed: QR (QRCoder) to `{origin}/claim?code=…` + "Quét để tích điểm"; claimed: member + points.
- Font: Noto Sans Regular/Bold (SIL OFL 1.1, `Assets/Fonts/OFL.txt`) as **embedded resources** of `ChonRieng.Api.dll`,
  registered with `FontManager.RegisterFontFromStream`; `Settings.UseSystemFonts = false` so a glyph can never fall back
  to a font without Vietnamese. `ReceiptPdf.Initialize()` at startup fails fast if the font is missing.
- Claim origin: `Receipt__CustomerAppUrl` if set, else `{scheme}://{host}` of the request (nginx forwards `Host`; the
  customer app is `/` on the same host).
- POS: `PrintBillButton` ("In bill": `api.sessions.receiptPdf` → `http.download` with the `Authorization` header → blob →
  temporary object URL → `bill-{code}.pdf`) in `PaymentDoneModal` (cash check-in/extension/add-on), the VietQR Paid
  footer and `SeatActionsSheet`. Customer `ClaimPage` prefills `?code=`; the login redirect keeps the query string.

### Part A — add-ons at check-in (`feat(checkin)`, `d54e8f2`)
- `CheckInSessionRequest.items` and `CreateCheckInPaymentRequest.items` (optional `[{ productId, quantity }]`).
  `AddOnService.PriceLinesAsync` (shared with "Thêm đồ"): ≤ 30 lines, qty 1–99 (also after merging duplicate ids),
  active products of the branch or chain-wide, prices from the DB. Errors → 400 `errors.Items` in Vietnamese.
- Cash: `SessionService.CheckInAsync` adds `BillItem`s to the new bill in the same `SaveChanges` as session + bill;
  `Total`/`CashPaidTotal` = plan + items; `Loyalty.Award` once on the total; one `payment.received` notification.
- VietQR: `PrepareCheckInPaymentAsync` → amount = plan + items, `PaymentRequest.Items` = lines snapshot (relaxed JSON
  escaping; > 4000 chars → 400 rather than truncation). Settlement validates `Amount == plan.Price + Σ snapshot`, creates
  session + bill + items (`PaymentRequestId`, `VietQr`) in the webhook's transaction; corrupt snapshot →
  `NeedsAttention`. Payment log `RequestCreated` detail lists the lines.
- POS: `AddOnGrid` + `CartChips` + `Stepper` extracted from `AddOnSheet` (same menu states, search, categories).
  `CheckInSheet` shows the grid under the plan; footer = chips, "Gói / Đồ thêm (n món) / Tổng cộng", payment method,
  one "Thu tiền {total}" / "Tạo QR thanh toán {total}". Done modal lists the add-ons; "Thêm đồ ăn / uống" and the seat
  actions "Thêm đồ" flow are unchanged.
- Receipts, POS seat bill, customer `/bills/:code` already list `Bill.Items`, so check-in items appear everywhere.

### Not changed
SePay signature verification, points rules (1.000đ = 1 point on the paid amount, awarded once per payment), pricing,
`.env`/compose files.

## Verification

Step 0:
- `dotnet test --filter SqlTranslationTests` → `Failed: 1, Passed: 29` (the `menu` case — expected until the fix).
- `npm run typecheck` → OK; `vitest` pos `addOnMenu.test.ts` 7 passed; api-client 8 passed.

Fix:
- `dotnet build CafeCR.slnx` → 0 errors.
- `dotnet test CafeCR.slnx` → UnitTests `Passed: 115, Failed: 0`; IntegrationTests `Passed: 12, Failed: 0`.
- `frontend`: `npm run typecheck` OK; `npm test` → 9 + 8 + 16 passed (3 workspaces); `npm run build` → 3 apps built.

Part B (`76c0dd8`): `dotnet test` 115 + 12 passed; `npm test` pos 9, api-client 8, ui 19 passed; typecheck/build OK.

Part C (`c6fdbd1`):
- `dotnet build CafeCR.slnx` → 0 warnings, 0 errors.
- `dotnet test CafeCR.slnx` → UnitTests `Passed: 119`; IntegrationTests `Passed: 13` (receipt: `application/pdf`,
  attachment `bill-{code}.pdf`, text contains the code and "Quét để tích điểm", 404 for staff of another branch, Admin
  200, Customer 403, Swagger `application/pdf`). `ReceiptPdfTests`: every line in Vietnamese via PdfPig, fonts are
  NotoSans only, width 226.77 pt (80 mm), claimed vs unclaimed.
- Docker: `docker build -f backend/src/ChonRieng.Api/Dockerfile .` OK; `/app/Assets/Fonts/OFL.txt`,
  `/app/runtimes/linux-x64/native/libquestpdf_skia.so` present; a harness calling `ReceiptPdf.Render` inside the
  `aspnet:10.0` image (Ubuntu 24.04) → `OK 29747 bytes`; `pdffonts` → `NotoSans-Bold`, `NotoSans-Regular` embedded,
  diacritics checked visually.
- `npm test` pos 10, api-client 10, ui 19 passed; typecheck/build OK.

Part A (`d54e8f2`) — final:
- `dotnet build CafeCR.slnx` → 0 warnings, 0 errors.
- `dotnet test CafeCR.slnx` → UnitTests `Passed: 123, Failed: 0`; IntegrationTests `Passed: 14, Failed: 0`.
  New: cash check-in with items (one bill, 2 items, one point award = 83 on 83.000đ, one notification); VietQR
  check-in with items (amount 76.000đ, snapshot price kept after a menu change, items `PaymentRequestId`, 76 points
  once); corrupt snapshot → NeedsAttention, nothing created; inactive / other branch / unknown / qty 0 / qty 100 /
  merged qty 100 / 31 lines → 400 (`Items`) for cash and VietQR, nothing created; HTTP: staff menu 200, check-in with
  items 201 with the right total, unknown product 400 "Có món không còn bán tại chi nhánh này.".
- `frontend`: `npm run typecheck` OK; `npm test` → pos 14, api-client 10, ui 19 passed; `npm run build` → 3 apps built.
- Not done: clicking through the POS/customer UI in a browser (no local SQL Server in this session); see demo script.

## Manual demo script

Prerequisites: deploy `development`; **no migration to apply**. Seed or add ≥ 2 active products in Bảng giá.

1. **Check-in + 2 add-ons, cash (one payment).** POS → Quầy → tap a free seat → keep the plan → in "Đồ thêm" tap +
   on "Mì ly" twice and "Trứng" once → footer shows Gói 45.000đ, Đồ thêm (3 món), Tổng cộng → Tiền mặt → "Thu tiền …"
   → confirm. "Đã thanh toán" shows the total and "Gồm đồ thêm: 2 Mì ly, 1 Trứng". Tap the seat → bill shows the
   lines. With a member attached: one "+N điểm" on the full total, one notification in the customer bell.
2. **Same with VietQR.** Choose VietQR → "Tạo QR thanh toán {total}" → one QR for plan + add-ons → pay (or send the
   SePay test webhook) → Paid screen; the bill has the items marked VietQR. Admin → Nhật ký thanh toán → the
   RequestCreated detail lists the items.
3. **Add more later** still works: seat → "Thêm đồ ăn / uống" → separate cash/QR sale on the same bill.
4. **Print a walk-in bill.** On the "Đã thanh toán" screen (or seat → "In bill") → `bill-XXXXXXXX.pdf` downloads; it
   shows branch, code, seat, plan, times, add-ons, cash/VietQR, total, staff, print time and the QR "Quét để tích điểm".
   Scan the QR with a phone → customer app `/claim?code=XXXXXXXX` (login first if needed; the code stays prefilled)
   → "Tích điểm". Print again → the receipt now shows the member and points instead of the QR.
5. **Points history → bill.** Customer app → Điểm → tap a "Lịch sử điểm" row with a bill (chevron) → `/bills/{code}`
   with seat, plan, extensions, add-ons and points. Rows without a bill are not clickable. Tôi → Hóa đơn rows behave
   the same.
