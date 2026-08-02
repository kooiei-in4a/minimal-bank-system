# ADR-0006: Persistence model, identifiers and time

- Status: Proposed
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-006, ADR-CANDIDATE-007, ADR-CANDIDATE-008, ADR-CANDIDATE-009, ADR-CANDIDATE-010

## Context

The specification requires one Customer to one Account, synchronized state and closure time, immutable Transaction history, deterministic history ordering, a simple unique account number and JST presentation.

## Proposed decision

### Identifiers

- Customer, Account, Transaction, transfer, Operator and Audit Log IDs use application-generated UUID version 7
- Account number uses a PostgreSQL sequence and is exposed as a zero-padded 12-digit string
- Account number is unique and immutable

### State representation

- C# uses strongly typed enums
- PostgreSQL stores stable lowercase text values with check constraints
- Customer and Account use `active` / `closed`
- Operator uses `active` / `disabled`
- Transaction type uses the four approved stable values

### Constraints

- Account has a unique Customer foreign key, enforcing one-to-one ownership
- Customer and Account closure rules are enforced by application logic and database check constraints where a single row can express the rule
- Cross-row Customer/Account synchronization is enforced in the application transaction and verified by integration tests
- Transaction rows cannot be updated or deleted; PostgreSQL triggers reject `UPDATE` and `DELETE`

### Time

- Store instants as PostgreSQL `timestamptz` in UTC
- Generate application timestamps through an injected `TimeProvider`
- Convert to Asia/Tokyo only at the API presentation boundary
- History order is `occurred_at DESC, transaction_id DESC`

## Consequences

### Positive

- UUIDv7 IDs are globally unique and approximately time ordered.
- UTC storage avoids daylight and environment ambiguity.
- Text states are readable during database inspection.
- Database triggers provide strong evidence that Transaction history is append-only.

### Negative

- A 12-digit sequential account number is predictable; acceptable only for the internal demo.
- Cross-row state synchronization cannot be expressed fully as a simple check constraint.
- Triggers add migration and test complexity.

## Rejected alternatives

- Random account numbers: require collision handling without product value for this demo.
- PostgreSQL native enum: harder to evolve through migrations than text plus checks.
- Store local JST without offset: ambiguous and environment-dependent.
- Application-only Transaction immutability: weaker protection and weaker verification evidence.

## Verification

- Migration tests prove unique one-to-one ownership and account-number uniqueness.
- Update/delete attempts against Transaction fail at the database layer.
- History order is deterministic for identical timestamps.
