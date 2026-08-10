# FND-05 Implementation and Test Design Contract

Revision: `fnd05-design-contract-v2`

Status: **PRE-RUN DRAFT / IMPLEMENTATION PROHIBITED**

## 1. Authority

### Product authority

1. Kooが承認した製品方針・承認済み仕様
2. Accepted ADR-0001 / ADR-0008 / ADR-0009
3. Issue #43
4. `AGENTS.md`
5. 本contract

本contractは上位正本を上書きしない。矛盾した場合はcandidate実行を停止する。

### Gate / current-state evidence

Parent Issue #3、WP-1 Issue #33、dependency #42、Issue Ready、Koo start authorizationは進行状態の証拠であり、製品仕様・ADR・Issue #43の意味を変更しない。

## 2. Purpose — observable contract

FND-05で必ず成立させる外部挙動は次である。

```text
PostgreSQL becomes usable
  ↓
FND-04 explicit Migrator execution
  ├─ success / exit 0
  │    ↓
  │  API start is permitted
  └─ failure / non-zero
       ↓
     API must not start
```

APIの通常startup pathはmigrationを実行しない。

## 3. Runtime roles and implementation freedom

Issue #43が要求するruntime roleは次の3つである。

- PostgreSQL 18 runtime with named data volume
- FND-04 explicit Migrator execution before API start permission
- normal ASP.NET Core API runtime

専用の`postgres` / `migrator` / `api` 3-service Compose構成は**reference design**として許可するが、Issue #43は「one-shot migrator serviceまたは同等のCompose正本経路」を許可している。

したがって、pre-runで別途Kooが固定しない限り、次は独立Acceptance Criteriaではない。

- exact service count / service name
- exact `depends_on` condition syntax
- exact file path / Dockerfile path
- exact wrapper / helper implementation

どの方式でも、Section 2のobservable contract、Scope / Out of scope、required evidenceを同じ基準で満たす必要がある。

追加の常設infra（Redis、broker、monitoring、backup service、scheduler、production orchestrator等）はIssue #43のScope外であり禁止する。

## 4. Startup contract

### 4.1 PostgreSQL readiness

**MUST**

- PostgreSQL 18を使用する。
- PostgreSQL dataはnamed volumeで保持する。
- Migrator開始前に、PostgreSQLが実際に利用可能であることを判定する。
- containerが単に`running`であることだけをreadiness証拠にしない。

`healthcheck` + `service_healthy`は有力な実装手段だが、exact mechanismはD-01 / candidate-common lockで決めるまでMUSTとしない。

### 4.2 Migrator execution

**MUST**

- FND-04の`MinimalBankSystem.Migrator` production pathを使用する。
- API hostをmigration実行主体にしない。
- failureを握り潰さずnon-zeroで終了する。
- successだけをexit 0とする。

### 4.3 API start permission

**MUST**

- Migrator成功を確認した後だけAPI startを許可する。
- migration failure / timeout / invalid credential / invalid history等でMigratorがnon-zeroの場合、APIを開始しない。
- API startupで`Migrate`、`MigrateAsync`、`EnsureCreated`、ad-hoc DDLを実行しない。

`service_completed_successfully`は有力な実装手段だが、Compose fileの記述だけをAcceptance Criteriaの証拠にしない。

## 5. External observation contract

FND-06 health endpointを先取りしない。

FND-05で外部から確認すべき**観測項目**は次である。

- Migrator exit code
- Migratorが完了したことを示す時刻または順序証拠
- API container / processの状態
- APIが開始したことを示す時刻または順序証拠
- PostgreSQL migration history
- success / failure時の必要ログ
- Compose project / resource identity

これらを取得するexact command / field / toolはD-05でlocal / CI共通方式としてlockする。

### Success path

- PostgreSQL usable
- Migrator exit 0
- expected migration historyが存在
- APIはMigrator成功後にrunning
- rerunでmigration historyが不正に重複しない

### Failure path

- Migrator non-zero
- APIは一度もstartしない
- 「startして即exit」を「非起動」と誤認しない
- failureをsuccess logへ変換しない
- secret sentinelをlogs等へ出さない

## 6. Image contract

**MUST**

- PostgreSQLはapproved major内のdigest-qualified imageを使用する。
- .NET base imageもapproved major内でdigest-qualified referenceを使用する。
- `latest`やtag-onlyをimmutable evidenceとして扱わない。

Exact PostgreSQL / .NET image identityはD-02でofficial sourceからlockする。

Multi-stage buildやruntime image minimizationは推奨するが、上位正本またはpre-run lockが要求しない限り、それ自体をcandidateのmerge-blocking ACにしない。

## 7. Secret and connection configuration contract

### 7.1 Required security properties

**MUST**

- password、connection string、token等をrepositoryへ保存しない。
- secret valueをcommand-line argumentへ直接展開しない。
- secretを必要なservice / processにだけ渡す。
- missing required secretはfail-closedする。
- test sentinelを用い、repository diff、rendered config、process args、logs等の必要観測面で漏洩しないことを確認する。

