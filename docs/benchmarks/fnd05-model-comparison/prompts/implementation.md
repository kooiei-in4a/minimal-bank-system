# FND-05 Candidate Implementation Prompt

Revision: `fnd05-implementation-v2`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-05 Benchmark Candidate / Agent A** です。

Issue #43 `[FND-05] Docker Compose実行基盤を確立する` を、事前作成済みの独立branchへ実装してください。

この実行は1回のimplementation snapshotです。独立したFormal Self-Review / H1 phaseはありません。代わりに、事前定義されたCompletion Checksと一次証拠をこのexecution内で満たします。

## 0. Variable identity

```yaml
MODEL: "<EXACT_PRODUCT_LABEL>"
HARNESS: "<HARNESS>"
EFFORT: "<EXACT_EFFORT_LABEL>"
CANDIDATE_SLUG: "<SLUG>"
TARGET_BRANCH: "<PRECREATED_BRANCH>"
TARGET_PR: <PRECREATED_DRAFT_PR>
COMMON_BASE_SHA: "<LOCKED_FULL_SHA>"
RUN_REGISTRY_SHA: "<LOCKED_RUN_JSON_CONTENT_SHA256>"
ATTEMPT: 1
PROMPT_REVISION: "fnd05-implementation-v2"
```

許可candidate:

- GPT-5.6 Luna / Codex
- Claude Sonnet 5 / Claude Code
- Grok 4.5 / Cursor high

OpenCodeは使用しません。

## 1. Authority

### Product authority

1. Koo-approved product policy / approved specification
2. Accepted ADR-0001 / ADR-0008 / ADR-0009
3. Issue #43
4. `AGENTS.md`
5. locked FND-05 pre-run contracts
6. PR説明 / model self-report

### Gate / current-state evidence

- Parent Issue #3
- WP-1 Issue #33
- dependency #42
- Issue Ready
- Koo start authorization

Parent / WPはProduct authorityを上書きしません。矛盾があれば停止してください。

## 2. Required pre-run gate

実装開始前に`run.json`のexact locked stateを確認してください。

- current branch = `TARGET_BRANCH`
- merge-base = `COMMON_BASE_SHA`
- D-01〜D-08 = LOCKED with evidence
- D-06は`fnd05-mutation-determinism-v1`を満たす
- `gates.mutation_determinism_locked = true`
- Issue #43 Issue Ready = PASS
- `gates.issue_ready_pass = true`
- **`gates.koo_start_authorized = true`**
- implementation permitted = true
- dependency #42 COMPLETE / MERGED
- exact Model / Harness / Effort一致
- other candidate outputを参照していない

1つでも満たさなければ`STOPPED — PRE-RUN GATE NOT SATISFIED`として終了し、実装しません。

## 3. Independence

snapshot固定まで次を参照しない。

- other candidate branch / PR / diff / test / notes
- candidate score / ranking
- Selection / Adjudication
- Final Synthesis
- Light / Heavy review result

## 4. Task — observable contract

Issue #43のScopeだけを実装する。

必須なのはexact service name / file path / Compose conditionではなく、次のobservable behaviorである。

- PostgreSQL 18 runtime + named data volume
- FND-04 explicit Migrator execution
- PostgreSQL usable後にmigration開始
- Migrator success後だけAPI start
- Migrator failure時API never starts
- API startup no-auto-migration
- D-02でlockedされたdigest-pinned images
- D-03でlockedされたsecret / connection injection
- D-04でlockedされたlifecycle behavior / commands
- D-05でlockedされたexternal observation
- D-06でlockedされたfailure injection / mutation determinism contract
- D-07 cross-platform contract
- actual runtime verification

Dedicated `postgres` / `migrator` / `api` servicesやroot `compose.yaml`等はreference design / project conventionであり、pre-run lockでMUST化されていない限り同等経路を自動FAILにしません。

## 5. Hard scope boundaries

実装しない。

- `/health/live` / `/health/ready`等FND-06 API health
- business endpoint / schema / seed data
- backup / restore
- monitoring / metrics / alerting
- production deployment / Kubernetes / Swarm / cloud orchestrator
- scheduler / additional permanent infra
- API startup migration / `EnsureCreated`
- production codeへのtest-only backdoor
- candidate comparison / score / benchmark result変更

## 6. Implementation workflow

