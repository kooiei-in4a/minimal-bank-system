# FND-05 Project Rule Catalog

Revision: `fnd05-project-rules-v1`

目的は、曖昧な「きれいに実装する」ではなく、何をどこへ書き、何を書かないかを事前に固定することである。

各ruleは次の形式で判定する。

```text
RULE-ID: PASS / FAIL / NOT APPLICABLE
Evidence: path / command / runtime observation
```

## 1. Governance / traceability

### RULE-GOV-001 — Authority order

**MUST**

- Approved specification → Accepted ADR → Issue #43 → code/test → PRの順で従う。

**MUST NOT**

- benchmark文書やPR説明だけでADR / Issueを変更する。

**Evidence**

- PRのAuthority section
- changed-file traceability

**Primary owner**: Luna Light Review

### RULE-GOV-002 — One target Issue

**MUST**

- 変更をIssue #43へ追跡可能にする。

**MUST NOT**

- FND-06、business feature、backupを同じPRへ混ぜる。

**Primary owner**: Luna Light Review

## 2. File and responsibility placement

### RULE-PLACE-001 — Canonical Compose file

**MUST**

- canonical Compose definitionをrepository rootの`compose.yaml`へ置く。

**MUST NOT**

- 複数の同等Compose fileを正本として並立させる。
- `docker-compose.yml`と`compose.yaml`を重複管理する。

**Primary owner**: Composer Light Review

### RULE-PLACE-002 — Dockerfiles

**MUST**

- API Dockerfileを`src/MinimalBankSystem.Api/Dockerfile`へ置く。
- Migrator Dockerfileを`src/MinimalBankSystem.Migrator/Dockerfile`へ置く。
- repository rootの`.dockerignore`を共通build contextへ使用する。

**MUST NOT**

- unrelated projectへDockerfileを置く。
- test projectのDockerfileをproduction imageとして使用する。

**Primary owner**: Composer Light Review

### RULE-PLACE-003 — Operational documentation

**MUST**

- start / stop / restart / down / clean reset手順を`docs/operations/docker-compose.md`へ置く。

**MUST NOT**

- PR本文だけを運用手順の正本にする。

**Primary owner**: Composer Light Review

### RULE-PLACE-004 — Compose test assets

**MUST**

- test-only override、fixture、validatorを`tests/MinimalBankSystem.IntegrationTests/Compose/`または`tests/compose/`へ置く。
- production Composeとtest-only mutation assetを区別する。

**MUST NOT**

- failure injectionをproduction `compose.yaml`のdefault pathへ混ぜる。

**Primary owner**: Composer Light Review

### RULE-PLACE-005 — Application persistence ownership

**MUST**

- DbContext、provider、migrationはInfrastructureに残す。
- explicit applyはMigrator hostが所有する。

**MUST NOT**

- API projectへmigration実行logicを置く。
- Compose ordering logicをDomain / Applicationへ置く。

**Primary owner**: Luna Light Review

## 3. Architecture and dependency

### RULE-ARCH-001 — API startup is schema-read-only

**MUST**

- API startupはservice registrationとhost startだけを行う。

**MUST NOT**

- `Migrate` / `MigrateAsync` / `EnsureCreated` / schema DDLを呼ぶ。

**Automated check**

- source scan
- FND-04 regression tests

**Primary owner**: Luna Light Review

### RULE-ARCH-002 — Explicit one-shot Migrator

**MUST**

- FND-04 Migrator production entry pointを使用する。
- successだけexit 0とする。

**MUST NOT**

- shell wrapperでnon-zeroを0へ変える。
- `|| true`、unconditional `exit 0`、error swallowingを使う。

**Primary owner**: Composer Light Review

### RULE-ARCH-003 — No hidden orchestrator

**MUST NOT**

- API entrypointに独自wait loopとmigrationを隠す。
- sidecar、scheduler、daemonを追加する。
- Docker socket制御でservice順序を実装する。

**Correct location**

- service ordering: `compose.yaml`
- verification orchestration: integration test / validator

**Primary owner**: Sol Heavy Review

## 4. Compose authoring

### RULE-COMPOSE-001 — Compose Specification

**MUST**

- current Compose Specificationを使用する。
- `docker compose config --quiet`を通す。

**MUST NOT**

- obsolete top-level `version:`を追加する。

**Primary owner**: Static Check

### RULE-COMPOSE-002 — Explicit conditions

**MUST**

