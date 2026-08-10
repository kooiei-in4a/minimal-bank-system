# FND-04 Final Synthesis — Agent A Implementation Prompt

Revision: `fnd04-final-synthesis-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-04 Final Synthesis Implementer / Agent A** です。

この作業は8 candidateの追加benchmark attemptではありません。H0 / Formal Self-Review / H1 / Implementation Evaluation / Selection-Adjudicationは完了・LOCK済みです。

候補を再採点したり、candidate branchを修正したりせず、LOCK済みselectionを入力としてIssue #42のcurated Final Synthesisを実装してください。

---

## 1. Repository / Target

```yaml
REPOSITORY: kooiei-in4a/minimal-bank-system
TARGET_ISSUE: 42
TARGET_TITLE: "[FND-04] EF Core・明示的migration実行基盤を確立する"

BASE_BRANCH: main
EXPECTED_BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826

TARGET_BRANCH: agent/issue-42-fnd-04-final-code

BENCHMARK_CONTROL_BRANCH: agent/fnd04-benchmark-control
SELECTION_REVISION: fnd04-selection-adjudication-v1
FINAL_SYNTHESIS_PROMPT_REVISION: fnd04-final-synthesis-v1
```

作業開始時に`git fetch origin`し、`origin/main`が`EXPECTED_BASE_SHA`とexact一致することを確認してください。

一致しない場合、実装を開始せず、現在のmain SHAと差分概要を報告して停止してください。

working treeに無関係な変更がある場合も、それを消したり巻き込んだりせず停止してください。

---

## 2. Duration collection — 今回は分単位で必ず記録

GitHub timestampから後で推測しないため、作業開始時にローカル時刻を分単位で記録してください。

```text
STARTED_AT_LOCAL: YYYY-MM-DD HH:MM
```

全実装・local verification・Draft PR作成・exact Head CI確認まで完了した時点で、

```text
FINISHED_AT_LOCAL: YYYY-MM-DD HH:MM
DURATION_MINUTES: integer
```

を算出し、最終報告とDraft PR本文へ記録してください。

この時間はFinal Synthesis execution metadataであり、H0/H1 candidateのSpeed Scoreへ遡及使用しません。

---

## 3. Authority

実装前に必ず一次証拠を読み直してください。

優先順位:

1. Issue #42本文
2. `AGENTS.md`
3. `docs/plans/phase-4-implementation-issue-decomposition.md`
4. Accepted ADR-0001 / ADR-0009
5. repository current main code / tests / CI
6. LOCK済みSelection / Adjudication
7. candidate implementationsは参考資料のみ

Selection / Adjudicationはcontrol branchから読むこと。control branchをmergeしないでください。

```bash
git show origin/agent/fnd04-benchmark-control:docs/benchmarks/fnd04-model-comparison/results/selection-adjudication.md
git show origin/agent/fnd04-benchmark-control:docs/benchmarks/fnd04-model-comparison/results/implementation-evaluation.md
```

Issue / ADRとbenchmark文書が矛盾する場合、Issue / ADRを優先してください。

---

## 4. Construction rule

Final Synthesisは**current mainから新規構築**してください。

禁止:

- candidate branchのmerge
- candidate commitのcherry-pick
- candidate branch / PRの変更
- candidate ranking / benchmark resultの変更
- `agent/fnd04-benchmark-control`への書き込み

candidate codeは`git show <SHA>:<path>`やdiff等で参照してよいですが、採用する設計を理解した上でFinal Synthesisとして構成してください。

---

## 5. Locked selection

### Primary — C5

```yaml
CANDIDATE: C5
MODEL_HARNESS: Claude Opus 5 / Claude Code
PR: 134
H1_HEAD: 3a788cc31b3f65177d60dd3995842231dd505187
H1_SCORE: 99
FINDINGS: 0 / 0 / 0 / 0
```

C5をarchitecture / production-path verificationの主軸にしてください。

参照してよい主要ファイル例:

```bash
git show 3a788cc31b3f65177d60dd3995842231dd505187:src/MinimalBankSystem.Infrastructure/Persistence/BankPersistence.cs
git show 3a788cc31b3f65177d60dd3995842231dd505187:src/MinimalBankSystem.Infrastructure/Persistence/BankDbContextFactory.cs
git show 3a788cc31b3f65177d60dd3995842231dd505187:src/MinimalBankSystem.Migrator/Program.cs
git show 3a788cc31b3f65177d60dd3995842231dd505187:tests/MinimalBankSystem.IntegrationTests/PostgreSql/MigrationBaselineTests.cs
git show 3a788cc31b3f65177d60dd3995842231dd505187:tests/MinimalBankSystem.IntegrationTests/Persistence/MigrationModelTests.cs
```

### Partial adoption — C1

```yaml
CANDIDATE: C1
MODEL_HARNESS: GPT-5.6 Sol / Codex
H1_HEAD: 7025c256b8b1ec1f0f4b9904f71a1047faac4cca
```

C1から採用するのは、**failed Migrator outputにcredential/passwordが漏れないことを確認するregression test**です。

C1 architecture全体へ置換しないでください。

### Mandatory regression — C8-M01

```yaml
CANDIDATE: C8
H1_HEAD: 8af19e033b79d42ab8a03b32521ec809fd0a8588
FINDING: C8-M01
SEVERITY: Major
```

C8の次のfallback patternは採用禁止です。

```text
Host=127.0.0.1;Port=5432;Database=design_time;Pooling=false;Timeout=5
```

`ConnectionStrings__Database`未設定時に、架空localhost / `design_time` / ambient default等の接続先をfabricateしてはいけません。

Final SynthesisではこのMajorを再発防止する自動testを必須追加してください。

最低条件:

1. child processの`ConnectionStrings__Database`を明示的にremoveする。
2. production `IDesignTimeDbContextFactory<BankDbContext>`を通るconnection-required EF operationを実行する。
3. non-zero / fail-closedを確認する。
4. fake provider / fabricated localhost / ambient defaultへfallbackしていないことを確認する。
5. test process自身のglobal environment variableを書き換えない。

推奨はrepository-local `dotnet-ef database update`等をchild processで実行する方法です。より単純で強い同等証拠があれば採用可です。

model-only EF operationが、connection stringなしのNpgsql contextを作ること自体は禁止しません。禁止対象は**架空destinationを生成してconnection-required operationへ使える状態にすること**です。

### Explicit non-selection — C6

```yaml
CANDIDATE: C6
H1_HEAD: af7bdc27f8daaae682a602946b04b122b50dee38
NON_SELECTED: TimeProvider timeout testability seam
```

C6のTimeProvider seamは初期Final Synthesisへ導入しないでください。

C5にはreal PostgreSQL lockでproduction entry pointのactual 60-second timeoutを通す証拠があります。CIで実際にflaky / 過大コストになる一次証拠が出ない限り、testability目的だけでproduction abstractionを増やさないでください。

---

## 6. Fixed Issue #42 implementation contract

### Package / tool exact versions

```text
Microsoft.EntityFrameworkCore                 10.0.10
Microsoft.EntityFrameworkCore.Design          10.0.10
Npgsql                                        10.0.3
Npgsql.EntityFrameworkCore.PostgreSQL         10.0.3
dotnet-ef                                     10.0.10
```

- `Microsoft.EntityFrameworkCore.Design`: tooling-only / `PrivateAssets=all`相当
- repository-local tool manifestへ`dotnet-ef` exact pin
- preview 11.x禁止

### Ownership

- `BankDbContext`: Infrastructure
- Npgsql provider configuration: Infrastructure
- migrations / snapshot: Infrastructure
- `IDesignTimeDbContextFactory<BankDbContext>`: Infrastructure
- migrations assembly: Infrastructure
- explicit one-shot migrator: `src/MinimalBankSystem.Migrator`
- MigratorからAPI hostを起動しない

### Connection configuration

```text
ConnectionStrings:Database
ConnectionStrings__Database
```

- credential付きconnection stringをrepoへ固定しない
- passwordをCLI argumentへ埋め込まない
- API / Migrator / design-timeはsame PostgreSQL provider / migrations assembly

### Empty baseline

```text
Migration name: InitialFoundation
History: public.__EFMigrationsHistory
```

- `Up` / `Down`にbusiness table / sequence / trigger / constraint等を追加しない
- apply後はmigration historyだけが成立する

### Explicit Migrator

標準経路:

```bash
dotnet run --project src/MinimalBankSystem.Migrator
```

- latest migrationまでapply
- successのみ0
- connection / migration / timeout failureはnon-zero
- database command timeout = 60 seconds
- whole migration cancellation budget = 60 seconds
- pending model warningをignoreして成功扱いにしない

### API startup

通常API startupで次を禁止:

- `Database.Migrate` / `MigrateAsync`
- `EnsureCreated` / `EnsureCreatedAsync`
- migration CLI / shell apply
- ad-hoc schema DDL

### Model drift

標準command:

```bash
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
```

constant comparison / migration listだけで代替しない。

### Idempotent SQL

```bash
dotnet tool run dotnet-ef migrations script --idempotent \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator
```

review / release evidence生成経路として文書化する。

---

## 7. Required implementation characteristics

C5を土台に、次の状態を成立させてください。

- shared Infrastructure helperでprovider / migrations assembly / history table設定を一元化
- APIとMigratorはそれぞれ.NET Configurationからcanonical keyを解決
- design-time factoryはenvironment formを使用可能
- model-only design-time時にfake/fabricated connection destinationを生成しない
- connection-required design-time時、接続情報がなければfail-closed
- Migratorはstructured loggingを使用してよいがsecretを出力しない
- 60秒budgetをコードと実testの両方で固定
- migration failureを成功へ変換しない
- APIはDbContextをDI resolve可能だがstartup schema mutationなし
- FND-03 PostgreSQL fixtureを再利用
- business schema / FND-05 Compose / FND-06 healthを先取りしない

単純性を優先し、候補の全機能を合成しないでください。

---

## 8. Mandatory automated evidence

少なくとも次を自動testまたは同等の再現可能なverificationで固定してください。

### P01 Identity / package

- exact package versions
- exact local dotnet-ef 10.0.10
- migrations assembly = Infrastructure
- provider = Npgsql PostgreSQL

### P02 Clean real PostgreSQL apply

- clean DB
- history tableなしから開始
- explicit Migrator process exit 0
- `InitialFoundation`がhistoryへ1件
- business tableなし

### P03 Rerun

- second Migrator run exit 0
- history unchanged

### P04 Failure propagation

- missing connection -> non-zero
- unreachable server -> non-zero
- rejected credential -> non-zero

### P04b Secret non-disclosure — C1 adoption

- sentinel passwordを持つfailure connectionで実行
- stdout / stderrへsentinel passwordが含まれない
- testを通すためだけの不要なproduction abstraction追加は禁止

### P05 Actual bounded execution

- real PostgreSQL側でmigration executionをblocking
- production Migrator processが約60秒でnon-zero timeout
- external test timeoutだけでproduction 60秒を代替しない

### P06 API no-auto-migration

- clean PostgreSQLへAPI startup
- startup前後でhistory / schema unchanged
- migrated DBへ再startupしてもunchanged

### P06b API actual DbContext resolve

- API servicesから実`BankDbContext`をresolve
- Npgsql provider / Infrastructure migrations assembly / intended connectionを確認
- resolve後もschema mutationなし

### P07 / P08 Pending model

- clean sourceでactual `HasPendingModelChanges` / standard CLI pathがPASS
- evaluator-only temporary model-only changeでpending modelを実際に検出
- temporary changeを完全discard
- clean sourceへ戻すと再度PASS
- temporary entity / migrationをcommitしない

### P09 Forbidden schema path

- application sourceに`EnsureCreated` / startup migration applyなし

### P10 Design-time/runtime consistency + C8-M01 regression

- API / Migrator / design-timeがNpgsql + same migrations assembly
- model-only operationはconnectionなしでもfabricated destinationなし
- child processでconnection envをremoveしたconnection-required EF operationがnon-zero / fail-closed
- fake / SQLite / InMemory / localhost `design_time` fallbackなし

### P11 Idempotent SQL

- repository-local commandで生成成功
- `InitialFoundation`を含む

### P12 Scope boundary

- business entity / business DDLなし
- Compose / health等の先取りなし

### P13 FND-03 regression

- existing real PostgreSQL fixture / lifecycle / CI regressionが全PASS

---

## 9. Local verification

実装後、少なくとも次を実行してください。repositoryの実際の構成に合わせ、同等以上のcommandへ調整可です。

```bash
dotnet tool restore
dotnet restore MinimalBankSystem.slnx
dotnet build MinimalBankSystem.slnx --no-restore
```

non-PostgreSQL testとreal PostgreSQL categoryを分けて実行し、両方PASSを記録してください。

さらに:

```bash
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator

