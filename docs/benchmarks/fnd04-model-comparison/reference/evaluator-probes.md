# FND-04 Evaluator-Only Probe Plan

Status: **LOCKED BEFORE CANDIDATE EXECUTION**

Revision: `fnd04-evaluator-probes-v1`

これらのprobeはcandidate自身の自己申告やvisible testだけに依存せず、全candidateを同じ条件で評価するための追加検証である。

probeの具体的なmutation patchやControlled Mutant Goldはreviewer raw capture前に公開しない。ここでは評価対象の契約だけを固定する。

## P-01 — Exact identity / package lock

確認:

- common base -> candidate Head
- exact package versions
- local `dotnet-ef` version
- expected project references

Fail signal:

- floating version
- preview 11.x
- candidate-specific base drift

## P-02 — Clean real-PostgreSQL migration apply

FND-03 fixtureから完全にcleanなdatabaseを作成し、dedicated Migratorを実行する。

PASS:

- process exit 0
- `public.__EFMigrationsHistory`が存在
- `InitialFoundation`がappliedとして記録
- business table / sequence / triggerが作成されていない

## P-03 — Migrator rerun

P-02と同じDBへMigratorをもう一度実行する。

PASS:

- exit 0
- baseline historyが重複しない
- schemaにunexpected mutationがない

目的はone-shot entry pointの通常再実行安全性を確認することであり、business migrationのfull idempotency一般を証明するものではない。

## P-04 — Missing / invalid connection failure

connection configurationをmissingまたはunreachable PostgreSQLへ向けてMigratorを実行する。

PASS:

- non-zero exit
- migration successと報告しない
- SQLite / InMemory / local fallbackへ切り替わらない
- secretをlogへ出さない

## P-05 — Bounded execution / cancellation

60秒budgetがproduction migrator pathへ実際に伝播することを確認する。

方法はcandidateの実装に応じて、command timeout設定、CancellationToken wiring、deterministic failure injectionを組み合わせる。

PASS:

- unlimited waitを許容する構造でない
- timeout/cancellationをcatchしてexit 0へ変換しない

実評価で60秒の実sleepを毎candidateへ強制する必要はない。wiringと決定論的probeで証明できる。

## P-06 — API startup no migration

clean PostgreSQLを用意し、通常API startup前後で次を比較する。

- `__EFMigrationsHistory` existence / rows
- user-created relations

PASS:

- API startupだけではmigration historyもapplication schemaも変化しない
- API process内からMigrator executable / migration CLIを起動しない

source grepだけではPASSにしない。

## P-07 — Pending model check positive case

unmodified candidate Headで標準pending-model commandを実行する。

PASS:

- no pending model changes
- command succeeds

## P-08 — Pending model check negative case

isolated evaluator workspaceで、migrationを追加せずmodel-only temporary changeを加える。

PASS:

- actual EF pending-model mechanismがfailureを返す
- temporary changeを破棄するとP-07へ戻る

probe changeをcandidate branchへcommit / pushしない。

## P-09 — EnsureCreated / startup migration absence

実コード・runtime wiringを確認する。

Major候補:

- normal API startupの`Migrate/MigrateAsync`
- application schema evolutionの`EnsureCreated/EnsureCreatedAsync`
- startupからmigration commandをshell実行
- migration failureをstartup時に黙って許容

## P-10 — Design-time / runtime consistency

repository-local `dotnet-ef`からDbContextを解決し、Infrastructure migrationsを列挙・drift checkできることを確認する。

確認:

- provider = Npgsql
- migrations assembly = Infrastructure
- design-time factoryがAPI host startupへ依存しない
- connection configuration keyの意味がruntime / design-time / Migratorで一致

## P-11 — Idempotent SQL generation

標準`migrations script --idempotent` commandを実行する。

PASS:

- command success
- migration historyを前提にしたidempotent scriptが生成される
- `InitialFoundation`が対象rangeへ含まれる

生成SQLをcandidate sourceへ固定すること自体は必須としない。

## P-12 — Business schema boundary

migration、snapshot、DbContext model、database after applyを確認する。

FAIL / Major:

- Customer / Account / Operator / Identity / AuditLog / Transaction / Idempotency等のbusiness table
- business sequence / trigger / constraint
- dummy entityをbaseline成立のためproduction modelへ追加

## P-13 — FND-03 regression

既存non-PostgreSQL testsとreal PostgreSQL testsをexact candidate Headで実行する。

PASS:

- existing suite green
- FND-03 fixtureを壊さない
- no provider fallback

## Evidence rule

各probeは以下を記録する。

- candidate slug
- exact Head
- command / test / inspected source
- PASS / FAIL / NOT EVALUATED
- evidence reference
- corresponding Finding ID（失敗時）

Evaluatorはcandidate PR本文の「PASS」自己申告をprobe結果の代替にしない。
