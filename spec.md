# Cafe CR Product Specification

## Current scope

The repository currently provides a minimal .NET 10 API scaffold with Swagger/OpenAPI. No business domain, database model, authentication provider, or production endpoints have been defined.

## Source of truth

- Product requirements: this file
- Project overview and run instructions: [README.md](README.md)
- Architecture and coding rules: [.rule/](.rule)
- Task backlog: [.task/](.task)

## Requirements for future work

1. Define product behavior and acceptance criteria before implementation.
2. Preserve the existing project layering unless a task explicitly changes it.
3. Keep secrets and environment-specific values out of source control.
4. Add focused tests for behavior changes.
5. Verify changes with the smallest relevant build or test command and report the actual result.
