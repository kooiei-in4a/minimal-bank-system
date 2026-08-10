# FND-04 Final Synthesis Independent Review

Reviewer:

- Slot: R1
- Model: GPT-5.6 Sol
- Harness: Codex
- Effort: xHigh
- Primary role: runtime_failure_path
- Attempt: 1

Target verification:

- Repository: `kooiei-in4a/minimal-bank-system`
- PR: #140
- Base SHA: `38c07e210fe4e8689f1d8aeabbb07b92610d1826`
- Head SHA: `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- PR state: open / draft / unmerged。レビュー終了時にもHead不変
- Control gate: Parent Issue #3の最新同期でFND-04はIssue Ready PASS / Implementation PERMITTED
- CI identity:
  - Run `31350916189`: `pull_request`、実checkoutはPR merge ref `d12de2ae07003a10d19d576808cf88ec7796da23`
  - Run `31350870902`: `push`、実checkoutはdirect Head `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- Result: PASS

Verdict:

- APPROVE_WITH_FINDINGS

Merge-ready:

- YES（Blocker/Major 0。PRは現在draft）

Findings:

### Blocker

- NONE

### Major

- NONE

### Minor

#### R1-F01 — command timeoutとwhole-operation cancellationの同時発火時にexit codeが非決定的になり得る

- ID: `R1-F01`
- Severity: Minor
- blocking: no
- affected component/path:
  - `src/MinimalBankSystem.Infrastructure/Persistence/BankPersistence.cs`
  - `src/MinimalBankSystem.Migrator/Program.cs`
  - `tests/MinimalBankSystem.IntegrationTests/PostgreSql/MigrationBaselineTests.cs`
- evidence:
  - 同じ定数60秒がNpgsql `CommandTimeout`とwhole-operation CTSの両方に使われる。
  - MigratorはCTS由来の`OperationCanceledException`だけをexit 2へ分類し、Npgsql timeoutが`NpgsqlException`として先に表面化すると一般failureのexit 1になる。
  - Npgsql 10.0.3はtimeout時に`UserCancellationRequested`が未成立なら`NpgsqlException`、成立済みなら`OperationCanceledException`を生成する。
  - CIと今回のlocal実行ではexit 2となり問題は再現していない。ただし同一deadlineの独立timer間に順序保証はなく、非決定性はsourceからの推論。
  - committed testは単発のexit 2を確認する一方、許容上限は60秒＋60秒で、command-timeout先行経路を分離検証していない。
- root cause: 独立した二つのtimeout機構を同一deadlineで発火させ、例外型だけでexit semanticsを分類している。
- impact: bounded failureとnon-zero要件は維持されるが、実際のtimeoutがexit 1と一般failure logへ分類され、文書化されたexit 2 semanticsや運用診断が不安定になり得る。
- required fix direction: blockingではない。Npgsql timeoutを明示的にexit 2へ分類するか、deadline順序を明確に分離し、command timeoutとwhole-operation cancellationをそれぞれ決定的に検証することを推奨。

### Nit

#### R1-F02 — PR本文の「Exact Head CI」がmerge-ref runを指している

- ID: `R1-F02`
- Severity: Nit
- blocking: no
- affected component/path: PR #140本文 / CI evidence metadata
- evidence:
  - PR本文の「Exact Head CI」はRun `31350916189`を参照。
  - 同runのcheckout logは`d12de2ae...`を`refs/remotes/pull/140/merge`としてcheckout。
  - direct Headを実checkoutした成功runは別のpush Run `31350870902`。
- root cause: Actions runの`head_sha`関連付けをrunner上の実checkout SHAと同一視した。
- impact: コード品質への影響はないが、監査証跡上でmerge-state CIとdirect-head CIが混同される。
- required fix direction: blockingではない。PR本文のCI欄で両runを分け、各checkout SHAを記載することを推奨。

Probe matrix:

