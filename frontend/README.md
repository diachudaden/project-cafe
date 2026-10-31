# Cafe CR frontend

npm workspaces:

| Path | Purpose |
| --- | --- |
| `apps/admin` | Admin web app (React + Vite + Ant Design). Sign-in and management of branches, zones, seats, price plans, and user accounts. |
| `packages/api-client` | Typed client for the .NET API: auth session, token refresh, admin CRUD resources. Reuse it for future POS/customer apps. |

## Run locally

1. Start the API on `http://localhost:63842` (see the root [README](../README.md)).
2. From this folder:

```bash
npm install
npm run dev:admin
```

Open `http://localhost:5173` and sign in with an account that has the `Admin` role (e.g. the bootstrap admin from `.env`).

The dev server proxies `/api` to the API, so no CORS setup is needed. Set `API_PROXY_TARGET` to use another API address.

## Auth behaviour

- Uses the existing `/api/auth/*` contract: login by email + password.
- Access token is kept in memory only; the refresh token is kept in `sessionStorage` so a reload keeps the session within the tab.
- On a 401 the client refreshes once and retries. Concurrent refreshes share one request because the API rotates (revokes) a refresh token on use.
- Non-Admin accounts are refused by the admin app.

## Checks

```bash
npm run typecheck
npm test
npm run build
```
