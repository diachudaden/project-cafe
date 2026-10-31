import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { Button, Result, Spin } from "antd";
import { isAdmin, useAuth } from "./AuthProvider";

export function RequireAdmin({ children }: { children: ReactNode }) {
  const { state, logout } = useAuth();
  const location = useLocation();

  if (state.status === "loading") {
    return <Spin size="large" fullscreen />;
  }

  if (state.status === "signedOut") {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (!isAdmin(state.user)) {
    return (
      <Result
        status="403"
        title="Không có quyền truy cập"
        subTitle="Ứng dụng quản trị chỉ dành cho tài khoản có vai trò Admin."
        extra={<Button onClick={() => void logout()}>Đăng xuất</Button>}
      />
    );
  }

  return children;
}
