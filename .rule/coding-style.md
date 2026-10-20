# Coding style rules

## General

- Keep solutions small, explicit, and consistent with the current codebase.
- Favor readability over cleverness.
- Prefer existing naming patterns and repository conventions over newly invented ones.

## Backend

- Keep methods small and focused.
- Avoid broad refactoring when the task is narrow.
- Use the current domain and application patterns for commands, contracts, and service behavior.

## Frontend

- Keep feature state and API calls aligned to the existing POS feature pattern.
- Reuse the current query and event helpers and do not create unrelated state wrappers.

## Documentation

- Update the task or rule files that are directly relevant to the change.
- Keep documentation brief and tied to actual repository behavior.
