import { useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { Alert, Button, Card, Form, Input, Typography } from "antd";
import { ApiError, type LoginInput } from "@cafecr/api-client";
import { errorText, hasRole } from "@cafecr/ui";
import { useAuth } from "./api";

export function LoginPage() {
  const { state, login, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const from = (location.state as { from?: string } | null)?.from ?? "/";

  if (state.status === "signedIn" && hasRole(state.user, "Staff", "BranchManager")) {
    return <Navigate to={from} replace />;
  }

  async function onFinish(values: LoginInput) {
    setError(null);
    setSubmitting(true);
    try {
      const user = await login({ ...values, deviceId: "pos-tablet" });
      if (!hasRole(user, "Staff", "BranchManager")) {
        await logout();
        setError("Tài khoản này không phải nhân viên.");
        return;
      }
      navigate(from, { replace: true });
    } catch (e) {
      setError(e instanceof ApiError && e.status === 401 ? "Email hoặc mật khẩu không đúng." : errorText(e));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", background: "#2b1d12", padding: 16 }}>
      <Card style={{ width: "100%", maxWidth: 420 }}>
        <Typography.Title level={3} style={{ marginTop: 0 }}>Chốn Riêng — POS</Typography.Title>
        {error && <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} />}
        <Form<LoginInput> layout="vertical" onFinish={onFinish} requiredMark={false}>
          <Form.Item label="Email" name="email" rules={[{ required: true, message: "Nhập email." }, { type: "email", message: "Email không hợp lệ." }]}>
            <Input autoComplete="username" autoFocus />
          </Form.Item>
          <Form.Item label="Mật khẩu" name="password" rules={[{ required: true, message: "Nhập mật khẩu." }]}>
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={submitting}>Đăng nhập</Button>
        </Form>
      </Card>
    </div>
  );
}
