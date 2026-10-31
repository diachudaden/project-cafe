import type { ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { hasRole } from "@cafecr/ui";
import { useAuth } from "./api";
import { RequireManagement } from "./auth/RequireManagement";
import { LoginPage } from "./auth/LoginPage";
import { AdminLayout } from "./layout/AdminLayout";
import { DashboardPage } from "./features/dashboard/DashboardPage";
import { SchedulePage } from "./features/schedule/SchedulePage";
import { RequestsPage } from "./features/requests/RequestsPage";
import { AttendancePage } from "./features/attendance/AttendancePage";
import { ClosingsPage } from "./features/closings/ClosingsPage";
import { ExpensesPage } from "./features/expenses/ExpensesPage";
import { PayrollPage } from "./features/payroll/PayrollPage";
import { ReportsPage } from "./features/reports/ReportsPage";
import { FloorPage } from "./features/floor/FloorPage";
import { PricingPage } from "./features/pricing/PricingPage";
import { EmployeesPage } from "./features/employees/EmployeesPage";
import { BranchesPage } from "./features/branches/BranchesPage";
import { UsersPage } from "./features/users/UsersPage";

function AdminOnly({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  return hasRole(state.user, "Admin") ? <>{children}</> : <Navigate to="/" replace />;
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <RequireManagement>
            <AdminLayout />
          </RequireManagement>
        }
      >
        <Route index element={<DashboardPage />} />
        <Route path="/schedule" element={<SchedulePage />} />
        <Route path="/requests" element={<RequestsPage />} />
        <Route path="/attendance" element={<AttendancePage />} />
        <Route path="/closings" element={<ClosingsPage />} />
        <Route path="/expenses" element={<ExpensesPage />} />
        <Route path="/payroll" element={<PayrollPage />} />
        <Route path="/reports" element={<ReportsPage />} />
        <Route path="/floor" element={<FloorPage />} />
        <Route path="/pricing" element={<PricingPage />} />
        <Route path="/employees" element={<EmployeesPage />} />
        <Route path="/branches" element={<AdminOnly><BranchesPage /></AdminOnly>} />
        <Route path="/users" element={<AdminOnly><UsersPage /></AdminOnly>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
