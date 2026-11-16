import { useNavigate, useSearchParams } from "react-router-dom";
import { Card, Typography } from "antd";
import { ResetPasswordForm } from "@cafecr/ui";
import { useAuth } from "../api";

export function ResetPasswordPage() {
  const { resetPassword } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  return (
    <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 16 }}>
      <Card style={{ width: "100%", maxWidth: 380 }}>
        <Typography.Title level={3} style={{ marginTop: 0 }}>Đặt lại mật khẩu</Typography.Title>
        <ResetPasswordForm email={params.get("email")} token={params.get("token")} onSubmit={resetPassword}
          onDone={() => navigate("/login", { replace: true })}
          onRequestNewLink={(email) => navigate(email ? `/forgot-password?email=${encodeURIComponent(email)}` : "/forgot-password")} />
      </Card>
    </div>
  );
}
