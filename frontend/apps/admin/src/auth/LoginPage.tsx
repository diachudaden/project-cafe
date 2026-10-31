import { useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { Alert, Button, Card, Form, Input, Typography } from "antd";
import { ApiError, type LoginInput } from "@cafecr/api-client";
import { isAdmin, useAuth } from "./AuthProvider";

export function LoginPage() {
  const { state, login, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const from = (location.state as { from?: string } | null)?.from ?? "/";

  if (state.status === "signedIn" && isAdmin(state.user)) {
    return <Navigate to={from} replace />;
  }

  async function onFinish(values: LoginInput) {
    setError(null);
    setSubmitting(true);
    try {
      const user = await login({ ...values, deviceId: "admin-web" });
      if (!isAdmin(user)) {
        await logout();
        setError("Tài khoản này không có vai trò Admin.");
        return;
      }
      navigate(from, { replace: true });
    } catch (e) {
      // The API answers 401 without a body; do not reveal whether the email exists.
      setError(
        e instanceof ApiError && e.status === 401
          ? "Email hoặc mật khẩu không đúng."
          : e instanceof Error
            ? e.message
            : "Đăng nhập không thành công.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div style={{ minHeight: "100vh", display: "grid", placeItems: "center", background: "#f5f0eb", padding: 16 }}>
      <Card style={{ width: "100%", maxWidth: 380 }}>
        <Typography.Title level={3} style={{ marginTop: 0 }}>
          Chốn Riêng — Quản trị
        </Typography.Title>
        {error && <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} />}
        <Form<LoginInput> layout="vertical" onFinish={onFinish} requiredMark={false}>
          <Form.Item
            label="Email"
            name="email"
            rules={[
              { required: true, message: "Nhập email." },
              { type: "email", message: "Email không hợp lệ." },
            ]}
          >
            <Input autoComplete="username" autoFocus />
          </Form.Item>
          <Form.Item label="Mật khẩu" name="password" rules={[{ required: true, message: "Nhập mật khẩu." }]}>
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={submitting}>
            Đăng nhập
          </Button>
        </Form>
      </Card>
    </div>
  );
}
