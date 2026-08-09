# Independent Review Result

## Target

- Benchmark ID: fnd03-final-synthesis-independent-review
- Run ID: fnd03-final-91e3fca-20260809
- Repository: kooiei-in4a/minimal-bank-system
- Issue: #41
- PR: #104
- Base SHA: 7946cc55e49c0c6e21ad7b86c20a8435b4976269
- Head SHA: 91e3fca181558cd1523390347f4f2f80d6014d26
- CI target SHA: 91e3fca181558cd1523390347f4f2f80d6014d26

## Reviewer

- Model: DeepSeek V4 Flash
- Harness: Open Code
- Effort: 指定値
- Reviewer slug: deepseek-v4-flash-opencode
- Attempt: 1

## Target verification

- Repository: PASS
- PR: PASS
- Base SHA: PASS
- Head SHA: PASS
- CI SHA: PASS

## Verdict

APPROVE

- Blocker: 0
- Major: 0
- Minor: 0
- Nit: 1

## Findings

### F-01 — Nit — `ConsoleCapture`のlockは.NET 10の`SyncTextWriter`がロックレス実装のため、write-read排他には寄与しない

- Blocking: false
- Affected component: `tests/MinimalBankSystem.IntegrationTests/ApiRuntimeContractTests.cs` (ConsoleCapture)
- Description:
  CI race修正として追加された `lock (synchronizedWriter)` は、同一 `ConsoleCapture` インスタンスへの複数reader/disposerアクセスを直列化するが、**書き込み側（`Console.Out`/`Console.Error`経由）とは相互排他されない**。`TextWriter.Synchronized(buffer)` が返す `SyncTextWriter` は .NET 10ではMonitorを一切使用しない純粋なロックレスデコレータであり（runtime probeで確認）、writeと`buffer.ToString()`読み取りの間のraceはこのlockでは防げない。raceの実質的な解決は、`[Collection(TestExecutionCollections.ConsoleSensitive)]`による直列化とCIのカテゴリ分割（non-PG/PGステップ分離）によるものである。実害は観測されず（CI 2 runs、ローカル4 runsで25/25安定。`capture.Content`読み取りはテスト構造上、write完了後に発生する）、修正が誤った結果を生むこともないためNitとする。
- Evidence:
  - .NET 10.0.302 runtime probe: `System.IO.TextWriter+SyncTextWriter` の `Write(char)`/`Flush()`/`Dispose(bool)` のILに`Monitor.Enter`/`Monitor.Exit`が存在せず、フィールドは`_out`（デコレート対象）のみ。`lock(sync)`保持中も`sync.Write`がブロックされないことを動作検証で確認（S1/A probe）。
  - `ApiRuntimeContractTests.cs` の修正後コード: `lock (synchronizedWriter) { synchronizedWriter.Flush(); return buffer.ToString(); }` はwrite側（ロックレス）と排他されない。
  - 一方、`[Collection(TestExecutionCollections.ConsoleSensitive)]`（`DisableParallelization = true`）への配置、CIの`Category!=PostgreSqlIntegration`と`Category=PostgreSqlIntegration`のステップ分割、並行collectionのテストがConsoleを操作しないことにより、現実的なrace源は排除されている。
- Proposed root-cause key: N/A

## Acceptance Criteria assessment

