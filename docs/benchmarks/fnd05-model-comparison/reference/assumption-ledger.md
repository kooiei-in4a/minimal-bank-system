# FND-05 Assumption Ledger

Revision: `fnd05-assumptions-v1`

Status: **PRE-RUN DRAFT**

外部toolとcurrent project stateに関する前提をcandidate outputを見る前に固定する。

## 1. Evidence classes

- `CONFIRMED_EXTERNAL`: official primary documentationで確認
- `CONFIRMED_PROJECT`: repository一次証拠で確認
- `TO_LOCK`: candidate開始前に確定が必要
- `PROHIBITED_ASSUMPTION`: 証拠なしで前提にしてはいけない

## 2. Docker Compose assumptions

### A-01 — Running is not ready

```yaml
status: CONFIRMED_EXTERNAL
```

Composeはdependency containerがrunningになっただけではreadyを待たない。

Implication:

- PostgreSQL readinessにはhealthcheckと`service_healthy`相当を使用する。
- short syntax `depends_on`だけをready証拠にしない。

Source:

- https://docs.docker.com/compose/how-tos/startup-order/

### A-02 — Successful one-shot dependency

```yaml
status: CONFIRMED_EXTERNAL
```

Compose service dependencyには`service_completed_successfully` conditionがある。

Implication:

- APIをone-shot Migrator成功後に開始する実装候補として使用できる。
- exact installed Composeがこのconditionをsupportすることをpre-runで確認する。

Source:

- https://docs.docker.com/reference/compose-file/services/#depends_on

### A-03 — `docker compose ps` machine-readable state

```yaml
status: CONFIRMED_EXTERNAL
```

`docker compose ps --all --format json`はservice、state、health、exit code等をmachine-readableに返す。

Implication:

- API non-start、Migrator exit、PostgreSQL healthの外部観測に使用できる。

Source:

- https://docs.docker.com/reference/cli/docker/compose/ps/

### A-04 — Compose config validation

```yaml
status: CONFIRMED_EXTERNAL
```

`docker compose config`は変数解決後のcanonical modelをrenderし、`--quiet`でvalidationできる。

Implication:

- syntax、service、image、volume、interpolationをstatic gateで確認する。
- rendered outputにsecret valueを出す方式はsecret non-disclosure上のriskとして扱う。

Source:

- https://docs.docker.com/reference/cli/docker/compose/config/

### A-05 — Compose secrets

```yaml
status: CONFIRMED_EXTERNAL
```

Compose secretはserviceへ明示grantし、container内の`/run/secrets/<name>`へfileとしてmountできる。sourceはfileまたはhost environmentを使用できる。

Implication:

- database passwordをargvへ展開せずにserviceへ渡すpreferred designとして使用できる。

Sources:

- https://docs.docker.com/reference/compose-file/secrets/
- https://docs.docker.com/compose/how-tos/use-secrets/

### A-06 — Compose Specification

```yaml
status: CONFIRMED_EXTERNAL
```

current ComposeはCompose Specificationを使用し、legacy top-level `version`は不要である。

Source:

- https://docs.docker.com/reference/compose-file/

### A-07 — Image digest semantics

```yaml
status: CONFIRMED_EXTERNAL
```

image tagは可変で、digestはcontent identityを固定する。

Implication:

- PostgreSQL imageとDockerfile base imageをdigest-qualifiedにする。

Source:

- https://docs.docker.com/dhi/explore/security-concepts/digests/

## 3. Current project assumptions

### P-01 — Platform baseline

```yaml
status: CONFIRMED_PROJECT
```

ADR-0001は.NET 10、ASP.NET Core 10、PostgreSQL 18、EF Core 10、Docker Compose v2、一つのapplication serviceを採用している。

Evidence:

- `docs/adr/0001-application-platform-baseline.md`

### P-02 — Explicit migration

```yaml
status: CONFIRMED_PROJECT
```

ADR-0009はmigrationをexplicit command / one-shot Compose serviceでAPI開始前に適用し、API startup auto-migrationを禁止する。

Evidence:

- `docs/adr/0009-database-schema-migration-and-rollback.md`

