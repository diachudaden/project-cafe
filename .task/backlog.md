# Task backlog

This backlog is intentionally small and requirement-driven. It stores the project’s active tasks and dependencies without creating speculative work beyond the current repo scope.

## Current backlog

| ID | Status | Title | Notes |
| --- | --- | --- | --- |
| TASK-001 | Completed | Establish GitHub Copilot workflow for Cafe CR | Creates the repository-level rules, prompts, and task structure.
| TASK-002 | Planned | Create the Cafe CR domain schema with EF Core Code First | Define and review schema decisions before implementation; generate but do not apply the initial migration. See [TASK-002](./TASK-002-code-first-domain-schema.md).

## Working rules

- Keep tasks linked to the canonical spec in [../spec.md](../spec.md).
- Prefer narrow tasks with explicit acceptance criteria and verification commands.
- Treat missing or conflicting requirements as blocked rather than guessed behavior.