1. exact target / authority / gateを確認
2. D-01〜D-08 locked valuesとevidenceを読む
3. changed-file planとtest scenarioを作る
4. production execution pathを実装
5. automated test / validatorを実装
6. Completion Checksを一次証拠付きで実行
7. Scope内の問題だけ修正
8. existing Draft PRを更新。new PRを作らない
9. direct-head CIを確認
10. final full Head SHAをsnapshotとして固定し停止

### Separate self-review prohibition

- 別sessionのFormal Self-Reviewを開始しない
- H1 branch / H1 commit / self-review artifactを作らない
- 自由形式の自己正当化でscopeを追加しない

## 7. Evidence-backed Completion Checks

各Checkは`PASS`だけでなく`EVIDENCE:`を必須とする。

### C-01 Identity / authority / scope

- Product authorityとGate evidenceを分離確認
- Issue #43 Scope / Out of scope
- FND-06 / business / backup / deployment先取りなし
- exact branch / common base / Head

### C-02 Runtime ordering / failure

- PostgreSQL usable後にMigrator実行
- Migrator exit 0後だけAPI start
- Migrator non-zero時API never starts
- started-then-exitedをnever-startedと誤認しない
- D-05のexternal state evidenceあり

### C-03 Migration boundary

- FND-04 production Migratorを使用
- API startup migrationなし
- migration historyを外部確認
- existing-volume rerunでcontract維持

### C-04 Secret / image / volume

- D-02 exact image identity
- D-03 secret contract
- named PostgreSQL volume
- secret sentinel evidence
- repository / argv / required observation面で漏洩なし

### C-05 Lifecycle

- D-04 canonical validate / start / stop / restart / resetを実行
- expected resource stateをD-05で確認
- cleanup exit 0だけでabsenceを主張しない

### C-06 Test oracle

- production pathへ到達
- negative testにpath marker + failure reason/state marker
- `exit != 0`だけでPASSしない
- source scanだけでruntime orderingを証明しない
- M-01〜M-10のprotected contractを検出可能なoracleを用意
- `mutation-determinism-contract.md`のdeterministic precondition property / barrier・fixture class / expected failure signature classを満たせる設計にする

Candidateへ開示されるのはmutation ID、protected contract、observable property、deterministic precondition property、barrier / fixture class、expected / invalid failure signature classまでである。Evaluatorのexact mutation patch / exact source edit / exact injection recipeへ過学習しない。

### C-07 Project rules / code quality

- Static-owned MUST rulesの自動check結果を添付
- Composer / Luna / Heavy-owned rulesを自己採点してlockしない
- unrelated refactor / speculative abstractionなし
- `git diff --check`
- generated / mutation residueなし

### C-08 Verification / CI

- Compose/config validation
- restore / build / existing tests
- clean start
- migration failure / API non-start
- rerun / lifecycle / clean reset
- secret sentinel
- direct-head CI actual checkout SHA
- Unverifiedを成功扱いしていない

## 8. Minimum evidence record

```text
Docker Engine version:
Docker Compose version:
Locked D-01..D-08 refs:
Mutation determinism contract ref:
Resolved runtime roles:
Resolved images:
Resolved volumes:
Clean start result:
Migrator exit:
Migrator completion evidence:
API state:
API start ordering evidence:
Migration history:
Failure injection result:
Secret sentinel result:
Clean reset / resource absence:
```

## 9. Duration

```text
STARTED_AT_LOCAL:
FINISHED_AT_LOCAL:
DURATION_MINUTES:
```

分単位でよい。GitHub timestampから推測しない。

## 10. Final report

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
- C-01: PASS / FAIL
  EVIDENCE:
- C-02: PASS / FAIL
  EVIDENCE:
- C-03: PASS / FAIL
  EVIDENCE:
- C-04: PASS / FAIL
  EVIDENCE:
- C-05: PASS / FAIL
  EVIDENCE:
- C-06: PASS / FAIL
  EVIDENCE:
- C-07: PASS / FAIL
  EVIDENCE:
- C-08: PASS / FAIL
  EVIDENCE:

KNOWN_CONCERNS:
UNVERIFIED:
STARTED_AT_LOCAL:
FINISHED_AT_LOCAL:
DURATION_MINUTES:
SNAPSHOT: LOCKED / NOT_LOCKED
SEPARATE_FORMAL_SELF_REVIEW: NOT_APPLICABLE
```

## 11. Operation permissions

- target branch以外へpushしない
- new PRを作らない
- PRをReady化 / mergeしない
- Issueを変更しない
- other candidateを参照しない
- branchを削除しない
