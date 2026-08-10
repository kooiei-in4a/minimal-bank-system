# FND-04 Final Synthesis Independent Review

Reviewer:

- Slot: R5
- Model: Cursor Auto
- Harness: Cursor
- Effort: Auto
- Primary role: fast_independent_review
- Attempt: 1

```text
ROUTED_MODEL: "NOT_EXPOSED"
```

Target verification:

- Repository: kooiei-in4a/minimal-bank-system
- PR: #140 (OPEN, draft, MERGEABLE)
- Base SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
- Head SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
- PR state: open / draft
- CI identity:
  - Known PR run `31350916189` (pull_request): success、checkout = merge ref `d12de2ae07003a10d19d576808cf88ec7796da23` (`99cee…` into `38c07…`)
  - Push run `31350870902` (push): success、checkout = direct head `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- Result: PASS

Verdict:

- APPROVE_WITH_FINDINGS

Merge-ready:

- YES

Findings:

### Blocker

- NONE

### Major

- NONE

### Minor

#### R5-F01

- Severity: Minor
- blocking: no
- affected path/component: `tests/.../DesignTimeConnectionSafetyTests.cs`（C8-M01回帰）
- evidence:
  - 子プロセスの stdout/stderr に `localhost` / `design_time` / `Sqlite` 等が**含まれないこと**だけを断言している
  - 実 destination（`DbContextOptions` の connection / server）を検査していない
  - ただし同一 Head でローカル再実行した `dotnet-ef database update`（接続未設定）は exit 1、`The ConnectionString property has not been initialized.` / `database '' on server ''` であり、fabricated destination は観測されなかった
- root cause: 回帰テストが「禁止文字列の非出現」に偏り、destination safety の直接証明が弱い
- impact: 実装は fail-closed に見えるが、将来 factory が別ホストへ逃げた場合にメッセージ次第で見逃し得る
- required fix direction: options/connection の未初期化、または `server ''` 相当の正の失敗信号を断言する（任意）

#### R5-F02

- Severity: Minor
- blocking: no
- affected path/component: `Migrator/Program.cs` + `MigrationBaselineTests.MigrationExecutionStopsAtTheFixedBudgetInsteadOfHanging`
- evidence:
  - production は `CommandTimeout(60)` と `CancellationTokenSource(60s)` を併用
  - テストは exit code が厳密に `MigratorExitCode.Timeout` (2) であることを要求
  - lock 待ちでは Npgsql command timeout（Failure=1）と CTS（Timeout=2）が競合し得る
  - Duration 許容は `60s±` で上限が `+60s` と広い
- root cause: 同じ60秒 budget の二重機構に対し、分類コードまで厳密断言している
- impact: AC上の「非0・bounded」は満たしても、稀に Failure 分類で flaky になり得る
- required fix direction: non-zero + duration帯で十分とする、または CTS と CommandTimeout の責務をずらす（任意）

### Nit

- NONE

Probe matrix:

- P01: PASS — EF/Design 10.0.10、Npgsql/provider 10.0.3、dotnet-ef 10.0.10 exact pin。Infrastructure ownership、専用 Migrator、migrations assembly = Infrastructure
- P02: PASS — CI real PG + `ExplicitMigratorAppliesTheBaselineToACleanDatabase`（history 1、public tables = `__EFMigrationsHistory` のみ）
- P03: PASS — rerun test、history unchanged
- P04: PASS — missing / unreachable / rejected credentials / malformed history が非0
- P05: PASS — Migrator 自身の 60s CTS + CommandTimeout(60)。lock 試験で Timeout 経路を production DLL 経由で確認（分類レースは R5-F02）
- P06: PASS — API に Migrate/EnsureCreated なし。before/after schema 不変 + DbContext resolve 後も不変
- P07: PASS — `HasPendingModelChanges()` + CI `dotnet-ef migrations has-pending-model-changes`（ローカルでも exit 0）
- P08: PARTIAL — positive は committed。negative model-drift は Issue契約どおり evaluator-only で、Head に再現可能な committed 証拠なし
- P09: PASS — EnsureCreated / API auto-migration / ad-hoc DDL なし（静的+実DB）
- P10: PASS — factory は同 Npgsql + Infrastructure assembly。接続なしは model-only `UseNpgsql()`（公式の connectionless パターン）。接続必須は fail-closed（ローカル再確認）。テスト弱さは R5-F01
- P11: PASS — idempotent script 生成を model test で確認。README に標準 command
- P12: PASS — business schema / Compose / health / FND-05・06 先取りなし。過剰抽象も重大ではない
- P13: PASS — FND-03 fixture 本体は diff なし。`MigrationBaselineTests` 追加のみ。CI で PG 23 tests success
- P14: PASS — rejected credential を production Migrator プロセス出力で sentinel 非漏洩確認
- P15: PASS — merge-ref PR CI と direct-head push CI の両方 success（混同せず区別済み）

Primary-role deep dive:

- Broad scan の結果、runtime bug / wrong assembly / EnsureCreated / API auto-migration / business schema / credential の明確漏洩 / FND-03破壊は見つからず
- 高信号候補だった design-time fallback と timeout は、実装は契約整合、テスト側の弱さ/脆さが Minor に留まる
- ローカルで build 0警告、non-PG 4+38 PASS、pending-model PASS、design-time update fail-closed を再確認

CI assessment:

- `31350916189` = PR merge-ref CI（既知）success
- `31350870902` = **exact direct-head** push CI success（checkout `99cee…`）
- pending-model step / non-PG / real PG いずれも success、build 0 warning

Unverified / evidence limits:

- 本環境では real PostgreSQL suite を再実行していない（Docker時間）。PG主張は committed tests + 両CI run に依存
- evaluator-only temporary model-drift negative は Head 上で未再現（P08 PARTIAL）
- Cursor UI は routed model 名を露出しなかった

Final rationale:

- Identity gate PASS。Issue #42 / ADR-0009 の中核 AC（explicit migrator、60s bound、API no-migrate、empty baseline、pin、fail-closed design-time）は一次証拠で充足
- Blocker/Major なしのため merge-ready YES。Minor 2件は回帰テストの厳密さ/脆さであり、現 Head の製品挙動を merge 不適にする根拠にはならない
