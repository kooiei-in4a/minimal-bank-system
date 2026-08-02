# ADR-0001: Application and platform baseline

- Status: Accepted
- Date: 2026-08-02
- Decision owner: Koo
- Related: #3, #24, PR #9

## Context

The approved specification fixes product behavior but intentionally leaves implementation technologies open. The project needs one reproducible and conventional platform so that later ADRs can define transactions, locks, migrations, authentication and operations without comparing unrelated ecosystems.

## Decision

Use a modular monolith with the following baseline:

- .NET 10 LTS and ASP.NET Core 10
- C# with nullable reference types enabled
- REST API implemented with ASP.NET Core controllers and `[ApiController]`
- PostgreSQL 18
- EF Core 10 with Npgsql.EntityFrameworkCore.PostgreSQL 10
- Docker Compose v2 for local and closed-environment execution
- One application service and one PostgreSQL service
- No Redis, message broker, external identity provider or cloud-only service in v0.1.0

Use the latest supported patch release within each approved major version. Package versions and container image digests are pinned by the implementation PR, not by this ADR.

Organize the solution into explicit projects or equivalent boundaries:

- API: HTTP contracts, authentication entry points and response mapping
- Application: use cases and transaction orchestration
- Domain: business rules and domain types
- Infrastructure: EF Core, PostgreSQL, authentication persistence and operational adapters
- Tests: unit, integration and API tests

## Consequences

### Positive

- The selected technologies have first-class transaction, migration, authentication and testing support.
- A single deployable application keeps the internal demo operationally small.
- Explicit boundaries make specification and ADR traceability easier to review.
- PostgreSQL-specific locking can be used where the approved requirements demand it.

### Negative

- The modular boundaries add more projects and mapping code than a single-project demo.
- PostgreSQL-specific behavior reduces database portability.
- EF Core does not remove the need for SQL in locking and database-constraint scenarios.

## Rejected alternatives

- Microservices: unnecessary distributed transactions and operational complexity.
- Minimal APIs as the default API style: concise, but controllers provide a clearer review surface for a contract-heavy demo.
- In-memory or SQLite database: cannot validate PostgreSQL locking and transaction behavior.
- External identity provider: introduces infrastructure outside the internal-demo objective.

## Verification

- The implementation solution targets `net10.0`.
- The application starts through Docker Compose with PostgreSQL 18.
- No application code is created before this ADR set is accepted and Architecture Ready passes.
