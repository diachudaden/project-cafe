---
applyTo: "frontend/**/*.{ts,tsx}"
---

# Frontend instructions

Use the project requirements in [../../spec.md](../../spec.md) and the payment rules in [../../docs/payments/SPEC.md](../../docs/payments/SPEC.md) as the standard for user-visible behaviors.

## Required behavior

- Keep UI logic consistent with the existing feature structure under the POS payment modules.
- Reuse existing API client and event subscription patterns before creating new wrappers.
- Avoid duplicating state or business rules already modeled on the backend.
- Treat payment alerts and realtime events as observable behavior that must remain consistent with the spec.

## Implementation checklist

1. Check the current payment feature files and related API client contracts.
2. Confirm the active task and required acceptance criteria.
3. Change the minimal UI surface needed for the task.
4. Update or add tests when the frontend behavior is significant.
5. Verify with the smallest relevant lint/type/test command and document the result.

## Avoid

- Rewriting feature boundaries or introducing a separate app state pattern without evidence.
- Copying backend logic into the client in a way that bypasses server-side validation.
- Adding UI warnings or success messages that conflict with the existing payment workflow.