### P-03 — Secret / logging baseline

```yaml
status: CONFIRMED_PROJECT
```

ADR-0008はsecretをrepositoryやargvへ置かず、environment、Docker secret、protected password file等で外部注入することを要求する。

Evidence:

- `docs/adr/0008-audit-logging-technical-logging-and-backup.md`

### P-04 — FND-04 Migrator exists

```yaml
status: CONFIRMED_PROJECT
```

current mainは次を持つ。

- `MinimalBankSystem.Infrastructure` owned DbContext / migration
- `MinimalBankSystem.Migrator` one-shot entry point
- API no-auto-migration
- `ConnectionStrings:Database` / `ConnectionStrings__Database`
- empty `InitialFoundation`

Evidence:

- PR #140
- merge commit `9a352a3a61945647273ccc7dfbc8e1816c3ca07c`

### P-05 — No Compose runtime yet

```yaml
status: CONFIRMED_PROJECT
```

Issue #43開始前のmainにcanonical `compose.yaml`とAPI / Migrator Dockerfileは存在しない。

### P-06 — FND-06 API health is not available

```yaml
status: CONFIRMED_PROJECT
```

FND-05では`/health/live` / `/health/ready`を使用できない。API orderingはcontainer state / timestampで検証する。

Evidence:

- Issue #44
- current API source

### P-07 — PostgreSQL digest

```yaml
status: TO_LOCK
```

FND-03 / FND-04で使用したPostgreSQL 18.4 digestをFND-05 common contractとして再確認する。

Candidate開始前にfull referenceを`run.json`へ固定する。

## 4. Decisions to lock before candidate execution

### D-01 — Minimum Compose version

```yaml
status: TO_LOCK
```

- installed local version
- GitHub Actions version
- `service_completed_successfully`
- secrets.environmentまたは採用secret source
- `ps --format json`

のsupportを確認し、minimum versionを固定する。

### D-02 — .NET image digests

```yaml
status: TO_LOCK
```

- SDK image
- ASP.NET runtime image
- runtime-depsが必要な場合のimage

をofficial sourceから選び、full digestを固定する。

### D-03 — Secret source

```yaml
status: TO_LOCK
```

preferred:

- host environment → Compose secret
- container file mount
- repo-owned entrypointでread
- argvへ展開せず`exec dotnet`

Windows / Linux / CIで再現可能か確認する。

### D-04 — Canonical lifecycle commands

```yaml
status: TO_LOCK
```

- validate
- clean start
- stop
- start after stop
- restart
- down retain data
- clean reset

のcopyable commandを固定する。

### D-05 — API start timestamp

```yaml
status: TO_LOCK
```

Docker inspect / Compose ps / Engine metadataのうち、localとCIで同じ方法を選ぶ。

### D-06 — Failure injection override

```yaml
status: TO_LOCK
```

production codeを変更せず、invalid credential / malformed history / failure commandを注入できるtest-only方式を固定する。

### D-07 — Cross-platform scope

```yaml
status: TO_LOCK
```

minimum contract:

- GitHub Actions Linux: required
- primary local environment: required
- shell-specific scriptを置く場合は代替commandまたはcross-platform helperを用意

### D-08 — Final Synthesis author

```yaml
status: TO_LOCK
```

default候補はfresh-context GPT-5.6 Luna / Codexとするが、Selection後にKooが固定する。Final Synthesisはcandidateとして採点しない。

## 5. Prohibited assumptions

### X-01

`depends_on`があるためDB readyとみなす。

### X-02

Migrator containerがexitedしたためsuccessとみなす。exit code 0が必要。

### X-03

API containerが一度作成されたためorderingを満たすとみなす。

### X-04

`exit != 0`のためintended failureを検出したとみなす。

### X-05

secretがgitに無いためlogs / argvにも出ないとみなす。

### X-06

image tagにversionがあるためimmutableとみなす。

### X-07

command exit 0のためresource cleanupが完了したとみなす。

### X-08

PR本文に書かれているためruntime evidenceが存在するとみなす。

## 6. Lock rule

`TO_LOCK`が1件でも未解決なら、candidate branchを作成してもexecutionは開始しない。
