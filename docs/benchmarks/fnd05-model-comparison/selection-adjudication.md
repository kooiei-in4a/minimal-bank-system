# FND-05 Selection / Adjudication

## INPUT_ARTIFACT_LOCK

```yaml
REPOSITORY: kooiei-in4a/minimal-bank-system
TARGET_ISSUE: 43
PRODUCT_COMMON_BASE_SHA: ee8abbb15758c1a2cfb624791482b755be578da2
CANDIDATE_COMMON_INITIAL_HEAD: 236372d2ac9547b74fe5455672f9284cd51a8b5f
PROMPT_REVISION: fnd05-selection-adjudication-v2

IMPLEMENTATION_EVALUATION:
  artifact_path: docs/benchmarks/fnd05-model-comparison/implementation-evaluation-gpt-5.6-sol-codex-xhigh-attempt-1.md
  content_sha256: 15d96bf366b4f1fe9bd766806badf5e114e45e9a62ba27bfab04e76bc20a04cd
  prompt_revision: fnd05-implementation-evaluation-v2
  run_registry_source_sha256: c2e38184aaf2a234106813bd1a19c851900a7272dc4257b96f9b139b5fed22fb
  producer_slot: implementation_evaluator:gpt-5.6-sol-codex-xhigh:attempt-1
  producer_commit_sha: 43a49e8a06c544d0810cfc3ff6de3c722ab334f9
  evaluation_branch: codex/fnd05-implementation-evaluation-gpt-5.6-sol-codex-xhigh-attempt-1
  evaluation_branch_head: e3fad3dc255e83b3ecfdd2182047ed3e6ce1b587
  artifact_byte_sha_verified: PASS
  run_json_stage_identity_verified: PASS

CANDIDATES:
  C1:
    pr: 150
    head: c3599c9bd4bc920b5c87c80148d81b8a53aa95fc
    state: OPEN_DRAFT
    score: 87
    direct_head_ci: NO
    ci_run: 31461355578
    actual_checkout: 42b09c04fb350a1805c76615837a1d6cb76747ac
    checkout_ref: refs/pull/150/merge
  C2:
    pr: 151
    head: 146ea92a4e815a5a08fe81562ef80f70f80c551b
    state: OPEN_DRAFT
    score: 88
    direct_head_ci: YES
    ci_run: 31460858572
    actual_checkout: 146ea92a4e815a5a08fe81562ef80f70f80c551b
  C3:
    pr: 152
    head: b69910dd00bca56254f3340fd7f5954da38b2814
    state: OPEN_DRAFT
    score: 74
    direct_head_ci: YES
    ci_run: 31460753661
    actual_checkout: b69910dd00bca56254f3340fd7f5954da38b2814

INPUT_LOCK_RESULT: PASS
```

`run.json.stage_artifacts.implementation_evaluation`のartifact path、content SHA256、prompt revision、candidate Head参照、producer slot、producer commitはGitHub上のlocked評価branchと一致した。`PRODUCT_COMMON_BASE_SHA`上の`run.json`実バイト列も、lock済み`RUN_REGISTRY_SHA256`と一致した。

C1のrun `31461355578`は成功しているが、Actions log上の実checkoutはcandidate HeadではなくPR merge ref `42b09c04fb350a1805c76615837a1d6cb76747ac`である。C1をdirect-head CI成功として扱わない。C2の2 jobとC3のjobは各candidate Headを直接checkoutした。

## PRIMARY_ARCHITECTURE_SOURCE

```yaml
TYPE: AUTHORITY_CURATED
WHOLE_CANDIDATE_PRIMARY: NONE
PRODUCT_AUTHORITY:
  - approved specification
  - Accepted ADR-0001
  - Accepted ADR-0008
  - Accepted ADR-0009
  - Issue #43
  - locked D-01 through D-08
STRUCTURAL_REFERENCE:
  - C1: three runtime roles, ordering evidence, canonical lifecycle evidence
  - C2: three runtime roles, secret transport, actual argv oracle, direct-head CI shape
  - C3: typed external state snapshot only
RULE: candidate codeのmerge、cherry-pick、または無批判なcopyを許可しない
```

Final Synthesisのarchitecture正本は、candidate順位ではなく上位authorityが定めるobservable contractである。3 candidateに共通するPostgreSQL usable → explicit one-shot Migrator → APIという構造はreferenceとして使えるが、candidate全体をbaseにはしない。

