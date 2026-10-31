# Task backlog

This backlog tracks requirement-driven implementation tasks. Feature tasks include developer deliverables and tester-ready evidence: Vietnamese OpenAPI/Swagger documentation, reproducible requests, test data, expected outcomes, and verification results.

## Current backlog

| ID | Status | Title | Notes |
| --- | --- | --- | --- |
| TASK-001 | Completed | Establish GitHub Copilot workflow for Cafe CR | Creates the repository-level rules, prompts, and task structure.
| TASK-002 | Completed | Create the Cafe CR domain schema with EF Core Code First | Added 22 business entities, Identity-backed user schema, SQL Server mappings and initial migration; verified on disposable SQL Server 2025. See [TASK-002](./TASK-002-code-first-domain-schema.md).
| TASK-003 | Completed | Add authentication, authorization, and admin CRUD API | JWT/refresh-token baseline, customer registration, environment-provisioned Admin, and protected business-resource management. See [TASK-003](./TASK-003-api-auth-crud.md).
| TASK-004 | Completed | Define roles and branch-scoped authorization | Approved Admin/BranchManager/Staff/Customer roles, branch/self-scope policies, and Admin-only generic CRUD. See [TASK-004](./TASK-004-role-authorization.md).
| TASK-005 | Completed | Add repeatable development seed data | Opt-in Development/Testing seed command, local-only test accounts, and synthetic cafe records; depends on TASK-004. See [TASK-005](./TASK-005-development-seed.md).
| TASK-006 | Planned | Manage branches, pricing, and seat availability | Admin/Manager setup plus customer availability lookup; depends on TASK-004 and TASK-005. See [TASK-006](./TASK-006-reference-data-availability.md).
| TASK-007 | Planned | Implement the POS session lifecycle | Check-in, extend, checkout, and seat/session consistency; depends on TASK-004, TASK-005, and TASK-006. See [TASK-007](./TASK-007-pos-session-lifecycle.md).
| TASK-008 | Planned | Implement billing and customer points | Session bills, claiming, and points; depends on TASK-007. See [TASK-008](./TASK-008-billing-customer-points.md).
| TASK-009 | Planned | Implement workforce scheduling and attendance | Branch-scoped staff assignments, shifts, requests, and attendance; depends on TASK-004 and TASK-005. See [TASK-009](./TASK-009-workforce-scheduling-attendance.md).
| TASK-010 | Planned | Implement shift closing and payroll | Cash reconciliation, expenses, payroll, and approvals; depends on TASK-008 and TASK-009. See [TASK-010](./TASK-010-shift-closing-finance.md).
| TASK-011 | Completed | Admin web app on the existing API | React/Vite/Ant Design Admin app with real sign-in and CRUD for branches, zones, seats, price plans, and users; no backend changes. See [TASK-011](./TASK-011-admin-frontend.md).

## Working rules

- Keep tasks linked to the canonical spec in [../spec.md](../spec.md).
- Prefer narrow tasks with explicit acceptance criteria and verification commands.
- Treat missing or conflicting requirements as blocked rather than guessed behavior.
- Do not seed development data into production or apply migrations to an unintended database.
- Swagger-facing documentation for API tasks must be in Vietnamese for the Vietnamese frontend team and project demo. Keep routes, JSON property names, enum wire values, and operation IDs stable/English unless a task explicitly changes the API contract.
- Each implemented API task must leave Swagger documentation and reproducible tester requests aligned with actual behavior.
- Notifications are deferred until a user workflow defines when they are created and read.
