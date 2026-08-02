# ADR-0006: Persistence model, identifiers and time

- Status: Accepted
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-006, ADR-CANDIDATE-007, ADR-CANDIDATE-008, ADR-CANDIDATE-009, ADR-CANDIDATE-010

## Context

The specification requires one Customer to one Account, synchronized state and closure time, immutable Transaction history, deterministic history ordering, a simple unique account number and JST presentation.

## Decision

### Identifiers

- Customer, Account, Transaction, transfer, Operator and Audit Log IDs use application-generated UUID version 7
- Account number uses a PostgreSQL sequence with range `1` through `999999999999`
- The sequence is `NO CYCLE`
- The sequence value is exposed as a zero-padded 12-digit string
- Account number is unique and immutable
- If the sequence is exhausted, Customer/Account registration fails atomically and does not publish a 13-digit value or reuse an old number

No rollover service, alternate number range or capacity dashboard is required for v0.1.0. The internal demo cannot realistically approach the 12-digit limit; the ADR only fixes safe boundary behavior.

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
- Account-number exhaustion fails safely without reuse or format drift.
- UTC storage avoids daylight and environment ambiguity.
- Text states are readable during database inspection.
- Database triggers provide strong evidence that Transaction history is append-only.

### Negative

- A 12-digit sequential account number is predictable; acceptable only for the internal demo.
- Cross-row state synchronization cannot be expressed fully as a simple check constraint.
- Triggers add migration and test complexity.
- There is no rollover path after account-number exhaustion; such a future requirement needs a new ADR.

## Rejected alternatives

- Let the sequence produce 13-digit account numbers: violates the fixed 12-digit representation.
- Cycle the sequence: can reuse an existing account number and is unsafe.
- Build a production-scale account-number allocation service: unnecessary for the internal demo.
- Random account numbers: require collision handling without product value for this demo.
- PostgreSQL native enum: harder to evolve through migrations than text plus checks.
- Store local JST without offset: ambiguous and environment-dependent.
- Application-only Transaction immutability: weaker protection and weaker verification evidence.

## Verification

- Migration tests prove unique one-to-one ownership and account-number uniqueness.
- Schema inspection verifies the account-number sequence maximum and `NO CYCLE` setting.
- An isolated boundary integration test places the sequence at its maximum and proves the next registration fails without a partial Customer or Account.
- Update/delete attempts against Transaction fail at the database layer.
- History order is deterministic for identical timestamps.
