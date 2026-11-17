# TASK-022: Modern "Lịch ca" grid with pre-defined shift slots

## Status

Implemented (frontend only, phase 1). Bulk activation per row/day and drag-to-assign are deferred to a later task.

## Goal

Replace the admin `/admin/schedule` week view (7 plain columns with a "+" button and a create-shift modal) with a modern **shift-type × day grid**:

1. Each day column has its own colour, so the week is easy to scan.
2. Every shift slot ("Loại ca": Sáng, Chiều, Tối, Đêm) already exists in the grid for every day. The admin clicks a slot and turns it **active** to create the shift. No create-shift form for the normal case.

## Current problems

- Columns are plain and identical; today is only bold text. Hard to scan a full week.
- To add a shift: "+" → modal → pick type → times → staff count → switch → "Tạo". 5 steps for the most common action.
- Shifts are stacked in creation order, so "Ca sáng" on Monday and "Ca sáng" on Tuesday don't line up.
- Status (draft, open, understaffed, waiting for answers) is shown as several tags of similar weight; the card gets noisy.

## Proposed layout

```
              Thứ 2 17/11   Thứ 3 18/11   Thứ 4 19/11  ...  Chủ nhật 23/11
              (blue)        (teal)        (green)            (rose)
 ☀ Ca sáng    [ An, Bình ]  [ + Kích hoạt][ Chi  1/2 ⚠]      [ ... ]
 06:00–14:00
 🌤 Ca chiều  [ Dũng     ]  [ ... ]
 14:00–22:00
 🌙 Ca tối    ...
 🌑 Ca đêm    ...  (only for 24/7 branches, see D3)
```

- **Rows = shift types** (`shiftTypeLabels`, default hours from `shiftTypeDefaults`), **columns = days**. Same slot always in the same place.
- **Day colours**: each weekday gets one accent colour (header strip + very light column tint). Today: stronger tint + "Hôm nay" badge. Past days: faded. Colours are theme tokens so they work in light and dark mode (TASK light/dark toggle).
- **Inactive slot**: dashed, muted cell showing "+ Kích hoạt" on hover.
- **Active slot (shift exists)**: filled card with
  - assignee chips (initials/avatar + name), colour by assignment status (Đã nhận / Chờ phản hồi / Từ chối);
  - small coverage bar `acceptedCount / requiredStaff` (green full, orange short);
  - compact icons instead of tag rows: draft (pencil), open shift "Ca trống" (hand), in progress (dot).
- **Summary bar** above the grid: total shifts · drafts · understaffed · open shifts · waiting answers. Clicking one highlights matching cells.
- **Legend** under the grid.

## Interaction

- Click an inactive slot → small popover (not a modal):
  - switch **Kích hoạt** (on = create the shift with default hours),
  - "Số người tối thiểu" (default 1), switch "Ca trống (nhân viên tự nhận)",
  - collapsed "Tùy chỉnh giờ" for start/end if the defaults don't fit.
  - Quick path: one click on "Kích hoạt" creates it with defaults.
- Click an active slot → existing `ShiftDrawer` (assign, validate rules, remove). Drawer gets a **Kích hoạt** switch at the top; turning it off = delete the shift, with confirm.
- **Bulk**: click a row header (e.g. "Ca sáng") → "Kích hoạt cả tuần"; click a day header → "Kích hoạt mọi ca trong ngày". Shows a summary like copy-week (created / skipped).
- Keep week navigation, "Sao chép tuần trước", "Công bố tuần".
- Mobile / narrow screens: switch to a day view (tabs per day, slots stacked vertically) instead of horizontal scroll.

## Decisions (confirmed 2026-11-14)

- D1 – "Active" = the shift row exists. Deactivate = `DELETE /api/shifts/{id}`; the backend already refuses shifts with attendance or bills, and notifies assignees if published. No new `IsActive` column.
- D2 – Deactivating a slot that has assignees asks for confirmation and lists who will be notified.
- D3 – Rows per branch: 24/7 branches (`Branch.Is24h`, in reality 2 locations) → Sáng, Chiều, Đêm; other branches → Sáng, Tối (2 shifts). A "Hiện tất cả loại ca" toggle shows all 4; a type that already has a shift that week is always shown.
- D4 – 24/7 branches use `shiftTypeDefaults`. Other branches: Ca sáng = opening → 15:00, Ca tối = 15:00 → closing; other types clipped to opening hours. Demo-level precision; "Đổi giờ" in the popover overrides.
- D5 – More than one shift of the same type on the same day (allowed by the API if start time differs) is shown stacked inside the cell.
- D6 – Bulk activation (row/day) is **deferred** to a later task.

## Scope

- Frontend only for phase 1: `frontend/apps/admin/src/features/schedule/SchedulePage.tsx` (split into `ScheduleGrid`, `SlotCell`, `ActivatePopover`, `ScheduleSummary`), day/shift-type colour tokens and icons in `@cafecr/ui`.
- Needs branch info (`Is24h`, open/close time) on the page; reuse the branch query already used elsewhere in admin.
- No API or schema change unless D6 needs a bulk endpoint.
- Optional phase 2: drag an employee from a side list onto a cell to assign (uses the existing validate → assign flow).

## Acceptance criteria

- The grid shows one row per visible shift type and one column per day; every cell is either an inactive slot or the existing shift(s).
- Each day column has a distinct colour; today is clearly marked; readable in light and dark mode.
- Clicking an inactive slot and turning on "Kích hoạt" creates a Draft (or Published, if the week is already published) shift with the type's default hours in ≤ 2 clicks.
- Turning a slot off deletes the shift after confirm; shifts with attendance/bills show the backend error and stay.
- Assign, rule-validation, publish and copy-week still work as before.
- Works at ~375px width (day view) without horizontal page scroll.

## Verification

- `npm run typecheck`, `npm test`, `npm run build` in `frontend`.
- Manual: activate/deactivate single slot, bulk row and day, published-week activation, deactivate a shift with attendance (must fail cleanly), 24/7 vs non-24h branch rows, dark mode, mobile width.

## Implementation

- `frontend/apps/admin/src/features/schedule/slots.ts` – visible shift types per branch, default hours per type/branch, day colours (Ant Design preset palettes, so dark mode adapts), highlight filters.
- `ScheduleGrid.tsx` – desktop grid (`ScheduleGrid`), phone day view (`ScheduleDayView`), coloured day headers with "Hôm nay", shift tiles (assignee chips, coverage bar, draft/open/in-progress icons), empty slot + activate popover (hours, staff count, Ca trống, **Kích hoạt** switch), legend. Past empty slots are not clickable.
- `ShiftDrawer.tsx` – moved out of the page; "Xóa ca" replaced by a **Kích hoạt** switch that deletes the shift after confirm (names who will be notified for published shifts).
- `SchedulePage.tsx` – compact week navigator, summary chips (Nháp / Thiếu người / Ca trống / Chờ phản hồi) that highlight matching shifts, "Hiện tất cả loại ca" toggle. The old create-shift modal is removed.
- No API or schema change.

## Verification results

- `npm run typecheck`, `npm test`, `npm run build` passed.
- Headless Chrome against a mocked API: light, dark, 390 px phone width, 24/7 branch (Sáng/Chiều/Đêm) and 07:00–23:00 branch (Sáng 07:00–15:00, Tối 15:00–23:00). Activating Sunday "Ca tối" on the non-24h branch sent `POST /api/shifts` with 15:00–23:00, requiredStaff 1, isOpen false.
- Not yet checked against the real API/server.
