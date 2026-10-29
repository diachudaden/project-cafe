import type { ReactNode } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { z } from "zod";

const signInSchema = z.object({
  identifier: z.string().trim().min(1, "Nhập email hoặc số điện thoại."),
  password: z.string().min(1, "Nhập mật khẩu."),
});

export type SignInValues = z.infer<typeof signInSchema>;

export function SampleDataNotice() {
  return (
    <aside className="cr-sample-notice" role="note">
      Bản xem trước: số liệu nghiệp vụ là dữ liệu mẫu, không được lưu vào hệ thống.
    </aside>
  );
}

export function AppFrame({
  children,
  className = "",
}: {
  children: ReactNode;
  className?: string;
}) {
  return <div className={`cr-app-frame ${className}`.trim()}>{children}</div>;
}

export function PageContent({
  children,
  className = "",
}: {
  children: ReactNode;
  className?: string;
}) {
  return <main className={`cr-page-content ${className}`.trim()}>{children}</main>;
}

export function PageHeading({
  eyebrow,
  title,
  description,
  action,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <header className="cr-page-heading">
      <div>
        {eyebrow && <p className="cr-eyebrow">{eyebrow}</p>}
        <h1>{title}</h1>
        {description && <p className="cr-page-description">{description}</p>}
      </div>
      {action && <div className="cr-page-action">{action}</div>}
    </header>
  );
}

export function MetricCard({
  label,
  value,
  detail,
  tone = "default",
}: {
  label: string;
  value: string;
  detail?: string;
  tone?: "default" | "positive" | "warning";
}) {
  return (
    <article className={`cr-metric-card cr-metric-card--${tone}`}>
      <p>{label}</p>
      <strong>{value}</strong>
      {detail && <span>{detail}</span>}
    </article>
  );
}

export function StatePanel({
  state,
  title,
  description,
  onRetry,
}: {
  state: "loading" | "error" | "empty";
  title: string;
  description?: string;
  onRetry?: () => void;
}) {
  return (
    <section
      className={`cr-state-panel cr-state-panel--${state}`}
      role={state === "error" ? "alert" : "status"}
      aria-live="polite"
    >
      {state === "loading" && <span className="cr-spinner" aria-hidden="true" />}
      <h2>{title}</h2>
      {description && <p>{description}</p>}
      {state === "error" && onRetry && (
        <button className="cr-button cr-button--secondary" type="button" onClick={onRetry}>
          Thử lại
        </button>
      )}
    </section>
  );
}

export function Badge({
  children,
  tone = "neutral",
}: {
  children: ReactNode;
  tone?: "neutral" | "success" | "warning" | "danger";
}) {
  return <span className={`cr-badge cr-badge--${tone}`}>{children}</span>;
}

export function Button({
  children,
  className = "",
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button className={`cr-button ${className}`.trim()} {...props}>
      {children}
    </button>
  );
}

export function EmptyFeature({ title }: { title: string }) {
  return (
    <section className="cr-feature-placeholder">
      <span className="cr-feature-placeholder__icon" aria-hidden="true">
        ◷
      </span>
      <h2>{title}</h2>
      <p>Chưa có trong phiên bản này.</p>
    </section>
  );
}

export function FieldLabel({
  children,
  htmlFor,
}: {
  children: ReactNode;
  htmlFor: string;
}) {
  return (
    <label className="cr-field-label" htmlFor={htmlFor}>
      {children}
    </label>
  );
}

export function SignInForm({
  appName,
  error,
  fieldErrors = {},
  onSubmit,
}: {
  appName: string;
  error: string | null;
  fieldErrors?: Partial<Record<keyof SignInValues, string>>;
  onSubmit: (values: SignInValues) => Promise<void>;
}) {
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<SignInValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { identifier: "", password: "" },
  });
  const identifierError = errors.identifier?.message ?? fieldErrors.identifier;
  const passwordError = errors.password?.message ?? fieldErrors.password;

  return (
    <main className="cr-auth-page">
      <section className="cr-auth-card" aria-labelledby="cr-auth-title">
        <div className="cr-auth-brand" aria-hidden="true">CR</div>
        <p className="cr-eyebrow">Chốn Riêng · {appName}</p>
        <h1 id="cr-auth-title">Đăng nhập</h1>
        <p className="cr-auth-description">Dùng email hoặc số điện thoại đã được cấp.</p>
        <form
          className="cr-auth-form"
          noValidate
          onSubmit={handleSubmit(async (values) => {
            await onSubmit(values);
          })}
        >
          <div className="cr-auth-field">
            <label htmlFor="cr-auth-identifier">Email hoặc số điện thoại</label>
            <input
              autoComplete="username"
              id="cr-auth-identifier"
              {...register("identifier")}
              aria-invalid={Boolean(identifierError)}
              aria-describedby={identifierError ? "cr-auth-identifier-error" : undefined}
            />
            {identifierError && (
              <span className="cr-auth-field-error" id="cr-auth-identifier-error">
                {identifierError}
              </span>
            )}
          </div>
          <div className="cr-auth-field">
            <label htmlFor="cr-auth-password">Mật khẩu</label>
            <input
              autoComplete="current-password"
              id="cr-auth-password"
              type="password"
              {...register("password")}
              aria-invalid={Boolean(passwordError)}
              aria-describedby={passwordError ? "cr-auth-password-error" : undefined}
            />
            {passwordError && (
              <span className="cr-auth-field-error" id="cr-auth-password-error">
                {passwordError}
              </span>
            )}
          </div>
          {error && <p className="cr-auth-error" role="alert">{error}</p>}
          <button className="cr-button cr-auth-submit" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Đang đăng nhập..." : "Đăng nhập"}
          </button>
        </form>
      </section>
    </main>
  );
}

export function SignOutButton({
  onSignOut,
}: {
  onSignOut: () => void;
}) {
  return (
    <button className="cr-button cr-button--secondary cr-sign-out" type="button" onClick={onSignOut}>
      Đăng xuất
    </button>
  );
}
