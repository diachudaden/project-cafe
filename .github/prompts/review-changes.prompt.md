---
mode: "agent"
description: "Review a patch against requirements, architecture, and acceptance criteria without modifying code automatically."
---

# Review the current change set

Review the diff and project state against the task requirements and specification.

Check for:

- Requirement coverage and scope drift.
- Logic regressions or missing validation.
- Security or secret issues.
- Missing tests or incomplete verification.
- Conflicts with the repo’s architecture or rules.

Report findings with file references and severity levels. Do not automatically change code in review-only mode.

Focus on evidence: compare behavior to the active task, the project rules, and the acceptance criteria.