## ELEMENT_DECISIONS

### S-01 — Runtime roles / ordering

```text
ELEMENT: Runtime roles / ordering
OBSERVABLE_CONTRACT: PostgreSQL 18がusableになった後にFND-04 production Migratorを実行し、exit 0かつexpected migration history成立後だけAPI startを許可する。Migrator non-zero時はAPIを一度もstartしない。API通常startupはschema evolutionを行わない。
PRIMARY_SOURCE: C1の3-role runtime、external inspect/history、StartedAt >= FinishedAt ordering assertion
PARTIAL_SOURCE: C2の同等3-role runtimeとsplit validator。C3のtyped State/ExitCode/StartedAt/FinishedAt/HasEverStarted表現
REJECTED_SOURCES: candidate全体の自動採用。Compose記述だけをordering proofにする方式。C3のsession lifecycle実装
DECISION: USE_WITH_MODIFICATION
RATIONALE: 全candidateのproduction runtimeは上位contractと整合するが、test/oracleには個別gapがある。exact service名、service数、file placement、helper形はmandatory化しない。
REQUIRED_TEST: clean volumeでPostgreSQL usability、Migrator exit 0、expected history、API running、API StartedAtがMigrator FinishedAtより前でないことを確認する。failure時はMigrator non-zeroとAPI never-startを確認し、started-then-exitedを区別する。
REQUIRED_RUNTIME_EVIDENCE: docker compose ps -a --format json、docker inspect State.Status/ExitCode/StartedAt/FinishedAt、Compose project/service labels、timestamped logs、public.__EFMigrationsHistory
REQUIRED_MUTATION: M-01、M-02、M-03、M-08、M-09
SCOPE_EFFECT: ADR-0001の単一application境界を維持し、追加常設infraを導入しない。one-shot Migratorはschema適用責務であり新しいbusiness serviceではない。
```

### S-02 — Images / build

```text
ELEMENT: Images / build
OBSERVABLE_CONTRACT: linux/amd64上でD-02のPostgreSQL 18、.NET SDK 10、ASP.NET runtime 10のexact digest-qualified identityを使用し、API/Migrator production artifactsを再現可能にbuildする。
PRIMARY_SOURCE: C2のdigest-qualified multi-stage buildとdirect-head Compose CI分離
PARTIAL_SOURCE: C1のdigest-qualified API/Migrator build。C3のmulti-stage build概念
REJECTED_SOURCES: C3のruntime image内でapt-get updateによりbashを追加するexact build shape。tag-only、latest、digest再解決、unlocked package layerをimmutable proofとする方式
DECISION: USE_WITH_MODIFICATION
RATIONALE: C1/C2のbuildはD-02と整合する。multi-stageやexact Dockerfile placementはSHOULDでありMUSTへ昇格しない。C3の追加OS package layerはbase digestだけでは内容を固定できない。
REQUIRED_TEST: docker compose config --quiet、rendered configと全Dockerfileのexact digest assertion、clean build、restore/build/existing tests、resolved platform確認
REQUIRED_RUNTIME_EVIDENCE: rendered image references、build logs、resolved image identity、actual checkout SHA
REQUIRED_MUTATION: M-05
SCOPE_EFFECT: external registry publication、production deployment、追加runtime serviceを含めない。
```

### S-03 — Secret contract

```text
ELEMENT: Secret contract
OBSERVABLE_CONTRACT: host environment → Compose top-level secret(environment) → explicit per-service mounted secret fileを用い、PostgreSQLはfile reader、API/Migratorはwrapper内でconnection stringを構成してexecする。missing secretはfail-closedし、secretをrepository、rendered config、logs、configured container fields、actual process argvへ露出しない。
PRIMARY_SOURCE: C2のsentinel end-to-end exerciseとdocker top actual process argv検査
PARTIAL_SOURCE: C1/C3のmounted secret + wrapper transport、explicit grant、rendered config/logs/docker inspect検査
REJECTED_SOURCES: C1/C3のconfigured inspectだけでactual argvをproofにするoracle。C2のgit grep、config --quiet、docker top失敗を|| trueで空結果へ変換できるexact script
DECISION: USE_WITH_MODIFICATION
RATIONALE: C2だけがM-04をkillしたが、inspection command failureをPASSへ変換できる局所gapは残る。採用対象はactual argv観測propertyであり、exact scriptではない。
REQUIRED_TEST: unique sentinelを実secret経路で使用してMigrator成功/API runningを確認後、tracked repository、rendered JSON config、logs、docker inspect Env/Cmd/Entrypoint/Args、docker top actual process argvの全観測面で非露出を確認する。各検査commandのexit成功を先にassertする。
REQUIRED_RUNTIME_EVIDENCE: sentinelがend-to-endで使用されたpositive evidence、各観測commandのexit code、非露出結果、missing-secret non-zero result
REQUIRED_MUTATION: M-04
SCOPE_EFFECT: real credentialを使用・保存しない。secret literalをComposeへ固定しない。credentialをargvへ直接展開しない。
```

