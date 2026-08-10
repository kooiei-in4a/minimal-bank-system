# FND-04 Final Synthesis Independent Review

Reviewer:

- Slot: R2
- Model: Claude Opus 5
- Harness: Claude Code
- Effort: xHigh
- Primary role: deep_technical_test_assurance
- Attempt: 1

Target verification:

- Repository: `kooiei-in4a/minimal-bank-system`
- PR: #140 `[FND-04] EF Core・明示的migration実行基盤 — Final Synthesis`（OPEN / Draft / 未merge / MERGEABLE / CLEAN）
- Base SHA: `38c07e210fe4e8689f1d8aeabbb07b92610d1826`（`main`、observed一致）
- Head SHA: `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`（`agent/issue-42-fnd-04-final-code`、observed一致）
- CI identity: merge-ref run `31350916189`（`d12de2a` = Merge 99cee43 into 38c07e2、KNOWN値と一致）に加え、direct-head push run `31350870902`（checkout = `99cee43`そのもの）も独立に存在しSUCCESS
- Result: **PASS**

**Verdict:**

- **CHANGES_REQUIRED**

**Merge-ready:**

- **NO**（Major 1件）

---

## Findings

### Blocker

NONE

### Major

**R2-F01 — C8-M01 design-time regression testがfalse assuranceであり、防ぐべきdefect classを実際には検出できない**

- Severity: Major / **blocking: yes**
- Affected path: `tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs`

**Evidence**

production側は正しいことを確認。connection未設定で標準design-time commandを実行すると、production `BankDbContextFactory` を実際に経由してfail-closedする。

```text
An error occurred using the connection to database '' on server ''.
System.InvalidOperationException: The ConnectionString property has not been initialized.
   at Npgsql.NpgsqlConnection.Open()
   at Microsoft.EntityFrameworkCore.Migrations.HistoryRepository.GetAppliedMigrations()
   at Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal.NpgsqlHistoryRepository.GetAppliedMigrations()
   at Microsoft.EntityFrameworkCore.Design.Internal.MigrationsOperations.UpdateDatabase(...)
EXITCODE=1
```

`NpgsqlMigrator` / `NpgsqlHistoryRepository` frameが現れており、fake providerへのfallbackもfabricated destinationもない。production実装は契約通り。

問題は、この事実をcommitted testが証明していない点。2つの独立mutation probeで実証。

1. `UseBankPostgreSqlModelOnly()` をblocklistに載っていないambient destination `Host=db;Database=ambient_fallback;Username=postgres`へ改変。実際に`server 'tcp://db:5432'`へ接続を試みるようになったにもかかわらずcommitted testは合格。
2. Migratorのbuild outputを退避し、`--no-build`のdesign-time commandがfactoryへ到達できない状態でもcommitted testは合格。

**Root cause**

assertionが `process.ExitCode != 0` と6要素の固定blocklist（`127.0.0.1` / `localhost` / `design_time` / `Data Source=` / `Sqlite` / `InMemory`）の不在のみで構成される。

- literal文字列が出力にないことは、providerが実際にどのdestinationを構成したかを証明しない。blocklist外のhost名、Unix socket、ambient defaultは素通りし得る。
- design-time context生成へ到達したことを示すpositive assertionがないため、tool未restore、MSBuild評価失敗、stale build outputなどあらゆる非0終了が合格条件を満たす。

**Impact**

Issue #42 §8.4（未設定時にfake / SQLite / InMemory / ambient destinationへfallbackしない）に対する唯一のcommitted regression guardが、その退行を検出できない。PR本文・tests README・test XML docは「fabricated localhost / fake provider / ambient destinationが無いことを検証する」と記述しており、証拠強度が主張に届いていない。production defectではなくassurance defectだが、Final SynthesisがC8-M01を明示的な採用差分として掲げているためblocking Majorと判定。

**Required fix direction**

