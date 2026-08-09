# FND-04 Pre-Run Assumption Ledger

Status: **DRAFT FOR ISSUE-READY LOCK / NO CANDIDATE OUTPUT READ**

この文書はcandidate実装を見る前に、FND-04の外部library semanticsとproject-owned実装境界を固定する。Issue #42とAccepted ADRが優先する。

## A-01 — EF Core stable line

- Microsoft EF Core packages: `10.0.10`
- `dotnet-ef`: `10.0.10` repo-local toolとして固定する想定
- Source checked: Microsoft / NuGet, 2026-08-09

Microsoft EF Core packagesは同一patch lineへ揃える。preview 11.xを使用しない。

## A-02 — Npgsql provider line

- `Npgsql`: repository既存 `10.0.3`
- `Npgsql.EntityFrameworkCore.PostgreSQL`: `10.0.3`
- Source checked: Npgsql / NuGet, 2026-08-09

## A-03 — Migration-only schema evolution

Accepted ADR-0009により、application-owned schema evolutionはEF Core migrationsのみ。`EnsureCreated`やnormal API startupのad-hoc DDLを代替に使用しない。

## A-04 — Explicit migrator

Normal API startupとmigration executionを分離する。FND-04ではdedicated one-shot migrator entry pointを成立させ、FND-05がCompose orderingへ接続する。

## A-05 — Empty foundation baseline

FND-04はbusiness schemaを所有しないため、baseline migrationはempty application migrationを許可・要求する。

- application business table / sequence / triggerを作らない
- `Up` / `Down`へbusiness DDLを入れない
- apply後にEF migration historyへbaselineが記録されることをもってmachineryを検証する

最初のnon-empty business migrationは将来のschema-owning Issueが所有する。

## A-06 — Pending model changes

EF Core 10では `dotnet ef migrations has-pending-model-changes` と programmatic `HasPendingModelChanges()` が利用可能。標準drift checkは実際にこの機能へ到達する必要がある。

Evaluator-only probeでは、一時的なmodel-only changeを作り、drift command/testがFAILすることを確認して変更を破棄する。candidate repositoryへsynthetic business entityを残さない。

## A-07 — Migrate with pending changes

EF Core 9以降、pending model changesがある状態の`Migrate/MigrateAsync`は例外となる。この挙動を無効化してgreenにする設定を標準採用しない。

## A-08 — EnsureCreated incompatibility

`EnsureCreated/EnsureCreatedAsync`はMigrationsを迂回するため、application schema evolutionの検証へ使用しない。

## A-09 — Real PostgreSQL evidence

FND-03で確立したPostgreSQL 18.4 fixtureを再利用する。clean apply / failure / no-auto-migration verificationをSQLite/InMemoryへ置換しない。

## A-10 — API startup no-migration proof

clean PostgreSQLへAPIを通常起動しても、migration historyまたはapplication schemaが勝手に変化しないことをbefore/afterで確認する。単に`Program.cs`に`Migrate`文字列がないことだけを証拠にしない。

## A-11 — Failure propagation

explicit migratorのconnection / migration failureは成功扱いにしない。process exit / exception / CI結果でfail-closedを確認する。

## A-12 — Bounded migration execution

ADR-0009のbounded timeout要求を維持する。無期限hangを許容しない。timeout値そのものはIssue #42の実装契約で固定する。

## A-13 — Idempotent SQL evidence

ADR-0009に従い、EF Coreが対象rangeで生成可能なidempotent migration SQLをreview / release evidenceとして生成できる経路を確認する。FND-04ではempty baselineでもmachinery成立を確認する。

## Lock rule

このledgerをcandidate結果を見た後で有利不利に変更しない。変更が必要な場合はrevision reasonを記録し、全candidateへ同一適用する。
