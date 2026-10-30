# Cafe CR Product Specification

## Current scope

The repository currently provides a .NET 10 API with Swagger/OpenAPI, an EF Core Code First schema for the 22 business entities in the Cafe CR project reference, and a baseline authentication and administrator API. The schema uses ASP.NET Core Identity with Guid keys and SQL Server mappings. Feature workflows and automatic migration-on-startup are not implemented.

## Source of truth

- Product requirements: this file
- Project overview and run instructions: [README.md](README.md)
- Architecture and coding rules: [.rule/](.rule)
- Task backlog: [.task/](.task)

## API baseline decisions

- Authentication uses JWT bearer access tokens and rotating refresh tokens.
- Customer self-registration is allowed and creates a customer profile. Email verification is deferred until an email-delivery service exists; the baseline therefore permits immediate sign-in and is not sufficient by itself for a public production deployment.
- The `Admin` role alone can use business-resource management endpoints. The `Customer` role is assigned on self-registration. No business role matrix or branch-scoped authorization is defined yet.
- Business-resource management uses explicit request/response DTOs. Identity framework tables and refresh-token session records are never exposed as generic CRUD resources. Password hashes, employee PIN hashes, and refresh-token hashes are never returned by API responses.
- The first administrator is provisioned from environment configuration. Secrets remain outside source control.
- EF migrations are applied deliberately with EF tooling; the API does not migrate the database automatically at startup.

## Requirements for future work

1. Define product behavior and acceptance criteria before adding feature workflows or changing authorization.
2. Preserve the existing project layering unless a task explicitly changes it.
3. Keep secrets and environment-specific values out of source control.
4. Add focused tests for behavior changes.
5. Verify changes with the smallest relevant build or test command and report the actual result.

The payment gateway feature has been removed. `PaymentMethod` is only a simulated tender type on `Bill`; this project does not define gateway integrations or webhooks.
