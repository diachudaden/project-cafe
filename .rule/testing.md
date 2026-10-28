# Testing rules

## Required approach

- Add or update tests for changed behavior.
- Prefer the smallest relevant test scope instead of broad suites.
- Use real behavior and realistic inputs; avoid purely mock-driven assertions.

## Verification requirements

- Run the smallest available check that proves the changed behavior.
- Report actual results, including skipped or unavailable checks.
- Do not claim a pass state without fresh command output.