- 失敗がconnection-required design-time pathから来たことをpositiveに固定する（例：`The ConnectionString property has not been initialized`、Npgsql/EF operation frame、empty server/database等）。
- blocklistは補助に留め、「destinationが構成されていない」ことをassertする。
- build/tooling起因の失敗で合格しないよう、EF operationへ到達したmarkerを要求する。あるいは`--no-build`をやめる／build freshnessを確認する。

### Minor

**R2-F02 — Migratorのexit code taxonomy（1 = Failure / 2 = Timeout）がnegative testで固定されていない**

- Severity: Minor / blocking: no
- Affected path: `MigrationBaselineTests.cs`、`MigratorExitCode.cs`、`README.md`
- Evidence: missing connection / unreachable / rejected credentials / malformed historyの4本はすべて `Assert.NotEqual(MigratorExitCode.Success, run.ExitCode)` のみ。suite内で`MigratorExitCode.Failure`（1）を固定するassertionはなく、特定codeを固定するのはtimeout testのみ。
- Root cause: Issue #42契約が「非0」までしか要求しないため、実装側が独自に公開した3値taxonomyがtest外に置かれた。
- Impact: 全failureをexit 2へ誤分類する退行が4本とも素通りする。READMEはtaxonomyをdeployment向け契約として公開している。
- 契約上必須ではないため非blocking。

### Nit

**R2-F03 — constant同士の比較によるassertion**

`MigrationModelTests.TheMigrationBudgetIsSixtySeconds` は `Assert.Equal(60, BankPersistence.MigrationTimeoutSeconds)` 等の定数比較で挙動を検証しない。実効的な60秒証拠はreal PostgreSQL timeout testが担っておりfalse assuranceには至らないためNit。`Assert.NotNull(response)`、`ReadMigrationHistoryIdsFromMalformedTableAsync()`のpass-through aliasも同種の無情報assertion／命名。

**R2-F04 — PR本文がmerge-ref runを「Exact Head CI」と表示している**

PR #140本文はrun `31350916189`を"Exact Head CI"として掲げるがcheckoutは`d12de2a` PR merge ref。direct-head push run `31350870902`（checkout=`99cee43`）が存在しSUCCESSしているため実害はないが、citationはpush runまたは両方へ向けるのが正確。

---

## Probe matrix

- **P01 PASS** — props pinだけでなくresolved assemblyを実測。EF Core / Design / Relational = `10.0.10`、Npgsql = `10.0.3`、provider = `10.0.3`、dotnet-ef = `10.0.10`。ownership / dedicated Migratorも適合。
- **P02 PASS** — local real PG clean apply。exit 0、history 1、public schemaは`__EFMigrationsHistory`のみ。
- **P03 PASS** — rerun test成功、history不変。
- **P04 PASS** — missing / unreachable / rejected credentials / malformed historyすべて非0。taxonomy未固定はR2-F02。
- **P05 PASS** — mutationでMigratorのexplicit 60s command timeoutを外すとfixtureの`CommandTimeout=10`が支配し約10秒でexit 1、testがExpected 2 / Actual 1で失敗。production CTSとNpgsql command timeout=60の両方をtestが実際に固定。通常実測1m01s。
- **P06 PASS** — real PGでstartup前後と`BankDbContext` resolve後もschema/history不変。`MigrateAsync`はMigratorのみ、`EnsureCreated` / ad-hoc DDLなし。
- **P07 PASS** — actual EF `HasPendingModelChanges()` + CI標準CLI。tautology代替なし。
- **P08 PASS（独立再現）** — 一時model-only entity追加でCLI exit 1、関連testも失敗。revert後exit 0 / 全合格。Head残骸なし。
- **P09 PASS** — forbidden pathsなし。
- **P10 PARTIAL** — productionはPASS。unmutated HeadでNpgsql factory path、empty destination、ConnectionString uninitialized、exit 1を確認。committed evidenceはR2-F01によりFAIL。
- **P11 PASS** — documented idempotent SQL CLIを実行しexit 0。history guard付きbaselineのみ、business DDLなし。
- **P12 PASS** — business schema / Compose / health / FND-05 / FND-06先取りなし。
- **P13 PASS** — FND-03 fixture source無変更。local real PG 23本すべて合格、残留container 0。
- **P14 PASS** — unreachable host + sentinel passwordでMigratorを直接起動。structured failure logとException fieldが実際にstdoutへ出力され、sentinel不在。
- **P15 PASS** — merge-ref run `31350916189`とdirect-head push run `31350870902`の双方SUCCESS。build 0 warning / 0 error、pending-model、non-PG、real PG全成功。

