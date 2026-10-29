import { Navigate, Outlet } from "react-router-dom";
import { StatePanel } from "@chonrieng/ui";
import { useAuth } from "./AuthContext";

export function RequireAuth() {
  const { status, error } = useAuth();

  if (status === "checking") {
    return (
      <main className="cr-auth-gate">
        <StatePanel state="loading" title="Đang khôi phục phiên đăng nhập" />
      </main>
    );
  }

  if (status === "anonymous") {
    return <Navigate to="/login" replace state={{ authError: error }} />;
  }

  return <Outlet />;
}
