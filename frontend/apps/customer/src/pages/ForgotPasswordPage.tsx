import { useNavigate, useSearchParams } from "react-router-dom";
import { Card, Typography } from "antd";
import { ForgotPasswordForm } from "@cafecr/ui";
import { useAuth } from "../api";

export function ForgotPasswordPage() {
  const { forgotPassword } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  return (
    <div style={{ padding: 16 }}>
      <Typography.Title level={3}>Quên mật khẩu</Typography.Title>
      <Card>
        <ForgotPasswordForm app="customer" initialEmail={params.get("email") ?? undefined} onSubmit={forgotPassword}
          onBackToLogin={() => navigate("/login")} />
      </Card>
    </div>
  );
}
