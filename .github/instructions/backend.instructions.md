---
applyTo: "backend/src/**/*.cs"
---

# Backend instructions

Read the product constraints in [../../spec.md](../../spec.md) before editing backend code.

## Required behavior

- Respect the existing layering and module boundaries.
- Reuse domain/application contracts before creating new abstractions.
- Keep database changes minimal and consistent with the current data model.

## Implementation checklist

1. Inspect the relevant service, endpoint, and tests before changing code.
2. Confirm the task scope and acceptance criteria in the active task file.
3. Make the smallest change that satisfies the task.
4. Add or update tests where the behavior changes materially.
5. Run the smallest relevant verification command, and report the actual result.

## Avoid

- Broad refactors unrelated to the active task.
- Inventing new business logic not described in the spec.
- Hard-coding secrets or environment-specific values.
- Mixing frontend and backend concerns in one change.
