import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Navigate, Route, Routes, useLocation } from "react-router-dom";
import { EmptyFeature } from "@chonrieng/ui";
import { AuthProvider, RequireAuth } from "@chonrieng/auth";
import { apiBaseUrl } from "./config";
import { SignInPage } from "./features/auth/SignInPage";
import { BranchesPage } from "./features/branches/BranchesPage";
import { DashboardPage } from "./features/dashboard/DashboardPage";
import { AdminLayout } from "./features/navigation/AdminLayout";
import "./styles.css";
import "@chonrieng/ui/styles.css";

function UnavailablePage() {
  const location = useLocation();
  const labels: Record<string, string> = {
    "/shifts": "Lịch ca",
    "/ledger": "Thu chi",
    "/reports": "Báo cáo",
  };

  return (
    <main className="cr-page-content">
      <EmptyFeature title={labels[location.pathname] ?? "Trang này"} />
    </main>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AuthProvider apiBaseUrl={apiBaseUrl} client="admin">
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<SignInPage />} />
          <Route element={<RequireAuth />}>
            <Route element={<AdminLayout />}>
              <Route index element={<Navigate to="/dashboard" replace />} />
              <Route path="dashboard" element={<DashboardPage />} />
              <Route path="branches" element={<BranchesPage />} />
              <Route path="shifts" element={<UnavailablePage />} />
              <Route path="ledger" element={<UnavailablePage />} />
              <Route path="reports" element={<UnavailablePage />} />
              <Route path="*" element={<UnavailablePage />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  </StrictMode>,
);
