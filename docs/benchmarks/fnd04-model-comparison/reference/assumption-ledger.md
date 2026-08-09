# FND-04 Pre-Run Assumption Ledger

Status: **LOCKED BEFORE CANDIDATE EXECUTION**

Lock date: 2026-08-09

この文書はcandidate実装を見る前に、FND-04の外部library semanticsとproject-owned実装境界を固定する。Issue #42とAccepted ADRが優先する。

## A-01 — EF Core stable line

- `Microsoft.EntityFrameworkCore`: `10.0.10`
- `Microsoft.EntityFrameworkCore.Design`: `10.0.10`
- `dotnet-ef`: `10.0.10` repository-local tool
- Source checked: official NuGet, 2026-08-09

Microsoft EF Core packagesは同一patch lineへ揃える。preview 11.xを使用しない。

## A-02 — Npgsql provider line

- `Npgsql`: repository既存 `10.0.3`
- `Npgsql.EntityFrameworkCore.PostgreSQL`: `10.0.3`
- Source checked: official NuGet, 2026-08-09

## A-03 — Migration-only schema evolution

Accepted ADR-0009により、application-owned schema evolutionはEF Core migrationsのみ。`EnsureCreated`やnormal API startupのad-hoc DDLを代替に使用しない。

## A-04 — Explicit migrator

Normal API startupとmigration executionを分離する。FND-04ではdedicated one-shot `MinimalBankSystem.Migrator` entry pointを成立させ、FND-05がCompose orderingへ接続する。

## A-05 — Infrastructure ownership

- `BankDbContext`
- EF Core / Npgsql provider configuration
- migrations / model snapshot
- `IDesignTimeDbContextFactory<BankDbContext>`

は`MinimalBankSystem.Infrastructure`が所有する。

migrations assemblyもInfrastructureとする。MigratorはInfrastructureを参照するが、API hostを起動しない。

## A-06 — Connection configuration

canonical keyは`ConnectionStrings:Database`とする。

環境変数では`.NET` configuration conventionに従い`ConnectionStrings__Database`を使用できる。

- credential付きconnection stringをrepositoryへ固定しない
- password等をmigrator / design-time toolのcommand-line引数へ埋め込まない
- 未設定時にSQLite / InMemory / fake providerへfallbackしない

## A-07 — Empty foundation baseline

FND-04はbusiness schemaを所有しないため、baseline migrationとして`InitialFoundation`を要求する。

- application business table / sequence / trigger / constraintを作らない
- `Up` / `Down`へbusiness DDLを入れない
- apply後に`public.__EFMigrationsHistory`へbaselineが記録されることでmachineryを検証する

最初のnon-empty business migrationは将来のschema-owning Issueが所有する。

ADR-0009のprevious-schema + representative-row upgradeは、最初のnon-empty schema-owning migrationから適用する。FND-04では`0 -> InitialFoundation`がforward baseline validationとなる。

## A-08 — Pending model changes

EF Core 10では `dotnet ef migrations has-pending-model-changes` と programmatic `HasPendingModelChanges()` が利用可能。標準drift checkは実際にこの機能へ到達する必要がある。

Evaluator-only probeでは、一時的なmodel-only changeを作り、drift command/testがFAILすることを確認して変更を破棄する。candidate repositoryへsynthetic business entityを残さない。

## A-09 — Migrate with pending changes

EF Core 9以降、pending model changesがある状態の`Migrate/MigrateAsync`は例外となる。このwarningをignoreしてgreenにする設定を標準採用しない。

## A-10 — EnsureCreated incompatibility

`EnsureCreated/EnsureCreatedAsync`はMigrationsを迂回するため、application schema evolutionの検証へ使用しない。

## A-11 — Real PostgreSQL evidence

FND-03で確立したPostgreSQL 18.4 fixtureを再利用する。clean apply / failure / no-auto-migration verificationをSQLite/InMemoryへ置換しない。

## A-12 — API startup no-migration proof

clean PostgreSQLへAPIを通常起動しても、migration historyまたはapplication schemaが勝手に変化しないことをbefore/afterで確認する。単に`Program.cs`に`Migrate`文字列がないことだけを証拠にしない。

## A-13 — Failure propagation

explicit migratorのconnection / migration / timeout failureは成功扱いにしない。

- failure: non-zero process exit
- success: zero process exit
- failure pathをcatchして成功へ変換しない

## A-14 — Bounded migration execution

Issue #42でmigration database command timeout / cancellation budgetを**60 seconds**へ固定した。無期限hangを許容しない。

## A-15 — Idempotent SQL evidence

ADR-0009に従い、EF Core CLIの`migrations script --idempotent`でreview / release evidence用SQLを生成できる経路を確認する。FND-04ではempty baselineでもmachinery成立を確認する。

生成SQLはproduction deploymentそのものではなくevidence generation pathとして扱う。

## A-16 — Design-time creation

`IDesignTimeDbContextFactory<BankDbContext>`をInfrastructureへ置く。EF Core CLIはAPI startupへ依存せず、runtimeと同じprovider / migrations assemblyを使用する。

## Authoritative external sources checked before lock

- Microsoft / NuGet: EF Core 10.0.10
- Microsoft / NuGet: `dotnet-ef` 10.0.10
- Microsoft / NuGet: EF Core Design 10.0.10
- Npgsql / NuGet: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3
- Microsoft Learn: `migrations has-pending-model-changes`, `HasPendingModelChanges()`, pending-change migration behavior, `EnsureCreated` migration incompatibility, idempotent migration scripts

## Lock rule

このledgerをcandidate結果を見た後で有利不利に変更しない。

変更が必要になった場合:

1. revision numberを上げる
2. reasonを記録する
3. candidate outputを見た後の変更か明示する
4. 全candidateへ同一基準を遡及適用する
5. pure pre-locked benchmarkとして扱えなくなった場合はその制約を明記する