- MigratorはPostgreSQL `service_healthy`相当を待つ。
- APIはMigrator `service_completed_successfully`相当を待つ。

**MUST NOT**

- short syntax `depends_on`だけでready / successを主張する。
- `sleep N`をreadiness contractにする。

**Primary owner**: Luna Light Review

### RULE-COMPOSE-003 — No masking restart policy

**MUST NOT**

- Migratorへ`restart: always` / `unless-stopped`を設定する。
- API restart policyでmigration failureを見えなくする。

**Primary owner**: Composer Light Review

### RULE-COMPOSE-004 — No fixed container names

**MUST NOT**

- `container_name`を固定する。

**Reason**

- Compose project isolation、parallel test、cleanupを壊しやすい。

**Primary owner**: Composer Light Review

### RULE-COMPOSE-005 — Required interpolation fails closed

**MUST**

- 必須non-secret値は`${VAR:?message}`等で未設定をfail-fastする。

**MUST NOT**

- 空文字や危険なdefaultで継続する。

**Primary owner**: Composer Light Review

### RULE-COMPOSE-006 — Minimal host exposure

**MUST**

- API portを公開する場合はlocalhost bindを標準にする。

**MUST NOT**

- PostgreSQL portをhostへ公開する。
- host networkを使用する。

**Primary owner**: Composer Light Review

## 5. Image rules

### RULE-IMG-001 — Digest pin

**MUST**

- PostgreSQL imageをdigest-qualified referenceで固定する。
- DockerfileのSDK / runtime `FROM`をdigest-qualified referenceで固定する。

**MUST NOT**

- `latest`
- tag-only image reference

**Primary owner**: Static Check

### RULE-IMG-002 — Approved source

**MUST**

- approved PostgreSQL official imageとMicrosoft .NET official imageを使用する。

**MUST NOT**

- unreviewed third-party base imageを追加する。

**Primary owner**: Luna Light Review

### RULE-IMG-003 — Runtime image minimization

**MUST**

- multi-stage buildでSDKをruntime imageへ残さない。
- APIとMigratorが必要なartifactだけをcopyする。

**MUST NOT**

- repository source全体をruntime imageへcopyする。

**Primary owner**: Composer Light Review

## 6. Secret rules

### RULE-SEC-001 — No committed secret

**MUST NOT**

- password / connection string / token / private keyをcommitする。
- `.env` real valueをcommitする。

**Allowed**

- `.env.example`等にvariable nameと非secret placeholderだけを置く。

**Primary owner**: Static Check

### RULE-SEC-002 — No secret in argv

**MUST NOT**

- Compose `command:`またはentrypoint argumentへsecret valueを展開する。
- `dotnet ... --password <value>`等を使用する。

**Primary owner**: Composer Light Review

### RULE-SEC-003 — Least secret grant

**MUST**

- secretを必要なserviceだけへ付与する。

**MUST NOT**

- APIに不要なdatabase superuser secretを渡す。
- unrelated serviceへsecretを共有する。

**Primary owner**: Sol Heavy Review

### RULE-SEC-004 — Secret-safe logs

**MUST**

- sentinelでstdout / stderr / Compose logsの非露出を確認する。

**MUST NOT**

- connection string全体をerror messageへ出す。

**Primary owner**: Opus Heavy Review

## 7. Volume and lifecycle rules

### RULE-VOL-001 — Named PostgreSQL volume

**MUST**

- PostgreSQL dataにtop-level named volumeを使用する。

**MUST NOT**

- anonymous volume
- host bind mountをdata正本にする

**Primary owner**: Static Check

### RULE-VOL-002 — Reset is explicit

**MUST**

- normal stop / downでdataを保持する。
- clean resetだけがvolumeを削除する。

**MUST NOT**

- standard stop commandへ`--volumes`を混ぜる。

**Primary owner**: Composer Light Review

### RULE-LIFE-001 — Canonical restart re-evaluates migration

**MUST**

- documented restart pathでMigratorを再作成・再実行する。

**MUST NOT**

- raw API-only restartを安全なstack restartとして記載する。

**Primary owner**: Opus Heavy Review

### RULE-LIFE-002 — Cleanup verifies absence

**MUST**

- clean reset後にcontainer、network、volumeのabsenceを外部確認する。

**MUST NOT**

- command exit 0だけでcleanup完了を主張する。

**Primary owner**: Composer Light Review

## 8. Test rules

### RULE-TEST-001 — Production path evidence

**MUST**

