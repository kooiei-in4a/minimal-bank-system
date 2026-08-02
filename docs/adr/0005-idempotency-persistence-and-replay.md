# ADR-0005: Idempotency persistence and replay

- Status: Proposed
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-005, ADR-CANDIDATE-013

## Context

The approved product contract defines scope, request equivalence, fixed results, non-consuming errors, in-progress behavior, different-payload conflict and no expiry in v0.1.0. The implementation must preserve atomicity, avoid persisting the raw idempotency key and avoid a durable `Processing` record becoming orphaned after a crash.

## Proposed decision

Use PostgreSQL for durable idempotency results and PostgreSQL advisory transaction locks for in-progress exclusion.

### Key digest

The raw idempotency key exists only in request-processing memory.

Derive a versioned, domain-separated SHA-256 digest from:

- operator ID
- operation type
- raw idempotency key

Persist and query only the digest. Do not store the raw key in PostgreSQL, Audit Log, technical logs, traces, exceptions or backup artifacts.

The idempotency key is not an authentication credential. For the internal demo, a versioned SHA-256 digest is sufficient; an HSM, KMS or separately rotated HMAC key is not required. A future production profile may replace the digest derivation without changing the product contract.

### Durable record

Store one record keyed by:

- operator ID
- operation type
- idempotency-key digest

Store:

- canonical request fingerprint
- completion time
- fixed HTTP status
- fixed error code when present
- response payload required to reproduce the first business result
- references to the resulting business records where useful

Enforce a unique constraint on `(operator_id, operation_type, idempotency_key_digest)`.

### Request fingerprint

- Normalize only approved business inputs
- Resolve Customer ID and Account number to the same Account identity before comparison where the specification defines equivalence
- Serialize a versioned canonical representation
- Hash it with SHA-256 independently from the idempotency-key digest
- Do not log unnecessary request data

### In-progress exclusion

- Derive the full idempotency-key digest in memory
- Begin the relevant PostgreSQL transaction
- Attempt a transaction-scoped PostgreSQL advisory lock derived from a bounded portion of the digest
- If the lock is unavailable, return HTTP 409 / `idempotency_in_progress`
- If acquired, inspect the durable record using the full scope and full digest
- Same fingerprint returns the stored result
- Different fingerprint returns HTTP 409 / `idempotency_key_conflict`

An advisory-lock hash collision may serialize unrelated requests, but it cannot cause result sharing because replay and conflict decisions use the full durable scope, full digest and request fingerprint.

### Atomic result persistence

For a successful operation, commit the following in one transaction:

- business mutation
- Transaction records
- successful Audit Log
- fixed idempotency result

For a fixed deterministic business rejection, commit the following in one short transaction while holding the advisory lock:

- rejection Audit Log
- fixed idempotency result

If either record fails to persist, roll back both, return an internal error and leave the key unconsumed.

Non-consuming errors roll back without a durable result. Transaction rollback or process crash releases the advisory lock and leaves neither business changes nor a false completed result.

No automatic internal retry is performed in v0.1.0. Idempotency records do not expire while their related business data exists.

## Consequences

### Positive

- Business changes, Audit Log and completed results have explicit atomic boundaries.
- Raw idempotency keys do not appear in durable storage or backup.
- No separately committed `Processing` row requires crash recovery.
- Concurrent duplicates can receive the approved in-progress response.

### Negative

- Advisory locks are PostgreSQL-specific.
- Canonicalization and digest versioning must be tested carefully.
- Hash collisions in the advisory-lock key can reduce concurrency, although durable full-digest comparison prevents an incorrect replay.

## Rejected alternatives

- Persist the raw idempotency key: unnecessarily exposes request correlation material in the database and backup.
- Add HSM/KMS-backed HMAC solely for this internal demo: adds operational machinery without changing the approved product behavior.
- In-memory keyed locks: unsafe with multiple application instances and process restarts.
- Commit a Processing row before the business transaction: creates orphan and atomicity problems.
- Store only a business-record reference: insufficient to reproduce all fixed business errors and response contracts.

## Verification

- Integration tests cover same request, different payload, concurrent duplicate, crash rollback, fixed business error replay and non-consuming conflict retry.
- Failure injection proves that fixed rejection result and rejection Audit Log commit or roll back together.
- Tests prove raw idempotency keys are absent from database rows, Audit Log, technical logs and backup output.
- A collision-focused test proves advisory-lock collisions only serialize work and never replay another scope's result.
