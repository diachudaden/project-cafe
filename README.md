# Cafe CR API

This repository contains a .NET 10 API, an EF Core Code First schema for the 22 Cafe CR business entities, and a baseline authentication and administrator API. The removed VietQR/SePay payment gateway module is not part of this project.

The schema uses ASP.NET Core Identity with Guid keys. Identity adds six supporting tables in addition to the 22 business tables; its user table is named `Users`.

## Configure local secrets

The API requires a SQL Server connection string, a JWT signing key, and initial administrator credentials. Keep these values in the ignored root `.env` file, never in source control. If `.env` does not exist, create it from the example; if it already exists, preserve its connection string and add any missing settings instead of overwriting it:

```bash
cp -n .env.example .env
```

Edit `.env` and set:

- `ConnectionStrings__DefaultConnection` to the intended SQL Server database.
- `Jwt__SigningKey` to a random secret of at least 32 UTF-8 bytes. Generate one with `openssl rand -base64 48`.
- `BootstrapAdmin__Email` and `BootstrapAdmin__Password` for the first administrator. Use a strong password (at least 12 characters with uppercase, lowercase, a digit, and a symbol).
- `DevelopmentSeed__Password` for the demo manager, staff, and customer accounts. This is used only by the explicit development seed command and should be a strong local-only password.

The API does not read `.env` by itself when run on the host. Load it in Bash from the repository root before starting the API or running EF commands:

```bash
set -a
source .env
set +a
```

## Database migrations

Migrations are not applied automatically at API startup. Apply them deliberately to the database configured above:

```bash
dotnet tool restore
dotnet ef database update \
  --project backend/src/ChonRieng.Infrastructure \
  --startup-project backend/src/ChonRieng.Api
```

### After every deploy that contains a migration

The deploy workflow (`docker compose up --build`) does **not** migrate the database. When the deployed commit adds a file under `backend/src/ChonRieng.Infrastructure/Persistence/Migrations`, run against the server database (after a backup):

```bash
dotnet ef database update --project backend/src/ChonRieng.Infrastructure --startup-project backend/src/ChonRieng.Api
```

Until then, features using the new tables fail (e.g. without `AddOnItems` the POS menu "Thêm đồ" and bill lookups return 500). The API logs an **Error** at startup listing pending migrations, and `GET /api/health` returns `Degraded` with their names (`Healthy` when up to date, `503 Unhealthy` when the database is unreachable).

Add-on items (đồ thêm) are business data, not migration data: create them in Web Quản lý → **Bảng giá** → "Đồ thêm" (chain-wide items Admin only), or use the development seed for demo items. The POS shows "Chi nhánh chưa có món nào đang bán." until at least one active item exists for the branch or chain-wide.

The latest migration, `RoleWorkflows`, adds the columns and indexes used by the role workflows (one active session per seat, bill/point timestamps, member codes, closing review, expense reversals, attendance approval). Review migration files under `backend/src/ChonRieng.Infrastructure/Persistence/Migrations` and confirm the target database before updating it. Do not apply migrations to shared or production databases without review and a backup.

### Seat auto-release (TASK-024)

The API frees seats whose session is overdue by more than a grace period and was not extended (`CloseReason = AutoReleased`). Settings (environment, `compose.yaml` passes them through with these defaults):

| Key | Default | Meaning |
| --- | --- | --- |
| `Sessions__AutoReleaseGraceMinutes` | `10` | Minutes past the paid end time before release; `0` disables it (0–240, validated at startup). |
| `Sessions__AutoReleaseIntervalSeconds` | `60` | How often the check runs (10–3600). |

Requires migration `SessionCloseReason`; `GET /api/health` lists it as pending until it is applied.

## Optional development seed

The seed command inserts demo data only when explicitly requested. It is allowed only in the `Development` or `Testing` environment; normal API startup never seeds demo records. It does not apply migrations, delete records, or reset the database. First apply the reviewed migrations to an isolated local database and configure the bootstrap Admin and `DevelopmentSeed__Password` in the ignored root `.env`.

From the repository root, load `.env` and run:

```bash
set -a
source .env
set +a
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project backend/src/ChonRieng.Api \
  -- --seed-development-data
```

For the API container configured by Compose, run `docker compose run --rm api --seed-development-data` after setting the same local `.env` values and confirming the configured database is the intended isolated Development database.

The command is idempotent and uses marked names/emails as stable lookup keys; it only inserts missing records and never changes non-seed data. Demo prices are synthetic (`45.000đ` per 4 hours, `40.000đ` extension; branch C `40.000đ`/`35.000đ`) and are not approved business pricing.

It creates five branches `[DEMO DATA] Chốn Riêng - Chi nhánh A–E` (C and D open 24/7, ~48 seats, a few in maintenance) and these accounts, all using `DevelopmentSeed__Password`:

