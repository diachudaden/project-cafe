---
mode: "agent"
description: "Analyze the repo, requirements, and task dependencies before implementing a feature or workflow change."
---

# Plan the next implementation task

Review the current repository state and the relevant requirements before proposing work.

Required steps:

1. Read [../../spec.md](../../spec.md) and the relevant task file in [.task/](../../.task).
2. Inspect the affected modules, tests, and interfaces before proposing any design change.
3. Identify module boundaries, risks, and unclear requirements.
4. Break the work into small, independently verifiable tasks.
5. List dependencies, acceptance criteria, and verification commands.
6. Ask for clarification when a required behavior is ambiguous or missing.

Output format:

- Summary of the requirement and current implementation state.
- Proposed tasks with dependencies and ordering.
- Risks, assumptions, and open questions.
- Minimal verification plan with actual commands to run.
