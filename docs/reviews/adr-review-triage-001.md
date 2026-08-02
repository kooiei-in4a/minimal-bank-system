# ADR Review Triage 001

- Date: 2026-08-02
- Review target: PR #25
- Reviewed head: `66cdf4648c03aa018216b10d8ab32448431a8c2b`
- Source review verdict: FAIL
- Findings: Blocker 0 / Major 3 / Minor 2 / Nit 0
- Koo approval application: CORRECT

## Triage policy

The project is an internal demonstration of specification-, ADR- and AI-PR-governed development. Findings that affect authorization, idempotency, atomicity or audit consistency are fixed at architecture level. Low-probability capacity and local backup-operational concerns receive bounded controls appropriate to the internal-demo scope rather than production-scale platforms.

## Triage result

| Finding | Severity | Triage | Response |
| --- | --- | --- | --- |
| F-01 stale JWT role authorization | Major | Must fix fully | Validate an authorization version on every request and authorize from the current database role. Old tokens become 401 after role/state change. |
| F-02 raw idempotency key persistence | Major | Must fix, bounded design | Persist only a versioned SHA-256 digest. Raw keys remain request-memory only. HSM/KMS-backed HMAC is not required for the internal demo because the key is not an authentication credential. |
| F-03 fixed business error / audit atomicity | Major | Must fix fully | Commit fixed rejection result and rejection Audit Log in the same short transaction under the advisory lock. Roll back both on either failure. |
| F-04 12-digit sequence exhaustion | Minor | Lightweight architecture fix | Define `1..999999999999`, `NO CYCLE`, atomic registration failure. No rollover service, capacity dashboard or elaborate exhaustion operations for v0.1.0. |
| F-05 backup artifact protection | Minor | Lightweight operational fix | Keep dumps outside the repository, restrict access, avoid credentials in argv, delete after RC evidence. No mandatory remote vault, KMS or scheduled encrypted-backup platform for v0.1.0; encrypt only if moved off the controlled host. |

## Scope deliberately not added

- HSM, KMS or secret-rotation platform solely for idempotency digesting
- Redis or distributed token-revocation service
- Account-number rollover or multi-range allocation service
- Scheduled remote backup service
- Enterprise observability, SIEM or immutable external audit store
- Application code, schema, migrations or Docker implementation

## Required verification after revision

- Role demotion, promotion and disablement invalidate previously issued JWTs.
- Raw idempotency keys do not appear in the database, logs, audit records or backup.
- Failure injection proves fixed business rejection result and Audit Log commit or roll back together.
- Sequence maximum is non-cycling and registration fails without partial data.
- Backup scripts reject repository paths and restore evidence includes cleanup.