dotnet tool run dotnet-ef migrations script --idempotent \
  --project src/MinimalBankSystem.Infrastructure \
  --startup-project src/MinimalBankSystem.Migrator

git diff --check main...HEAD
```

### Temporary model drift probe

一時的なmodel-only changeを入れ、standard pending-model checkが失敗することを確認してください。

その後必ず変更をdiscardし、

- clean pending-model check PASS
- `git status`にprobe残骸なし

を確認してください。

このtemporary probeをcommitしてはいけません。

---

## 10. Self-review before push

push前にcurrent diffをIssue #42へ照合し、次を確認してください。

- business schemaなし
- Composeなし
- health変更なし
- unrelated refactorなし
- candidate / benchmark artifact変更なし
- C8 fabricated fallbackなし
- API auto-migrationなし
- `EnsureCreated`なし
- secret / credential commitなし
- timeout失敗がsuccessにならない
- test assertionの主張が実際の証拠より強くない

問題があればpush前に修正してください。

---

## 11. Git / Draft PR

local validation完了後のみcommit / pushしてください。

- Target branch: `agent/issue-42-fnd-04-final-code`
- Base: `main`
- Draft PRを作成
- PR title例: `[FND-04] EF Core・明示的migration実行基盤 — Final Synthesis`
- `Refs #42`を含める

