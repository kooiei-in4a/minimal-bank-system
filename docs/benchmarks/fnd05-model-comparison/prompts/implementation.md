# FND-05 Candidate Implementation Prompt

Revision: `fnd05-implementation-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-05 Benchmark Candidate / Agent A** です。

Issue #43 `[FND-05] Docker Compose実行基盤を確立する` を、事前作成済みの独立branchへ実装してください。

この実行は1回のimplementation snapshotです。独立したFormal Self-Review / H1 phaseはありません。代わりに、以下のCompletion Checksを実装promptの一部として満たしてください。

## 0. Variable identity

実行前に次だけをcandidateごとに固定します。

```yaml
MODEL: "<EXACT_PRODUCT_LABEL>"
HARNESS: "<HARNESS>"
EFFORT: "<EXACT_EFFORT_LABEL>"
CANDIDATE_SLUG: "<SLUG>"
TARGET_BRANCH: "<PRECREATED_BRANCH>"
TARGET_PR: <PRECREATED_DRAFT_PR>
COMMON_BASE_SHA: "<LOCKED_FULL_SHA>"
ATTEMPT: 1
PROMPT_REVISION: "fnd05-implementation-v1"
```

許可candidateは次の3本だけです。

- GPT-5.6 Luna / Codex
- Claude Sonnet 5 / Claude Code
- Grok 4.5 / Cursor

OpenCodeは使用しません。

## 1. Authority

GitHub一次証拠から次を確認してください。

1. Parent Issue #3
2. WP-1 Issue #33
3. Target Issue #43
4. `AGENTS.md`
5. Accepted ADR-0001 / ADR-0008 / ADR-0009
6. `docs/benchmarks/fnd05-model-comparison/reference/assumption-ledger.md`
7. `reference/implementation-and-test-design-contract.md`
8. `reference/project-rule-catalog.md`
9. `reference/mandatory-mutations.md`

benchmark文書は上位正本を変更しません。

## 2. Independence

snapshot固定まで次を参照してはいけません。

- 他candidate branch / PR / diff / test / notes
- candidate score / ranking
- Selection / Adjudication
- Final Synthesis
- Light / Heavy review result

## 3. Gate verification

実装前に次を確認してください。

- current branch = `TARGET_BRANCH`
- merge-base = `COMMON_BASE_SHA`
- worktreeに他candidateの変更がない
- Issue #43 Issue Ready = PASS
- implementationが明示的に許可されている
- dependency #42がCOMPLETE / MERGED
- pre-run contractの`TO_LOCK`が解決済み

不一致がある場合、独自修正せず停止してください。

## 4. Task

Issue #43のScopeだけを実装してください。

最低限、次を成立させます。

- root `compose.yaml`
- PostgreSQL 18 service
- FND-04 explicit one-shot Migrator service
- API service
- PostgreSQL readiness → Migrator → APIの順序
- Migrator success後だけAPI start
- Migrator failure時のAPI non-start
- API startup no-auto-migration
- PostgreSQL named volume
- digest-pinned PostgreSQL / .NET base images
- external secret / connection injection
- start / stop / restart / down / clean reset手順
- actual Compose runtime verification
- test-only failure injection
- secret non-disclosure verification

## 5. Hard scope boundaries

実装してはいけません。

- `/health/live` / `/health/ready`
- API health endpoint
- business endpoint / business schema / seed data
- backup / restore
- monitoring / metrics / alerting
- production deployment
- Kubernetes / Swarm / cloud orchestrator
- scheduler / daemon / additional permanent service
- API startup migration
- `EnsureCreated`
- production codeへのtest-only backdoor
- Docker socket mount
- privileged / host network
- PostgreSQL host port publication
- candidate比較・score・benchmark resultの変更

## 6. Required placement

原則として次へ置いてください。

```text
compose.yaml
.dockerignore
src/MinimalBankSystem.Api/Dockerfile
src/MinimalBankSystem.Migrator/Dockerfile
docs/operations/docker-compose.md
tests/MinimalBankSystem.IntegrationTests/Compose/**
```

別配置が必要な場合、project ruleと同等以上に責務が明確である理由をPRへ記録してください。重要な配置変更をcandidate独自の新設計として行わないでください。

## 7. Implementation workflow

1. exact identity / gate / common baseを確認する。
2. Authorityとpre-run contractを読む。
3. changed-file計画とtest scenarioを作る。
4. production Compose / Dockerfile / support codeを実装する。
5. automated test / validatorを実装する。
6. 下記Completion Checksを順に実行する。
7. accepted scope内の問題だけを修正する。
8. Draft PR #`TARGET_PR`を更新する。新しいPRを作らない。
9. exact Head CIを確認する。
10. final full Head SHAをsnapshotとして固定して停止する。

### Separate self-review prohibition

- 別sessionのFormal Self-Reviewを開始しない。
- H1 branch / H1 commit / self-review artifactを作らない。
- 「自由にセルフレビューした結果」として新しいscopeを追加しない。

Completion Checksはこのimplementation executionのDefinition of Doneです。

## 8. Completion Checks

### C-01 Authority / scope

- [ ] Parent #3 / WP-1 #33 / Issue #43を確認した
- [ ] ADR-0001 / 0008 / 0009へ追跡できる
- [ ] FND-06 healthを先取りしていない
- [ ] business schema / backup / production deploymentを追加していない

### C-02 Service topology

- [ ] `postgres` / `migrator` / `api`の3 serviceで成立する
- [ ] additional permanent serviceなし
- [ ] PostgreSQL port非公開
- [ ] privileged / host network / Docker socketなし

### C-03 Startup ordering

- [ ] PostgreSQL実health後にMigrator開始
- [ ] Migrator exit 0後だけAPI開始
- [ ] short syntax `depends_on`だけに依存していない
- [ ] API started timestamp >= Migrator finished timestamp
- [ ] migration failure時にAPIは一度もstartしない

### C-04 Explicit migration

- [ ] FND-04 production Migratorを使用
- [ ] failureはnon-zero
- [ ] API startupにmigration callなし
- [ ] migration historyを外部確認
- [ ] existing volume rerunで重複なし

### C-05 Secret

- [ ] real secretをrepositoryへ保存していない
- [ ] secret valueをargvへ展開していない
- [ ] serviceごとにleast grant
- [ ] sentinelがrendered config / logs / process argsへ出ない
- [ ] missing secretはfail-fast

### C-06 Image / build

- [ ] PostgreSQL image digest固定
- [ ] .NET SDK / runtime base digest固定
- [ ] `latest`なし
- [ ] multi-stage build
- [ ] runtime imageへSDK / source不要物を残さない
- [ ] build後image IDとbase digestを記録

### C-07 Volume / lifecycle

- [ ] top-level named volume
- [ ] normal stop / downでvolume保持
- [ ] canonical restartでMigrator gate再評価
- [ ] clean resetだけがvolume削除
- [ ] reset後にcontainer / network / volume absence確認

### C-08 Test oracle

- [ ] production Compose / entrypointを通す
- [ ] container state / exit code / timestampをassert
- [ ] negative testにpath markerあり
- [ ] negative testにfailure reason / state markerあり
- [ ] `exit != 0`だけでPASSしない
- [ ] source scanだけでruntime orderingを証明しない

### C-09 Mandatory mutation readiness

- [ ] M-01〜M-10を検出可能なtest / validator構造
- [ ] mutationが無関係なsyntax / build failureで検出されない
- [ ] temporary mutation / overrideをcommitしていない

candidate実行時は全mutationの常時実行までは要求しません。ただし、Evaluatorがapplicable probeを行える構造を作ってください。

### C-10 Project rules

- [ ] `project-rule-catalog.md`を全件確認
- [ ] required placementに従う
- [ ] `container_name` / obsolete `version:`なし
- [ ] `sleep` readinessなし
- [ ] exception / exit swallowingなし
- [ ] unrelated refactor / speculative abstractionなし
- [ ] test名 / comment / assertion一致

### C-11 Evidence

- [ ] `docker compose config --quiet`
- [ ] resolved service / image / volume evidence
- [ ] restore / build 0 warnings / 0 errors
- [ ] existing non-Compose tests
- [ ] Compose clean start
- [ ] migration failure / API non-start
- [ ] existing-volume rerun
- [ ] lifecycle / clean reset
- [ ] secret sentinel
- [ ] `git diff --check`
- [ ] exact Head CI
- [ ] Unverifiedを成功扱いしていない

## 9. Minimum commands / evidence

pre-run contractで固定されたcanonical commandを使用してください。

最低限記録するもの:

```text
Docker Engine version:
Docker Compose version:
Resolved Compose config:
Resolved services:
Resolved images:
Resolved volumes:
Clean start result:
Migrator exit:
Migrator finished timestamp:
API state:
API started timestamp:
Migration history:
Failure injection result:
Secret sentinel result:
Clean reset result:
```

## 10. Duration

開始直前と終了直後を同じ方式で記録してください。

```text
STARTED_AT_LOCAL:
FINISHED_AT_LOCAL:
DURATION_MINUTES:
```

分単位の概算でよい。GitHub timestampから推測しない。

## 11. Draft PR requirements

PR本文へ次を記録してください。

- identity
- common base / full Head
- authority / scope
- changed files
- runtime design
- test design
- verification results
- exact-head CI
- known concerns
- unverified
- duration
- Light reviewer focus
- Heavy reviewer focus
- merge / Issue操作禁止

## 12. Final report

```text
## FND-05 Candidate Result

MODEL:
HARNESS:
EFFORT:
CANDIDATE_SLUG:
ATTEMPT:

BRANCH:
COMMON_BASE_SHA:
HEAD_SHA:
DRAFT_PR:
DIRECT_HEAD_CI:

CHANGED_FILES:

RUNTIME_DESIGN:
SECRET_DESIGN:
LIFECYCLE_DESIGN:
TEST_DESIGN:

COMPLETION_CHECKS:
- C-01:
- C-02:
- C-03:
- C-04:
- C-05:
- C-06:
- C-07:
- C-08:
- C-09:
- C-10:
- C-11:

VERIFICATION:
- compose config:
- restore:
- build:
- existing tests:
- clean start:
- migration failure:
- API non-start:
- rerun:
- lifecycle:
- secret sentinel:
- git diff --check:

KNOWN_CONCERNS:
UNVERIFIED:
STARTED_AT_LOCAL:
FINISHED_AT_LOCAL:
DURATION_MINUTES:

SNAPSHOT: LOCKED / NOT LOCKED
SEPARATE_FORMAL_SELF_REVIEW: NOT APPLICABLE
```

## 13. Operation permissions

- target branch以外へpushしない
- new PRを作らない
- PRをReady化しない
- mergeしない
- Issueを変更しない
- other candidateを参照しない
- branchを削除しない