- AC-01 Real PostgreSQL 18: PASS — `postgres:18.4`コンテナを実起動し、`SHOW server_version_num`=180004 を実providerで検証（ローカル7/7、CI 7/7、Skipped 0）
- AC-02 Digest pin: PASS — `ImageReference`定数 + runtimeで`Image.FullName`/`Image.Digest`をassert。ローカルpull済みイメージのdigestが`sha256:3a82e1f5...`と一致
- AC-03 Automatic database lifecycle: PASS — `IAsyncLifetime`でtest単位にCREATE/DROPが自動実行
- AC-04 Test isolation: PASS — test単位DB、`TEMPLATE template0`、一意名、`Pooling=false`。probe tableの非共有を実DBで検証
- AC-05 Parallel / serialization policy: PASS — assembly並列有効化、Console-sensitive collectionの明示、READMEのparallel/serialized範囲が実装と一致。overlap testをxUnit schedulingの証明と誤認しない旨も明記
- AC-06 Cleanup failure visibility: PASS — drop失敗は`InvalidOperationException`化・握り潰しなし・retry可能・最終回収をfailure testが証明
- AC-07 Startup / connection failure: PASS — unreachable Docker endpoint / PostgreSQL endpointのfailure testが明確な例外とInnerExceptionを検証
- AC-08 CI real PostgreSQL: PASS — Run 31277771209（headSha=91e3fca）の「Test (real PostgreSQL)」stepが7/7、Skipped 0で成功。skip/fallback/success変換なし
- AC-09 No InMemory / SQLite substitute: PASS — fallbackパスなし（コード・READMEとも）
- AC-10 No business table / migration: PASS — diff全体でDbContext/Migrate/Migration/SQLite/InMemory/Docker Compose/business schema/tableなし。Npgsqlはtest project限定

## Verification performed

- CI independently checked: YES — `gh run view 31277771209`でheadSha一致・全step successを確認。stepログでnon-PG（Unit 3 + Integration 27）とreal PG（7）の実行件数・Skipped 0を確認
- Local build/test/probe performed: YES — detached worktree（HEAD 91e3fca、リポジトリのツリーは変更せず）で以下を実行:
  - `dotnet restore` / `dotnet build`（0 warnings / 0 errors）
  - non-PostgreSQL: Unit 3/3 + Integration 27/27 PASS
  - real PostgreSQL category: 7/7 PASS（2回実行、コンテナリーク0、残存postgresコンテナなし）
  - `ApiRuntimeContractTests`: 25/25 PASS（計4回）
  - `git diff --check` PASS
  - local Dockerの`postgres:18.4`イメージdigestがピンと一致
- Summary:
  - Target Identity（PR #104のbase/head、CI run 31277771209のhead）はすべて指定値と一致。
  - 全Acceptance Criteriaをコード・runtime・CIの一次証拠から独立検証し、達成を確認。
  - cleanup exceptionの握り潰しは全パスでなし（`InitializeAsync`はstartup+cleanup両失敗をAggregateExceptionで保持、`DisposeAsync`は再throw・handle保持、`OpenConnectionAsync`は接続+dispose両失敗を保持）。
  - CI race修正は最小の回帰修正として妥当（FND-02 scope侵食なし）。lockの実効性のみNitで指摘（実害なし）。

## Scope assessment

- Scope drift: NO
- Out-of-scope implementation detected: none — `DbContext`/`EnsureCreated`/`Migrate`/Migration/SQLite/InMemory/Docker Compose/business schema/business table/production Npgsql wiringの追加なし。Npgsql 10.0.3・Testcontainers.PostgreSql 4.13.0は`Directory.Packages.props`のCPMとIntegrationTestsプロジェクトのみ。`ApiRuntimeContractTests.cs`の変更はConsoleCaptureへのlockとcollection属性の最小限（parallelization有効化の必須回帰修正）

## Notes

- 既存review / benchmark結果への偶発的接触: なし（`gh pr view`のbodyは二次証拠としてのみ参照。PR #104の既存review・inline thread・docs/benchmarks配下の評価文書は参照していない）。
- 独立性: レビュー中にtracked file・branch・PR・Issueを一切変更していない。ローカル検証は`C:\in4a\tmp\opencode\fnd03-wt`（detached worktree）で実施。
- SyncTextWriterのロックレス挙動は.NET 10.0.302のIL解析とruntime probeにより独立確認した事実に基づく。
- 制約: Windows PowerShellからの実行のため、CIログの日本語文字化けがあり件数・結果は英語表記（Passed!/Skipped）で確認した。
