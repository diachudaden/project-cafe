# Task backlog

This backlog is intentionally small and requirement-driven. It stores the project’s active tasks and dependencies without creating speculative work beyond the current repo scope.

## Current backlog

| ID | Status | Title | Notes |
| --- | --- | --- | --- |
| TASK-001 | Completed | Establish GitHub Copilot workflow for Cafe CR | Creates the repository-level rules, prompts, and task structure.
| TASK-002 | Planned | Integrate payment module with the actual app domain and persistence layer | Depends on the spec and the repo’s real entity types.
| TASK-003 | Planned | Verify end-to-end payment behavior and reporting flows | Requires runtime validation with test data or a live environment.
| TASK-004 | Bị chặn | Cấu hình thông tin SQL local qua file `.env` được Git bỏ qua | Phụ thuộc TASK-002; gỡ chặn khi có API project, SQL provider và cấu hình runtime thực tế.

## Working rules

- Keep tasks linked to the canonical spec in [../spec.md](../spec.md) and [../docs/payments/SPEC.md](../docs/payments/SPEC.md).
- Prefer narrow tasks with explicit acceptance criteria and verification commands.
- Treat missing or conflicting requirements as blocked rather than guessed behavior.
