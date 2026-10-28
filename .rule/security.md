# Security rules

## Secret handling

- Never commit secrets, keys, or environment-specific values.
- Keep secrets in local user secrets or deployment environment variables.
- Do not add a webhook secret or other credentials to the repository.

## Logging

- Avoid logging full sensitive payloads at info level.
- Keep logs minimal and safe for production monitoring systems.
