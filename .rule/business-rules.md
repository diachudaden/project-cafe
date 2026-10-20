# Business rules

These rules summarize the project’s working constraints and should be treated as the project’s business baseline.

- The payment workflow must follow [../docs/payments/SPEC.md](../docs/payments/SPEC.md) as the canonical requirement set.
- Bills follow a pending-to-paid flow with seat hold and allocation behavior.
- Ledger entries are append-only; corrections use reversals, not updates.
- Duplicate SePay callbacks must be treated as idempotent and ignored after the first accepted transaction.
- Manual attention and review must be possible when a transaction is unmatched, underpaid, or duplicate.
- The POS and manager UI must react to payment events and seat allocation changes.