### S-04 — Lifecycle contract

```text
ELEMENT: Lifecycle contract
OBSERVABLE_CONTRACT: D-04 canonical validate/start/stop/restart/clean-resetを再現し、stopはnamed volumeを保持、restartはdown→upでmigration gateを再評価、clean resetはcanonical command直後にsame-project container/volume/networkが0となる。
PRIMARY_SOURCE: C1のcanonical lifecycle、volume retention、project-scoped container/volume/network residue assertion
PARTIAL_SOURCE: C2のsplit lifecycleとnamed-volume precondition。ただしnetwork absence assertionを追加する
REJECTED_SOURCES: C3のcleaned state、Start後にdirtyへ戻さないsession、contract assertion前のforce-remove、Dispose cleanup skip、cleanup exception握り潰し
DECISION: USE_WITH_MODIFICATION
RATIONALE: C1はM-10をkillし、3 resource種のabsenceを観測する。C2はM-10をkillするがnetworkを直接assertしない。C3はbaselineで反復residueを残し、force-removeでcanonical commandの欠落をmaskできる。
REQUIRED_TEST: canonical stopでvolume保持、canonical restartでMigrator gate再評価/history不変、canonical clean reset前にtarget resources存在、実行直後にcontainer=0/volume=0/network=0、次回clean start成功。test harness teardownとは別testにする。
REQUIRED_RUNTIME_EVIDENCE: Compose project labelsでscopedしたcontainer/volume/network一覧、named volume identity、各canonical commandのexit、reset前後snapshot
REQUIRED_MUTATION: M-06、M-10
SCOPE_EFFECT: contract assertion前の補正削除を禁止する。最終safety cleanupはassertion後の別phaseとしてのみ許可する。
```

### S-05 — External evidence

```text
ELEMENT: External evidence
OBSERVABLE_CONTRACT: source/Compose declarationではなく、D-05のexternal stateからruntime success/failure、ordering、history、resource identity、secret非露出、CI checkout identityを判定する。
PRIMARY_SOURCE: C1のdocker inspect/history/project-scoped residue evidence。CI identityについてはC2のdirect-head job
PARTIAL_SOURCE: C3のtyped external snapshotとNeverStarted/HasEverStarted表現。C2のdocker top actual argv evidence
REJECTED_SOURCES: C1 run 31461355578をdirect-head proofとする扱い。PR本文自己申告。command exit 0だけのproof。観測command failureを空結果へ変換する方式
DECISION: USE_WITH_MODIFICATION
RATIONALE: C1のruntime evidenceは強いがCI identityはmerge refである。C3のtyped表現は採用可能だが、そのsession/cleanup ownershipは分離する。
REQUIRED_TEST: evidence extractor自体のfailureをREDにし、success/failure/lifecycle/secret各scenarioでtypedまたはmachine-readable snapshotを保存・assertする。direct-head CIではactual checkout SHA = Final Head SHAを明示する。
REQUIRED_RUNTIME_EVIDENCE: State.Status/ExitCode/StartedAt/FinishedAt、project/service/volume labels、history、logs、docker top、container/volume/network counts、workflow actual checkout SHA
REQUIRED_MUTATION: M-01、M-02、M-04、M-08、M-09、M-10
SCOPE_EFFECT: FND-06 health endpointをevidence sourceにしない。events JSONはcorroborating onlyとする。
```

### S-06 — Failure injection

