# Testing rules

## Required approach

- Add or update tests for changed behavior.
- Prefer the smallest relevant test scope instead of broad suites.
- Use real behavior and realistic inputs; avoid purely mock-driven assertions.

## Focus areas

- Payment matching and duplicate handling.
- Webhook validation and timestamp checks.
- Seat hold and release logic.
- Append-only ledger behavior and reversal logic.
- Manager attention and reconciliation flows.

## Verification requirements

- Run the smallest available check that proves the changed behavior.
- Report actual results, including skipped or unavailable checks.
- Do not claim a pass state without fresh command output.