### 7.2 D-03 remains open

Secret source / reader implementationはD-03でlockする。

候補としてCompose secret、environment、protected password file等があり得るが、pre-run lock前に特定方式をpreferred answerとしてcandidateへ与えない。

## 8. Network / privilege hardening

次は**project preference / hardening guidance**であり、Issue #43の独立ACではない。採用する場合は全candidateへ同じpre-run ruleとしてlockする。

- PostgreSQL host portを不要に公開しない
- API公開は必要最小限にする
- `privileged: true`、host network、Docker socket mountを避ける
- host root / repository全体の不要mountを避ける
- runtime privilegeを必要最小限にする

これらが上位正本のsecurity contract違反へ直結する場合はBlocker / Majorになり得る。

## 9. Volume contract

**MUST**

- PostgreSQL dataはnamed volumeを使用する。
- normal stop / down相当ではdata保持が可能である。
- clean resetはdata volumeを明示的に削除できる。
- clean reset後の次回startはclean databaseからmigrationを適用できる。

Anonymous volumeや一時bindをdatabase正本にしない。

## 10. Lifecycle contract and D-04

Issue #43が必須とするのは、少なくともstart / stop / clean resetが再現可能であること。

Verificationではrestartも確認対象であるため、D-04で次を同時にlockする。

- canonical command strings
- stop / start / restart時にmigration gateをどう再評価するか
- down-with-data-retentionの意味
- clean-resetの意味
- cleanup完了を外部状態でどう確認するか

D-04がlockされるまで、特定restart commandや「必ずMigratorを再作成する」等をcandidateのMUSTとして先取りしない。

## 11. Failure injection contract and D-06

**MUST**

- production codeへtest-only bypass / backdoorを追加しない。
- failure pathへ到達する前の無関係なbuild / syntax failureを有効証拠にしない。
- real credentialをmutationに使用しない。

Exact failure injection mechanismはD-06でlockする。

候補例としてinvalid credential、unreachable endpoint、malformed migration history、test-only Compose override等があるが、candidate開始前に共通条件を固定する。

## 12. Required verification scenarios

### V-01 Static configuration validation

- canonical Compose/configurationのvalidation成功
- required runtime rolesが解決される
- named volumeを確認
- image pinningを確認
- secret valueが不適切にrenderされない

### V-02 Clean start

- clean volume
- PostgreSQL usable
- Migrator exit 0
- expected migration history
- APIがMigrator success後にrunning

### V-03 Migration failure

- intended migration failureへ到達
- Migrator non-zero
- API never starts
- secret sentinel absent
- verification command自体もfailureをsuccessへ変換しない

### V-04 Existing-volume rerun

- migration historyが不正に重複しない
- Migrator rerunのcontractを満たす
- data volume identityを保持

### V-05 Lifecycle

D-04でlockしたstart / stop / restart semanticsを再現する。

### V-06 Clean reset

- D-04で定義したresourceを削除
- cleanup後のabsenceを外部確認
- 次回startでclean apply

### V-07 API no-auto-migration

- FND-04 regressionを維持
- API起動 / DbContext resolveがschema evolutionを行わない

### V-08 Secret non-disclosure

D-03でlockした方式に対しtest sentinelを用いて必要観測面を検証する。

### V-09 Scope boundary

次を追加しない。

- API health endpoint
- business endpoint / schema / data
- backup / restore
- monitoring / metrics
- production deployment / scheduled orchestrator

### V-10 Mandatory mutations

`mandatory-mutations.md`のFinal Synthesis mandatory setを実行する。

## 13. Required evidence

Candidate PRは最低限次を記録する。

- common base / candidate Head full SHA
- exact model / harness / effort
- changed files
- Docker Engine / Compose version
- resolved runtime roles / images / volumes
- build / test results
- clean start evidence
- migration failure / API non-start evidence
- D-05でlockしたordering evidence
- migration history evidence
- secret sentinel evidence
- image identity evidence
- known concerns / unverified
- duration

PR本文の自己申告だけをruntime evidenceにしない。

## 14. Out of scope

- FND-06 health endpoint
- business smoke / schema / data
- backup / restore
- production deployment / external registry publication
- monitoring / metrics / alerting
- scheduled service / production orchestrator
- Kubernetes / Swarm / cloud orchestration
- zero-downtime deployment
- automatic remediation

## 15. Design lock conditions

本contractをlockedにする前にD-01〜D-08を一次証拠で解決する。

特に:

- D-01 minimum Compose version / required features
- D-02 exact PostgreSQL + .NET image identities
- D-03 secret source / reader design
- D-04 lifecycle commands + semantics
- D-05 external state capture method
- D-06 failure injection override
- D-07 cross-platform contract
- D-08 Final Synthesis exact identity

解決内容は`run.json`をmutable-state SSOTとして記録し、candidate開始前に全candidateへ同一条件として反映する。
