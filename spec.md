# Cafe CR Product Specification

## Current scope

The repository currently provides a minimal .NET 10 API scaffold with Swagger/OpenAPI and an EF Core Code First schema for the 22 business entities in the Cafe CR project reference. The schema uses ASP.NET Core Identity with Guid keys and SQL Server mappings. Business workflows, authentication endpoints/JWT configuration, seed data, and automatic migration-on-startup have not been implemented.

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

The payment gateway feature has been removed. `PaymentMethod` is only a simulated tender type on `Bill`; this project does not define gateway integrations or webhooks.
