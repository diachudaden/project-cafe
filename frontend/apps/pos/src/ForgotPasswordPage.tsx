import { useNavigate, useSearchParams } from "react-router-dom";
import { Card, Typography } from "antd";
import { ForgotPasswordForm } from "@cafecr/ui";
import { useAuth } from "./api";

export function ForgotPasswordPage() {
  const { forgotPassword } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  return (
    <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 16, background: "#2b1d12" }}>
      <Card style={{ width: "100%", maxWidth: 420 }}>
        <Typography.Title level={3} style={{ marginTop: 0 }}>Quên mật khẩu</Typography.Title>
        <ForgotPasswordForm app="pos" initialEmail={params.get("email") ?? undefined} onSubmit={forgotPassword}
          onBackToLogin={() => navigate("/login")} />
      </Card>
    </div>
  );
}
