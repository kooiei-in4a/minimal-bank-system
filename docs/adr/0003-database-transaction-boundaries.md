# ADR-0003: Database transaction boundaries

- Status: Proposed
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-002

## Context

Customer registration, closure, deposits, withdrawals and transfers have approved all-or-nothing outcomes. The system uses one PostgreSQL database, so distributed transaction machinery is unnecessary.

## Proposed decision

Use one explicit PostgreSQL transaction per state-changing application use case.

- Isolation level: PostgreSQL `READ COMMITTED`
- One scoped EF Core `DbContext` per request/use case
- Customer registration atomically creates Customer and Account
- Closure atomically updates Customer and Account with the same closure time
- Deposit and withdrawal atomically update balance and append Transaction
- Transfer atomically updates both balances and appends both Transaction records with one transfer ID
- Successful Audit Log records are written in the same transaction as the successful business change
- `SaveChangesAsync` is not treated as a transaction boundary when the use case contains locking, multiple persistence steps or idempotency handling; those cases use explicit transaction orchestration
- No `TransactionScope`, distributed transaction or cross-database transaction in v0.1.0

Application services own transaction orchestration. Domain objects do not open transactions, and controllers do not call `SaveChangesAsync` directly.

## Consequences

### Positive

- The approved atomicity rules map directly to one database transaction.
- Failure injection tests can prove that partial updates are not visible.
- Transaction ownership is easy to find during independent review.

### Negative

- Long-running logic inside the transaction can increase lock time.
- External I/O cannot be included atomically and must not be performed while locks are held.

## Rejected alternatives

- Implicit transaction per individual `SaveChanges`: insufficient for multi-step use cases.
- Serializable isolation for every use case: stronger than required and likely to create avoidable conflicts.
- Distributed transactions: no second transactional resource exists.

## Verification

- PostgreSQL integration tests inject failure after each mutation step and verify full rollback.
- No network call, email or other external I/O occurs inside a business transaction.
