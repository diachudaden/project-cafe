# Cafe CR Product Specification

## Current scope

The repository currently provides a minimal .NET 10 API scaffold with Swagger/OpenAPI. Business domain persistence and production business endpoints are not implemented yet.

This file is the canonical product specification. Product decisions for the first frontend slice and real authentication are recorded here from [docs/payments/reference/project.md](docs/payments/reference/project.md). Later tasks must follow this file and must not invent additional account, session, role, or screen behavior.

## Source of truth

- Product requirements: this file
- Product design reference (full vision, not all in the first slice): [docs/payments/reference/project.md](docs/payments/reference/project.md)
- Project overview and run instructions: [README.md](README.md)
- Architecture and coding rules: [.rule/](.rule)
- Task backlog: [.task/](.task)

## Requirements for future work

1. Define product behavior and acceptance criteria before implementation.
2. Preserve the existing project layering unless a task explicitly changes it.
3. Keep secrets and environment-specific values out of source control.
4. Add focused tests for behavior changes.
5. Verify changes with the smallest relevant build or test command and report the actual result.
6. Treat items in [Open blockers](#open-blockers) as blocked. Do not assume an answer in order to continue.

## First frontend slice

Three separate frontend applications share one backend account system:

| App | Path | Audience | First screen after sign-in |
| --- | --- | --- | --- |
| `admin` | `frontend/apps/admin` | Chain owner (`Admin`) and branch manager (`BranchManager`) | Operations dashboard |
| `pos` | `frontend/apps/pos` | Staff (`Staff`) on a counter tablet | Live seat map |
| `customer` | `frontend/apps/customer` | Member (`Customer`), mobile-first | Nearby branches with availability |

### Mock-data boundary

- Sign-in, current user, session refresh, and sign-out call the real backend. Frontend must not fake a successful login, hard-code passwords, or treat mock users as authenticated.
- Every business screen in this slice reads **in-app sample data** behind a feature data/service boundary. Sample data is not stored as a system of record, is not written to the API, and must be named so it is obviously mock (module path or export name).
- Presentation components must not import mock arrays directly.
- Mock business data must not be shown as saved backend data. Each app shows a persistent non-production notice that business figures are sample-only.
- Replacing a mock data/service with a real API client later must not require rewriting presentational components.

### `admin` — initial screens and flow

1. Sign-in (real auth).
2. App shell with a left nav: Hoạt động, Lịch ca, Chi nhánh, Thu chi, Báo cáo. Only **Hoạt động** and **Chi nhánh** are in this slice; the other items are visible but route to a “Chưa có trong phiên bản này” empty state.
3. Hoạt động: dashboard with today’s revenue, occupancy, and alerts from mock data. A top branch switcher (or “Tất cả”) filters the mock dashboard. `Admin` may choose any sample branch or all branches; `BranchManager` is limited to assigned `branchIds`.
4. Chi nhánh: read-only list of sample branches (name, address, hours, active flag).
5. Sign-out.

### `pos` — initial screens and flow

1. Sign-in (real auth, password identifier in this slice).
2. App shell with top context: current sample shift label and cash-on-hand (mock).
3. Seat map: sample seats colored empty / occupied / expiring / maintenance. Tapping an empty seat opens a check-in sheet (seat, sample price, cash or VietQR choice). Confirming check-in only updates local mock state; it does not persist.
4. Sign-out.

### `customer` — initial screens and flow

1. Sign-in (real auth, password identifier in this slice).
2. Bottom nav: Tìm quán, Quét bill, Điểm, Tôi.
3. Tìm quán: sample branch list with distance-like ordering and open-seat counts (mock).
4. Quét bill and Điểm: screens that render mock empty or sample-loyalty states only.
5. Tôi: current user from the real `/me` response (name, phone/email, role) plus sign-out. Loyalty totals on this screen remain mock and labeled as sample.

### Out of this slice (do not implement as if approved)

Full shift scheduling, attendance, shift close, payroll, expenses, reports export, SignalR, maps providers, payment-gateway capture, physical printers, native apps, POS PIN unlock, customer SMS OTP, and self-registration. They remain in the design reference until a later task records acceptance criteria.

## Authentication (approved for TASK-003 and TASK-006)

### Account model

- One shared ASP.NET Core Identity user store for all three apps (`Users` plus role claims). A person has one account, not one account per app.
- Four roles: `Admin`, `BranchManager`, `Staff`, `Customer`.
- Claims: `role` and `branchIds` (JSON array of branch GUIDs). APIs that are added later must authorize on both role and branch. This slice only needs those claims on the current-user payload and on app-access checks at login.
- App access:
  - `admin` accepts `Admin` and `BranchManager` only.
  - `pos` accepts `Staff` only.
  - `customer` accepts `Customer` only.
  - Valid password for the wrong app is **not** a successful login. Return `403` with `auth.forbidden_client`.
- Identifier for this slice: **email or Vietnamese phone number + password**. The login field is a single `identifier` that the API treats as email if it contains `@`, otherwise as phone.
- Phone values are stored normalized as digits with a leading `0` for VN local numbers (example `0901234567`). Login accepts `0901234567` or `+84901234567` and must normalize before lookup.
- Self-registration, POS PIN login, and customer OTP are **not** in this slice (see [Open blockers](#open-blockers)).
- Local development uses **seeded test users** created by backend seed/user-secrets. Test passwords must not be committed in frontend source. Document how to obtain them in environment setup, not in this repository as secrets.

### Provider and session

- Provider: **ASP.NET Core Identity** with **JWT access tokens** and **server-side refresh sessions** (`UserSessions`: user, client app, optional branch, refresh token hash, created/revoked timestamps).
- Access token: short-lived JWT in the JSON body. Frontend keeps it in memory only and sends `Authorization: Bearer`.
- Refresh token: opaque, stored only as a hash on the server, delivered in an **HttpOnly** cookie `cr_refresh` (`Path=/api/v1/auth`, `HttpOnly`, `Secure` in non-development, `SameSite=Lax` when the apps are same-site through the gateway, `SameSite=None; Secure` when the SPA origin differs from the API). JavaScript must not read this cookie.
- Access token lifetime: **15 minutes**. Refresh token lifetime: **7 days**. Refresh rotates the refresh token (previous token is revoked).
- CORS: allow only configured frontend origins, with credentials. Never use `*` together with credentials.
- Page reload: frontend calls `POST /api/v1/auth/refresh` with credentials; a valid cookie issues a new access token without asking for the password again.
- Sign-out: `POST /api/v1/auth/logout` revokes the current refresh session and clears the cookie. Frontend then drops the in-memory access token and routes to sign-in.
- One active POS refresh session per staff user: a new successful `pos` login revokes any existing unrevoked POS session for that user. Admin and customer clients are not limited to one session in this slice.

### Endpoints

Base path: `/api/v1`. Errors use RFC 9457 `ProblemDetails` with `type` values listed below. Do not reveal whether an identifier exists on credential failure.

#### `POST /api/v1/auth/login`

Request:

```json
{
  "identifier": "string",
  "password": "string",
  "client": "admin" | "pos" | "customer"
}
```

Success `200`:

```json
{
  "accessToken": "string",
  "expiresIn": 900,
  "user": {
    "id": "guid",
    "email": "string | null",
    "phone": "string | null",
    "fullName": "string",
    "role": "Admin | BranchManager | Staff | Customer",
    "branchIds": ["guid"]
  }
}
```

Sets `cr_refresh` cookie.

#### `POST /api/v1/auth/refresh`

No JSON body. Sends the refresh cookie. Success `200` returns the same shape as login (new `accessToken`, `expiresIn`, `user`) and rotates `cr_refresh`.

#### `POST /api/v1/auth/logout`

Sends the refresh cookie. Success `204`. Idempotent if the cookie is missing or already revoked. Clears `cr_refresh`.

#### `GET /api/v1/auth/me`

Requires `Authorization: Bearer`. Success `200` returns the `user` object from login.

Do not add register, PIN login, or session-list endpoints in TASK-003 unless a later approved change removes the matching blocker.

### Auth errors and frontend handling

| Case | HTTP | `ProblemDetails.type` (suffix) | User-visible copy | Frontend |
| --- | --- | --- | --- | --- |
| Missing/invalid body | 400 | `validation` | “Kiểm tra lại thông tin đăng nhập.” | Stay on form; show field errors from `errors` if present |
| Wrong identifier or password | 401 | `auth.invalid_credentials` | “Email/số điện thoại hoặc mật khẩu không đúng.” | Stay on form; do not disclose which field failed |
| Identity lockout after failed attempts | 401 | `auth.account_locked` | “Tài khoản đang bị khóa tạm thời. Thử lại sau.” | Stay on form |
| Valid credentials, wrong app | 403 | `auth.forbidden_client` | “Tài khoản này không dùng được trên ứng dụng này.” | Stay on form |
| Missing/expired/invalid access token | 401 | `auth.invalid_token` | None on background calls | Try refresh once; if refresh fails, route to sign-in |
| Refresh missing, expired, or revoked | 401 | `auth.session_revoked` | “Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.” | Clear memory, route to sign-in |
| API unreachable / non-auth 5xx | — | — | “Không kết nối được máy chủ. Thử lại sau.” | Never treat as signed in |
| Unauthenticated visit to a protected route | — | — | — | Redirect to sign-in; after success, go to the app home route |

Login form (shared presentation, app-specific `client` value): React Hook Form + Zod for `identifier` (non-empty) and `password` (non-empty). Show submitting state. Disable submit while pending. Do not navigate on error.

Post-login route: each app’s `/` home listed in the table above. There is no cross-app redirect.

### Shared sign-in UI

- Presentation lives in `frontend/packages/ui` and is reused by all three apps.
- Each app supplies `client`, API base URL, and post-login navigation.
- Auth API calls stay in an app/auth service layer, isolated from mock business data modules.

## Acceptance criteria (this specification slice)

- `admin`, `pos`, and `customer` have the initial screens and flows listed above.
- Business widgets use isolated mock data; authentication uses the real API only.
- TASK-003 can implement login, refresh, logout, and me exactly as specified, including errors and CORS/credentials.
- TASK-005 can implement only the listed screens, with loading / empty / error states on mock reads, without inventing extra business rules.
- Open blockers below are not silently filled in by implementers.

## Open blockers

These are not approved. Implementation tasks must stop or skip them rather than guess.

1. **Customer SMS/OTP login** — The design reference mentions phone + simulated OTP. Simulated OTP is not real authentication. No SMS provider is chosen. Out of this slice; customers sign in with identifier + password until a real OTP provider is approved.
2. **POS PIN login and idle lock** — The design reference wants name + 6-digit PIN and a 5-minute idle lock. PIN hashing, PIN reset, and lock UX are not specified for implementation. Out of this slice; POS uses identifier + password. One POS session per staff user still applies to password login.
3. **Self-registration** — No approved public register contract, password policy copy, or verification flow. Accounts for local/demo use come from backend seed only.
4. **Production Identity store and JWT signing keys** — Provider type is approved (Identity + JWT). Connection string, signing key, and cookie domain per environment stay in user secrets / deployment config, never in source control.
5. **Token lifetime changes, SSO, external identity providers (Google, etc.)** — Not approved.
6. **Business APIs, prices, and persistence** — Sample figures on mock screens are not official prices or a data model. Do not treat dashboard numbers, seat maps, or points as backend truth.

## Mapping to follow-up tasks

- TASK-003: implement the four auth endpoints, Identity + JWT + refresh cookie, role/client checks, ProblemDetails, CORS with credentials, seed test users, focused tests for success, invalid credentials, forbidden client, refresh, and logout.
- TASK-005: implement the screens in [First frontend slice](#first-frontend-slice) with shared UI and mock business data only.
- TASK-006: wire the shared sign-in form to the real auth service per app `client`; session restore via refresh; sign-out; documented error handling.
