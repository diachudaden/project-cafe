# GitHub Copilot instructions for Cafe CR

## Mission

Use the project specification and task backlog as the source of truth for implementation decisions. Preserve the existing architecture and only change the scope requested by the active task.

## Required reading order

Before making changes, read:

1. [../spec.md](../spec.md)
2. [../docs/payments/SPEC.md](../docs/payments/SPEC.md)
3. The relevant task file in [.task/](../.task)
4. The relevant rule file in [.rule/](../.rule)
5. The affected implementation files and existing tests

## Working rules

- Follow the repository’s existing layer boundaries: domain, application, infrastructure, API, and frontend feature modules.
- Reuse or extend existing abstractions before introducing new ones.
- Treat undocumented business behavior as a blocker, not an assumption.
- Keep changes minimal and reviewable; avoid unrelated refactors or dependencies.
- Add or update focused tests when behavior changes.
- Run the smallest relevant verification command available and report the actual result.
- Never claim completion without fresh evidence from a command or test run.
- Keep secrets out of the repository. Do not commit webhook secrets or environment-specific values.

## Repository conventions

- Payment and business rules live in [../docs/payments/SPEC.md](../docs/payments/SPEC.md).
- Cursor and Claude-specific project rules are already present in the repo; keep their intent aligned with this workflow.
- The task and rule directories are intentionally explicit, because Copilot does not automatically load arbitrary custom files by convention.
- Path-specific instructions in [.github/instructions/](instructions) should be used for backend, frontend, and tests.

## Task-driven workflow

- Use a task file for planned work and acceptance criteria.
- Keep backlog entries small and dependency-aware.
- Record implementation notes, blockers, and verification evidence in the task file when the work is completed or partially completed.
- Prefer the project’s existing patterns over introducing a new framework or app layout.