```text
ELEMENT: Failure injection
OBSERVABLE_CONTRACT: production backdoorを追加せず、unique projectとtest-only isolated fixtureで意図したMigrator runtime path/reasonへ到達させ、non-zero、API never-start、positive path/reason markerを同時に観測する。
PRIMARY_SOURCE: C1のpositive Migration failed path marker、non-zero、StartedAt zero-value assertion
PARTIAL_SOURCE: C3のpre-path failure marker assertionとtyped never-start state。ただしfixed 8-second delayを除去する
REJECTED_SOURCES: C2のnon-zero/API non-start/history emptyだけでintended path/reasonを要求しないfailure oracle。missing executable等のpre-path unrelated RED。fixed sleepだけのproof
DECISION: USE_WITH_MODIFICATION
RATIONALE: C1/C3はM-07をkillした。C2はexit 127のunrelated failureをPASSしたため、failure oracleのprimaryにはできない。
REQUIRED_TEST: intended fixtureのpreconditionとpositive path markerを先に確認し、Migrator non-zero、expected reason、API never-start、false success marker不在をassertする。settle判定はcontrolled barrier/state pollingで行う。
REQUIRED_RUNTIME_EVIDENCE: fixture identity、path/reason marker、Migrator terminal state/exit、API StartedAt/HasEverStarted、history、logs、cleanup result
REQUIRED_MUTATION: M-02、M-07、M-09
SCOPE_EFFECT: test-only override/mutation assetをproduction default pathから分離し、production codeへbypassを残さない。
```

### S-07 — Test oracle

```text
ELEMENT: Test oracle
OBSERVABLE_CONTRACT: V-01〜V-09をproduction pathとexternal stateで検証し、false GREENを防ぐ。特にAPI no-auto-migration、actual argv、positive failure path、canonical cleanup、started-then-exitedをruntimeで判定する。
PRIMARY_SOURCE: COMPOSITE — C1のordering/history/failure/lifecycle oracle + C2のdocker top/direct-head CI oracle
PARTIAL_SOURCE: C3のtyped snapshotとstate distinctionのみ
REJECTED_SOURCES: C1のM-03/M-04 gap、C2のM-03 race/M-07 gap、C3のM-03/M-04/M-10 gapとlifecycle residue。いずれのcandidate test suiteもそのまま使用しない
DECISION: USE_WITH_MODIFICATION
RATIONALE: 全candidateがELEMENT_SELECTION_ELIGIBLE=NOで、単独suiteではmandatory guardを満たさない。observable property単位のcurated oracleが必要である。
REQUIRED_TEST: V-01〜V-09に加え、migration済みDBへisolated pending migrationを置き、API process readiness/settle barrier成立後にhistory/schema不変を比較する。source scanは補助のみ。検査command failureはREDとする。
REQUIRED_RUNTIME_EVIDENCE: baseline precondition、before/after history+schema、process readiness、all secret surfaces、failure markers、canonical lifecycle snapshots、actual checkout、post-CI residue 0
REQUIRED_MUTATION: M-01〜M-10すべて
SCOPE_EFFECT: testのためにhealth/business schema/backup/production orchestrationを先取りしない。isolated pending migration fixtureはevaluator/test-onlyでproduction成果物へ残さない。
```

### S-08 — M-01〜M-10 mutation guards

```text
ELEMENT: M-01〜M-10 mutation guards
OBSERVABLE_CONTRACT: baseline GREEN → deterministic precondition PASS → controlled barrier/fixture → one defect injection → expected signatureでREDかつinvalid signature不在 → restore GREEN → cleanup/residue 0を全mutationで成立させる。
PRIMARY_SOURCE: locked mandatory-mutations.md v2 + mutation-determinism-contract.md v1 + D-06
PARTIAL_SOURCE: C1のM-07/M-10 kill、C2のM-04 kill、C3のtyped started/never-started表現
REJECTED_SOURCES: candidate固有kill率をFinal Synthesisのwaiverにすること。M-09 BLOCKEDをcandidate差へ使うこと。precondition不成立、unrelated RED、fixed timing、存在しないcleanup targetをkillとすること
DECISION: USE_WITH_MODIFICATION
RATIONALE: candidate評価のM-09は公平な共通probeを成立させられずNOT SCOREDだが、Final Synthesis mandatory setからは除外されない。各candidateでsurviveしたguardをcurated suiteで閉じる。
REQUIRED_TEST: M-01〜M-10についてlocked report schemaを満たし、PRECONDITION_RESULT、CONTROLLED_BARRIER_OR_FIXTURE、EXPECTED/OBSERVED/INVALID signature、restore、residueを記録する。
REQUIRED_RUNTIME_EVIDENCE: mutationごとのmachine-readable precondition、injection artifact ref、target runtime state、expected reason、invalid reason absence、git/Compose/resource residue 0
REQUIRED_MUTATION: M-01〜M-10 mandatory。M-09もFinal Synthesisでdeterministic fixtureを成立させる。成立しない場合はBLOCKEDでありLOCKED/GREENとして扱わない
SCOPE_EFFECT: exact evaluator patchはcandidate/production成果物へ持ち込まず、mutation assetsをtemporary isolated worktree/projectへ限定する。
```

