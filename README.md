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

The current migration adds refresh-token expiry to `UserSessions`. Review migration files under `backend/src/ChonRieng.Infrastructure/Persistence/Migrations` and confirm the target database before updating it. Do not apply migrations to shared or production databases without review and a backup.

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

Compose currently runs only the API; it does not provision a SQL Server container. It passes the database and authentication settings from the root `.env` into the API container. The connection string must identify a SQL Server reachable from inside that container. Configure and apply migrations before starting:

```bash
docker compose up --build
```

The API is available at `http://localhost:8080`; Swagger UI is at `/swagger`. If port 8080 is already in use, set `API_PORT` in `.env`, for example `API_PORT=18080`.

The project does not migrate the database automatically during `docker compose up`.
