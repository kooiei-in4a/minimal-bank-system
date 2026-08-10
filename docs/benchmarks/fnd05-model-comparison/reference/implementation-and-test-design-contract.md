# FND-05 Implementation and Test Design Contract

Revision: `fnd05-design-contract-v1`

Status: **PRE-RUN DRAFT / IMPLEMENTATION PROHIBITED**

## 1. Authority

優先順位は次のとおり。

1. Kooが承認した製品方針・仕様
2. Accepted ADR-0001 / ADR-0008 / ADR-0009
3. Issue #43
4. `AGENTS.md`
5. 本contract

本contractが上位正本と矛盾する場合は上位正本を優先し、candidate実行を停止する。

## 2. Purpose

FND-05では、Docker Compose v2上で次を成立させる。

```text
PostgreSQL becomes connectable
  ↓
one-shot Migrator runs
  ├─ success / exit 0
  │    ↓
  │  API may start
  └─ failure / non-zero
       ↓
     API must not start
```

APIの通常startup pathはmigrationを実行しない。

## 3. Fixed service topology

Compose projectは最低限次の3 serviceを持つ。

| Service | Responsibility | Lifetime |
| --- | --- | --- |
| `postgres` | PostgreSQL 18 runtime / named data volume | long-running |
| `migrator` | FND-04 explicit Migratorを1回実行 | one-shot |
| `api` | ASP.NET Core APIを起動 | long-running |

追加の常設serviceは禁止する。

- Redis
- message broker
- reverse proxy
- monitoring stack
- backup service
- scheduler
- production orchestrator

Test-onlyのtemporary helperは、Issue #43の検証に必要で、production Composeへ混入しない場合だけ許可する。

## 4. Startup contract

### 4.1 PostgreSQL readiness

- `postgres`はdigest-qualified PostgreSQL 18 imageを使用する。
- DB dataはtop-level named volumeを使用する。
- readinessは実接続可能性を確認するhealthcheckで判定する。
- 単にcontainerがrunningであることをreadinessとしない。

### 4.2 Migrator start

- `migrator`は`postgres`のhealth success後に開始する。
- FND-04の`MinimalBankSystem.Migrator` production entry pointを使用する。
- API hostを起動しない。
- failureを握り潰さずnon-zeroで終了する。
- successだけをexit 0とする。

### 4.3 API start

- `api`は`migrator`のsuccessful completion後だけ開始する。
- migration failure、timeout、invalid credential、invalid migration history等でMigratorがnon-zeroの場合、APIを開始しない。
- short syntax `depends_on`または`service_started`だけでsuccess contractを代替しない。
- API startupで`Migrate`、`MigrateAsync`、`EnsureCreated`、ad-hoc DDLを呼ばない。

### 4.4 Compose dependencyとverificationの分離

`service_healthy`と`service_completed_successfully`は実装手段として使用できる。

ただし、Compose fileの記述だけをAcceptance Criteriaの証拠にしない。testは実container stateを外部観測する。

## 5. External observation contract

FND-06 health endpointを先取りしない。

Startup orderingは次を用いて証明する。

- `docker compose ps --all --format json`
- Migrator container exit code
- Migrator container finished timestamp
- API container state
- API container started timestamp
- Compose logs
- PostgreSQL `public.__EFMigrationsHistory`

### Success path

- `postgres`: running / healthy
- `migrator`: exited / exit 0
- `api`: running
- API started timestamp >= Migrator finished timestamp
- `InitialFoundation`がmigration historyへ1件記録される
- rerunでhistoryが重複しない

### Failure path

- `migrator`: exited / non-zero
- `api`: not started
- `api`がrunning、restarting、exited after startのいずれにもならない
- failureをsuccess logへ変換しない
- secret sentinelをlogsへ出さない

API containerが一度起動して即時停止した場合も「API非起動」とは扱わない。

## 6. Image contract

### PostgreSQL

- runtime imageはtag + digestで固定する。
- FND-03 / FND-04で固定済みのPostgreSQL 18.4 digestを再確認して使用する。

### API / Migrator

- .NET 10 approved majorのofficial base imageを使用する。
- Dockerfileのruntime / SDK `FROM`をdigest-qualified referenceへ固定する。
- APIとMigratorで不要なDockerfile重複を増やさない。
- application imageをpublic registryへpublishすることはIssue範囲外。
- build後のimage IDと使用base digestをevidenceへ記録する。

禁止:

- `latest`
- tag-only `FROM`
- floating remote build context
- unreviewed third-party base image

## 7. Secret and connection configuration contract

### 7.1 Required behavior

- password、connection string、tokenをrepositoryへ保存しない。
- secretをDockerfile、Compose command、entrypoint argument、PR本文へ書かない。
- secretを必要なserviceだけへ付与する。
- missing secretはfail-fastする。
- secret sentinelはlogs、rendered config、process argsへ出ない。

### 7.2 Preferred FND-05 design

- host側のprotected valueをCompose secretへ渡す。
- PostgreSQLは`POSTGRES_PASSWORD_FILE`等のfile-based contractを使用する。
- API / Migratorはmounted secret fileをrepo-owned entrypointで読み、process内部のenvironmentへconnection stringを設定して`exec dotnet ...`する。
- secret value自体をcommand-line argumentへ展開しない。

