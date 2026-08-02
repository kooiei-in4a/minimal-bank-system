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

## Phase 3 Draft set

- Specification Ready: `PASS`
- Architecture Ready: `NOT EVALUATED`
- Base specification merge: `8df8caee4afcacad2c2d05b3ae39bf94217ee12b`
- Technology stack selected by Koo: .NET / ASP.NET Core / PostgreSQL / EF Core / REST API / Docker Compose

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
| 0009 | Database schema migration and rollback | Proposed |

These ADRs are proposals until independently reviewed and approved by Koo. They do not authorize application implementation, schema creation, migration generation or Docker configuration.

## Review-fix scope

The first independent review of PR #25 reported three Major and two Minor findings.

The correction scope is intentionally bounded for the internal demo:

- stale authorization, raw idempotency-key persistence and audit/idempotency atomicity are mandatory architecture fixes;
- account-number exhaustion receives a safe non-cycling boundary without a rollover service or capacity platform;
- backup artifacts receive local access, location and cleanup controls without requiring a remote vault, KMS or scheduled backup service;
- application code, schema, migration and Docker implementation remain out of scope;
- all ADRs remain `Proposed` until the revised head passes independent review.

## Approval order

1. ADR-0001 establishes the shared platform baseline.
2. ADR-0002 through ADR-0009 may be reviewed together, but their decisions must remain consistent with ADR-0001.
3. Architecture Ready is evaluated only after all required ADRs are Accepted and all Blocker/Major findings are resolved.
