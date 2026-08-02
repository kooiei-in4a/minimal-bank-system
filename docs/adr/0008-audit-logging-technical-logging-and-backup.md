# ADR-0008: Audit logging, technical logging and backup

- Status: Proposed
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-012, ADR-CANDIDATE-014

## Context

The product requires Audit Log records, separate technical failure logs, secret non-disclosure, health checks and a backup procedure. User-facing Audit Log browsing is outside v0.1.0.

## Proposed decision

### Audit Log

Store Audit Log records in PostgreSQL as append-only data.

- Successful state-changing operations write their audit record in the same transaction as the business change
- Rejected authenticated operations write a failure audit record in a separate short transaction before the response
- Operator-management success and rejection are audited
- Audit records contain the approved actor, role, operation, target, time, result, error code and correlation ID
- PostgreSQL triggers reject Audit Log update and delete
- No user-facing Audit Log API or UI in v0.1.0

If required Audit Log persistence fails, fail closed with an internal error and emit a technical log. An unauthenticated 401 has no authenticated actor and is recorded only in technical/security logs, not the product Audit Log.

### Technical logs

- Use `Microsoft.Extensions.Logging` with JSON console output
- Include correlation ID and fixed error code where available
- Do not log passwords, JWTs, signing keys, raw idempotency keys or unnecessary personal data
- Docker collects stdout/stderr; no separate logging service is added

### Health checks

- `/health/live`: process liveness only
- `/health/ready`: includes PostgreSQL connectivity
- Health responses do not expose connection strings or exception details

### Backup and restore

- PostgreSQL data uses a named Docker volume
- Provide documented scripts for `pg_dump --format=custom` and `pg_restore`
- Create and verify a backup before Release Candidate validation
- Restore into a clean database and run smoke checks
- No scheduled or remote backup service in v0.1.0

## Consequences

### Positive

- Audit records share atomicity with successful business operations.
- Technical diagnosis remains separate from product audit history.
- Backup and restore can be demonstrated without new infrastructure.

### Negative

- Failure-audit persistence adds an extra database transaction.
- A database outage can prevent both business processing and Audit Log persistence.
- Console logs are suitable for the demo but not a full production observability platform.

## Rejected alternatives

- Store Audit Log only in application logs: insufficient structure and durability.
- Add Elasticsearch, Loki or a cloud log service: unnecessary infrastructure.
- Filesystem-only PostgreSQL backup: weaker portability and verification than logical backup/restore.

## Verification

- Integration tests verify success and rejection Audit Log records.
- Tests prove Audit Log update/delete is rejected.
- Secret scanning and log-content tests cover prohibited fields.
- Release evidence includes a successful clean restore and smoke test.