### S-09 — Scope / Out of scope

```text
ELEMENT: Scope / Out of scope
OBSERVABLE_CONTRACT: Issue #43が所有するCompose runtime、PostgreSQL、explicit Migrator、API、named volume、digest、secret injection、ordering、failure gating、lifecycleだけを実装する。
PRIMARY_SOURCE: approved specification、Accepted ADR-0001/0008/0009、Issue #43、AGENTS.md
PARTIAL_SOURCE: 3 candidateのscope discipline。いずれもruntime参考に限定する
REJECTED_SOURCES: health endpoint、business endpoint/schema/data、backup/restore、monitoring、production deployment、scheduler/orchestrator、Kubernetes/Swarm、zero-downtime、unrelated refactor。unlocked exact service name/file placement/Compose shapeのMUST化
DECISION: USE
RATIONALE: Scopeはcandidate順位で選ばず上位authorityから固定する。仕様は物理schema/migration方式をADRへ委ね、ADR-0009とIssue #43がexplicit migration pathを定める。
REQUIRED_TEST: product common base/current mainからのchanged-file scope scan、business/health/backup/orchestrator追加不在、test-only fixture隔離、git diff --check
REQUIRED_RUNTIME_EVIDENCE: resolved service/resource inventoryがFND-05 rolesに限定されること、no committed secret、no temporary mutation artifacts
REQUIRED_MUTATION: M-01〜M-10 assetsがproduction default path・final diffへ残らないことを各restore/residue checkで確認
SCOPE_EFFECT: FND-06以降を先取りしない。exact placement/shapeはD-01〜D-08とobservable contractの範囲でFinal Synthesis Authorに委ねる。
```

## REJECT_PATTERNS

```yaml
RP-01:
  ROOT_CAUSE: scoreまたはcandidate全体をarchitecture authorityとして扱う
  REJECT: C2全体採用、C1全体採用、C3低得点を理由とする全要素破棄
RP-02:
  ROOT_CAUSE: runtime contractをsource/config declarationだけでproofにする
  REJECT: source scanだけのAPI no-auto-migration、Compose depends_on記述だけのordering proof
RP-03:
  ROOT_CAUSE: observation command failureを非露出・absenceへ変換する
  REJECT: git grep、docker top、docker inspect、compose ps/config等の失敗を|| trueや空結果でPASSにする
RP-04:
  ROOT_CAUSE: configured commandとactual processを同一視する
  REJECT: docker inspect Args/Cmdだけでsecret argv非露出を証明する
RP-05:
  ROOT_CAUSE: unrelated REDをintended failureとして受理する
  REJECT: positive path/reason markerなしのnegative test、pre-path executable/build/CLI failure
RP-06:
  ROOT_CAUSE: lifecycle contractとsafety cleanupを混同する
  REJECT: canonical assertion前のforce-remove、pre-clean後cleaned=trueのままStart、Dispose cleanup skip、cleanup exception握り潰し
RP-07:
  ROOT_CAUSE: timingをstate barrierの代用にする
  REJECT: immediate post-restart comparison、fixed sleepだけのnever-start/readiness/mutation proof
RP-08:
  ROOT_CAUSE: incomplete resource inventory
  REJECT: clean resetでcontainer/volumeだけを見てnetwork absenceを確認しない
RP-09:
  ROOT_CAUSE: CI metadata上のHead関連付けを実checkout identityと誤認する
  REJECT: PR merge ref successをdirect-head proofとする
RP-10:
  ROOT_CAUSE: D-02で固定されないmutable package layerをimage digest proofへ含める
  REJECT: runtime apt-get update結果をbase image digestだけで再現可能と扱う
RP-11:
  ROOT_CAUSE: implementation preferenceを未承認MUSTへ昇格する
  REJECT: exact service名、service数、file placement、helper言語、Compose condition形の自動標準化
```

