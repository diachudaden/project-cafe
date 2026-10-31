import { Navigate, Route, Routes } from "react-router-dom";
import { RequireAdmin } from "./auth/RequireAdmin";
import { LoginPage } from "./auth/LoginPage";
import { AdminLayout } from "./layout/AdminLayout";
import { BranchesPage } from "./features/branches/BranchesPage";
import { ZonesPage } from "./features/zones/ZonesPage";
import { SeatsPage } from "./features/seats/SeatsPage";
import { PricePlansPage } from "./features/price-plans/PricePlansPage";
import { UsersPage } from "./features/users/UsersPage";

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <RequireAdmin>
            <AdminLayout />
          </RequireAdmin>
        }
      >
        <Route index element={<Navigate to="/branches" replace />} />
        <Route path="/branches" element={<BranchesPage />} />
        <Route path="/zones" element={<ZonesPage />} />
        <Route path="/seats" element={<SeatsPage />} />
        <Route path="/price-plans" element={<PricePlansPage />} />
        <Route path="/users" element={<UsersPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
