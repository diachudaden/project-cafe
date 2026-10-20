# Cafe CR Product Specification

This repository implements the payment and seat-management workflow for Cafe CR. The project is primarily a .NET backend plus a POS frontend, with the payment feature documented in detail in [docs/payments/SPEC.md](docs/payments/SPEC.md).

## Source of truth

- Primary product and business rules: [docs/payments/SPEC.md](docs/payments/SPEC.md)
- Project overview: [README.md](README.md)
- AI integration prompt: [docs/payments/PROMPT.md](docs/payments/PROMPT.md)
- Project rules: [.rule/](.rule)
- Task backlog: [.task/](.task)

## Product scope

- Generate VietQR codes for pending bills.
- Hold seats during QR validity windows and release expired holds.
- Accept SePay webhook callbacks with HMAC verification and duplicate protection.
- Mark bills as paid, allocate seats, and create append-only ledger entries.
- Route attention-worthy transactions to POS managers.
- Expose payment events through SignalR for the POS UI.

## Explicit non-goals

- MoMo, ZaloPay, or other payment gateways are not part of the current implementation.
- The repo does not assume an undefined billing model; changes must match the specification and existing code.
- New abstractions are allowed only when the existing architecture clearly requires them.

## Required behavior

1. Read the canonical spec before implementing any payment behavior.
2. Preserve the repository’s existing layering: domain concerns, application services, infrastructure adapters, and API endpoints.
3. Keep ledger activity append-only and use reversals instead of destructive updates.
4. Treat SePay webhook verification as raw-body HMAC validation before JSON parsing.
5. Add or update relevant tests when changing behavior.
6. Report real verification evidence instead of claiming checks pass without execution.

## Acceptance expectations

- Requirements are implemented only in the requested task scope.
- Existing architecture and security conventions remain intact.
- Changes are consistent with the payment rules captured in [docs/payments/SPEC.md](docs/payments/SPEC.md).
- Unclear requirements are treated as blocked or needing clarification rather than guessed.