- P01: PASS — EF Core/Design/dotnet-ef `10.0.10`、Npgsql/provider `10.0.3`をexact固定。Infrastructure ownershipと専用Migratorを確認。
- P02: PASS — local real PostgreSQLでclean apply、exit 0、history 1件、business tableなし。
- P03: PASS — 2回目もexit 0、history不変。
- P04: PASS — missing、unreachable、rejected authentication、malformed historyがproduction processでnon-zero。
- P05: PARTIAL — 実PostgreSQL lockでproduction Migratorが自発的にexit 2。local test所要1分8秒で、外部180秒waitは未発火。R1-F01の例外分類競合は残る。
- P06: PASS — API startup/requestおよび実`BankDbContext` resolve後もschema/history不変。
- P07: PASS — repository-local pending-model commandと`HasPendingModelChanges()`を使用。
- P08: PASS — 一時コピーへのmodel-only変更でexit 1、除去・rebuild後exit 0。Headへの残骸なし。
- P09: PASS — APIに`Migrate`、`EnsureCreated`、ad-hoc DDLなし。
- P10: PASS — factoryは同一Npgsql provider/migrations assemblyを使用し、接続先をfabricateしない。EF公式仕様でも`IDesignTimeDbContextFactory`は他の生成方法より優先。
- P11: PASS — local repository-local `dotnet-ef migrations script --idempotent`が成功し、baseline history insertをguard。
- P12: PASS — business entity/table/constraint、Compose、health、FND-05/FND-06先取りなし。
- P13: PASS — direct-head/merge-ref CI双方でreal PostgreSQL 23件成功。FND-03 fixtureのownership/lifecycle変更なし。
- P14: PASS — rejected credential testはproduction stdout/stderrを別々に取得しsentinel非露出。local unreachable probeでもdummy password非露出。
- P15: PARTIAL — merge-ref CIとdirect-head CIは双方成功し実checkoutも確認済み。ただしPR本文のrun表記がR1-F02のとおり不正確。

Primary-role deep dive:

- 実entry pointは専用one-shot executableで、`GetPendingMigrationsAsync → MigrateAsync → GetAppliedMigrationsAsync`全体へ60秒tokenを渡している。
- missing/unreachable/auth/malformed migrationはすべて成功扱いされずexit 1。timeoutは観測実行でexit 2。
- Npgsql command timeoutは各DB commandを制限し、CTSはpending取得・migration・applied取得を含むwhole operationを制限する。両者は別機構であり、R1-F01の分類上の境界問題を除けば実装はこの区別を満たす。
- failure logはJSON consoleへ固定メッセージとexceptionを出す。認証失敗のsentinel passwordはstdout/stderrの双方に出なかった。
- C8系について、model-only factoryは接続文字列なしのNpgsql optionsを作るだけで、localhost・`design_time`・SQLite・InMemory destinationを生成しない。

CI assessment:

- Direct-head Run `31350870902`: checkout `99cee438...`、build warning 0 / error 0、pending-model PASS、Unit 4、non-PostgreSQL Integration 38、real PostgreSQL 23件成功。
- PR Run `31350916189`: checkout `d12de2ae...`、同じ全step成功。
- したがってmerge-stateとdirect-headの双方に成功証拠がある。Run `31350916189`単独をdirect-head checkoutとは扱っていない。

Unverified / evidence limits:

- localでのconnection-required `dotnet-ef database update`は、接続先誤解決時の変更リスクにより実行許可されなかった。代わりにexact direct-head CIで同committed testが成功したこと、factory source、EF公式factory優先順位を照合。
- local full PostgreSQL categoryは外部runnerの304秒上限で完了結果を取得できなかった。この外部timeoutをproduction timeoutの証拠には使用していない。重点`MigrationBaselineTests`は別途9/9成功し、CIではfull 23件成功。
- 一時model drift probeは固定Headの一時展開コピーだけで実施し、元blobとhash一致へ復帰後に一時ディレクトリを削除。
- repository worktreeは`main`のままclean。branch、PR、Issue、コードへの変更・投稿なし。

Final rationale:

- Issue #42の主要ACは実diff、committed tests、direct-head/merge-ref CI、local production-path probeで成立。
- Blocker/Majorはない。R1-F01はtimeout時の診断的exit semanticsに限定されたMinor、R1-F02は証拠表記のNitであり、どちらもmigration failureを成功扱いする欠陥ではない。
- よって`APPROVE_WITH_FINDINGS`、merge-readyは`YES`と判定。
