# Cafe CR API

This repository currently contains a minimal .NET 10 API scaffold. The Payments feature, its persistence model and migration, POS UI, tests, and supporting documentation have been removed.

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

The API is a scaffold and does not currently define business endpoints or connect to a database. Define product requirements before adding domain behavior or persistence.
