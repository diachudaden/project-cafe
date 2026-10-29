# Cafe CR API

.NET 10 API for Cafe CR / Chốn Riêng. This slice provides **real authentication** for the future `admin`, `pos`, and `customer` apps. Business data endpoints are not implemented yet.

Product behavior: [spec.md](spec.md).

## Run locally

Requirements: .NET 10 SDK.

```powershell
dotnet run --project backend/src/ChonRieng.Api
```

Open `http://localhost:5000/swagger` or the URL printed by ASP.NET Core (`launchSettings.json` may use other ports).

Development uses SQLite (`chonrieng.db`) and seeds local users when `AuthSeed:Password` is configured. Keep the JWT signing key and seed password in .NET user secrets, environment variables, or a deployment secret store; never commit them.

Configure local development secrets once:

```powershell
dotnet user-secrets set "Jwt:SigningKey" "<unique random value of at least 32 characters>" --project backend/src/ChonRieng.Api
dotnet user-secrets set "AuthSeed:Password" "<strong local development password>" --project backend/src/ChonRieng.Api
```

Seeded Development users (same password from `AuthSeed:Password`):

| Email | Role | App (`client`) |
| --- | --- | --- |
| `admin@chonrieng.local` | Admin | `admin` |
| `manager@chonrieng.local` | BranchManager | `admin` |
| `staff@chonrieng.local` (phone `0901234567`) | Staff | `pos` |
| `customer@chonrieng.local` | Customer | `customer` |

Auth endpoints: `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`, `GET /api/v1/auth/me`.

## Tests

```powershell
dotnet test backend/tests/ChonRieng.UnitTests/ChonRieng.UnitTests.csproj
dotnet test ChonRieng.IntegrationTests/ChonRieng.IntegrationTests.csproj
```

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
