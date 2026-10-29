# Security rules

Canonical auth and session rules are in [../spec.md](../spec.md). Do not invent token formats, identity providers, or session storage.

## Secret handling

- Never commit secrets, keys, or environment-specific values.
- Keep secrets in local user secrets or deployment environment variables.
- Do not add a webhook secret, JWT signing key, test password, or other credentials to the repository.

## Authentication (approved)

- ASP.NET Core Identity + JWT access token (memory on the client) + HttpOnly refresh cookie.
- CORS allow-list with credentials; never `*` with credentials.
- Login failures must not reveal whether the identifier exists.
- Do not store access tokens or passwords in `localStorage`.
- Simulated OTP, hard-coded credentials in the frontend, and mock “successful login” are forbidden.

## Logging

- Avoid logging full sensitive payloads at info level.
- Do not log passwords, tokens, or refresh cookies.
- Keep logs minimal and safe for production monitoring systems.
