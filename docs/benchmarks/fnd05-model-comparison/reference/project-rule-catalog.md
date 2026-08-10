# FND-05 Project Rule Catalog

Revision: `fnd05-project-rules-v2`

目的は、AI実装が守るべきProject Ruleを具体化しつつ、Issue #43やADRが許す実装自由度を未承認のMUSTで狭めないことである。

## 1. Rule levels

- `MUST / MUST NOT`: 上位正本またはpre-run lockで確定した必須contract
- `SHOULD / SHOULD NOT`: project convention / quality preference。違反だけでMajorにしない
- `TO_LOCK`: D-01〜D-08の未確定事項。candidate開始前に共通化する

Primary ownerが完全判定する。他stageはその結果をconsumeし、Blocker / Major root cause候補だけをescalateする。

```text
RULE-ID:
LEVEL:
OWNER:
EVIDENCE:
RESULT: PASS / FAIL / N/A / ADVISORY
```

## 2. Governance / scope

### RULE-GOV-001 — Product authority

LEVEL: MUST  
OWNER: Luna Light Review

```text
Koo-approved policy / specification
→ Accepted ADR
→ Issue #43
→ AGENTS.md
→ locked FND-05 contracts
```

Parent #3 / WP-1 #33はGate evidenceでありProduct authorityを上書きしない。

### RULE-GOV-002 — Issue #43 scope only

LEVEL: MUST  
OWNER: Luna Light Review

FND-06 health、business schema/data、backup/restore、monitoring、production deployment、scheduler等を先取りしない。

## 3. Architecture / ordering

### RULE-ARCH-001 — API startup does not evolve schema

LEVEL: MUST  
OWNER: Luna Light Review

API startupで`Migrate` / `MigrateAsync` / `EnsureCreated` / startup DDLを実行しない。

### RULE-ARCH-002 — Explicit Migrator owns migration apply

LEVEL: MUST  
OWNER: Composer Light Review

FND-04 Migrator production pathを使用し、failureはnon-zero、successだけexit 0。

### RULE-ORDER-001 — PostgreSQL usable before migration

LEVEL: MUST  
OWNER: Luna Light Review

Containerが単にrunningであることだけをready証拠にしない。Exact mechanismはD-01等のlockに従う。

### RULE-ORDER-002 — API starts only after Migrator success

LEVEL: MUST  
OWNER: Luna Light Review

Migrator success前またはnon-zero failure後にAPIがstartし得る設計を許可しない。

`service_completed_successfully`等は実装手段であり、pre-run lockなしに唯一のMUST mechanismとしない。

### RULE-ORDER-003 — No fixed sleep as readiness proof

LEVEL: MUST NOT  
OWNER: Composer Light Review

固定時間待ちだけでreadiness / orderingを証明しない。

## 4. Placement conventions

次はpre-runで別途lockしない限りSHOULDである。

### RULE-PLACE-001

LEVEL: SHOULD  
OWNER: Composer Light Review

Repository root `compose.yaml`をreference conventionとする。Equivalent canonical placementはownershipが明確なら自動FAILにしない。

### RULE-PLACE-002

LEVEL: SHOULD  
OWNER: Composer Light Review

API / Migrator Dockerfileを各project近傍へ置くことを推奨する。Exact pathは独立ACではない。

### RULE-PLACE-003

LEVEL: SHOULD  
OWNER: Composer Light Review

運用手順はrepository内docsへ残し、PR本文だけを唯一の正本にしない。

### RULE-PLACE-004

LEVEL: MUST  
OWNER: Composer Light Review

Test-only override / mutation assetをproduction default pathから分離する。

## 5. Image / volume / secret

### RULE-IMG-001 — Exact digest identity

LEVEL: MUST  
OWNER: Static Gate

D-02でlockしたPostgreSQL / .NET image identityをdigest-qualified referenceで使用する。

### RULE-VOL-001 — Named PostgreSQL volume

LEVEL: MUST  
OWNER: Static Gate

PostgreSQL dataはnamed volumeを使用する。

### RULE-SEC-001 — No committed secret

LEVEL: MUST NOT  
OWNER: Static Gate

Real credentialをrepositoryへ保存しない。

### RULE-SEC-002 — No secret value in process arguments

LEVEL: MUST NOT  
OWNER: Composer Light Review

Secret valueをcommand-line argumentへ直接展開しない。

### RULE-SEC-003 — Required grant only

LEVEL: MUST  
OWNER: Sol Heavy Review

