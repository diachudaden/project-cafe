---
mode: "agent"
description: "Implement one task while keeping within the project rules, task scope, and verification requirements."
---

# Implement the selected task

Before editing code:

1. Read the relevant product requirements in [../../spec.md](../../spec.md).
2. Read the active task file in [.task/](../../.task) and the relevant rule files in [.rule/](../../.rule).
3. Inspect the affected implementation and its tests.
4. Present a short plan for the task before making non-trivial edits.

Implementation rules:

- Keep the change limited to the selected task.
- Reuse the existing architecture and naming patterns.
- Do not introduce unrelated refactors or broad abstractions.
- Add or update focused tests for changed behavior.
- Preserve existing user changes and avoid destructive commands.

After the change:

- Run the smallest relevant verification command.
- Report the exact command and actual result.
- List modified files and any remaining open issues or blockers.
