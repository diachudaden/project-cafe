import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "@chonrieng/auth";
import { AppFrame, SampleDataNotice, SignOutButton } from "@chonrieng/ui";

const navItems = [
  { label: "Hoạt động", to: "/dashboard", icon: "◫" },
  { label: "Lịch ca", to: "/shifts", icon: "◷" },
  { label: "Chi nhánh", to: "/branches", icon: "⌂" },
  { label: "Thu chi", to: "/ledger", icon: "↗" },
  { label: "Báo cáo", to: "/reports", icon: "▤" },
];

export function AdminLayout() {
  const { signOut, user } = useAuth();

  return (
    <AppFrame className="admin-app">
      <SampleDataNotice />
      <div className="admin-layout">
        <aside className="admin-sidebar">
          <a className="admin-brand" href="/dashboard" aria-label="Chốn Riêng trang hoạt động">
            <span className="admin-brand__mark">CR</span>
            <span>
              <strong>Chốn Riêng</strong>
              <small>QUẢN TRỊ</small>
            </span>
          </a>
          <p className="admin-nav-caption">VẬN HÀNH</p>
          <nav className="admin-nav" aria-label="Điều hướng quản trị">
            {navItems.map((item) => (
              <NavLink
                className={({ isActive }) => `admin-nav-link${isActive ? " is-active" : ""}`}
                key={item.to}
                to={item.to}
              >
                <span aria-hidden="true">{item.icon}</span>
                {item.label}
              </NavLink>
            ))}
          </nav>
          <div className="admin-sidebar-footer">
            <span className="admin-avatar" aria-hidden="true">CR</span>
            <span>
              <strong>{user?.fullName}</strong>
              <small>{user?.role}</small>
            </span>
            <SignOutButton onSignOut={() => void signOut()} />
          </div>
        </aside>
        <div className="admin-main">
          <div className="admin-topbar">
            <span>Không gian quản trị</span>
            <span className="admin-topbar-status"><i aria-hidden="true" /> Dữ liệu minh họa</span>
          </div>
          <Outlet />
        </div>
      </div>
    </AppFrame>
  );
}