## FINAL_SYNTHESIS_REQUIRED_GUARDS

1. **Curated implementation** — Final Synthesis開始時のcurrent `main`から新規構築する。candidate branch mergeとcandidate commit cherry-pickを禁止する。
2. **API no-auto-migration** — migration済みDBへisolated pending migrationを用意する。API起動後にprocess readiness/settle barrierを成立させ、その後のmigration historyとschema/table stateがbeforeから不変であることを確認する。source scanだけをproofにしない。
3. **Secret argv** — unique sentinelを実secret経路で使用し、repository、rendered config、logs、docker inspect、docker top actual process argvの全てで非露出を確認する。各検査command失敗をPASSへ変換しない。
4. **Failure path** — Migrator failure時にnon-zero exit、API never-start、intended path/reason positive markerを必須とし、unrelated REDを成功証拠にしない。
5. **Clean reset** — canonical `docker compose -p minimal-bank-system-fnd05 down --volumes --remove-orphans`直後、補正削除前にsame-project container=0、volume=0、network=0を外部assertする。test harness最終cleanupとcontract assertionを分離する。
6. **Test session lifecycle** — sessionはStart後にdirty/cleanup-requiredとなり、success/failureを問わずDisposeでunique projectをcleanupする。pre-clean完了をpost-start clean状態として保持しない。
7. **CI identity** — direct-head CIでactual checkout SHA = Final Head SHAを明示する。PR merge refをdirect-head proofにしない。CI/test後、FND-05 projectのcontainer/volume/network residue 0を確認する。
8. **Deterministic mutation** — fixed sleepや偶然timingだけをproofにせず、precondition、controlled barrier/fixture、expected signature、invalid signature absence、restore GREEN、residue 0を維持する。

## MANDATORY_MUTATIONS

```yaml
M-01:
  GUARD: controlled incomplete-Migrator barrier下でordering weakenを検出
  EXPECTED: Migrator successful completion前のAPI startをexternal stateでRED
M-02:
  GUARD: intended Migrator failureのnon-zero-to-zero maskingを検出
  EXPECTED: expected non-zero不一致とAPI startをRED
M-03:
  GUARD: isolated pending migration + API readiness/settle barrier
  EXPECTED: baseline不変、mutated APIのhistory/schema deltaでRED。source scan REDはinvalid
M-04:
  GUARD: sentinelをactual process argvへ露出
  EXPECTED: docker topでRED。observation command failureはinvalid
M-05:
  GUARD: D-02 digest-qualified imageをtag-onlyへ変更
  EXPECTED: static/resolved image identityでRED
M-06:
  GUARD: named PostgreSQL volumeをanonymous/bindへ置換
  EXPECTED: rendered configまたはactual volume identityでRED
M-07:
  CLASS: ORACLE_META_DEFECT
  GUARD: intended path前のunrelated failure
  EXPECTED: positive path/reason marker欠落でRED。exit non-zeroだけはPASSにしない
M-08:
  GUARD: unchanged oracleのままMigrator exit 0/no applyを注入
  EXPECTED: expected history absenceでRED、Migrator exitは0
M-09:
  GUARD: running baselineからAPI start-then-exitをdeterministically注入
  EXPECTED: success-path running assertionでREDし、never-startedと区別
  CANDIDATE_EVALUATION: BLOCKED_NOT_SCORED
  FINAL_SYNTHESIS: MANDATORY_NOT_WAIVED
M-10:
  GUARD: existence確認済みsame-project resourceに対応するcanonical cleanup責任を弱める
  EXPECTED: 補正削除前のproject-scoped residue assertionでRED
COMMON:
  - baseline GREEN
  - deterministic precondition PASS
  - one mutation at a time
  - expected failure signature matched
  - invalid failure signature absent
  - restore GREEN
  - cleanup/residue 0
```

