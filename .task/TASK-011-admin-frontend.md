# TASK-011: Admin web app on the existing API

## Status

Completed (first slice).

## Goal

Provide an Admin web app that uses the real authentication and Admin CRUD API from TASK-003/TASK-004 without changing the backend.

## Scope

- `frontend/packages/api-client`: typed client for `/api/auth/*` and `/api/admin/{branches,zones,seats,price-plans,users}`.
- `frontend/apps/admin`: sign-in, Admin-only guard, list/create/edit/delete screens for the resources above. UI text in Vietnamese.

## Decisions

- Stack: React 18, Vite, TypeScript, Ant Design, TanStack Query, React Router.
- Access token in memory; refresh token in `sessionStorage`; single in-flight refresh because refresh tokens rotate.
- Dev server proxies `/api` to the API; no CORS change in the backend.

## Out of scope

- POS and customer apps (wait for TASK-006..008).
- Employees, employee-branch assignments, shifts, and other generic CRUD resources.
- HttpOnly refresh cookie (would need a backend change).

## Verification

- `npm run typecheck`, `npm test` (api-client auth session tests), `npm run build` in `frontend/`.
- Manual check against a local API: login, `/me`, branch list, validation error mapping, refresh rotation.
