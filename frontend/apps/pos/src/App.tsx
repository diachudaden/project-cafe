import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import { Button, Result, Spin } from "antd";
import { hasRole } from "@cafecr/ui";
import { useAuth } from "./api";
import { LoginPage } from "./LoginPage";
import { PosLayout } from "./PosLayout";
import { CounterPage } from "./features/counter/CounterPage";
import { SchedulePage } from "./features/schedule/SchedulePage";
import { PayrollPage } from "./features/payroll/PayrollPage";
import { ActivityPage } from "./features/activity/ActivityPage";

function RequireEmployee({ children }: { children: React.ReactNode }) {
  const { state, logout } = useAuth();
  const location = useLocation();
  if (state.status === "loading") return <Spin size="large" fullscreen />;
  if (state.status === "signedOut") return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  if (!hasRole(state.user, "Staff", "BranchManager")) {
    return <Result status="403" title="POS dành cho nhân viên" subTitle="Tài khoản này không có hồ sơ nhân viên."
      extra={<Button onClick={() => void logout()}>Đăng xuất</Button>} />;
  }
  return <>{children}</>;
}

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireEmployee><PosLayout /></RequireEmployee>}>
        <Route index element={<CounterPage />} />
        <Route path="/schedule" element={<SchedulePage />} />
        <Route path="/payroll" element={<PayrollPage />} />
        <Route path="/activity" element={<ActivityPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