## FINAL_SYNTHESIS_AUTHOR_CONSTRAINTS

```yaml
AUTHOR_IDENTITY:
  model: GPT-5.6 Terra
  harness: Codex
  effort: xHigh
  fresh_context_required: true
  unavailable_action: STOP_AND_RELOCK

BASE:
  source: current main at Final Synthesis start
  candidate_merge: PROHIBITED
  candidate_cherry_pick: PROHIBITED
  candidate_whole_as_base: PROHIBITED

AUTHORITY:
  order:
    - approved specification
    - Accepted ADR-0001/0008/0009
    - Issue #43
    - AGENTS.md
    - D-01 through D-08 and locked FND-05 contracts
    - this Selection artifact
  new_unapproved_decision: STOP — NEW DECISION REQUIRES LOCK

IMPLEMENTATION:
  - observable contractを再構成し、candidate固有workaroundを無批判に移植しない
  - exact service名、file placement、Compose shapeを新しいMUSTにしない
  - test-only fixtureとmutation assetをproduction default pathから隔離する
  - health/business/backup/production orchestrationを追加しない

VERIFICATION:
  - V-01 through V-09
  - static gate PASS
  - M-01 through M-10 deterministic mandatory execution
  - direct-head CI with actual checkout SHA proof
  - post-test container/volume/network residue 0
  - git diff --check and no temporary artifacts

STOP_AFTER_SELECTION:
  final_synthesis_branch_create: NO
  final_synthesis_implementation: NO
  light_review: NO
  heavy_review: NO
  issue_43_close: NO
```

## CANDIDATE_MERGE

```yaml
CANDIDATE_MERGE: PROHIBITED
```

## CANDIDATE_CHERRY_PICK

```yaml
CANDIDATE_CHERRY_PICK: PROHIBITED
```

## ARTIFACT_LOCK

このartifact自身のSHA256と、このartifactを導入するproducer commit SHAを本文へ実値で埋め込むとself-referenceになるため、実値は`run.json.stage_artifacts.selection_adjudication`を正本とする。

```yaml
stage: selection_adjudication
artifact_path: docs/benchmarks/fnd05-model-comparison/selection-adjudication.md
content_sha256: EXTERNAL_LOCK_IN_RUN_JSON
prompt_revision: fnd05-selection-adjudication-v2
target_head_sha: e3fad3dc255e83b3ecfdd2182047ed3e6ce1b587
candidate_head_shas:
  C1: c3599c9bd4bc920b5c87c80148d81b8a53aa95fc
  C2: 146ea92a4e815a5a08fe81562ef80f70f80c551b
  C3: b69910dd00bca56254f3340fd7f5954da38b2814
source_artifact_refs:
  - docs/benchmarks/fnd05-model-comparison/implementation-evaluation-gpt-5.6-sol-codex-xhigh-attempt-1.md@sha256:15d96bf366b4f1fe9bd766806badf5e114e45e9a62ba27bfab04e76bc20a04cd
  - run.json@ee8abbb15758c1a2cfb624791482b755be578da2#sha256:c2e38184aaf2a234106813bd1a19c851900a7272dc4257b96f9b139b5fed22fb
  - issue:3
  - issue:43
  - pr:150@c3599c9bd4bc920b5c87c80148d81b8a53aa95fc
  - pr:151@146ea92a4e815a5a08fe81562ef80f70f80c551b
  - pr:152@b69910dd00bca56254f3340fd7f5954da38b2814
  - github-actions-run:31461355578@checkout:42b09c04fb350a1805c76615837a1d6cb76747ac
  - github-actions-run:31460858572@checkout:146ea92a4e815a5a08fe81562ef80f70f80c551b
  - github-actions-run:31460753661@checkout:b69910dd00bca56254f3340fd7f5954da38b2814
producer_slot: technical_selection_lead
producer_commit_sha: EXTERNAL_LOCK_IN_RUN_JSON
```

```yaml
STATUS: LOCKED

OPERATION_CONFIRMATION:
  candidate_branch_changed: NO
  candidate_pr_changed: NO
  main_changed: NO
  issue_changed: NO
  final_synthesis_started: NO
```

ここで停止する。Final Synthesis、Light Review、Heavy Review、candidate変更、candidate merge/cherry-pick、main変更、Issue #43 closeへ進まない。
