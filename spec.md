# Cafe CR Product Specification

## Current scope

The repository provides a .NET 10 API with Swagger/OpenAPI, an EF Core Code First schema for the 22 Cafe CR business entities, authentication, Admin CRUD, and the role workflows for Admin, BranchManager, Staff and Customer (TASK-006…010): reference data and availability, POS sessions and bills, loyalty points, shifts and attendance, shift closing, expenses, payroll, reports and notifications. Three React apps consume it: Web Quản lý (`/admin`), POS (`/pos`) and Web Khách (`/`). Automatic migration-on-startup is not implemented. Business rules are in [.rule/business-rules.md](.rule/business-rules.md).

## Source of truth

- Product requirements: this file
- Project overview and run instructions: [README.md](README.md)
- Architecture and coding rules: [.rule/](.rule)
- Task backlog: [.task/](.task)

## API baseline decisions

- Authentication uses JWT bearer access tokens and rotating refresh tokens.
- Customer self-registration is allowed and creates a customer profile. Email verification is deferred until an email-delivery service exists; the baseline therefore permits immediate sign-in and is not sufficient by itself for a public production deployment.
- Supported roles are `Admin`, `BranchManager`, `Staff`, and `Customer`; a user may hold multiple roles. Existing generic CRUD remains Admin-only. BranchManager/Staff access is scoped to branch IDs in signed JWT `branch_id` claims populated from `EmployeeBranch`; Customer access to customer-owned resources is self-only. Feature-specific permissions remain to be defined in their tasks.
- Role and branch claims are snapshots: existing access tokens remain effective until their 15-minute expiry; refresh uses current roles and branch assignments.
- Business-resource management uses explicit request/response DTOs. Identity framework tables and refresh-token session records are never exposed as generic CRUD resources. Password hashes, employee PIN hashes, and refresh-token hashes are never returned by API responses.
- The first administrator is provisioned from environment configuration. Secrets remain outside source control.
- Development demo data is inserted only by the explicit seed command in Development/Testing; normal startup never seeds or resets business data.
- EF migrations are applied deliberately with EF tooling; the API does not migrate the database automatically at startup.

## Requirements for future work

1. Define product behavior and acceptance criteria before adding feature workflows or changing authorization.
2. Preserve the existing project layering unless a task explicitly changes it.
3. Keep secrets and environment-specific values out of source control.
4. Add focused tests for behavior changes.
5. Verify changes with the smallest relevant build or test command and report the actual result.

The payment gateway feature was removed from the baseline and is replaced by TASK-013: a real VietQR + SePay payment flow with webhook verification and realtime bill updates. `PaymentMethod` remains a simulated tender type on `Bill` for the baseline, while the gateway integration work is implemented in the approved payment task.
