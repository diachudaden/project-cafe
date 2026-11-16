import { useEffect, useState } from "react";
import { Alert, App, Button, Form, Input, Result, Typography } from "antd";
import type { Rule } from "antd/es/form";
import {
  ApiError,
  isInvalidResetLink,
  type ChangePasswordInput,
  type ForgotPasswordInput,
  type ForgotPasswordResult,
  type PasswordResetApp,
  type ResetPasswordInput,
} from "@cafecr/api-client";
import { errorText } from "./errors";
import { passwordPolicyText, passwordProblems } from "./password";

/** Shown after every forgot-password request, whether or not the email is registered. */
export const FORGOT_PASSWORD_NOTICE = "Nếu email đã đăng ký, bạn sẽ nhận được liên kết đặt lại mật khẩu trong vài phút.";
const TOO_MANY_REQUESTS = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút.";
const RESEND_COOLDOWN_SECONDS = 60;

const newPasswordRules: Rule[] = [
  { required: true, message: "Nhập mật khẩu mới." },
  {
    validator: (_, value: string | undefined) => {
      const problems = passwordProblems(value ?? "");
      return value && problems.length ? Promise.reject(new Error(problems.join(" "))) : Promise.resolve();
    },
  },
];

const confirmationRules = (field: string): Rule[] => [
  { required: true, message: "Nhập lại mật khẩu mới." },
  ({ getFieldValue }) => ({
    validator: (_, value: string | undefined) =>
      !value || value === getFieldValue(field) ? Promise.resolve() : Promise.reject(new Error("Mật khẩu nhập lại không khớp.")),
  }),
];

function useCountdown() {
  const [until, setUntil] = useState(0);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (until <= now) return;
    const id = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, [until, now]);
  const left = Math.max(0, Math.ceil((until - now) / 1000));
  return { left, start: (seconds: number) => { const t = Date.now(); setNow(t); setUntil(t + seconds * 1000); } };
}

export interface ForgotPasswordFormProps {
  app: PasswordResetApp;
  initialEmail?: string;
  onSubmit(input: ForgotPasswordInput): Promise<ForgotPasswordResult>;
  onBackToLogin(): void;
}