| Accounts | Emails | Role and scope |
| --- | --- | --- |
| 3 admins | `admin01…03.demo@cafecr.invalid` | `Admin`, whole chain |
| 10 branch managers | `manager01…10.demo@cafecr.invalid` | `BranchManager`; 01–02 branch A, 03–04 B, 05–06 C, 07–08 D, 09–10 E; manager10 also A |
| 20 staff (POS) | `staff01…20.demo@cafecr.invalid` | `Staff`; 01–04 A, 05–08 B, 09–12 C, 13–16 D, 17–20 E; staff04 also B, staff08 also C |
| 20 customers | `customer01…20.demo@cafecr.invalid` | `Customer`; member codes `CRDEMO01…20` |
| TASK-005 accounts | `manager.demo`, `staff.demo`, `customer.demo` `@cafecr.invalid` | unchanged (branch A) |
| Bootstrap admin | Value of `BootstrapAdmin__Email` | `Admin`, configured separately |

On the first run (when the demo branches have no shifts) it also writes consistent history from the first day of last month to 7 days ahead: published shifts with accepted assignments, attendance (some late, a few missed), sessions and bills, member claims and points, shift closings (some with differences, recent ones awaiting review), expenses with one reversal, last month's payroll (finalized for A–C, draft for D–E), availability, pending leave/swap requests and notifications. Currently running shifts start with the assigned staff checked in and a few active sessions (one ending soon, one overdue). Later runs only add missing upcoming shifts, so the POS demo keeps working on later days. The TASK-005 `1.000đ` demo plans are closed so the chain plan applies. To start over, point the command at a newly created disposable database; there is no destructive reset option.

## Run the API locally

From the repository root, after configuring `.env` and applying migrations:

```bash
dotnet run --project backend/src/ChonRieng.Api
```

Open the Swagger UI at the URL printed by ASP.NET Core plus `/swagger` (the current HTTP launch URL is `http://localhost:63842/swagger`). Swagger supports the JWT bearer **Authorize** control.

## Authentication and authorization

- `POST /api/auth/register` creates a customer account and returns access and refresh tokens.
- `POST /api/auth/login` authenticates an account.
- `POST /api/auth/refresh` rotates a valid refresh token.
- `POST /api/auth/logout` revokes the submitted refresh token.
- `GET /api/auth/me` returns the authenticated account's safe profile.
- Access tokens last 15 minutes by default; refresh tokens last 7 days by default.
- Email verification is not implemented because no email-delivery service exists. Immediate sign-in is allowed for this prototype; add verification before public production use.
- Supported roles are `Admin`, `BranchManager`, `Staff`, and `Customer`; an account may have multiple roles. Only `Admin` can access the current generic `/api/admin/*` CRUD.
- Branch-scoped feature endpoints must use the `BranchScope` policy and filter queries with the branch IDs in the signed access token. Those IDs come from `EmployeeBranch`; `IsHome` does not change authorization. Customer-owned endpoints must use the `CustomerOwnResource` policy with route `userId` or `customerId`.
- Role and branch scope claims are snapshots. A role/branch change does not update an already-issued access token; it remains effective until its 15-minute expiry. Refreshing re-reads current roles and branch assignments.
- Assigning `BranchManager` or `Staff` does not grant generic CRUD or automatically create an employee profile/branch assignment. An Admin must provision the employee and its `EmployeeBranch` rows separately.
- The first Admin account is provisioned from environment settings on API startup; existing accounts are not reset if those settings change.
- Admin routes provide paginated CRUD for business resources and safe user management. Lists default to 50 records and accept `page` and `pageSize` (maximum 100). Identity tables and refresh-token sessions are not exposed as generic CRUD resources. Password, employee PIN, and refresh-token hashes are never returned.
- This is a technical CRUD baseline, not feature workflows. Admin CRUD can change transactional records directly; add feature-specific validation and authorization before opening those operations to other roles.

## Docker Compose

Compose runs two services (`api` and `web`); it does not provision a SQL Server container. Settings come from the root `.env`; the connection string must identify a SQL Server reachable from inside the API container. Configure and apply migrations before starting:

```bash
docker compose up --build -d
```

| Service | Address | Notes |
| --- | --- | --- |
| `web` (nginx) | `http://localhost/` | Web Khách (customers, mobile-first). |
| | `http://localhost/admin/` | Web Quản lý (Admin, BranchManager). |
| | `http://localhost/pos/` | POS Nhân viên (Staff; tablet). |
| | `http://localhost/api/*` | Proxied to the API container, so the browser needs no CORS. |
| `api` | `http://127.0.0.1:8080` | Bound to localhost only; Swagger UI at `/swagger`. |

Optional `.env` settings: `WEB_PORT` (default `80`), `API_PORT` (default `8080`), `ASPNETCORE_ENVIRONMENT` (default `Development`; use `Production` on a server to reduce logging).

The project does not migrate the database automatically during `docker compose up`. Useful commands: `docker compose logs -f api`, `docker compose down`.



## Admin Account
BootstrapAdmin__Email=`admin@example.test`
BootstrapAdmin__Password=`Local-Only-Admin-2026!`
BootstrapAdmin__FullName=`Local Administrator`
