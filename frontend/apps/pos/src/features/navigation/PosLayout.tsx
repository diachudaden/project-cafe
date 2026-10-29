import { Outlet } from "react-router-dom";
import { useAuth } from "@chonrieng/auth";
import { AppFrame, SampleDataNotice, SignOutButton } from "@chonrieng/ui";

export function PosLayout() {
  const { signOut, user } = useAuth();

  return (
    <AppFrame className="pos-app">
      <SampleDataNotice />
      <header className="pos-header">
        <a className="pos-brand" href="/seats">
          <span className="pos-brand__mark">CR</span>
          <span><strong>Chốn Riêng</strong><small>ĐIỂM PHỤC VỤ · BẢN MẪU</small></span>
        </a>
        <div className="pos-header-actions">
          <span className="pos-header-context">
            {user?.fullName} · Ca chiều <i aria-hidden="true" /> Nguyễn Trãi
          </span>
          <SignOutButton onSignOut={() => void signOut()} />
        </div>
      </header>
      <Outlet />
    </AppFrame>
  );
}
