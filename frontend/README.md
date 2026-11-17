# Cafe CR frontend

npm workspaces:

| Path | URL (nginx) | Users |
| --- | --- | --- |
| `apps/customer` | `/` | Customers (mobile-first): find a branch, claim bills, points, member code |
| `apps/admin` | `/admin/` | Admin (whole chain) and BranchManager (assigned branches): schedule, approvals, finance, reports, setup |
| `apps/pos` | `/pos/` | Staff (tablet): join shift, seat map and check-in, closing wizard, my schedule and payroll |
| `packages/api-client` | — | Typed client for every API route; auth session with single-flight token refresh |
| `packages/ui` | — | VND/time formatting (UTC+7), Vietnamese labels, shared auth provider |

## Run locally

1. Start the API on `http://localhost:63842` (see the root [README](../README.md)), with migrations applied and the demo seed loaded.
2. From this folder:

```bash
npm install
npm run dev:admin     # http://localhost:5173/admin/
npm run dev:pos       # http://localhost:5174/pos/
npm run dev:customer  # http://localhost:5175/
```

Each dev server proxies `/api` to the API (set `API_PROXY_TARGET`, e.g. `http://localhost:8080` for the Docker API). In Docker, `frontend/Dockerfile` builds all three apps into one nginx image (`frontend/nginx.conf`).

Demo accounts are listed in the root README (`admin01`, `manager01`, `staff01`, `customer01` `.demo@cafecr.invalid`, password from `DevelopmentSeed__Password`).

## Auth behaviour

- Login by email + password against `/api/auth/*`. Each app keeps its own session: access token in memory, refresh token in `sessionStorage`.
- On a 401 the client refreshes once and retries; concurrent refreshes share one request because the API rotates refresh tokens.
- Apps refuse accounts without the right role (Admin/BranchManager for `/admin`, Staff/BranchManager for `/pos`, Customer for `/`).

## Checks

```bash
npm run typecheck
npm test
npm run build
```
