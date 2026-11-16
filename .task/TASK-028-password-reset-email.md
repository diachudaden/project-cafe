# TASK-028: Quên mật khẩu, đặt lại mật khẩu, đổi mật khẩu (gửi mail SMTP)

## Status

**In progress** on branch `feat/password-reset` (from `development`).

## Goal

Users of all three apps (Web Khách `/`, Web Quản lý `/admin`, POS `/pos`) can recover a forgotten password by email and
change their password while signed in. The API gets its first email-delivery service (SMTP via MailKit; Mailpit in dev).

## Rationale

- Accounts sign in with email + password, but a forgotten password currently needs a database edit.
- `spec.md` defers email verification "until an email-delivery service exists"; this task adds that service (verification
  itself stays out of scope).
- `/login` and `/register` only have per-account lockout (5 failures → 15 min); there is no per-IP throttling.

## Spec references

- [spec.md](../spec.md) — API baseline decisions (JWT + rotating refresh tokens, email verification deferred).
- [.rule/security.md](../.rule/security.md) — secrets outside source control, minimal safe logging, Vietnamese Swagger.
- [.rule/architecture.md](../.rule/architecture.md) — thin endpoints, logic in services, no new framework without approval.
- [.rule/testing.md](../.rule/testing.md) — focused tests with real behaviour, report actual command output.
- Existing auth: `Endpoints/AuthEndpoints.cs`, `Endpoints/AuthContracts.cs`, `Services/AuthService.cs`, `UserSessions`.

## Dependencies

- TASK-003 (auth + refresh-token sessions), TASK-011/012 (the three frontends). No database migration.

## Scope

### Configuration and infrastructure

| Key | Prod example (Gmail) | Dev default (Mailpit) |
| --- | --- | --- |
| `Email__Host` | `smtp.gmail.com` | `mailpit` |
| `Email__Port` | `587` | `1025` |
| `Email__Username` | `xxx@gmail.com` | empty |
| `Email__Password` | 16-char App Password | empty |
| `Email__FromAddress` | `xxx@gmail.com` | `no-reply@chonrieng.local` |
| `Email__FromName` | `Chọn Riêng Cafe` | `Chọn Riêng Cafe` |
| `Email__UseStartTls` | `true` | `false` |
| `App__PublicBaseUrl` | `https://domain` (no trailing `/`) | `http://localhost` |
| `PasswordReset__TokenMinutes` | `15` | `15` |
| `DataProtection__KeysPath` | `/keys` (compose volume) | empty = framework default |

- `EmailOptions`, `AppOptions`, `PasswordResetOptions` bound with `ValidateOnStart` (Host, Port, FromAddress required;
  `PublicBaseUrl` absolute http/https).
- `compose.yaml`: env keys above, `mailpit` service (`axllent/mailpit`, UI on `127.0.0.1:8025` only, 1025 not
  published), named volume for Data Protection keys. `.env.example` with empty `Email__Username`/`Email__Password`.
- `IEmailSender` + `SmtpEmailSender` (MailKit; StartTls when `UseStartTls`, otherwise None; authenticate only with a
  username). Sending goes through an in-process queue (`Channel` + `BackgroundService`, 3 attempts with backoff) so the
  response is never blocked. Logs carry only user id + outcome — never token, link or password.
- Identity: `.AddDefaultTokenProviders()`, `TokenLifespan = PasswordReset__TokenMinutes`; Data Protection keys
  persisted to `DataProtection__KeysPath` with a fixed application name.
- `UseForwardedHeaders` (X-Forwarded-For from nginx, trusted from loopback/private networks only) so per-IP limits see
  the client address instead of the nginx container.

### API (`/api/auth`)

- `POST /forgot-password` `{ email, app: customer|admin|pos }` → always `202` with the same body. Existing user only:
  token (`GeneratePasswordResetTokenAsync`, `Base64UrlEncode`), link built server-side from `App__PublicBaseUrl` + a
  whitelisted path (`/reset-password`, `/admin/reset-password`, `/pos/reset-password`) + `?email=&token=` (URL-encoded),
  queued mail. Rate limit per IP and per email (5 / 15 min) → `429`.
- `POST /reset-password` `{ email, token, newPassword }` → `204`. Unknown email / bad / used / expired token → `400`
  "Liên kết không hợp lệ hoặc đã hết hạn"; weak password → ValidationProblem via `IdentityErrorFormatter`. On success:
  security stamp updated, failed count reset, lockout cleared, **every open `UserSessions` row revoked**, "password
  changed" mail queued. Rate limit per IP.
- `POST /change-password` (authenticated) `{ currentPassword, newPassword, refreshToken? }` → `204`. Wrong current
  password → `400` ValidationProblem on `currentPassword` (no lockout count, rate limited). On success: security stamp
  updated, all sessions revoked **except** the one matching `refreshToken`, notification mail queued.
- Per-IP rate limit added to `/login` and `/register`. Limits are configurable (`AuthRateLimits__*`) so tests can
  exercise them.
- DTOs in `AuthContracts.cs` with Vietnamese `[Description]`; logic in `IAuthService`/`AuthService` (or a small
  `PasswordService` beside it); Swagger lists 202/204/400/401/429.

### Mail content (`PasswordEmailTemplates`)

- Reset: greeting with `FullName`, "Đặt lại mật khẩu" button, plain-text link, validity `{TokenMinutes} phút`, "Nếu bạn
  không yêu cầu, hãy bỏ qua email này". HTML (inline style) + text.
- Changed: time in Vietnam time, "Nếu không phải bạn, hãy liên hệ quản trị viên ngay".

### Frontend

- `@cafecr/api-client`: `forgotPassword`, `resetPassword`, `changePassword` + types + tests.
- `@cafecr/ui`: shared `ForgotPasswordForm`, `ResetPasswordForm`, `ChangePasswordForm` and a pure
  `validateNewPassword` (≥ 12, upper, lower, digit, special) with tests.
- Each app: "Quên mật khẩu?" on login; public `/forgot-password` (same message always, button locked 60 s, 429 message)
  and `/reset-password` (reads `email`, `token`; success → `/login`; token error → "Gửi lại liên kết").
- Change password: customer `MePage`; admin and POS from the account area of their layout. Current session kept.

## Exclusions

- Email verification at registration.
- Admin resetting another user's password; admin lock/unlock accounts (separate task).
- SMS OTP (Twilio), Google sign-in.
- Changing the POS employee PIN.
- A durable mail outbox: a queued mail is lost if the API restarts before it is sent (the user can request again).

## Acceptance criteria

1. Forgot-password returns the same status and body for registered and unregistered emails; only a registered email
   produces a mail.
2. The reset link uses the path for `app` and the host from `App__PublicBaseUrl`; client-supplied URLs are not accepted.
3. Wrong, already-used and expired tokens all return 400 with the same message.
4. After a successful reset: the new password signs in, the old one does not, an old refresh token gets 401, lockout is
   cleared.
5. Change password: wrong current password → 400 on `currentPassword`; success keeps the current session refreshable and
   makes other sessions' refresh tokens 401.
6. Exceeding a rate limit returns 429.
7. No token, link or password appears in logs.
8. Reset links stay valid across an API container restart (persisted Data Protection keys).

## Verification

- `dotnet build backend/ChonRieng.sln` (or the repo solution) and `dotnet test` — including new HTTP-level tests
  (`WebApplicationFactory`, in-memory database, `FakeEmailSender`).
- `npm test` and `npm run build` in `frontend/` (api-client, ui, three apps).
- Manual: `docker compose up`, request a reset in each app, open Mailpit at `http://localhost:8025`, follow the link.
