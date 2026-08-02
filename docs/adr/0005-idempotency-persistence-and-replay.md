# ADR-0005: Idempotency persistence and replay

- Status: Proposed
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-005, ADR-CANDIDATE-013

## Context

The approved product contract defines scope, request equivalence, fixed results, non-consuming errors, in-progress behavior, different-payload conflict and no expiry in v0.1.0. The implementation must preserve atomicity and avoid a durable `Processing` record becoming orphaned after a crash.

## Proposed decision

Use PostgreSQL for durable idempotency results and PostgreSQL advisory transaction locks for in-progress exclusion.

### Durable record

Store one record keyed by:

- operator ID
- operation type
- idempotency key

Store:

- canonical request fingerprint
- completion time
- fixed HTTP status
- fixed error code when present
- response payload required to reproduce the first business result
- references to the resulting business records where useful

Enforce a unique constraint on `(operator_id, operation_type, idempotency_key)`.

### Request fingerprint

- Normalize only approved business inputs
- Resolve Customer ID and Account number to the same Account identity before comparison where the specification defines equivalence
- Serialize a versioned canonical representation
- Hash it with SHA-256
- Do not log the raw idempotency key or unnecessary request data

### In-progress exclusion

- Begin the business transaction
- Attempt a transaction-scoped PostgreSQL advisory lock derived from scope and key
- If the lock is unavailable, return HTTP 409 / `idempotency_in_progress`
- If acquired, inspect the durable record
- Same fingerprint returns the stored result
- Different fingerprint returns HTTP 409 / `idempotency_key_conflict`
- A new fixed result is inserted in the same transaction as the business outcome

Non-consuming errors roll back without a durable result. Transaction rollback or process crash releases the advisory lock and leaves neither business changes nor a false completed result.

No automatic internal retry is performed in v0.1.0. Idempotency records do not expire while their related business data exists.

## Consequences

### Positive

- Business changes and completed idempotency results commit atomically.
- No separately committed `Processing` row requires crash recovery.
- Concurrent duplicates can receive the approved in-progress response.

### Negative

- Advisory locks are PostgreSQL-specific.
- Canonicalization must be versioned and tested carefully.
- Hash collisions in the advisory-lock key can reduce concurrency, although durable key comparison prevents an incorrect replay.

## Rejected alternatives

- In-memory keyed locks: unsafe with multiple application instances and process restarts.
- Commit a Processing row before the business transaction: creates orphan and atomicity problems.
- Store only a business-record reference: insufficient to reproduce all fixed business errors and response contracts.

## Verification

- Integration tests cover same request, different payload, concurrent duplicate, crash rollback, fixed business error replay and non-consuming conflict retry.
