import type { ReactNode } from "react";
import { Navigate, NavLink, Route, Routes, useLocation } from "react-router-dom";
import { Space, Spin, theme } from "antd";
import { ThemeToggle } from "@cafecr/ui";
import { useAuth } from "./api";
import { FindPage } from "./pages/FindPage";
import { ClaimPage } from "./pages/ClaimPage";
import { PointsPage } from "./pages/PointsPage";
import { MePage } from "./pages/MePage";
import { LoginPage } from "./pages/LoginPage";
import { BillPage } from "./pages/BillPage";
import { NotificationBell } from "./components/NotificationBell";

const tabs = [
  { to: "/", label: "Tìm quán", icon: "📍" },
  { to: "/claim", label: "Quét bill", icon: "🧾" },
  { to: "/points", label: "Điểm", icon: "⭐" },
  { to: "/me", label: "Tôi", icon: "👤" },
];

function RequireCustomer({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  const location = useLocation();
  if (state.status === "loading") return <Spin style={{ display: "block", marginTop: 80 }} />;
  if (state.status === "signedOut") return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  return <>{children}</>;
}

/** Mobile-first layout with 4 bottom tabs (reference: Tìm quán · Quét bill · Điểm · Tôi). */
export function App() {
  const { token } = theme.useToken();
  const { state } = useAuth();
  return (
    <div style={{ maxWidth: 560, margin: "0 auto", minHeight: "100vh", background: token.colorBgLayout, paddingBottom: 72 }}>
      <div style={{ position: "fixed", top: 12, right: 12, zIndex: 10 }}>
        <Space>
          {state.status === "signedIn" && <NotificationBell />}
          <ThemeToggle />
        </Space>
      </div>
      <Routes>
        <Route path="/" element={<FindPage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/claim" element={<RequireCustomer><ClaimPage /></RequireCustomer>} />
        <Route path="/points" element={<RequireCustomer><PointsPage /></RequireCustomer>} />
        <Route path="/me" element={<RequireCustomer><MePage /></RequireCustomer>} />
        <Route path="/bills/:code" element={<RequireCustomer><BillPage /></RequireCustomer>} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
      <nav style={{ position: "fixed", bottom: 0, left: 0, right: 0, background: token.colorBgContainer, borderTop: `1px solid ${token.colorBorderSecondary}`, display: "flex", justifyContent: "center", paddingBottom: "env(safe-area-inset-bottom)" }}>
        <div style={{ display: "flex", width: "100%", maxWidth: 560 }}>
          {tabs.map((t) => (
            <NavLink key={t.to} to={t.to} end style={({ isActive }) => ({
              flex: 1, textAlign: "center", padding: "8px 0", textDecoration: "none", color: isActive ? token.colorPrimary : token.colorTextSecondary, fontWeight: isActive ? 600 : 400,
            })}>
              <div style={{ fontSize: 20 }}>{t.icon}</div>
              <div style={{ fontSize: 12 }}>{t.label}</div>
            </NavLink>
          ))}
        </div>
      </nav>
    </div>
  );
}