PR本文には少なくとも次を記録してください。

- Final Synthesisであること
- Base SHA / Head SHA
- C5をprimaryとしたselection
- C1からsecret non-disclosure regressionを採用
- C8-M01 patternを拒否しmandatory regressionを追加
- C6 TimeProvider seamを意図的に採用しなかった理由
- architecture
- changed files
- Acceptance Criteria evidence
- local commands / results
- temporary drift probe結果とdiscard確認
- C8-M01 regression結果
- exact Head CI
- known concerns / unverified
- Duration metadata

---

## 12. CI

push / Draft PR後、**exact final Head SHA**に対するCIを確認してください。

- restore
- build
- non-PostgreSQL tests
- real PostgreSQL tests

すべてSUCCESSが必要です。

失敗した場合は原因を修正し、Headが変わったら新しいexact Head CIを取り直してください。

CI runの古いHeadを最終証拠として使わないでください。

---

## 13. Prohibited actions / stop boundary

この作業では次を行わないでください。

- candidate branch / PR変更
- `agent/fnd04-benchmark-control`変更
- benchmark score / ranking変更
- PR Ready化
- merge
- Issue #42 close
- main direct push
- FND-05実装
- FND-06実装
- business schema追加

Final Synthesis Draft PR + exact Head CIまでで停止してください。

