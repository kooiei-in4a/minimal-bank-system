# ADR-0008: Audit logging, technical logging and backup

- Status: Proposed
- Date: 2026-08-02
- Decision owner: Koo
- Related candidates: ADR-CANDIDATE-012, ADR-CANDIDATE-014

## Context

The product requires Audit Log records, separate technical failure logs, secret non-disclosure, health checks and a backup procedure. User-facing Audit Log browsing is outside v0.1.0.

Audit persistence must remain consistent with successful operations and fixed deterministic business rejections. Backup artifacts also contain credential hashes, Audit Log data and idempotency records, even though the internal demo uses no real personal or banking data.

## Proposed decision

### Audit Log

Store Audit Log records in PostgreSQL as append-only data.

- Successful state-changing operations write their Audit Log in the same transaction as the business change and fixed successful idempotency result
- A fixed deterministic business rejection writes its rejection Audit Log and fixed idempotency result in the same short transaction while the idempotency advisory lock is held
- Authenticated non-consuming rejections write a failure Audit Log in a separate short transaction before the response
- Operator-management success and rejection are audited
- Audit records contain the approved actor, role, operation, target, time, result, error code and correlation ID
- PostgreSQL triggers reject Audit Log update and delete for the application database role
- No user-facing Audit Log API or UI in v0.1.0

If required Audit Log persistence fails:

- the transaction containing the Audit Log is rolled back;
- a fixed idempotency result in that transaction is also rolled back;
- the idempotency key remains unconsumed; and
- the API fails closed with an internal error and emits a technical log.

An unauthenticated 401 has no authenticated actor and is recorded only in technical/security logs, not the product Audit Log.

The append-only database controls protect against application mistakes and the normal application role. They do not claim to prevent a PostgreSQL superuser or storage administrator from altering data.

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
- Backup output must be outside the repository working tree
- Backup scripts reject repository-contained output paths rather than relying only on `.gitignore`
- Use owner-only file access where the host platform supports it
- Supply database credentials through environment, Docker secret or a protected password file; do not place credentials directly in command arguments
- Create and verify a backup before Release Candidate validation
- Restore into a clean database, inject runtime secrets externally and run smoke checks
- Retain the backup only through the Release Candidate evidence period, then delete it unless an explicit investigation requires retention
- If a backup is copied away from the controlled local validation host, encrypt it in transit and at rest
- No scheduled remote backup service, KMS-backed archive platform or enterprise retention system is required in v0.1.0

## Consequences

### Positive

- Audit records share atomicity with successful business operations and fixed business rejections.
- Technical diagnosis remains separate from product audit history.
- Backup and restore can be demonstrated without new infrastructure.
- Backup artifacts receive bounded protection appropriate to the internal-demo scope.

### Negative

- Failure-audit persistence adds an extra database transaction for non-consuming authenticated rejections.
- A database outage can prevent both business processing and Audit Log persistence.
- Console logs are suitable for the demo but not a full production observability platform.
- Local backup handling depends partly on host filesystem protections.

## Rejected alternatives

- Persist a fixed business rejection and its Audit Log in separate transactions: can make the first result differ from replay.
- Require a remote encrypted backup service and KMS for the internal demo: disproportionate to the release boundary.
- Store Audit Log only in application logs: insufficient structure and durability.
- Add Elasticsearch, Loki or a cloud log service: unnecessary infrastructure.
- Filesystem-only PostgreSQL backup: weaker portability and verification than logical backup/restore.

## Verification

- Integration tests verify success and rejection Audit Log records.
- Failure injection proves fixed rejection result and rejection Audit Log commit or roll back together.
- Tests prove Audit Log update/delete is rejected for the application role.
- Secret scanning and log-content tests cover prohibited fields.
- Backup script tests reject repository output paths and avoid credentials in argv.
- Release evidence includes restricted backup creation, successful clean restore, smoke test and post-evidence cleanup.
