import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "@chonrieng/auth";
import { AppFrame, SampleDataNotice, SignOutButton } from "@chonrieng/ui";

const items = [
  { to: "/places", label: "Tìm quán", icon: "⌖" },
  { to: "/scan", label: "Quét bill", icon: "▦" },
  { to: "/points", label: "Điểm", icon: "✧" },
  { to: "/profile", label: "Tôi", icon: "○" },
];

export function CustomerLayout() {
  const { signOut, user } = useAuth();

  return (
    <AppFrame className="customer-app">
      <SampleDataNotice />
      <header className="customer-header">
        <a className="customer-brand" href="/places">
          <span className="customer-brand__mark">CR</span>
          <span><strong>Chốn Riêng</strong><small>GÓC RIÊNG CỦA BẠN</small></span>
        </a>
        <div className="customer-header-actions">
          <span className="customer-preview-pill">{user?.fullName}</span>
          <SignOutButton onSignOut={() => void signOut()} />
        </div>
      </header>
      <Outlet />
      <nav className="customer-bottom-nav" aria-label="Điều hướng khách hàng">
        {items.map((item) => (
          <NavLink
            className={({ isActive }) => `cr-bottom-link${isActive ? " is-active" : ""}`}
            key={item.to}
            to={item.to}
          >
            <span aria-hidden="true">{item.icon}</span>
            {item.label}
          </NavLink>
        ))}
      </nav>
    </AppFrame>
  );
}
