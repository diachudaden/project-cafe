---
applyTo: "frontend/**/*.{ts,tsx}"
---

# Frontend instructions

Use the project requirements in [../../spec.md](../../spec.md) as the standard for user-visible behaviors.

## Required behavior

- Keep UI logic consistent with the existing feature structure.
- Reuse existing API client and event subscription patterns before creating new wrappers.
- Avoid duplicating state or business rules already modeled on the backend.

## Implementation checklist

1. Check the related feature files and API client contracts.
2. Confirm the active task and required acceptance criteria.
3. Change the minimal UI surface needed for the task.
4. Update or add tests when the frontend behavior is significant.
5. Verify with the smallest relevant lint/type/test command and document the result.

## Avoid

- Rewriting feature boundaries or introducing a separate app state pattern without evidence.
- Copying backend logic into the client in a way that bypasses server-side validation.
- Adding UI warnings or success messages that conflict with documented product behavior.
