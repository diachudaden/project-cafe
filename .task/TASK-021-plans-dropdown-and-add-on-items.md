# TASK-021: All price plans in POS + add-on items (đồ thêm)

## Status

Implemented; automated checks pass. Pending: apply migration `AddOnItems` on the server, add real menu items in Bảng giá, try a cash and a VietQR add-on on the POS.

## Goal

1. POS check-in shows **every** valid price plan ("Gói") of the branch in a dropdown with its name, and the bill is created from the chosen one.
2. Staff can sell add-on items (mì, trứng, bánh, nước...) to a seated customer, separately from extending the session; they go on the same bill.

## Problem (1)

`CatalogService.ResolveActivePlans` keeps one plan per duration (decision D4: "one 4-hour plan per branch, branch overrides global"). On the server every plan is 240 minutes, so the 3+ valid plans collapse into one. The POS label also showed only "4 tiếng – giá", not the plan name.

## Decisions

- D4 is relaxed: all plans valid now for the branch are offered (branch plans first, then chain-wide, by duration and price). Check-in and payment validation accept any of them.
- Add-on items are attached to the **active session's bill**, like extensions: `Bill.Total`, `CashPaidTotal` / `VietQrPaidTotal`, loyalty points, shift closing and reports pick them up with no extra work.
- Line prices are snapshotted on the bill item (name, unit price), so later menu changes do not alter past bills.
- Payment: cash (immediate) or VietQR (new `PaymentRequestKind.AddOn`, items snapshotted on the request, bill lines created when SePay settles).
- Products are managed like price plans: BranchManager for own branches, chain-wide products Admin only. A product already sold is deactivated instead of deleted.
- Not included: selling items without a seat/session, stock/inventory, item refunds.

## Scope

- Domain: `Product`, `BillItem`; `PaymentRequest.Items` (JSON snapshot) and `PaymentRequestKind.AddOn`; migration `AddOnItems`.
- API: `GET /api/branches/{branchId}/products` (POS menu), `GET /api/branches/{branchId}/products/all`, `POST/PUT/DELETE /api/products`; `POST /api/sessions/{id}/items` (cash), `POST /api/sessions/{id}/items/payment-request` (VietQR); session view lists bill items.
- Settlement + payment trace (TASK-020) for the new kind.
- Admin "Bảng giá": "Đồ thêm" menu management.
- POS: plan dropdown at check-in; "Thêm đồ" sheet (grouped menu, +/- quantity, total, Cash/VietQR) from the seat sheet and right after check-in; bill lines shown on the seat sheet. Extension label uses the session's own plan price.
- Demo seed: a few chain-wide products.

## Acceptance criteria

- With N valid plans for a branch, POS lists N and check-in charges the chosen plan.
- Cash add-on: bill total and cash total grow by the item sum, lines are stored with snapshot prices, member gets points.
- VietQR add-on: request amount = server-computed sum; on settlement the lines are added and totals use VietQR.
- Inactive / other-branch products and quantities ≤ 0 are rejected; closed sessions cannot get items.

## Verification

- Unit: all valid plans offered (branch first, expired/foreign excluded); cash add-on totals, snapshot prices, merged duplicate lines, rejection of inactive / other-branch / zero-quantity items and closed sessions; VietQR add-on amount computed server-side and lines added only after SePay confirms (bill becomes Mixed, payment trace "Settled").
- `dotnet test CafeCR.slnx`: unit 90, integration 12 passed. `npm run typecheck`, `npm test`, `npm run build` passed.

## Deploy

```bash
dotnet ef database update --project backend/src/ChonRieng.Infrastructure --startup-project backend/src/ChonRieng.Api
```

Applies `PaymentLogs` (TASK-020) if not yet applied, then `AddOnItems` (new tables `Products`, `BillItems`, nullable `PaymentRequests.Items`). The API reads `BillItems` when listing sessions, so **apply before deploying this API**.
