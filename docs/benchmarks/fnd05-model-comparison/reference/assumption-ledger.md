# FND-05 Assumption Ledger

Revision: `fnd05-assumptions-v2`

Status: **PRE-RUN DRAFT / OPEN DECISIONS NOT LOCKED**

外部toolとcurrent project stateに関する前提をcandidate outputを見る前に固定する。

Mutable decision stateの正本は`../run.json`の`open_decisions`である。本ledgerはquestionとrequired evidenceを説明し、lock前の具体answerをcandidateへ与えない。

## 1. Evidence classes

- `CONFIRMED_EXTERNAL`: official primary documentationで確認
- `CONFIRMED_PROJECT`: repository一次証拠で確認
- `TO_LOCK`: candidate開始前に一次証拠で確定が必要
- `PROHIBITED_ASSUMPTION`: 証拠なしで前提にしてはいけない

## 2. Docker Compose assumptions

### A-01 — Running is not ready

```yaml
status: CONFIRMED_EXTERNAL
```

Compose dependencyがrunningになっただけではreadyを意味しない。

Implication:

- PostgreSQL usable状態を別途判定する必要がある。
- short syntax `depends_on`だけをreadiness証拠にしない。

Source:

- https://docs.docker.com/compose/how-tos/startup-order/

### A-02 — Successful one-shot dependency condition exists

```yaml
status: CONFIRMED_EXTERNAL
```

`service_completed_successfully`は利用可能なCompose実装手段である。

Implication:

- APIをMigrator成功後に開始する候補手段として使える。
- exact installed Compose version / feature supportはD-01でlockする。
- Issue #43のobservable contractを満たす別の同等経路をpre-run lock前から排除しない。

Source:

- https://docs.docker.com/reference/compose-file/services/#depends_on

### A-03 — Machine-readable container state is available

```yaml
status: CONFIRMED_EXTERNAL
```

`docker compose ps --all --format json`等でservice state / health / exit codeをmachine-readableに取得できる。

Exact observation methodはD-05でlockする。

Source:

- https://docs.docker.com/reference/cli/docker/compose/ps/

### A-04 — Compose config validation is available

```yaml
status: CONFIRMED_EXTERNAL
```

`docker compose config`でcanonical modelをrender / validateできる。

Exact command / fieldsはD-01 / D-05のlockに従う。

Source:

- https://docs.docker.com/reference/cli/docker/compose/config/

### A-05 — External secret mechanisms exist

```yaml
status: CONFIRMED_EXTERNAL
```

Compose secret、environment、protected file等の外部注入手段が存在する。

本項はD-03のanswerを固定しない。

Sources:

- https://docs.docker.com/reference/compose-file/secrets/
- https://docs.docker.com/compose/how-tos/use-secrets/

### A-06 — Compose Specification

```yaml
status: CONFIRMED_EXTERNAL
```

current ComposeはCompose Specificationを使用する。

Source:

- https://docs.docker.com/reference/compose-file/

### A-07 — Image digest semantics

```yaml
status: CONFIRMED_EXTERNAL
```

image tagは可変で、digestはcontent identityを固定する。

Source:

- https://docs.docker.com/dhi/explore/security-concepts/digests/

## 3. Current project assumptions

### P-01 — Platform baseline

```yaml
status: CONFIRMED_PROJECT
```

ADR-0001は.NET 10、ASP.NET Core 10、PostgreSQL 18、EF Core 10、Docker Compose v2を採用している。

Evidence:

- `docs/adr/0001-application-platform-baseline.md`

### P-02 — Explicit migration

```yaml
status: CONFIRMED_PROJECT
```

ADR-0009はmigrationをexplicit Migrator pathでAPI開始前に適用し、API startup auto-migrationを禁止する。

Evidence:

- `docs/adr/0009-database-schema-migration-and-rollback.md`

### P-03 — Secret / logging baseline

```yaml
status: CONFIRMED_PROJECT
```

ADR-0008はsecretをrepositoryやcommand-line argumentへ置かず、外部注入することを要求する。

Evidence:

- `docs/adr/0008-audit-logging-technical-logging-and-backup.md`

### P-04 — FND-04 Migrator exists

```yaml
status: CONFIRMED_PROJECT
```

current mainは次を持つ。

- Infrastructure-owned DbContext / migration
- `MinimalBankSystem.Migrator` one-shot entry point
- API no-auto-migration
- canonical connection configuration
- empty `InitialFoundation`

Evidence:

- PR #140
- merge commit `9a352a3a61945647273ccc7dfbc8e1816c3ca07c`

### P-05 — No FND-05 Compose runtime yet

```yaml
status: CONFIRMED_PROJECT
```

Issue #43開始前のmainにはFND-05 canonical Compose execution pathが存在しない。

### P-06 — FND-06 API health is unavailable

```yaml
status: CONFIRMED_PROJECT
```

FND-05では`/health/live` / `/health/ready`をordering証拠に使用しない。

Evidence:

- Issue #44
- current API source

## 4. Decisions to lock before candidate execution

