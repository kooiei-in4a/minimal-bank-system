# ADR-0002: Money representation

- Status: Proposed
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-001

## Context

The product supports Japanese yen only and all approved amounts are whole yen. Balance arithmetic must be exact, deterministic and protected from floating-point rounding.

## Proposed decision

Represent money as integer yen:

- Domain and application type: signed 64-bit integer (`long`)
- PostgreSQL type: `bigint`
- No `float`, `double` or binary floating-point arithmetic
- No fractional unit or currency column in v0.1.0
- Arithmetic uses checked operations so overflow fails instead of wrapping
- Database constraints enforce non-negative Account balance
- Command validation enforces each operation's approved minimum and maximum

Use a small domain value type such as `YenAmount` or equivalent to prevent accidental mixing with unrelated integers. The value type remains persistence-neutral.

## Consequences

### Positive

- Exact arithmetic with no rounding policy.
- Straightforward equality, comparison and database constraints.
- Easy boundary testing for 1 yen and 10,000,000 yen.

### Negative

- A future multi-currency or fractional-currency product would require a new decision and migration.
- Developers must use checked arithmetic consistently.

## Rejected alternatives

- `double` or `float`: rounding risk is unacceptable.
- PostgreSQL `money`: locale-dependent presentation and reduced portability of application logic.
- Decimal minor units: valid, but unnecessary for a yen-only internal demo.

## Verification

- Domain tests cover overflow and approved boundaries.
- Database integration tests prove negative balances are rejected.
- API contracts exchange integer yen values only.
