# ADR-0004: Concurrency control and row locking

- Status: Accepted
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-003, ADR-CANDIDATE-004

## Context

Withdrawals and transfers must use database row locking. Deposits must also produce an exact final balance and exact post-transaction balances under concurrency. Transfer locking must avoid deadlocks.

## Decision

Serialize every balance mutation for an Account by locking the Account row in PostgreSQL.

- Use `SELECT ... FOR UPDATE` inside the explicit transaction
- Deposits, withdrawals, full withdrawals and transfers all acquire Account row locks
- Transfers lock both Account rows in deterministic ascending account-ID order, independent of transfer direction
- Re-read the current balance after acquiring the lock and validate against that value
- Write the resulting balance and Transaction post-balance before commit
- Set a bounded transaction-local PostgreSQL `lock_timeout`
- Map lock timeout, deadlock and other safe concurrency-abort conditions to HTTP 409 / `concurrent_operation_conflict`
- Do not automatically retry business operations in v0.1.0; the client may retry with the same idempotency key

The exact SQL location may use EF Core raw SQL or an Npgsql command owned by Infrastructure. The domain layer remains unaware of SQL locking syntax.

## Consequences

### Positive

- A single simple mechanism guarantees exact post-balances for all balance mutations.
- Deterministic transfer order materially reduces deadlock risk.
- Bounded waiting avoids requests hanging indefinitely.

### Negative

- Concurrent deposits to one Account are serialized.
- PostgreSQL-specific SQL is required.
- Lock timeout values require testing under the target environment.

## Rejected alternatives

- Optimistic concurrency only: does not satisfy the explicit withdrawal/transfer row-lock requirement.
- Atomic increment for deposits and row locks for other operations: more scalable, but introduces two mutation models and more review complexity for this small demo.
- Automatic retry: can obscure contention and must be coordinated with idempotency; client retry is clearer for v0.1.0.

## Verification

- Parallel PostgreSQL tests prove exact balances and post-balances.
- Opposite-direction concurrent transfers complete without partial updates.
- Forced lock timeout returns the fixed conflict contract and leaves no partial state.
