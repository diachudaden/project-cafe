# TASK-028: Quên mật khẩu, đặt lại mật khẩu, đổi mật khẩu (gửi mail SMTP)

## Status

**Implemented** on branch `feat/password-reset` (from `development`, 6 commits, one per slice). **No migration.**
Decision D33 records the behaviour. New settings: `Email__*`, `App__PublicBaseUrl`, `PasswordReset__TokenMinutes`,
`DataProtection__KeysPath` (Compose sets `/keys` on the `dataprotection_keys` volume) and optional `AuthRateLimits__*`.

**Before deploying:** set `App__PublicBaseUrl` to the public origin (e.g. `https://15-134-162-105.sslip.io`) and the Gmail
`Email__*` values in the server `.env`; otherwise mail goes to the server's Mailpit and links point at `http://localhost`.

## Deviations from the prompt (approved before coding)

| Prompt | Implemented | Why |
| --- | --- | --- |
| Hangfire for sending | `EmailQueue` (`Channel`) + `EmailDispatchWorker` (`BackgroundService`), 3 attempts (after 5 s, 30 s) | Hangfire is not in the repo (D32); no new framework. Not durable: a mail queued during a restart is lost |
| Data Protection keys in volume or DB | File system at `DataProtection__KeysPath` on a named volume, fixed application name | No migration, no Infrastructure change |
| — | `UseForwardedHeaders` (X-Forwarded-For, trusted from loopback/private networks, no hop limit) | Caddy → nginx → API: without it every request has the nginx IP and per-IP limits would be global |
| Change password in an admin/POS page or menu | "Đổi mật khẩu" in a dropdown on the user name, opening a shared modal (also used by customer `MePage`) | One shared component, no extra route |
| Rate limit numbers "e.g. 5 / 15 min" | Configurable `AuthRateLimits__*`; login 30/5′ and register 10/60′ per IP | A branch's staff share one public IP on the POS |

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

## Implementation notes

- Backend: `Services/Email/` (`EmailOptions`/`AppOptions`/`PasswordResetOptions`, `SmtpEmailSender`, `EmailQueue` +
  `EmailDispatchWorker`, `PasswordEmailTemplates`, `EmailRegistration`), `Services/AuthRateLimits.cs`
  (`AuthRateLimitOptions`, policies, per-email `ForgotPasswordEmailLimiter`), `AuthService` (request/reset/change,
  `RevokeSessionsAsync`), `AuthEndpoints`/`AuthContracts`, `Program.cs` (token providers, Data Protection, forwarded headers).
- The 400 for a bad link is a ProblemDetails with `title` "Liên kết không hợp lệ hoặc đã hết hạn" and `code`
  `INVALID_RESET_LINK` (frontend: `isInvalidResetLink`).
- Identity's `ResetPasswordAsync`/`ChangePasswordAsync` rotate the security stamp themselves; that is what makes a used
  reset link invalid.
- A wrong current password on change uses `CheckPasswordAsync`, which does not touch `AccessFailedCount`.
- "Permanently locked" = `LockoutEnd` more than 100 years ahead (no such admin feature exists yet); such accounts get no mail.
- Identity's password validator is ASCII-only (e.g. `ậ` counts as a special character, not a lowercase letter);
  `passwordProblems` in `@cafecr/ui` uses the same classes.
- Frontend: `@cafecr/api-client` (`forgotPassword`, `resetPassword`, `changePassword` sending the stored refresh token),
  `@cafecr/ui` (`password.ts`, `PasswordForms.tsx`: `ForgotPasswordForm`, `ResetPasswordForm`, `ChangePasswordForm`,
  `ChangePasswordModal`; auth context exposes the three calls); thin `ForgotPasswordPage`/`ResetPasswordPage` in each app.

## Verification results (2026-11-16)

| Command | Result |
| --- | --- |
| `dotnet build CafeCR.slnx` | Build succeeded, 0 errors, 1 warning (existing `MsSqlBuilder()` obsolete warning in `SqlServerFixture.cs`) |
| `dotnet test CafeCR.slnx` | UnitTests 203/203 passed. IntegrationTests 41/41 passed on two consecutive runs; in one earlier full run the 5 `InventorySqlTests` failed in 1 ms (SQL Server container fixture start) and passed when rerun — not reproduced, see open issues |
| `npm test` (frontend) | api-client 14/14, ui 33/33, admin 14/14 passed |
| `npm run build` (frontend) | customer, admin, pos built |

New tests: `ChonRieng.IntegrationTests/PasswordResetTests.cs`, `ChangePasswordTests.cs`, `MailpitSmtpTests.cs` (real
SMTP to a Mailpit container), `PasswordApiFactory.cs` (`FakeEmailSender`, log capture, per-test client IP);
`backend/tests/ChonRieng.UnitTests/Api/EmailOptionsTests.cs`, `PasswordEmailTemplatesTests.cs`;
`frontend/packages/api-client/src/auth.test.ts`, `frontend/packages/ui/src/password.test.ts`.

Not run: a full `docker compose up` with a browser. The local `.env` points at a real database and server Compose
files, so the manual Mailpit walkthrough below is left to the developer. React components have no render tests (the
frontend has no DOM test setup); the form rules are covered through `passwordProblems`.

### Manual check with Mailpit

1. `.env` without Gmail values (or `Email__Host=mailpit`, `Email__Port=1025`, `Email__UseStartTls=false`),
   `App__PublicBaseUrl=http://localhost`; `docker compose up --build -d`.
2. `http://localhost/login` → "Quên mật khẩu?" → registered email → notice shown, button locked 60 s.
3. `http://localhost:8025` → open "Đặt lại mật khẩu" → button → `/reset-password?email=…&token=…`.
4. Set a new password → redirected to login; old password fails; Mailpit shows "Mật khẩu của bạn vừa được thay đổi".
5. Open the same link again → "Liên kết không hợp lệ hoặc đã hết hạn" + "Gửi lại liên kết".
6. Repeat from `/admin/login` and `/pos/login` (links go to `/admin/reset-password` and `/pos/reset-password`).
7. Signed in: customer "Tôi" → "Đổi mật khẩu"; admin/POS: click the user name → "Đổi mật khẩu". The current tab stays
   signed in; another browser signed in to the same account is signed out at its next refresh.
8. `docker compose restart api`, then use a link requested before the restart: it still works (persisted keys).

## Open issues

- One unreproduced failure of the SQL Server Testcontainers fixture during a full parallel test run (Mailpit and
  SQL Server containers start concurrently); worth watching in CI.
- Access tokens already issued stay valid up to 15 minutes after a reset/change (JWTs are not checked against the
  security stamp); only refresh is blocked immediately.
- The email queue is in memory; a durable outbox would be needed for guaranteed delivery.
- `compose.yaml` also starts Mailpit on the server; harmless (UI bound to 127.0.0.1) but can be moved to a dev profile.

## Verification

- `dotnet build backend/ChonRieng.sln` (or the repo solution) and `dotnet test` — including new HTTP-level tests
  (`WebApplicationFactory`, in-memory database, `FakeEmailSender`).
- `npm test` and `npm run build` in `frontend/` (api-client, ui, three apps).
- Manual: `docker compose up`, request a reset in each app, open Mailpit at `http://localhost:8025`, follow the link.
