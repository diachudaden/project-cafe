# TASK-019: Admin menu icons, employee/account filters, cross-branch open shifts

## Status

Implemented; automated checks pass. Manual browser check on the deployed site is pending.

## Goal

Four follow-ups from the admin/POS review:

1. Admin left menu: every item has an icon.
2. "Nhân viên" (`/admin/employees`): show all employees (not only the selected branch), free-text filter (name, email, phone) and tag filters (role, branch), plus a "Hiện tất cả" (no paging) option.
3. "Tài khoản & vai trò" (`/admin/users`): search by name / email / phone number, and three role tabs required by the teacher: Nhân viên (POS) = Staff, Quản lý = BranchManager + Admin, Khách hàng = Customer (plus "Tất cả").
4. Bug: a staff member often did not see an open shift ("Ca trống (nhân viên tự nhận)") in POS "Ca", mostly at non-main branches.

## Root cause (4)

`GET /api/shifts/open` only listed open shifts of branches the employee was linked to through `EmployeeBranch`. Most staff are linked to their home branch only (the demo `staff.demo` account only to the main branch), so open shifts at other branches never appeared. Open shifts also stay hidden while the week is still Draft, which the admin calendar did not make visible.

## Changes

- Open shifts are a chain-wide pool: every employee sees published open shifts of all active branches, own branches first; `OpenShiftView.isMyBranch` marks the others. Claiming a shift at another branch skips the `branch` rule and adds a non-home `EmployeeBranch` link (the next token refresh includes that branch, so check-in works). POS shows a "Chi nhánh khác" tag and refreshes the list every 60 s.
- Admin calendar labels a Draft open shift "Ca trống · chưa công bố" with a tooltip that staff only see it after publishing.
- `GET /api/admin/users` accepts `search` and `role` (comma-separated) and returns `roles` per row; ordered by name.
- New `GET /api/employees`: Admin gets the whole chain, BranchManager the employees of their branches.
- `CrudPage` supports `listParams` and a `toolbar`; `@ant-design/icons` added to the admin app.

## Follow-up (13/11, list still empty on the server)

Checked the live data: every published open shift with a free spot was already in the past, and tonight's open shift at Chi nhánh A was still **Draft**: it had been added to a week that was already published, and `CreateAsync` always created Draft, so it stayed hidden until "Công bố" was pressed again.

- A shift created in a week that already has published shifts is now created **Published**.
- Open shifts stay listed and claimable while running, until 30 minutes before the end (POS tag "Đang diễn ra").
- Lateness for a shift claimed after it started counts from the claim time (`ShiftAssignment.RespondedAt`), not the shift start.
- Integration test: open shift added to a published week is published; a running open shift is listed and claimable by a staff member with no branch link.

## Verification

- `dotnet test CafeCR.slnx`: unit 85, integration 10 passed (new: cross-branch open shift claim, user list role/search filters).
- `npm run typecheck`, `npm test`, `npm run build` in `frontend` passed.
