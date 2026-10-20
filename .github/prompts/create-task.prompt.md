---
mode: "agent"
description: "Create a scoped, dependency-aware task file from a requirement or issue."
---

# Create a task specification

Create a new task in [.task/](../../.task) using the repository’s task pattern.

Requirements:

- Use a consistent ID format like TASK-001.
- Include title, status, goal, rationale, and relevant specification references.
- Reference the real product spec and relevant rule files.
- State dependencies, scope, exclusions, acceptance criteria, and verification requirements.
- Keep the scope narrow and realistic for the repository’s actual implementation state.
- Mark unresolved requirements as blocked or needing clarification instead of treating assumptions as confirmed requirements.

Do not create speculative backlog entries without evidence from the repo or the current requirement.