各decisionは`question + required_evidence`だけを持つ。`locked_value`は`run.json`へ一次証拠付きで記録するまでnullとする。

### D-01 — Minimum Compose version / feature support

```yaml
status: TO_LOCK
question: "local / CIで共通利用するminimum Docker Compose versionとrequired featuresは何か"
required_evidence:
  - local docker compose version
  - GitHub Actions environment version
  - required feature support confirmation
  - exact commands used for validation
```

### D-02 — Exact PostgreSQL / .NET image identities

```yaml
status: TO_LOCK
question: "approved major内で使用するPostgreSQL 18および.NET 10 imagesのexact digestは何か"
required_evidence:
  - PostgreSQL image source + full digest
  - .NET SDK image source + full digest
  - ASP.NET runtime image source + full digest
  - platform / architecture compatibility
```

PostgreSQL digestを別decisionへ分離しない。

### D-03 — Secret source / reader design

```yaml
status: TO_LOCK
question: "ADR-0008とIssue #43を満たすsecret source / service grant / reader designを何に固定するか"
required_evidence:
  - repository non-disclosure
  - argv non-disclosure
  - log / rendered-config observation
  - missing-secret fail-closed behavior
  - local / CI / supported-host reproducibility
```

候補方式を本ledgerでpreferred answerとして固定しない。

### D-04 — Lifecycle commands and semantics

```yaml
status: TO_LOCK
question: "validate / start / stop / restart / down-retain-data / clean-resetのcopyable commandとmigration-gate semanticsを何にするか"
required_evidence:
  - exact commands
  - expected resource state before / after
  - migration gate re-evaluation rule
  - cleanup absence observation
```

### D-05 — External state capture method

```yaml
status: TO_LOCK
question: "local / CIでorderingとfailureを同じ方法で観測するには何を使うか"
required_evidence:
  - Migrator exit code field / command
  - Migrator completion ordering evidence
  - API never-started vs started-then-exited state
  - API start ordering evidence
  - migration-history query / result
  - Compose project/resource identity
  - machine-readable local / CI command
```

### D-06 — Failure injection / mutation determinism

```yaml
status: TO_LOCK
question: "production backdoorなしでrequired failure / mutationを決定的に発火・観測するにはどうするか"
contract: "reference/mutation-determinism-contract.md / fnd05-mutation-determinism-v1"
required_evidence:
  - test-only isolation
  - intended production path reachability
  - no real credential
  - no committed residue
  - M-01〜M-10 applicable injection plan
  - per-applicable-mutation deterministic precondition property
  - controlled barrier / fixture class
  - injection point class
  - expected failure signature
  - invalid failure signatures
  - cleanup requirement / residue check
  - M-01 controlled incomplete-Migrator barrier without natural race timing
  - M-03 DB precondition where auto-migration causes observable migration-state delta
  - M-08 unchanged oracle with exit 0 and missing expected migration state
  - M-10 pre-existing same-project target resource before cleanup weakening
```

D-06ではexact evaluator patch / exact source editをcandidate-facing answerとして固定しない。Candidateへはcontract / precondition / fixture class / failure signature classまでを開示し、exact injection recipeはevaluator側へ隔離する。

### D-07 — Cross-platform contract

```yaml
status: TO_LOCK
question: "必須実行環境とshell/helper portabilityをどこまで保証するか"
required_evidence:
  - GitHub Actions Linux
  - primary local environment
  - script/runtime requirements
  - path / line ending / shell behavior
```

### D-08 — Final Synthesis identity

```yaml
status: TO_LOCK
question: "Selection完了後のFinal Synthesisを担当するexact Model / Harness / Effortは何か"
required_evidence:
  - exact product-visible model label
  - harness
  - effort label
  - fresh-context availability
```

本ledgerではdefault authorを指定しない。

## 5. Prohibited assumptions

### X-01

`depends_on`があるためDB readyとみなす。

### X-02

Migratorがexitedしただけでsuccessとみなす。success contractにはexit 0とmigration state evidenceが必要。

### X-03

API containerがcreatedされたためorderingを満たすとみなす。

### X-04

`exit != 0`だけでintended failureを検出したとみなす。

### X-05

secretがgitに無いためlogs / argvにも出ないとみなす。

### X-06

version tagがあるためimage contentがimmutableとみなす。

### X-07

cleanup command exit 0だけでresource absenceを証明したとみなす。

### X-08

PR本文の自己申告をruntime evidenceとみなす。

### X-09

未lock decisionに書かれた例や候補をcandidateへの必須方式とみなす。

### X-10

MutationでREDになったため、deterministic preconditionやfailure signatureを確認せず有効なkillとみなす。

## 6. Lock rule

- D-01〜D-08の`locked_value`と`evidence_refs`が`run.json`へ記録されるまで未lock。
- D-06は`fnd05-mutation-determinism-v1`のlock schemaを満たし、`run.json.gates.mutation_determinism_locked = true`となるまで未lock。
- 1件でもTO_LOCKならcandidate execution禁止。
- Issue Ready PASSだけでは開始しない。`run.json.gates.koo_start_authorized = true`も必要。
