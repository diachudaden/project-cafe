import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { Button, Result, Spin } from "antd";
import { hasRole } from "@cafecr/ui";
import { useAuth } from "../api";

/** Web Quản lý is for Admin (whole chain) and BranchManager (assigned branches). */
export function RequireManagement({ children }: { children: ReactNode }) {
  const { state, logout } = useAuth();
  const location = useLocation();

  if (state.status === "loading") {
    return <Spin size="large" fullscreen />;
  }

  if (state.status === "signedOut") {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (!hasRole(state.user, "Admin", "BranchManager")) {
    return (
      <Result
        status="403"
        title="Không có quyền truy cập"
        subTitle="Web Quản lý dành cho Admin và Quản lý chi nhánh. Nhân viên dùng POS tại /pos, khách dùng trang chủ."
        extra={<Button onClick={() => void logout()}>Đăng xuất</Button>}
      />
    );
  }

  return children;
}