---

## 14. Final report format

作業終了時、次を日本語で報告してください。

```text
## Result

Branch:
Base SHA:
Head SHA:
Draft PR:
Exact Head CI:

## Duration
STARTED_AT_LOCAL:
FINISHED_AT_LOCAL:
DURATION_MINUTES:

## Final architecture
- ...

## Selection application
- C5 primary: APPLIED / NOT APPLIED
- C1 secret non-disclosure: APPLIED / NOT APPLIED
- C8-M01 regression: APPLIED / NOT APPLIED
- C6 TimeProvider non-selection: PRESERVED / CHANGED WITH EVIDENCE

## Verification
- tool restore:
- restore:
- build:
- non-PostgreSQL:
- real PostgreSQL:
- clean apply:
- rerun:
- failure propagation:
- actual 60s timeout:
- secret non-disclosure:
- API no-auto-migration:
- API DbContext resolve:
- pending-model positive:
- temporary drift negative:
- clean recovery:
- idempotent SQL:
- C8-M01 fail-closed:
- git diff --check:

## Scope check
- business schema:
- Compose:
- health:
- candidate / benchmark modification:

## Known concerns
- ...

## Unverified
- ...

## Independent review handoff
- review target PR / Head
- highest-risk areas
```

Final Synthesisをmerge-readyとは自己判定しないでください。次工程は独立Reviewerによる再検証です。
