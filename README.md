# Cafe CR API

This repository contains a minimal .NET 10 API scaffold and a Code First EF Core schema for the 22 business entities in the Cafe CR project reference. The removed VietQR/SePay payment gateway module is not part of this schema.

The schema uses ASP.NET Core Identity with Guid keys. Identity adds six supporting tables (`AspNetRoles`, `AspNetRoleClaims`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, and `AspNetUserTokens`) in addition to the 22 business tables. Its user table is named `Users`.

## Run locally

Requirements: .NET 10 SDK.

```powershell
dotnet run --project backend/src/ChonRieng.Api
```

Open `http://localhost:5000/swagger` or the URL printed by ASP.NET Core.

## Build with Docker Compose

Requirements: Docker Desktop with Docker Compose.

```powershell
docker compose build
docker compose up
```

The API is available at `http://localhost:8080`; OpenAPI UI is at `/swagger`.
If port 8080 is already in use, set a different host port before starting Compose:

```powershell
$env:API_PORT = "18080"
docker compose up
```

Then open `http://localhost:18080/swagger`.

The API is a scaffold and does not currently define business endpoints or connect to a database at runtime. Domain entities and persistence mappings exist, but business workflows and endpoints still need requirements and implementation.

## EF Core schema

The Infrastructure project owns `AppDbContext`, SQL Server mappings, and the initial migration. The API does not apply migrations automatically. For host-side EF tools, copy `.env.example` to `.env` if needed and put your SQL Server connection string in `ConnectionStrings__DefaultConnection`. Keep `.env` local; it is ignored by Git.

In Bash, load the local file into the environment before running EF tools:

```bash
set -a
source .env
set +a
dotnet tool restore
dotnet ef database update \
  --project backend/src/ChonRieng.Infrastructure \
  --startup-project backend/src/ChonRieng.Api
unset ConnectionStrings__DefaultConnection
```

Only run the update when the connection targets a disposable database you intend to initialize. Review the initial migration and generated SQL under `backend/src/ChonRieng.Infrastructure/Persistence/Migrations` before applying it anywhere persistent. Compose does not yet include a database service, and `docker compose up --build` does not apply migrations. Audit log, reporting views/aggregates, and business workflows remain out of scope.
