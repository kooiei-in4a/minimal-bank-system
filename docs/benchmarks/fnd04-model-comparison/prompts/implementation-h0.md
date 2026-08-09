# FND-04 Candidate H0 Implementation Prompt

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-04 Benchmark Candidate / Agent A** です。

この実行は、Issue #42 `[FND-04] EF Core・明示的migration実行基盤を確立する` の **H0 implementation snapshot** を作るための独立実装attemptです。

## Variable identity

実行前に以下だけをcandidateごとに固定してください。

```yaml
MODEL: "<MODEL>"
HARNESS: "<HARNESS>"
EFFORT: "<EFFORT>"
CANDIDATE_SLUG: "<SLUG>"
TARGET_BRANCH: "<PRECREATED_BRANCH>"
COMMON_BASE_SHA: "<LOCKED_COMMON_BASE_SHA>"
ATTEMPT: 1
```

## Authority

必ずGitHub一次証拠から確認してください。

1. Parent / Control Issue #3
2. WP-1 Issue #33
3. Target Issue #42
4. `AGENTS.md`
5. Accepted ADR-0001 / ADR-0009
6. `docs/benchmarks/fnd04-model-comparison/reference/assumption-ledger.md`

Benchmark文書は製品仕様・ADR・Issueを上書きしません。

## Independence

H0固定まで次を参照してはいけません。

- 他candidateのbranch / PR / diff / test
- benchmark score / ranking
- reviewer result
- Gold / Reference Review
- Final Synthesis

## Task

Issue #42のScope / Acceptance Criteriaだけを実装してください。

最低限、次を成立させます。

- EF Core / Npgsql application persistence baseline
- application DbContext baseline
- PostgreSQL provider configuration
- design-time context creation strategy
- source-controlled migration baseline
- explicit one-shot migrator entry point
- normal API startupでmigrationを自動実行しないこと
- clean PostgreSQLへのmigration apply verification
- migrator failure propagation
- pending model difference / drift detection
- migration history verification
- schema-owning Issue向けmigration追加手順
- ADR-0009で求めるidempotent SQL generation path

## Hard scope boundaries

実装してはいけません。

- Customer / Account / Operator / Identity / AuditLog / Transaction / Idempotency等のbusiness table
- business constraint / trigger / sequence
- Docker Compose
- health endpoint
- FND-05 / FND-06責任
- production deployment
- API startup auto-migration
- application schema evolutionへの`EnsureCreated`
- SQLite / InMemoryによるPostgreSQL固有verificationの代替

## H0 workflow

1. target identityとcommon baseを確認する。
2. Issue #42、ADR、既存FND-03 fixtureを確認する。
3. 実装計画を作る。
4. 実装する。
5. 必須local verificationを行う。
6. `AGENTS.md`で通常要求される基本的なdiff self-reviewを行う。
7. Draft PRを作成する。
8. exact Head CIを確認する。
9. Post-Implementation Notesを1回だけ記録する。
10. H0 full Head SHAを固定して停止する。

**この実行ではFormal Self-Review phaseへ進まないでください。**
Formal Self-ReviewはH0固定後、fresh contextの別実行で行います。

## Required verification

最低限、実際の一次証拠で確認してください。

- restore
- build 0 warnings / 0 errors
- existing non-PostgreSQL tests
- real PostgreSQL integration tests
- clean DB migration apply
- migration history inspection
- explicit migrator failure path
- normal API startup前後でmigration history / schemaが勝手に変化しないこと
- pending model changes check
- idempotent SQL generation path
- `EnsureCreated` / startup `Migrate`等の禁止pathがないこと
- business schema先取りがないこと
- `git diff --check`

実行できない検証は成功扱いせず、`Unverified`へ明記してください。

## Snapshot rule

H0の採点対象は、最終報告に記載したfull Head SHAだけです。

H0固定後は、Formal Self-ReviewのFindingが固定されるまでコードを修正しないでください。

## Final report

```text
## FND-04 H0 Result

Model:
Harness:
Effort:
Attempt:

Branch:
Common Base:
H0 Head:
Draft PR:
Exact-Head CI:

Changed files:

Verification:
- Restore:
- Build:
- non-PostgreSQL tests:
- PostgreSQL tests:
- clean migration apply:
- migration history:
- migrator failure:
- API no-auto-migration:
- model drift check:
- idempotent SQL generation:
- git diff --check:

Scope drift:
Known concerns:
Unverified:
Duration:

H0 snapshot:
LOCKED / NOT LOCKED

Formal Self-Review:
NOT STARTED
```