/** Email form; always shows the same notice afterwards and locks the button for 60 s. */
export function ForgotPasswordForm({ app, initialEmail, onSubmit, onBackToLogin }: ForgotPasswordFormProps) {
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const cooldown = useCountdown();

  async function onFinish({ email }: { email: string }) {
    setError(null);
    setSubmitting(true);
    try {
      await onSubmit({ email: email.trim(), app });
      setSent(true);
      cooldown.start(RESEND_COOLDOWN_SECONDS);
    } catch (e) {
      if (e instanceof ApiError && e.status === 429) {
        setError(TOO_MANY_REQUESTS);
        cooldown.start(RESEND_COOLDOWN_SECONDS);
      } else {
        setError(errorText(e));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <Typography.Paragraph type="secondary">Nhập email đăng nhập, chúng tôi sẽ gửi liên kết để đặt mật khẩu mới.</Typography.Paragraph>
      {sent && <Alert type="success" showIcon message={FORGOT_PASSWORD_NOTICE} style={{ marginBottom: 16 }} />}
      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
      <Form<{ email: string }> layout="vertical" onFinish={onFinish} requiredMark={false} initialValues={{ email: initialEmail ?? "" }}>
        <Form.Item label="Email" name="email" rules={[{ required: true, type: "email", message: "Nhập email hợp lệ." }]}>
          <Input autoComplete="email" inputMode="email" autoFocus />
        </Form.Item>
        <Button type="primary" htmlType="submit" block loading={submitting} disabled={cooldown.left > 0}>
          {cooldown.left > 0 ? `Gửi lại sau ${cooldown.left} giây` : sent ? "Gửi lại liên kết" : "Gửi liên kết"}
        </Button>
      </Form>
      <Button type="link" block style={{ marginTop: 8 }} onClick={onBackToLogin}>Quay lại đăng nhập</Button>
    </>
  );
}

export interface ResetPasswordFormProps {
  /** From the `email` and `token` query parameters of the emailed link. */
  email: string | null;
  token: string | null;
  onSubmit(input: ResetPasswordInput): Promise<void>;
  /** Called after a successful reset (navigate to the login page). */
  onDone(): void;
  /** Opens the forgot-password page (prefilled with the email) to request a new link. */
  onRequestNewLink(email: string | null): void;
}

export function ResetPasswordForm({ email, token, onSubmit, onDone, onRequestNewLink }: ResetPasswordFormProps) {
  const { message } = App.useApp();
  const [submitting, setSubmitting] = useState(false);
  const [linkInvalid, setLinkInvalid] = useState(!email || !token);
  const [error, setError] = useState<string | null>(null);

  if (linkInvalid) {
    return (
      <Result
        status="warning"
        title="Liên kết không hợp lệ hoặc đã hết hạn"
        subTitle="Hãy yêu cầu gửi lại liên kết đặt lại mật khẩu."
        extra={<Button type="primary" onClick={() => onRequestNewLink(email)}>Gửi lại liên kết</Button>}
      />
    );
  }

  async function onFinish({ newPassword }: { newPassword: string }) {
    setError(null);
    setSubmitting(true);
    try {
      await onSubmit({ email: email!, token: token!, newPassword });
      void message.success("Đã đặt lại mật khẩu. Hãy đăng nhập bằng mật khẩu mới.");
      onDone();
    } catch (e) {
      if (isInvalidResetLink(e)) setLinkInvalid(true);
      else setError(e instanceof ApiError && e.status === 429 ? TOO_MANY_REQUESTS : errorText(e));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <Typography.Paragraph type="secondary">Đặt mật khẩu mới cho <b>{email}</b>.</Typography.Paragraph>
      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
      <Form<{ newPassword: string; confirmation: string }> layout="vertical" onFinish={onFinish} requiredMark={false}>
        <input type="email" name="username" autoComplete="username" value={email ?? ""} readOnly hidden />
        <Form.Item label="Mật khẩu mới" name="newPassword" rules={newPasswordRules} extra={passwordPolicyText}>
          <Input.Password autoComplete="new-password" autoFocus />
        </Form.Item>
        <Form.Item label="Nhập lại mật khẩu mới" name="confirmation" dependencies={["newPassword"]} rules={confirmationRules("newPassword")}>
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        <Button type="primary" htmlType="submit" block loading={submitting}>Đặt lại mật khẩu</Button>
      </Form>
    </>
  );
}

export interface ChangePasswordFormProps {
  onSubmit(input: Omit<ChangePasswordInput, "refreshToken">): Promise<void>;
  onDone?(): void;
}

/** Change password while signed in; the current session stays signed in, other devices are signed out. */
export function ChangePasswordForm({ onSubmit, onDone }: ChangePasswordFormProps) {
  const { message } = App.useApp();
  const [form] = Form.useForm<{ currentPassword: string; newPassword: string; confirmation: string }>();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onFinish(values: { currentPassword: string; newPassword: string }) {
    setError(null);
    setSubmitting(true);
    try {
      await onSubmit({ currentPassword: values.currentPassword, newPassword: values.newPassword });
      void message.success("Đã đổi mật khẩu. Các thiết bị khác đã được đăng xuất.");
      form.resetFields();
      onDone?.();
    } catch (e) {
      const fields = e instanceof ApiError ? e.fieldErrors : {};
      if (fields.currentPassword || fields.newPassword) {
        form.setFields([
          { name: "currentPassword", errors: fields.currentPassword ?? [] },
          { name: "newPassword", errors: fields.newPassword ?? [] },
        ]);
      } else {
        setError(e instanceof ApiError && e.status === 429 ? TOO_MANY_REQUESTS : errorText(e));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
      <Form form={form} layout="vertical" onFinish={onFinish} requiredMark={false}>
        <Form.Item label="Mật khẩu hiện tại" name="currentPassword" rules={[{ required: true, message: "Nhập mật khẩu hiện tại." }]}>
          <Input.Password autoComplete="current-password" />
        </Form.Item>
        <Form.Item label="Mật khẩu mới" name="newPassword" rules={newPasswordRules} extra={passwordPolicyText}>
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        <Form.Item label="Nhập lại mật khẩu mới" name="confirmation" dependencies={["newPassword"]} rules={confirmationRules("newPassword")}>
          <Input.Password autoComplete="new-password" />
        </Form.Item>
        <Button type="primary" htmlType="submit" block loading={submitting}>Đổi mật khẩu</Button>
      </Form>
    </>
  );
}
