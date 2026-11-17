# 01 — Role and permission matrix

Scope rules (already approved in [.rule/security.md](../../../.rule/security.md)):

- **Admin**: whole chain.
- **BranchManager / Staff**: only branches in their `EmployeeBranch` rows (signed `branch_id` claims, refreshed every 15 min).
- **Customer**: only their own records.
- Every branch-scoped endpoint uses the `BranchScope` policy **and** filters the query by `BranchAccessScope` (never trust a branch ID from the client).

Legend: ✅ full · 👁 read only · 🔸 own branch(es) · 🙋 own records · — no access

## Matrix

| Module | Action | Admin | BranchManager | Staff | Customer |
| --- | --- | --- | --- | --- | --- |
| **Accounts** | Create/lock users, assign roles | ✅ | — | — | — |
| | Assign employee ↔ branch | ✅ | 🔸 (staff only, not managers) | — | — |
| | Own profile, change password | ✅ | ✅ | ✅ | ✅ |
| **Branches** | Create / edit branch profile | ✅ | 👁 🔸 | 👁 🔸 | 👁 public list |
| | Zones & seats (create, maintenance on/off) | ✅ | 🔸 | 👁 🔸 | 👁 availability counts only |
| | Price plans | ✅ (global + any branch) | 🔸 branch plans | 👁 🔸 | 👁 active plans |
| **Sessions (POS)** | Check-in, extend, checkout | ✅ | 🔸 | 🔸 *while in an open shift* | — |
| | View active sessions | ✅ | 🔸 | 🔸 | 🙋 own active session |
| | Correct / cancel a session | ✅ | 🔸 with reason | — | — |
| **Bills & points** | View bills | ✅ | 🔸 | 🔸 current shift | 🙋 claimed bills |
| | Claim bill for points | — | — | — | 🙋 |
| | Points balance & history | ✅ | 👁 🔸 customers who visited | — | 🙋 |
| **Shifts** | Create / publish / copy week / assign | ✅ | 🔸 | — | — |
| | Accept / decline / claim open shift | — | — | 🙋 | — |
| | View schedule | ✅ | 🔸 | 🙋 + colleagues on same shift | — |
| | Availability (giờ rảnh) | 👁 | 👁 🔸 | 🙋 edit | — |
| | Swap / leave / drop request | — | 🔸 approve/reject | 🙋 create | — |
| **Attendance** | Check-in / check-out ("Tham gia ca" / "Rời ca") | — | 🙋 (if also working the shift) | 🙋 | — |
| | Approve attendance | ✅ | 🔸 | — | — |
| **Shift closing** | Preview & submit closing | — | 🙋 | 🙋 | — |
| | Review / approve closing | ✅ | 🔸 | — | — |
| **Finance** | Expenses (append-only ledger) | ✅ | 🔸 | — | — |
| | Generate / adjust / finalize payroll | ✅ | 🔸 *(see decision D10)* | — | — |
| | View payroll | ✅ | 🔸 | 🙋 | — |
| **Reports** | Revenue, P&L, occupancy, branch comparison | ✅ all branches | 🔸 | — | — |
| **Notifications** | Own feed, mark read | ✅ | ✅ | ✅ | ✅ |

## Notes

- A BranchManager who also works shifts is both an employee (attendance, payroll) and a manager. The matrix applies per action, so one account can do both.
- Managers must not be able to promote themselves or other managers. Role assignment stays Admin-only.
- "Staff while in an open shift" means: the staff member has an attendance check-in for a shift at that branch that has not been closed. This is what links every bill to a shift (`Bill.ShiftId` is required).
- Customer access to `GET /me/...` endpoints needs no route ID, so `CustomerOwnResource` is used only where a `customerId` appears in the route (admin/manager views).
