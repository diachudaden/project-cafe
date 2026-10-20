# Architecture rules

## Repository layout

- Backend logic lives in the .NET module folders under [backend/src](../backend/src).
- Frontend POS logic lives under [frontend/apps/pos/src](../frontend/apps/pos/src) and the shared client under [frontend/packages/api-client/src](../frontend/packages/api-client/src).
- The project uses domain, application, infrastructure, API, and feature modules rather than a single monolith file.

## Required behavior

- Keep domain rules in the domain layer; application services orchestrate behaviors.
- Keep API endpoints thin and delegate to application services.
- Keep infrastructure code specific to persistence, external APIs, and integration concerns.
- Prefer existing interfaces and module entry points before creating new abstractions.

## Change constraints

- Do not bypass the payment workflow described in [../docs/payments/SPEC.md](../docs/payments/SPEC.md).
- Do not mix business rules into frontend presentation components unless the existing code already does so.
- Do not introduce a new framework or storage pattern without a clear need and team approval.
