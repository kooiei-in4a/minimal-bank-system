# FND-04 Implementation Scoring Rubric

Status: **LOCKED BEFORE CANDIDATE EXECUTION**

Revision: `fnd04-implementation-v1`

Target: Issue #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

このrubricはH0とH1へ同じ基準を適用する。H1を最終implementation rankingへ使用し、H0はSelf-Review Gain測定用に保持する。

## Merge-readiness gate

Coding Scoreに関係なく、次のいずれかが残るcandidateは`MERGE_CANDIDATE: NO`とする。

- Blocker >= 1
- Major >= 1
- exact Head CI failure / required verification failure
- Issue #42のHard Scope violation
- real PostgreSQL verificationをSQLite/InMemoryへ置換

## Score — 100 points

| Axis | Points | FND-04 meaning |
| --- | ---: | --- |
| A. Issue達成度 | 25 | Issue #42 ACを漏れなく満たす |
| B. 正しさ・実行可能性 | 15 | clean apply、migrator、startup、driftが実際に成立する |
| C. Scope遵守・指示追従 | 15 | business schema / FND-05を先取りしない |
| D. 設計・Repository適合性 | 10 | Infrastructure / Migrator / API責任境界、ADR整合 |
| E. テスト・検証品質 | 10 | failure / no-auto-migration / driftをfalse assuranceなく証明 |
| F. コード品質・保守性 | 10 | 明快な設定、migration追加容易性、重複・過剰抽象化が少ない |
| G. 変更精度・最小性 | 10 | 必要十分なproject/package/diffである |
| H. エラー・リスク管理 | 5 | timeout、exit status、credential、CI、failure propagation |
| **Total** | **100** | |

## A. Issue達成度 /25

主な確認:

- exact EF / Npgsql / dotnet-ef version pin
- `BankDbContext`
- `InitialFoundation` empty migration
- dedicated one-shot Migrator
- design-time factory
- migration history
- API no-auto-migration
- pending model check
- idempotent SQL generation
- schema-owner migration procedure

目安:

- 23–25: ACを実証付きでほぼ完全達成
- 18–22: 小さな不足はあるがMajorなし
- 10–17: 核心ACに部分欠落
- 0–9: FND-04基盤として成立しない

## B. 正しさ・実行可能性 /15

- clean PostgreSQL `0 -> InitialFoundation`
- rerun時に既適用migrationを壊さない
- failure時non-zero
- 60-second bounded execution
- normal API startupでDBを変更しない
- design-time / runtime provider consistency

## C. Scope遵守・指示追従 /15

満点条件:

- business table / trigger / sequence / business constraintなし
- Compose / health / production deployなし
- API startup auto-migrationなし
- `EnsureCreated`によるschema evolutionなし
- unrelated refactorなし

business schemaを先取りした場合は原則Major。

## D. 設計・Repository適合性 /10

- DbContext / migrations / factory: Infrastructure
- one-shot entry point: Migrator
- APIとMigratorの責任分離
- canonical connection keyを一貫利用
- dependency directionが既存modular monolithへ整合
- FND-05がMigratorを再利用できる

## E. テスト・検証品質 /10

高評価となる証拠:

- FND-03 real PostgreSQL fixtureの再利用
- migration history実DB inspection
- bad connection / migration failure
- startup before/after evidence
- actual pending-model mechanism
- negative drift probeが本当にFAIL
- idempotent SQL生成の実行証拠

低評価:

- source grepだけでruntime contractを証明
- constant同士の比較
- fake/test-only pathだけでproduction wiringを証明
- exceptionをcatchして「想定どおり」とするだけ

## F. コード品質・保守性 /10

- migration追加手順が明確
- configuration責務が一箇所に集約
- fail-closed behaviorが読める
- duplicate DI / factory configが不必要に分岐しない
- custom frameworkや過剰なabstractionを持ち込まない

## G. 変更精度・最小性 /10

- FND-04に必要なpackage / project / docsだけ
- empty baselineのためのdummy business entityなし
- test helperがproduction architectureを歪めない
- repository-wide unrelated formatting / cleanupなし

## H. エラー・リスク管理 /5

- 60秒timeout / cancellation
- non-zero exit on failure
- password / connection string非保存
- command-line secret非展開
- warnings / CI / unverified事項を隠さない

## Finding semantics

- Blocker: Koo判断が必要、正本矛盾、実装継続不能等
- Major: merge前修正必須。AC核心、failure safety、scope、false assurance等
- Minor: Issue Closeは妨げないが品質・証拠に有意な不足
- Nit: 非blockingで低影響

## H0 / H1 metrics

各candidateについて別途記録する。

```text
H0 Score
H1 Score
Self-Review Gain = H1 - H0
SR valid findings
SR false positives
accepted / rejected
fixed / unfixed
H1-introduced regressions
H0 duration
SR duration
H1 fix duration
```

H0からH1へ点数が上がらないこと自体を罰しない。H0が既に高品質の場合があるため、H1 absolute qualityとSR precision/recallを分けて読む。

## Speed / practical score

Coding Scoreとは分離する。親methodologyのQuality / Time IndexとPractical Scoreを使用してよい。

Formal Self-Reviewの追加時間はimplementation timeへ黙って合算せず、H0 / SR / H1に分離する。
