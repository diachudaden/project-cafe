import { useState } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { Alert, Button, Card, Form, Input, Segmented, Typography } from "antd";
import { ApiError, type RegisterInput } from "@cafecr/api-client";
import { errorText, hasRole } from "@cafecr/ui";
import { useAuth } from "../api";

export function LoginPage() {
  const { state, login, register, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [mode, setMode] = useState<"login" | "register">("login");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const from = (location.state as { from?: string } | null)?.from ?? "/points";

  if (state.status === "signedIn" && hasRole(state.user, "Customer")) {
    return <Navigate to={from} replace />;
  }

  async function onFinish(values: RegisterInput) {
    setError(null);
    setSubmitting(true);
    try {
      const user = mode === "login"
        ? await login({ email: values.email, password: values.password, deviceId: "customer-web" })
        : await register({ ...values, phoneNumber: values.phoneNumber || null, deviceId: "customer-web" });
      if (!hasRole(user, "Customer")) {
        await logout();
        setError("Tài khoản này không phải tài khoản khách hàng.");
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
    <div style={{ padding: 16 }}>
      <Typography.Title level={3}>Chốn Riêng</Typography.Title>
      <Card>
        <Segmented block value={mode} onChange={(v) => setMode(v as "login" | "register")} style={{ marginBottom: 16 }}
          options={[{ value: "login", label: "Đăng nhập" }, { value: "register", label: "Đăng ký" }]} />
        {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
        <Form<RegisterInput> layout="vertical" onFinish={onFinish} requiredMark={false}>
          {mode === "register" && (
            <Form.Item label="Họ tên" name="fullName" rules={[{ required: true, max: 150 }]}><Input autoComplete="name" /></Form.Item>
          )}
          <Form.Item label="Email" name="email" rules={[{ required: true, type: "email" }]}><Input autoComplete="email" inputMode="email" /></Form.Item>
          {mode === "register" && (
            <Form.Item label="Số điện thoại (tùy chọn)" name="phoneNumber"><Input autoComplete="tel" inputMode="tel" /></Form.Item>
          )}
          <Form.Item label="Mật khẩu" name="password" rules={[{ required: true, ...(mode === "register" ? { min: 12 } : {}) }]}
            extra={mode === "register" ? "Tối thiểu 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt." : undefined}>
            <Input.Password autoComplete={mode === "login" ? "current-password" : "new-password"} />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={submitting}>{mode === "login" ? "Đăng nhập" : "Tạo tài khoản"}</Button>
        </Form>
      </Card>
    </div>
  );
}