---

## Primary-role deep dive — deep_technical_test_assurance

**production path:** `MigratorProcess` はtest側でMigratorを再構築せず、built `MinimalBankSystem.Migrator.dll`を実process起動し、exit code / stdout / stderr / 実時間を観測。real PG schema/history assertionは別connectionの独立SQL観測。

**production component再構築:** `MigrationModelTests`はproduction `BankDbContextFactory`を直接使用し、`IMigrationsAssembly` / `IMigrator`もEFの実service経由。test専用DbContext/provider再構築はない。

**tautology:** 実質的なものはR2-F03。P05/P07/P08はmutationで感度を確認しtautologyではない。

**negative failure point:** rejected credentialは実PostgreSQL auth failure、malformed historyは`ProductVersion`欠落で`GetAppliedMigrations`を実際に失敗、timeoutは未commit CREATE TABLEによるlock待ちで実block。唯一の例外がR2-F01で、そこだけ何が失敗しても合格する構造。

**process-global state/race:** `Environment.SetEnvironmentVariable`は不使用。env除去はchild `ProcessStartInfo.Environment`のみ。Console差替えtestはDisableParallelization collectionへ隔離。

**working directory / build output coupling:** testsはgit working treeと`bin/<Config>/net10.0`レイアウトへ依存。逸脱時はfail-loudだが、R2-F01の`--no-build` staleness vectorはここに由来。

**FND-03 lifecycle/isolation:** regressionなし。新規classも既存fixture設計に沿う。

**local-only probe:** temporary driftは本レビューで独立再現してPASS。author自己申告には依存しない。

---

## CI assessment

- `31350916189` (`pull_request`): checkout `d12de2a Merge 99cee4386... into 38c07e210...`。merge-state CI SUCCESS。
- `31350870902` (`push`): direct Head `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`をcheckout。全step SUCCESS。

exact direct-head CI未確認という懸念は解消。real PostgreSQLはTestcontainers経由で実行され、SQLite / InMemory fallbackなし。

---

## Unverified / evidence limits

- P14 sentinel guardはrejected-credential pathのみ。他failure pathではfixture random password不在をassertしていない（実測漏洩なし）。
- Migrator loggingはstdoutへ出るため`StandardError` sentinel assertionは実質vacuous。
- default log levelでの観測。Debug等の運用構成は未検証。
- 60秒timeoutは本レビュー1 run + CI + author localで、統計的flaky判定には少ない。
- concurrent Migrator behaviorはIssue #42 scope外。
- empty baselineのため実際に`Up()`が失敗するmigrationは構成不能。malformed historyは停止条件上妥当なproxy。
- testsはgit worktreeとbuild output layoutに依存しpublished test bundleでは動かないがfail-loud。
- benchmark result/evaluation/selection/ranking/gold/judge/他reviewer artifactは参照していない。

---

## Final rationale

Issue #42のproduction contractは独立検証した範囲でほぼ全面的に満たされる。version pin、ownership、empty baseline、API no-schema-mutation、60秒budget、model driftはgreen CIだけに依存せずruntime/mutationでも成立。

一方、C8-M01 regression testは防ぐはずのdefectを埋め込んでも合格し、factoryへ到達できなくても合格する。production実装そのものは正しいが、このtestがそれを証明したわけではない。Issue §8.4に対する唯一のcommitted guardとして証拠強度不足のためMajor / blocking。

修正はtest側のみの小規模変更で済み、production変更は不要。R2-F01解消後は残り非blocking findingのみでmerge-ready到達は近いと判断する。