- actual Compose projectとproduction entrypointを通す。

**MUST NOT**

- test側で似たhostを再構築しただけでproduction wiringを証明する。

**Primary owner**: Luna Light Review

### RULE-TEST-002 — External state assertion

**MUST**

- container state、exit code、timestamps、migration historyをassertする。

**MUST NOT**

- source scanまたはlog文字列だけでstartup orderingを証明する。

**Primary owner**: Opus Heavy Review

### RULE-TEST-003 — Negative test positive markers

**MUST**

- intended component / pathへ到達したmarkerをassertする。
- expected failure reason / stateをassertする。

**MUST NOT**

- `exit != 0`だけでPASSする。
- blocklist absenceだけでsafetyを主張する。

**Primary owner**: Opus Heavy Review

### RULE-TEST-004 — Mutation sensitivity

**MUST**

- applicable mandatory mutationでtarget testがREDになる。
- revert後にGREENへ戻る。
- mutation residueがない。

**Primary owner**: Heavy Review / evaluator

### RULE-TEST-005 — No production test hook

**MUST NOT**

- failure injection専用backdoorをproduction codeへ追加する。

**Allowed**

- external config
- test-only Compose override
- isolated temporary patch / mutation

**Primary owner**: Sol Heavy Review

### RULE-TEST-006 — Honest naming

**MUST**

- test名、コメント、assertion、実際の観測範囲を一致させる。

**Primary owner**: Composer Light Review

## 9. Code quality rules

### RULE-CODE-001 — Minimal scope

**MUST**

- Issue #43に必要な変更だけを行う。

**MUST NOT**

- unrelated refactor
- business abstraction
- health framework
- backup framework

**Primary owner**: Composer Light Review

### RULE-CODE-002 — No exception swallowing

**MUST NOT**

- catchしてfailureを成功へ変換する。
- cleanup failureを黙って無視する。

**Primary owner**: Composer Light Review

### RULE-CODE-003 — No speculative abstraction

**MUST NOT**

- 将来用途だけのinterface / factory / orchestration layerを追加する。

**MUST**

- 現在の3 service contractを最小構造で表現する。

**Primary owner**: Composer Light Review

### RULE-CODE-004 — Centralize contract values

**MUST**

- image reference、service name、environment key等の正本を可能な範囲で一元化する。

**MUST NOT**

- testとproductionで同じ値を独立hard-codeし、互いをtautologyで検証する。

**Primary owner**: Luna Light Review

## 10. Documentation rules

### RULE-DOC-001 — Commands are copyable

**MUST**

- documented commandをそのまま実行できる形で記載する。
- expected stateとfailure resultを記載する。

**MUST NOT**

- pseudo commandを正本手順にする。

**Primary owner**: Composer Light Review

### RULE-DOC-002 — Known limitations are explicit

**MUST**

- FND-06 health未実装
- production deployment対象外
- canonical restartの意味
- secret source前提

を明記する。

**Primary owner**: Luna Light Review

## 11. Git / CI rules

### RULE-CI-001 — Exact Head identity

**MUST**

- candidate full Head SHAとdirect-head CIを記録する。
- merge-refの場合はactual checkout SHAとBase / Headを分けて記録する。

**MUST NOT**

- merge-ref runをdirect-head runと呼ぶ。

**Primary owner**: Luna Light Review

### RULE-CI-002 — No unrelated generated files

**MUST NOT**

- secret file
- build output
- generated logs
- temporary mutation
- local Compose override

をcommitする。

**Primary owner**: Static Check

## 12. Scope rules

### RULE-SCOPE-001 — FND-06 boundary

**MUST NOT**

- `/health/live`
- `/health/ready`
- API healthcheck endpoint

を追加する。

PostgreSQL containerのreadiness healthcheckはFND-05責任として許可する。

### RULE-SCOPE-002 — No business schema

**MUST NOT**

- Customer / Account / Operator / AuditLog / Transaction等のbusiness table、migration、seed dataを追加する。

### RULE-SCOPE-003 — No backup / production deployment

**MUST NOT**

- pg_dump / pg_restore workflow
- remote registry publish
- Kubernetes / Swarm
- cloud deployment

を追加する。

## 13. Reviewer behavior

- Light reviewerはこのcatalogを網羅的に確認する。
- Heavy reviewerはこのcatalogの全件再監査をしない。
- Heavy scopeのBlocker / Major root causeへ直結するrule violationだけをHeavy findingとして扱う。
