# Architecture Decision Records

変更コストが高く、複数機能へ影響する重要な設計判断を記録します。

## ADR化の対象

- データモデルとトランザクション境界
- 金額表現
- 残高更新の排他制御
- 複数口座のロック順序
- 冪等性
- 論理削除
- 取引履歴の不変性
- migrationとロールバック方式

## 状態

`Proposed → Accepted → Superseded / Rejected`

局所的で容易に変更可能な実装判断までADR化しません。

## Phase 3 Accepted set

- Specification Ready: `PASS`
- Architecture Ready: `NOT EVALUATED`
- Base specification merge: `8df8caee4afcacad2c2d05b3ae39bf94217ee12b`
- Technology stack selected and approved by Koo: .NET / ASP.NET Core / PostgreSQL / EF Core / REST API / Docker Compose
- Independent re-review head: `1e79828fb6a9c29a4e888e50423c94122bca2e68`
- Independent re-review: `PASS`, Blocker / Major / Minor / Nit = `0 / 0 / 0 / 0`

| ADR | Title | Status |
| --- | --- | --- |
| 0001 | Application and platform baseline | Accepted |
| 0002 | Money representation | Accepted |
| 0003 | Database transaction boundaries | Accepted |
| 0004 | Concurrency control and row locking | Accepted |
| 0005 | Idempotency persistence and replay | Accepted |
| 0006 | Persistence model, identifiers and time | Accepted |
| 0007 | Authentication, authorization and operator management | Accepted |
| 0008 | Audit logging, technical logging and backup | Accepted |
| 0009 | Database schema migration and rollback | Accepted |

These ADRs were independently reviewed and approved by Koo. They become architecture authority after merge to `main`.

Accepted status does not by itself authorize application implementation, schema creation, migration generation or Docker configuration. Implementation starts only after Architecture Ready passes and implementation Issues are approved.

## Review-fix scope

The first independent review of PR #25 reported three Major and two Minor findings. The revised head resolved all five findings, and the final independent re-review reported no new findings.

The correction scope remained bounded for the internal demo:

- stale authorization, raw idempotency-key persistence and audit/idempotency atomicity were mandatory architecture fixes;
- account-number exhaustion received a safe non-cycling boundary without a rollover service or capacity platform;
- backup artifacts received local access, location and cleanup controls without requiring a remote vault, KMS or scheduled backup service;
- application code, schema, migration and Docker implementation remained out of scope.

## Approval order

1. ADR-0001 establishes the shared platform baseline.
2. ADR-0002 through ADR-0009 are consistent with ADR-0001 and are Accepted as one architecture set.
3. Architecture Ready is evaluated separately after this Accepted set is merged and the merge evidence is fixed.
