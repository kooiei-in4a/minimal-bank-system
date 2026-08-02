# Architecture Decision Records

## Status

Phase 3 ADR Draft set for Issue #24.

- Specification Ready: `PASS`
- Architecture Ready: `NOT EVALUATED`
- Base specification merge: `8df8caee4afcacad2c2d05b3ae39bf94217ee12b`
- Technology stack selected by Koo: .NET / ASP.NET Core / PostgreSQL / EF Core / REST API / Docker Compose

## Draft ADR set

| ADR | Title | Status |
| --- | --- | --- |
| 0001 | Application and platform baseline | Proposed |
| 0002 | Money representation | Proposed |
| 0003 | Database transaction boundaries | Proposed |
| 0004 | Concurrency control and row locking | Proposed |
| 0005 | Idempotency persistence and replay | Proposed |
| 0006 | Persistence model, identifiers and time | Proposed |
| 0007 | Authentication, authorization and operator management | Proposed |
| 0008 | Audit logging, technical logging and backup | Proposed |

These ADRs are proposals until independently reviewed and approved by Koo. They do not authorize application implementation, schema creation, migration generation or Docker configuration.

## Approval order

1. ADR-0001 establishes the shared platform baseline.
2. ADR-0002 through ADR-0008 may be reviewed together, but their decisions must remain consistent with ADR-0001.
3. Architecture Ready is evaluated only after all required ADRs are accepted and all Blocker/Major findings are resolved.
