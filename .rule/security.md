# Security rules

## Secret handling

- Never commit secrets, keys, or environment-specific values.
- Keep secrets in local user secrets or deployment environment variables.
- Do not add a webhook secret or other credentials to the repository.

## Payment integrity

- Validate SePay signatures using the raw request body and HMAC before parsing JSON.
- Compare timestamps within the configured tolerance window.
- Reject requests with invalid signatures before processing any state changes.

## Logging

- Avoid logging full sensitive payloads at info level.
- Keep logs minimal and safe for production monitoring systems.
