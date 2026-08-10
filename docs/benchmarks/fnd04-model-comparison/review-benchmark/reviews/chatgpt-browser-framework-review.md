# FND-04 Final Synthesis Independent Review

Reviewer:

- Slot: **R4**
- Model: **GPT-5.6 Sol**
- Harness: **Browser**
- Effort: **NOT EXPOSED**
- Primary role: **framework_official_source_cross_check**
- Attempt: **1**

Target verification:

- Repository: `kooiei-in4a/minimal-bank-system`
- PR: **#140**
- Base SHA: `38c07e210fe4e8689f1d8aeabbb07b92610d1826`
- Head SHA: `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- PR state: **OPEN / DRAFT / UNMERGED / mergeable=true**
- CI identity: **PR merge-ref** `d12de2ae07003a10d19d576808cf88ec7796da23`
  - checkout内容は `Merge 99cee438... into 38c07e...`
  - direct branch Head checkoutのpush CIは、このレビューで独立確認できず
- Canonical review contract: `fnd04-final-review-v1` を確認
- Result: **PASS**

## Verdict

- **APPROVE**

## Merge-ready

- **YES**
- ただしこれは技術レビュー上の判定です。GitHub上のPR自体は現在もDraftです。

## Findings

### Blocker

- **NONE**

### Major

- **NONE**

### Minor

- **NONE**

### Nit

- **NONE**

## Probe matrix

- **P01: PASS — Identity / version / ownership**
  EF Core / Design / Relationalは`10.0.10`、Npgsql / providerは`10.0.3`、repository-local `dotnet-ef`は`10.0.10`でexact pin。`BankDbContext`、factory、provider config、migrationはInfrastructureにあり、migrations assemblyも`BankDbContext` assemblyへ明示。
- **P02: PASS — Clean real PostgreSQL apply**
  committed real-PostgreSQL testは実際のMigrator processをclean DBへ投入し、exit 0、`InitialFoundation` 1件、public tableは`__EFMigrationsHistory`だけであることを確認。`InitialFoundation`の`Up` / `Down`は空。
- **P03: PASS — Rerun**
  同一Migratorを2回実行し、2回目も成功、migration history不変を実DBで検証。
- **P04: PASS — Failure propagation**
  missing connection、unreachable server、rejected credential、malformed migration historyがすべてnon-zeroとなるproduction-process testがある。Migratorはmissing configurationを明示例外にし、一般exceptionもFailureへ変換するだけで成功扱いしない。
- **P05: PASS — Actual 60-second bounded execution**
  production Migrator自身が60秒CTSを生成し、`GetPendingMigrationsAsync`、`MigrateAsync`、`GetAppliedMigrationsAsync`へ同じtokenを渡す。Npgsql relational command timeoutも60秒。実PostgreSQLでlock待ちを発生させ、external 180秒watchdogではなくproduction processがexit code `Timeout`で約60秒付近に停止するテストもCIで成功。公式仕様とも整合。
- **P06: PASS — API no-auto-migration**
  APIは`BankDbContext`をDI登録するだけで、startup pathに`Migrate` / `EnsureCreated` / DDLなし。実PostgreSQLでAPI起動前後とDbContext resolve後のschema不変を検証。ADR-0009の明示migrator方針とも一致。
- **P07: PASS — Pending model positive**
  `Database.HasPendingModelChanges()`を直接使用しており、migration数などのtautologyではない。CIも標準`dotnet ef migrations has-pending-model-changes`を実行。
- **P08: PARTIAL — Temporary model drift negative**
  framework設計としては成立。snapshotにはprovider metadataが記録され、現在のDbContextはbusiness entity 0。ただし一時entity追加でexit 1になったnegative probeはPR本文のlocal-only自己申告であり、このReview-only Browser実行では再現していない。
- **P09: PASS — Forbidden paths**
  source scan testと実API testの両方で`EnsureCreated`、startup migration、schema mutationを防止。
- **P10: PASS — Design-time/runtime consistency / C8-M01**
  `IDesignTimeDbContextFactory<BankDbContext>`がInfrastructureにあり、connection有無にかかわらずNpgsql + Infrastructure migrations assemblyを構成。connectionless `UseNpgsql(...)`はproviderを構成するがdestinationを初期化しない公式パターンで、接続必須`database update`はconfigurationなしでfail-closed。committed regression test単体の「outputに禁止文字列がない」assertionだけなら証拠は弱めだが、production factory、standard `database update`、provider公式semantics、CIでmodel-only commandが正常動作する組合せでdestination safetyは十分裏付けられていると判断。
- **P11: PASS — Idempotent SQL**
  `IMigrator.GenerateScript(...Idempotent)`でbaseline guardを検証し、repository-local CLI commandもREADMEに固定。
- **P12: PASS — Scope boundary**
  `BankDbContext`にはentityがなく、`InitialFoundation`にもschema operationなし。business schema先取りなし。
- **P13: PASS — FND-03 regression**
  PRはFND-03 fixture自体を置換せず、そのreal PostgreSQL foundation上で23件のPostgreSQL integration testをCI成功。
- **P14: PASS — Secret non-disclosure**
  rejected passwordをsentinelへ差し替え、実Migrator subprocessのstdout / stderr双方からsentinel不在を確認。mock loggerではなくproduction JSON logger + production exception pathを含む。
- **P15: PARTIAL — CI fidelity**
  Run `31350916189` / job `build-test`を実ログで確認し、checkoutは`d12de2ae...` PR merge-ref。build 0 warning / 0 error、pending-model、non-PostgreSQL 42件、real PostgreSQL 23件成功。一方PR本文はこのrunを「Exact Head CI」と表記。利用可能なconnectorではdirect-head push runを独立確認できず、PARTIAL。

## Primary-role deep dive

### EF Core design-time

`IDesignTimeDbContextFactory<TContext>`をcontext project側に置く設計は公式semanticsと一致し、factory発見時にはEF Toolsが通常のhost/DI経路よりfactoryを優先する。`dotnet ef database update`はconnection-required operationであり、connectionless Npgsql contextはmodel inspectionには使えても、そのままDB updateを成功させるものではない。

### Model drift / empty baseline

空modelでもdrift checkは有効。EFはmodel snapshotをsource controlへ保持し現在modelとの差をmigration生成・drift判定へ利用する。empty migration自体もframework上サポートされるため、migration machineryのみを確立するFND-04の空`InitialFoundation`は不自然ではない。

### Migration semantics

`MigrateAsync(token)`はpending migrationsを適用する公式APIで、`__EFMigrationsHistory`により適用済みmigrationを追跡する。今回のコードはInfrastructure migrations assemblyと`public.__EFMigrationsHistory`を固定し、実DBテストでempty migrationでもhistory rowが作られることを証明。

### Timeout / cancellation

production process全体へ60秒CTSをかけ、database operationへ同じtokenを渡す。relational `CommandTimeout(60)`も設定され、「CTSだけ60秒・database command timeoutはdefaultのまま」というズレはない。実lock testはbuilt Migrator assemblyを子process起動しており、test-only timeout abstractionではない。

### Failure / secret

`MigratorLog.MigrationFailed`はexceptionをlogging subsystemへ渡すため自動的に安全とは評価しないが、rejected credentialについて実Npgsql auth failure → production catch → production JSON logging → stdout/stderr captureを通しpassword sentinelが出ないことを確認している。

### C8-M01結論

**C8-M01型リスクは、このHeadでは再現しないと判断。** connectionless Npgsql構成はfake destinationを作らず、DB接続時のみexplicit connectionが必要。production factoryを通す`database update`のnon-zero regressionと合わせ、Issue #42のfail-closed contractを満たす。

## CI assessment

Run `31350916189`は成功。実ログで確認したcheckoutはPR #140 merge ref `d12de2ae07003a10d19d576808cf88ec7796da23`、merge contentは`99cee438...` into `38c07e...`。Build success / 0 warning / 0 error、pending-model success、non-PostgreSQL 42 passed、real PostgreSQL 23 passed。したがってPR merge-state CI成功の証拠として有効だが、direct `99cee...` checkoutのpush runは今回確認できず、「exact direct-head CI成功」とは表現しない。

## Unverified / evidence limits

- **P08:** temporary model-only drift negative probeはPR本文上のlocal-only evidence。設計・clean-side証拠は確認したが、一時的にmodelを改変してexit 1を独立再現していない。
- **P15:** direct branch Head `99cee...` checkoutのpush CIは独立確認できず。確認済みRun `31350916189`はPR merge-ref CI。
- Browser reviewのため、reviewer独自のlocal build/testは未実施。
- 上記2点はいずれも現時点で実コード欠陥を示しておらず、Blocker / Major / Minorへは昇格しない。

## Final rationale

**Blocker 0 / Major 0 / Minor 0 / Nit 0。APPROVE。**

EF Core / Npgsql framework semanticsを公式一次資料と突合した結果、design-time factory、connectionless model-only Npgsql、pending model、empty migration、migration history、idempotent script、`MigrateAsync`、command timeout / cancellationの主要な実装主張はいずれも公式挙動と整合。C8-M01についてもfake destinationを作らないconnectionless Npgsql構成であることを確認し、connection-required operationはexplicit destinationなしでは成立しない。残るP08/P15は証拠identity / 再現性の限界であり、現Headのmergeを止めるframework defectとは判断しない。