D-03でlockした方式に従い、必要なprocessだけへ必要なcredentialを渡す。

### RULE-SEC-004 — Secret-safe runtime evidence

LEVEL: MUST  
OWNER: Opus Heavy Review

Test sentinelでD-03 / D-05が定めた観測面の非露出を確認する。

## 6. Lifecycle

### RULE-LIFE-001 — Lifecycle follows D-04

LEVEL: TO_LOCK  
OWNER: Opus Heavy Review

Start / stop / restart / clean resetのexact commandとmigration-gate semanticsはD-04 lock前にMUST化しない。

### RULE-LIFE-002 — Cleanup uses external state

LEVEL: MUST  
OWNER: Composer Light Review

Cleanup commandのexit 0だけで完了とせず、D-05で定義したproject resourceのabsenceを確認する。

## 7. Test oracle

### RULE-TEST-001 — Production-path evidence

LEVEL: MUST  
OWNER: Luna Light Review

Actual production Compose / entrypoint相当のpathを通す。

### RULE-TEST-002 — External-state assertion

LEVEL: MUST  
OWNER: Opus Heavy Review

D-05でlockしたstate / exit / ordering / migration historyをassertする。Source scanだけでruntime orderingを証明しない。

### RULE-TEST-003 — Negative-test positive markers

LEVEL: MUST  
OWNER: Opus Heavy Review

Intended path markerとexpected failure reason / state markerを持つ。`exit != 0`だけでPASSしない。

### RULE-TEST-004 — Mutation sensitivity

LEVEL: MUST  
OWNER: Evaluator / Heavy Review

Applicable mutationでRED、restore後GREEN、residue 0。

### RULE-TEST-005 — No production test backdoor

LEVEL: MUST NOT  
OWNER: Sol Heavy Review

Failure injection専用のproduction backdoorを追加しない。

### RULE-TEST-006 — Honest test description

LEVEL: MUST  
OWNER: Composer Light Review

Test名・コメント・assertion・観測範囲を一致させる。

## 8. Code quality

### RULE-CODE-001 — Minimal Issue scope

LEVEL: MUST  
OWNER: Composer Light Review

Unrelated refactor / feature追加を混ぜない。

### RULE-CODE-002 — Do not mask failures

LEVEL: MUST NOT  
OWNER: Composer Light Review

Exception / exit / cleanup failureを成功へ変換しない。

### RULE-CODE-003 — Avoid speculative abstraction

LEVEL: SHOULD NOT  
OWNER: Composer Light Review

将来用途だけのabstractionを追加しない。

### RULE-CODE-004 — Avoid tautological evidence

LEVEL: MUST NOT  
OWNER: Luna Light Review

Productionとtestで同じ値を独立hard-codeし、constant同士の比較でcontractを証明したことにしない。

## 9. Documentation / CI

### RULE-DOC-001 — Copyable canonical commands

LEVEL: MUST  
OWNER: Composer Light Review

D-04でlockしたcommandをcopyableに記録し、expected success / failure stateを併記する。

### RULE-DOC-002 — Known boundaries are explicit

LEVEL: MUST  
OWNER: Luna Light Review

FND-06 health未実装、production deployment対象外、D-03 / D-04の前提を正確に記載する。

### RULE-CI-001 — Exact Head identity

LEVEL: MUST  
OWNER: Luna Light Review

Direct-headとmerge-refを区別し、actual checkout SHAを記録する。

### RULE-CI-002 — No temporary residue

LEVEL: MUST NOT  
OWNER: Static Gate

Generated logs、temporary mutation / override、secret material等をcommitしない。

## 10. Hardening preferences

次はSHOULD / SHOULD NOT。上位security contractやpre-run lockがない限り、単独違反でMajorにしない。

- unnecessary host port exposureを避ける
- host network / privileged execution / Docker socket mountを避ける
- runtime imageを必要以上に大きくしない
- obsolete / conflicting Compose definitionを避ける
- fixed `container_name`等、project isolationを損ねる設定を避ける

## 11. Reviewer behavior

- Primary ownerがruleを完全判定する。
- L1 ComposerはComposer-owned ruleだけを全件判定する。
- S0 / Luna / Heavy-owned ruleをL1で再採点しない。
- 他owner領域でBlocker / Major root cause候補を発見したら`ESCALATION`する。
- Heavy reviewerはcatalog全件を再監査しない。
- `SHOULD / SHOULD NOT`違反は、具体的な実害なしにMajorへ昇格しない。
