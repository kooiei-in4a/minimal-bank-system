# ADR-0003: Database transaction boundaries

- Status: Accepted
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-002

## Context

Customer registration, closure, deposits, withdrawals and transfers have approved all-or-nothing outcomes. Fixed deterministic business rejections may also consume an idempotency key and require a durable Audit Log. The system uses one PostgreSQL database, so distributed transaction machinery is unnecessary.

## Decision

Use one explicit PostgreSQL transaction per state-changing application use case.

- Isolation level: PostgreSQL `READ COMMITTED`
- One scoped EF Core `DbContext` per request/use case
- Customer registration atomically creates Customer and Account
- Closure atomically updates Customer and Account with the same closure time
- Deposit and withdrawal atomically update balance and append Transaction
- Transfer atomically updates both balances and appends both Transaction records with one transfer ID
- Successful Audit Log records are written in the same transaction as the successful business change
- The fixed idempotency result for a successful operation is written in that same transaction
- `SaveChangesAsync` is not treated as a transaction boundary when the use case contains locking, multiple persistence steps, Audit Log persistence or idempotency handling; those cases use explicit transaction orchestration
- No `TransactionScope`, distributed transaction or cross-database transaction in v0.1.0

Application services own transaction orchestration. Domain objects do not open transactions, and controllers do not call `SaveChangesAsync` directly.

### Fixed deterministic business rejection

A deterministic business rejection that is a consuming idempotency result, such as insufficient balance or a closed account, uses one short explicit transaction while the transaction-scoped idempotency advisory lock is held.

That transaction atomically writes:

1. the rejection Audit Log; and
2. the fixed idempotency result used for replay.

Both records commit together or roll back together. If either persistence step fails, the transaction rolls back, the idempotency key is not consumed and the API returns an internal error rather than the business rejection.

### Non-consuming failures

Concurrency conflicts, lock timeouts, internal failures, indeterminate outcomes, authentication failures, authorization failures and pre-business input failures do not write a fixed idempotency result.

An authenticated rejection that requires an Audit Log but is non-consuming uses a separate short Audit Log transaction. If that required Audit Log cannot be persisted, the API fails closed with an internal error. The idempotency key remains unconsumed.

## Consequences

### Positive

- The approved atomicity rules map directly to one database transaction.
- Fixed business rejections cannot be replayed without their matching Audit Log.
- Failure injection tests can prove that partial updates are not visible.
- Transaction ownership is easy to find during independent review.

### Negative

- Long-running logic inside the transaction can increase lock time.
- External I/O cannot be included atomically and must not be performed while locks are held.
- Rejected consuming outcomes require a short transaction even though no business state changes.

## Rejected alternatives

- Implicit transaction per individual `SaveChanges`: insufficient for multi-step use cases.
- Persist the fixed rejection result and rejection Audit Log in separate transactions: can make the first response differ from replay and violates fail-closed auditing.
- Serializable isolation for every use case: stronger than required and likely to create avoidable conflicts.
- Distributed transactions: no second transactional resource exists.

## Verification

- PostgreSQL integration tests inject failure after each successful-operation mutation step and verify full rollback.
- Failure injection verifies that fixed rejection result and rejection Audit Log either both commit or both roll back.
- Tests prove that Audit Log persistence failure leaves no fixed idempotency result.
- No network call, email or other external I/O occurs inside a business transaction.