候補が別方式を採る場合、Issue #43とADR-0008を同等以上に満たす一次証拠を必要とする。重要な方式変更はcandidate独自判断ではなく、pre-run contract変更として全candidateへ共通適用する。

## 8. Network and privilege contract

- PostgreSQL portをhostへ公開しない。
- API portを公開する場合はlocalhost bindを標準とする。
- `privileged: true`を使用しない。
- Docker socketをmountしない。
- host networkを使用しない。
- repository全体やhost rootをcontainerへmountしない。
- production containerをroot権限前提にしない。base image上の実行userと必要permissionを明示する。

## 9. Volume contract

- PostgreSQL dataはtop-level named volumeを使用する。
- anonymous volumeをDB dataの正本にしない。
- host bind mountをDB dataの正本にしない。
- normal stop / downではdataを保持する。
- clean resetだけがnamed volumeを明示削除する。
- clean reset後は新しいvolumeとclean databaseからmigrationが再適用される。

## 10. Canonical lifecycle

Repositoryは次の再現可能な操作を文書化する。

### Validate

- Compose syntax / consistency validation
- resolved service、image、volume、secret sourceの確認

### Clean start

- build / pull
- clean volumeから3 serviceを起動
- ordering / exit / history / API stateを確認

### Stop

- long-running serviceを停止する
- named volumeを保持する

### Start after stop

- canonical start pathはmigration contractを再評価する。
- completed one-shot Migratorを再実行せずAPIだけ開始する曖昧な手順を標準としない。

### Restart

- canonical restartはMigratorを再作成・再実行し、success後だけAPIを開始する。
- raw `docker compose restart api`を安全な全体restart手順として扱わない。

### Down with data retention

- containers / networkを除去する
- named volumeを保持する

### Clean reset

- containers / network / named volumeを除去する
- orphan resourceを残さない
- 次回startでclean databaseへmigrationを適用する

Candidateはraw Compose command、repo-local script、またはcross-platform helperを提案できる。ただし全candidate比較前に同じAcceptance Criteriaで評価する。

## 11. Failure injection contract

Failure injectionはexternal configurationまたはtest-only Compose overrideで行う。

許可例:

- invalid credential secret
- unreachable database endpoint
- malformed migration history
- Migrator commandを明示的に失敗させるtest-only override
- API dependency conditionをmutationで弱める

禁止:

- production codeへtest-only bypassを追加
- API startup migrationを一時的に許可
- secretをsourceへ埋め込む
- pre-cancelled tokenだけで実I/O到達を代替
- failure pathへ到達する前の無関係なbuild failureを証拠にする

## 12. Required verification scenarios

### V-01 Static Compose validation

- canonical render成功
- services exactly contain expected topology
- required named volume exists
- image references / Dockerfile base references are pinned
- secret value is not rendered

### V-02 Clean start

- clean volume
- PostgreSQL healthy
- Migrator exit 0
- API started after Migrator finish
- migration history correct

### V-03 Migration failure

- Migrator non-zero
- API never starts
- secret sentinel absent
- overall verification command returns non-zero

### V-04 Existing-volume rerun

- migration history unchanged
- Migrator rerun success
- API start success
- data volume identity retained

### V-05 Stop / start / restart

- documented path reproducible
- unsafe API-only restartを標準手順にしない
- migration gateを再評価する

### V-06 Clean reset

- named volume removed
- orphan containers / networks absent
- next start is clean apply

### V-07 API no-auto-migration

- API source scan
- API start before / after schema snapshot
- DbContext resolutionがschemaを変えないFND-04 regression維持

### V-08 Secret non-disclosure

sentinelを配置し、次へ出ないことを確認する。

- git diff
- `docker compose config`
- Compose logs
- container process args
- PR body / test artifacts

### V-09 Scope boundary

次が追加されていない。

- API health endpoint
- business endpoint / business schema
- backup / restore
- monitoring / metrics
- production deployment
- scheduled orchestrator

### V-10 Mandatory mutations

`mandatory-mutations.md`のapplicable mutationをFinal Synthesisへ実行する。

## 13. Required evidence

Candidate PRは最低限次を記録する。

- full common base SHA
- full candidate Head SHA
- changed files
- exact model / harness / effort
- Compose version
- Docker Engine version
- resolved Compose services / images / volumes
- build / test results
- clean start evidence
- migration failure evidence
- API non-start evidence
- timestamps / exit codes
- secret sentinel evidence
- image digest / image ID evidence
- known concerns
- unverified items
- duration

## 14. Out of scope

- FND-06 health endpoint
- business smoke
- business schema / data
- backup / restore
- production deployment
- external registry publication
- scheduled service
- Kubernetes / Swarm / cloud orchestrator
- zero-downtime deployment
- automatic remediation

## 15. Design lock conditions

本contractをlockedにする前に次を解決する。

- API / Migrator secret file readerの配置と責務
- exact .NET base image digests
- exact PostgreSQL image digest再確認
- canonical lifecycle command形
- API start timestamp取得方法
- failure injection override形
- cross-platform execution要件

解決内容はcandidate開始前に全candidateへ同一条件として反映する。
