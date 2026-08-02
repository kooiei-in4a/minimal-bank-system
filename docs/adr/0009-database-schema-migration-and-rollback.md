# ADR-0009: Database schema migration and rollback

- Status: Proposed
- Date: 2026-08-02
- Decision owner: Koo
- Related: #3, #24

## Context

The project must eventually demonstrate migration, deployment and rollback. Database schema changes affect transaction safety, test reproducibility and release evidence. Applying migrations automatically during normal application startup makes failure handling and deployment order harder to control.

## Proposed decision

Use EF Core migrations as the only application-owned schema evolution mechanism.

- Do not use `EnsureCreated` or ad-hoc startup DDL outside disposable tests
- Generate and review every migration as source-controlled code
- Apply migrations through an explicit migrator command or one-shot Docker Compose service before starting the application
- Do not run migrations automatically from the normal API startup path
- Keep EF Core's migration history in PostgreSQL
- Generate an idempotent SQL script for review and release evidence where EF Core supports the target range
- Run migration commands with a bounded timeout and fail the deployment if migration fails

### Forward validation

Every migration must be tested:

- from an empty database to the latest schema
- from the immediately previous migration to the latest schema with representative existing rows
- against EF Core model drift so no pending model change remains

### Rollback

- Each migration supplies a meaningful `Down` path when reversal can preserve the prior contract
- Before Release Candidate migration, create and verify a logical PostgreSQL backup
- Destructive or data-rewriting migrations require an explicit data-preservation and restore plan; a superficial `Down` method is not accepted
- If a migration is safely reversible, rollback may target the previous migration and then deploy the compatible previous application version
- If reversal would lose or misinterpret data, rollback uses backup restore into a clean database rather than pretending the schema-only downgrade is safe
- Application rollback is permitted only when the previous application is compatible with the resulting schema

No production-style online zero-downtime migration guarantee is required for v0.1.0. The internal demo may use a bounded maintenance window.

## Consequences

### Positive

- Schema changes are explicit, reviewable and reproducible.
- Application startup cannot unexpectedly mutate the database.
- Rollback evidence distinguishes safe schema downgrade from backup restore.

### Negative

- Deployment has a separate migration step.
- Every migration requires upgrade and rollback testing.
- Destructive changes may require slower backup/restore rather than a simple `Down` call.

## Rejected alternatives

- Automatic migration on API startup: hides deployment ordering and can create multi-instance races.
- `EnsureCreated`: bypasses the reviewed migration history.
- Require every rollback to use `Down`: unsafe when data transformation is not reversible.
- Require online zero-downtime migration: disproportionate for the internal demo.

## Verification

- CI or release validation creates a clean database, migrates to latest and verifies no pending model differences.
- A representative previous-schema database upgrades without losing required fields.
- Release Candidate evidence includes either a tested safe downgrade or a clean backup restore and smoke test.
