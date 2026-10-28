---
applyTo: "backend/tests/**/*.cs"
---

# Testing instructions

Read the relevant task and product requirements before changing tests or production logic.

## Rules

- Prefer focused tests for the exact behavior under change.
- Validate actual behavior, not mock-only expectations.
- Add Add regression tests for edge cases specific to the changed behavior.
- Keep fixtures realistic and aligned with the existing test generic patterns.

## Required workflow

1. Review the active task and the relevant specification section.
2. Update or add tests for the changed behavior.
3. Run the smallest relevant verification command.
4. Report actual outcomes, including blockers or skipped checks.

## Avoid

- Testing only that a mock was called.
- Over-specifying implementation details that are not required by the behavior.
- Claiming a test passes when it was not actually executed.
