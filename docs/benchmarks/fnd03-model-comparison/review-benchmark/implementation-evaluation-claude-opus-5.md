# Issue #41 FND-03 — 17モデル独立レビュー性能評価

- Benchmark ID: `fnd03-final-synthesis-independent-review`
- Run ID: `fnd03-final-91e3fca-20260809`
- Target Issue: #41 / Target PR: #104
- Base SHA: `7946cc55e49c0c6e21ad7b86c20a8435b4976269`
- Head SHA: `91e3fca181558cd1523390347f4f2f80d6014d26`
- CI Target SHA: `91e3fca181558cd1523390347f4f2f80d6014d26`
- Primary CI Run: `31277771209`
- Raw artifact: branch `agent/fnd03-final-review-artifacts` / `2518e9b303c820794ccf82c4bf83754c9ae348c4` / PR #106
- 本文書はbenchmark評価成果物であり、製品仕様・ADR・実装Issueの正本ではない。

---

## 1. Executive Summary

FND-03 Final Synthesis（PR #104 / Head `91e3fca`）は、独立検証の結果、**Issue #41のAcceptance Criteria 10項目すべてを満たしており、Blocker 0 / Major 0でmerge可能**である。したがって本benchmarkは「隠れた重大bugを何件見つけられるか」を測る検出benchmarkではなく、**「重大問題が存在しない実装に対して、根拠のないblocking findingを作らず、どこまで一次証拠で健全性を確認できたか」を測るprecision / verification-depth benchmark**となった。

FND-02（Blocker 1 / Major 3が実在）とは難易度構造が根本的に異なるため、**絶対scoreをFND-02と直接比較してはならない**。

今回の1 executionで観測された要点は次の通り。

- 17 reviewerのうち16件が`APPROVE`、1件（`chatgpt-o3-browser`）が`INCOMPLETE`。**Blocker / Majorを誤って提示したreviewerは0件**であり、blocking False Positiveは全体でゼロだった。
- 差がついたのは「何を見つけたか」ではなく、**「どこまで自分で実行・計測して確かめたか」**と、**「framework semanticsを記憶ではなく実測で確定したか」**である。
- 最大の識別点は2つ。
  1. `PinnedPostgreSql184ContainerProvidesTheTestDatabase`のdigest assertionが**constant同士のtautology**であり、running containerのdaemon-side evidenceではないこと（Reference G-01）。これをFindingとして明示できたのは`claude-opus-5-claude-code`のみ。
  2. `lock (synchronizedWriter)`が本当にwriteと排他になるか、すなわち`TextWriter.Synchronized`が返す`SyncTextWriter`のmonitor identity。これを**実測で正しく確定**したのは`claude-opus-5-claude-code` / `claude-sonnet-5-claude-code` / `gpt-5.6-sol-codex` / `deepseek-v4-pro-opencode` / `qwen3.7-plus-opencode`。逆に**実測を主張しながら事実と反対の結論**を出したのが`deepseek-v4-flash-opencode`と`minimax-m3-opencode`であり、今回の最大の減点要因になった。
- 上位3件（`claude-opus-5-claude-code` 99.0 / `gpt-5.6-sol-codex` 97.0 / `claude-sonnet-5-claude-code` 95.5）は、いずれもrepository外へHeadを展開して実PostgreSQLを起動し、CI logをstep単位で読み、frameworkの挙動をprobeで確定していた。
- 実行時間と品質は単調な関係を示さなかった。最長の36分（`minimax-m3-opencode`）は誤った技術的Findingを1件生み、3分（`composer-2.5-cursor`）と6分（`grok-4.5-cursor`）は誤りゼロで実runtime証拠を伴う結果を出した。

---

## 2. 評価対象と方法

### 2.1 評価単位

評価単位は**モデル単体ではなく `Model + Agent/Harness + Effort + 今回の1 execution`** である。「このモデルは強い／弱い」という一般化は本文書では行わない。すべての記述は`RUN_ID = fnd03-final-91e3fca-20260809`の1回の実行についてのものである。

### 2.2 手順（評価独立性）

1. **Phase A**: `reviews/`配下、PR #104の既存review submission / inline thread、PR #105、reviewerランキング、過去のFND-03評価結果を**一切参照せず**、一次証拠のみからReference / Gold相当Reviewを作成した。
2. **Reference Review lock**: Reference Reviewをrepository外の一時ファイルへ固定した後に、初めて`reviews/`配下を開いた。
3. **Phase B**: 17 reviewerのMarkdown（人間向け一次review artifact）とJSON（structured result）を読み、root cause単位でReference Findingsと照合した。
4. raw artifact（`reviews/*.md` / `reviews/*.json` / `run.json` / `manifest.json` / `README.md`）は一切変更していない。

### 2.3 Reference Reviewが使用した一次証拠

| 種別 | 内容 |
| --- | --- |
| 正本 | Issue #41、Parent / Control Issue #3、Work Package #33、`AGENTS.md`、`docs/plans/phase-4-implementation-issue-decomposition.md`、Accepted ADR-0001 / 0003 / 0004 / 0005 / 0009 |
| diff | `git diff 7946cc5..91e3fca`（10 files / +607 / −9）、`git diff e769447..91e3fca`（fix commit単体） |
| CI | Run `31277771209`（`event=pull_request`, `headSha=91e3fca`, `conclusion=success`）のjob `93154058679` step単位log |
| 追加CI | Run `31277607769`（`e769447` push, **failure**）の失敗log、Run `31277769431`（push, success）、Run `31277639955`（同SHAのPR event, success） |
| local実行 | `git archive`でHeadをscratchpadへ展開（repository checkoutを変更しない方式）し、`dotnet build` / non-PG suite ×3 / real PG suiteを実行 |
| runtime probe | `TextWriter.Synchronized`のmonitor identity実測、Testcontainers 4.13.0の`IImage.FullName` / `Digest`の実測 |
| daemon evidence | `docker images --digests`によるRepoDigest照合、実行後のcontainer残存確認 |
| 一次資料 | xUnit v2 tag `v2-2.9.3`の`XunitTestAssemblyRunner` / `XunitTestClassRunner`のsource |

PR #104本文は補助情報としてのみ参照し、実装者の主張を一次証拠として扱っていない。

### 2.4 採点軸

FND-02独立レビューbenchmarkと縦比較可能にするため、同一の8軸100点満点を使用する。

```text
A. 重大問題検出 / 25
B. 誤検知抑制・Precision / 20
C. 一次証拠・技術検証品質 / 15
D. Severity精度 / 10
E. 仕様・Issue・Scope理解 / 10
F. Test / CI / runtime評価力 / 8
G. Signal-to-Noise / 7
H. 最終Verdict精度 / 5
```

Grade目安: `S 95.0–100` / `A+ 90.0–94.9` / `A 85.0–89.9` / `B+ 80.0–84.9` / `B 75.0–79.9` / `C 65.0–74.9` / `D 50.0–64.9` / `F 0.0–49.9`。

---

## 3. Reference / Gold相当 Review

### 3.1 Target identity

| 項目 | 固定値 | 独立検証結果 |
| --- | --- | --- |
| Repository | `kooiei-in4a/minimal-bank-system` | PASS |
| PR | #104（draft, `mergeable_state: clean`） | PASS |
| Base SHA | `7946cc55e49c0c6e21ad7b86c20a8435b4976269` | PASS。`origin/main`と一致 |
| Head SHA | `91e3fca181558cd1523390347f4f2f80d6014d26` | PASS。branch `agent/issue-41-fnd-03-final-code` |
| CI Run | `31277771209` | PASS。`headSha = 91e3fca`、`event = pull_request`、`conclusion = success` |
| diff規模 | — | 10 files / +607 / −9 / 2 commits（`e769447`, `91e3fca`） |

PR eventのcheckoutはGitHub生成merge ref `da2f91588acb049322d1479547dde8494749e00d`である。この commitのparentsを独立に確認したところ `[7946cc55…, 91e3fca1…]` であり、`origin/main == Base SHA`であるため、**CIが検証したtree contentはHead treeと同一**である。

CI step単位の結果（job `93154058679`）:

```text
Restore                    success
Build                      success  (0 Warning / 0 Error)
Test (non-PostgreSQL)      success  Unit 3/3, Integration 27/27
Test (real PostgreSQL)     success  7/7, Skipped 0, Duration 12s
```

### 3.2 Acceptance Criteria判定

| AC | 内容 | 判定 | 一次証拠 |
| --- | --- | --- | --- |
| AC-01 | PostgreSQL 18を実際に起動してtestできる | **PASS** | `InitializeAsync`が実接続で`SHOW server_version_num`を読み、`180004`以外なら例外。test側でも per-test database 経由で再検証。CIとlocalの双方で実行済み |
| AC-02 | container imageがdigest固定される | **PASS** | `postgres:18.4@sha256:3a82e1f5…744a` が唯一のimage入力。Dockerはdigestでcontent-addressed解決する。当方の`docker images --digests`でもRepoDigestがpinと一致（daemon-side確認） |
| AC-03 | test開始前後のdatabase lifecycleが自動化される | **PASS** | `PostgreSqlDatabaseTestBase : IAsyncLifetime` がFactごとにcreate / drop。`DisposingADatabaseScopeRemovesTheDatabase`が`pg_database`で実証 |
| AC-04 | 複数testが状態を共有せず相互干渉しない | **PASS** | `mbs_test_{GUID}` 一意名 + `TEMPLATE template0` + `Pooling=false`。`SeparateDatabasesDoNotShareProbeState`が`to_regclass`で不可視性を実証 |
| AC-05 | 並列実行可能範囲と直列化条件が明示される | **PASS** | assembly parallelization有効化 + `ConsoleSensitive` collection（`DisableParallelization=true`）。xUnit 2.9.3 sourceで**非並列collectionは並列collection完了後に単独実行**されることを確認。READMEはoverlap testをscheduler証明と主張していない |
| AC-06 | cleanup失敗を黙って無視しない | **PASS** | `DropDatabaseAsync`はwrapしてrethrow。`disposed`はdrop成功後のみtrue。`CleanupFailureIsVisibleAndTheDatabaseRemainsRetryable`が failure → database残存 → 同一leaseでretry → 最終削除まで実証 |
| AC-07 | container起動／接続失敗が明確なtest failureになる | **PASS** | `PostgreSqlFailureTests` 2件。skip / fallback / success変換は workflow・code のどこにも存在しない |
| AC-08 | CIで実PostgreSQL integration testが実行される | **PASS** | 専用step `Test (real PostgreSQL)` が指定Headで7/7成功。件数が非ゼロなのでfilter空振りの可能性も排除される |
| AC-09 | InMemory／SQLiteを代替に使用しない | **PASS** | 該当packageもcodeも存在しない。fixtureのmessageも明示的にfallback否定 |
| AC-10 | business tableやmigrationを追加しない | **PASS** | DbContext / EF / migration / Compose なし。唯一のDDLは使い捨てtest database内の`isolation_probe`。ADR-0009は"outside disposable tests"の禁止であり、disposable test内のDDLは適合 |

**10/10 PASS。**

### 3.3 Reference Findings

Findingは改善提案ではなく、実証できた実問題のみを記載する。**Blocker 0 / Major 0**。

#### G-01 — Minor（non-blocking）— digest assertionがself-referentialで、running containerのdaemon-side evidenceになっていない

- Affected component: `tests/MinimalBankSystem.IntegrationTests/PostgreSql/PostgreSqlFixtureTests.cs` — `PinnedPostgreSql184ContainerProvidesTheTestDatabase`
- Root cause: Testcontainers 4.13.0の`IImage.FullName` / `IImage.Digest`は、`PostgreSqlBuilder`へ渡したreference stringの**parse結果**であり、Docker daemonへの問い合わせ結果ではない。したがって`Assert.Equal(ImageReference, Fixture.Container.Image.FullName)`と`Assert.Equal("sha256:3a82e1f5…", Fixture.Container.Image.Digest)`は、同一processで同一constantから導出された値をそのconstantと比較しているに過ぎない。
- 一次証拠（独立runtime probe / Docker未起動）:

  ```text
  Build() elapsed ms (no StartAsync): 370
  Image type      : DotNet.Testcontainers.Images.DockerImage
  Image.FullName  : postgres:18.4@sha256:3a82e1f5…744a
  Image.Digest    : sha256:3a82e1f5…744a
  --- 実在しないdigestを与えた場合 ---
  fake Image.FullName: postgres:18.4@sha256:0000…0000
  fake Image.Digest  : sha256:0000…0000
  ```

  存在しない全ゼロdigestがそのままecho backされ、daemonへの通信も発生しない。
- merge判断への影響: **なし**。AC-02はpin自体で成立している（daemon側のcontent-addressed pullを当方が`docker images --digests`で独立確認済み）。さらに`server_version_num == 180004`という真のruntime guardが別途存在する。欠けているのはIssue #41 §9が求める「image digest確認」の**verification artifactの強度**だけである。

#### G-02 — Minor（non-blocking）— ConsoleCapture修正が、文書化されていないBCL実装詳細に依存している

- Affected component: `tests/MinimalBankSystem.IntegrationTests/ApiRuntimeContractTests.cs` — `ConsoleCapture`
- Root cause: `lock (synchronizedWriter)`がwriteと排他になるのは、`TextWriter.Synchronized`が返す`SyncTextWriter`の各methodが`MethodImplOptions.Synchronized`（= `lock(this)`）で実装されているためである。`TextWriter.Synchronized`のAPI契約は「返却instance自身がmonitorである」ことを保証していない。`ConsoleCapture`が所有するprivate lock objectで全write/read/disposeを包む方が契約に依存しない。
- 一次証拠（.NET SDK 10.0.302 = CIと同一runtime、実測）:

  ```text
  type=System.IO.TextWriter+SyncTextWriter
  Write(Char) implFlags=Synchronized
  Flush()     implFlags=Synchronized
  held lock ~1515 ms; concurrent Write blocked during hold = True; Write completed at 1515 ms
  CONCLUSION: lock(TextWriter.Synchronized(...)) DOES mutually exclude concurrent writes.
  ```

- merge判断への影響: **なし**。修正はCI run `31277607769`で観測された実raceを実際に閉じている。root cause一致も確認済み（下記）。

  ```text
  System.ArgumentOutOfRangeException : ... (Parameter 'chunkLength')
     at System.Text.StringBuilder.ToString()
     at ConsoleCapture.get_Content() ... ApiRuntimeContractTests.cs:line 816
  ```

  なお`[Collection(ConsoleSensitive)]`は失敗commit `e769447`に**既に存在していた**（`git diff e769447..91e3fca`はConsoleCaptureのみ）。したがって並行writerはxUnitの別collectionではなく、hostのconsole logger背景threadである。最終fixはこのroot causeに正しく対応している。

#### G-03 — Nit（non-blocking）— `ConsoleCapture.Dispose`のlock取得前にConsoleを復元している

- `Console.SetOut(originalOutput)`がlockの外にあるため、writer参照を既に保持しているbackground threadが、disposed済み`StringWriter`へ書き込む理論的窓が残る。
- ただし全10箇所の使用で`ConsoleCapture`が先に宣言されており（= 最後にdispose）、8箇所ではfactoryが内側の`using`ブロックで先にdisposeされる。hostのconsole logger processorはhost dispose時にflush / 停止するため、実運用上この窓は閉じている。
- **post-lock refinement**: Reference Review固定時はMinorとしていたが、宣言順序を自分で再確認した結果**Nitへ降格**した。この再分類はいずれのreviewerのscoreにも影響しない（G-03を指摘したreviewerは0件）。

#### G-04 — Nit — cleanup failure injectionの失敗点はDROP実行中ではなくconnection open時である

- `DisposeAsync(pre-cancelled token)`は`NpgsqlConnection.OpenAsync`でcancelされるため、`DROP DATABASE`文はserverへ送られない。
- testが実証しているのは**lease retry contract**（failure可視化 → `disposed`非遷移 → retry成功 → 最終削除）であり、これは有効。ただしDROP実行中failureのhandlingを証明してはいない。test名とREADMEの語感が後者を含意する点がNit。

#### G-05 — Nit — PR本文がclass fixture dispose例外を「test failureとして伝播」と表現している

- xUnit v2の`XunitTestClassRunner`はclass fixtureの`DisposeAsync`例外を`Aggregator`へ集約し、**test class cleanup failure**として報告する（per-test failureではない）。実行結果としてrunは失敗するため実害はない。`tests/.../README.md`側の表現は正確。

#### Findingとしなかった項目（Blocker / Majorで挙げればFalse Positiveになるもの）

- 「`Npgsql`を`Directory.Packages.props`へ追加したことがproductionへの永続化依存の持ち込み」— CPMはreferenceを追加しない。`PackageReference`はtest projectのみ。
- 「`isolation_probe` tableがAC-10 / ADR-0009違反」— ADR-0009はdisposable test内のDDLを明示的に許容範囲としている。tableは使い捨てdatabase内にのみ存在する。
- 「assembly parallelization有効化が危険 / 回帰を起こした」— raceは`ConsoleCapture`の既存latent bugであり、修正済み。xUnit 2.9.3の仕様上、Console-sensitive collectionは単独実行される。
- 「concurrency testはxUnit parallelismを証明していない」— 事実だが、READMEが自らそう明記しているため欠陥ではない。Majorとして挙げるのはartifactの読み違い。
- 「CIがHead SHAを検証していない可能性」— merge refのparentsとrun metadataで否定される。
- 「container起動に全体timeoutがない」— 妥当なnon-blocking観察（CI jobは`timeout-minutes: 15`で境界づけられる）。Minor / Nitなら可、blockerなら不可。

### 3.4 Reference Verdict

```text
APPROVE（Issue #41としてmerge可能）

Blocker: 0
Major:   0
Minor:   2   （G-01, G-02）  ※lock時点では3。G-03を自己再検証によりNitへ降格
Nit:     3   （G-03, G-04, G-05）
```

---

## 4. Finding normalization

### 4.1 Gold root causes

Reference ReviewのBlocker / Majorがbenchmark上の主要Gold root causeとなるが、**今回はBlocker 0 / Major 0**である。よってbenchmark rule §8に従い、全reviewerについて次を適用する。

```text
TP = 0
FN = 0
```

これは全reviewerが同点であることを意味しない。差は以下で評価する。

- unsupported blocking findingを出さなかったか
- clean implementationをcleanと**根拠付きで**認識したか
- 一次証拠（実行・計測・source確認）の深さ
- test / CI / runtime semanticsの理解精度
- Severity精度、Signal-to-Noise、Verdict精度、review完遂性

### 4.2 TP / FP / FNルール

| 分類 | 定義 | 今回の適用 |
| --- | --- | --- |
| TP | Reference Blocker / Major root causeを実質的に検出 | 該当なし（Gold 0件） |
| FN | Reference Blocker / Major root causeを見逃した | 該当なし（Gold 0件） |
| FP (blocking) | Blocker / Majorとして提示したが正本・実コード・test・runtime evidenceに支持されない | **全17 reviewerで0件** |
| Invalid non-blocking finding | Minor / Nitとして提示されたが、一次証拠で反証される技術的主張 | 2件（`minimax-m3-opencode` F-01、`deepseek-v4-flash-opencode` F-01）。blocking FPには算入せず、軸B / C / F / Gで減点 |
| Valid non-blocking finding | Reference Minor / Nitと一致、または一致しないが一次証拠で支持される非blocking観察 | 3件 |

### 4.3 Reviewer finding対応表

| Reviewer | Verdict | B/M/m/N | 提示Finding | Reference対応 | 判定 |
| --- | --- | --- | --- | --- | --- |
| claude-opus-5-claude-code | APPROVE | 0/0/1/0 | F-01 digest assertionはconstant同士のtautology | **G-01と一致** | Valid（Severityも一致） |
| claude-sonnet-5-claude-code | APPROVE | 0/0/2/0 | F-01 cleanup injectionはconnection open時に失敗 / F-02 container `DisposeAsync` failureの専用testなし | F-01 = **G-04と一致**（ReferenceはNit）/ F-02 = Reference非採用だが事実として正しい | Valid（F-01はやや過大severity、F-02はcoverage gap観察） |
| minimax-m3-opencode | APPROVE | 0/0/1/0 | F-01 `SyncTextWriter`は`lock(_out)`を取るためouter lockと排他にならず微小raceが残存 | **G-02の前提を誤認。反証済み** | **Invalid**（非blocking） |
| deepseek-v4-flash-opencode | APPROVE | 0/0/0/1 | F-01 .NET 10の`SyncTextWriter`はlock-lessでありlockはwriteと排他にならない。raceの実解決はcollection属性とCI分割による | **実測で反証。かつcollection属性は失敗commitに既存** | **Invalid**（非blocking） |
| その他13 reviewer | APPROVE 12 / INCOMPLETE 1 | 0/0/0/0 | Findingなし | — | blocking FPなし |

補足: **Markdownとjson間の意味差は17件すべてで検出されなかった**（verdict / counts / findings件数 / ci_verified / local_verification がすべて一致）。

### 4.4 Schema anomalyの扱い

- `deepseek-v4-pro-opencode.json`: schema外field（`ac_assessment` / `scope_drift` / `out_of_scope_detected`）を含む。意味内容はMarkdownと一致し、むしろ情報量は多い。**review品質のFalse Positiveとしては扱わない。**
- `chatgpt-o3-browser.json`: `outcome: "incomplete"` は現行schemaのenum外。raw結果として `verdict = INCOMPLETE` / `CI independently checked = NO` / implementation detailsをレビューできず終了、として扱う。schema不適合そのものは減点しないが、**Harnessとしてreviewを完遂できなかった事実**はA / C / F / H軸へ反映した。

raw artifactは一切変更していない。

---

## 5. 総合ランキング

| Rank | Model | Harness | Effort | Score | TP | FP | FN | Verdict精度 | Grade | 時間(分) |
| ---: | --- | --- | --- | ---: | ---: | ---: | ---: | --- | --- | ---: |
| 1 | Claude Opus 5 | Claude Code | xhigh | **99.0** | 0 | 0 | 0 | 完全一致（APPROVE / 0 Blocker・0 Major） | S | 12 |
| 2 | GPT-5.6 Sol | Codex | xHigh | **97.0** | 0 | 0 | 0 | 完全一致 | S | 11 |
| 3 | Claude Sonnet 5 | Claude Code | xhigh | **95.5** | 0 | 0 | 0 | 完全一致 | S | 7 |
| 4 | ChatGPT Opus 5.6 Sol | Browser | xhigh | **90.5** | 0 | 0 | 0 | 完全一致 | A+ | 7 |
| 5 | Grok 4.5 | Cursor | high fast | **90.0** | 0 | 0 | 0 | 完全一致 | A+ | 6 |
| 6 | GPT-5.6 Terra | Codex | xHigh | **89.5** | 0 | 0 | 0 | 完全一致 | A | 8 |
| 7 | DeepSeek V4 Pro | Open Code | 指定値 | **85.0** | 0 | 0 | 0 | 完全一致 | A | 20 |
| 8 | ChatGPT GPT 5.5 | Browser | xhigh | **84.0** | 0 | 0 | 0 | 完全一致 | B+ | 6 |
| 9 | Composer 2.5 | Cursor | null | **83.5** | 0 | 0 | 0 | 完全一致 | B+ | 3 |
| 10 | GPT-5.6 Luna | Codex | xHigh | **82.5** | 0 | 0 | 0 | 完全一致 | B+ | 11 |
| 11 | MiMo-V2.5-Pro | Open Code | null | **80.5** | 0 | 0 | 0 | 完全一致 | B+ | 7 |
| 12 | MiMo-V2.5 | Open Code | null | **78.0** | 0 | 0 | 0 | 完全一致 | B | 4 |
| 13 | Qwen3.7 Plus | Open Code | MAX | **76.5** | 0 | 0 | 0 | 完全一致 | B | 10 |
| 14 | GPT-5.6 Luna | Open Code | Xhigh | **72.0** | 0 | 0 | 0 | 一致するが根拠が薄い | C | 7 |
| 15 | MiniMax M3 | Open Code | 指定値 | **70.0** | 0 | 0 | 0 | 一致するが誤分析を含む | C | 36 |
| 16 | DeepSeek V4 Flash | Open Code | 指定値 | **65.0** | 0 | 0 | 0 | 一致するが誤分析を含む | C | 13 |
| 17 | ChatGPT o3 | Browser | Medium | **28.0** | 0 | 0 | 0 | **INCOMPLETE**（review未完遂） | F | 5 |

実行時間は`run.json`を正本とし、確定訂正値 `gpt-5.6-luna-codex = 11分` / `minimax-m3-opencode = 36分` を使用している。

---

## 6. 評価軸別スコア

| Model | Harness | A /25 | B /20 | C /15 | D /10 | E /10 | F /8 | G /7 | H /5 | Total |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Claude Opus 5 | Claude Code | 24.5 | 20.0 | 15.0 | 10.0 | 10.0 | 8.0 | 6.5 | 5.0 | **99.0** |
| GPT-5.6 Sol | Codex | 24.0 | 20.0 | 14.5 | 9.5 | 9.5 | 7.5 | 7.0 | 5.0 | **97.0** |
| Claude Sonnet 5 | Claude Code | 23.5 | 19.5 | 14.5 | 9.0 | 10.0 | 7.5 | 6.5 | 5.0 | **95.5** |
| ChatGPT Opus 5.6 Sol | Browser | 22.0 | 20.0 | 11.0 | 9.5 | 9.5 | 7.0 | 6.5 | 5.0 | **90.5** |
| Grok 4.5 | Cursor | 21.0 | 20.0 | 12.0 | 9.5 | 9.0 | 6.5 | 7.0 | 5.0 | **90.0** |
| GPT-5.6 Terra | Codex | 21.0 | 20.0 | 11.5 | 9.5 | 9.5 | 6.5 | 6.5 | 5.0 | **89.5** |
| DeepSeek V4 Pro | Open Code | 20.0 | 18.5 | 11.5 | 9.0 | 9.0 | 6.5 | 5.5 | 5.0 | **85.0** |
| ChatGPT GPT 5.5 | Browser | 19.0 | 20.0 | 9.5 | 9.5 | 9.5 | 5.5 | 6.0 | 5.0 | **84.0** |
| Composer 2.5 | Cursor | 19.0 | 20.0 | 10.0 | 9.5 | 8.0 | 5.5 | 6.5 | 5.0 | **83.5** |
| GPT-5.6 Luna | Codex | 18.5 | 20.0 | 9.0 | 9.5 | 9.0 | 5.5 | 6.0 | 5.0 | **82.5** |
| MiMo-V2.5-Pro | Open Code | 19.0 | 16.5 | 11.0 | 9.0 | 8.5 | 6.0 | 5.5 | 5.0 | **80.5** |
| MiMo-V2.5 | Open Code | 18.0 | 17.0 | 8.5 | 9.0 | 8.5 | 6.0 | 6.0 | 5.0 | **78.0** |
| Qwen3.7 Plus | Open Code | 17.5 | 16.5 | 8.5 | 9.0 | 9.0 | 5.0 | 6.0 | 5.0 | **76.5** |
| GPT-5.6 Luna | Open Code | 15.0 | 19.5 | 6.5 | 9.0 | 8.0 | 4.0 | 5.5 | 4.5 | **72.0** |
| MiniMax M3 | Open Code | 17.0 | 11.0 | 11.0 | 6.0 | 9.5 | 6.0 | 5.0 | 4.5 | **70.0** |
| DeepSeek V4 Flash | Open Code | 16.0 | 10.0 | 9.5 | 6.5 | 9.0 | 5.0 | 4.5 | 4.5 | **65.0** |
| ChatGPT o3 | Browser | 3.0 | 12.0 | 2.0 | 4.0 | 2.0 | 1.0 | 3.0 | 1.0 | **28.0** |

各行の合計はscriptで検算済みであり、いずれの軸も上限を超えていない。

---

## 7. 各Reviewer詳細評価

### 1. Claude Opus 5 / Claude Code

- **Score**: 99.0 / **Grade**: S
- **Verdict**: APPROVE（Blocker 0 / Major 0 / Minor 1 / Nit 0）— Reference一致
- **Findings**: F-01 Minor — digest assertionがconstant同士のtautology
- **True Positive**: 0（Gold Blocker / Major 0件のため）
- **False Positive**: 0
- **False Negative**: 0
- **Evidence quality**: 今回の最高水準。`git archive`でHeadをscratchへ展開してrepository checkoutを一切変更せず実行、CI logのstep単位読解、GitHub API、そして**4種類の独立probe**を実施。特にG-01については、当方のReference probeと**同一の手法**（存在しない全ゼロdigestを与えて`FullName` / `Digest`がそのまま返ることを示す）で証明しており、reviewer側の独立到達として最も強い。
- **Test / CI / runtime verification**: xUnit 2.9.3の並列semanticsを、非並列collectionと2つの並列classにtimestampを仕込んだ実験で確定（並列collectionが同時実行され、非並列collectionはその後に単独実行）。`lock(TextWriter.Synchronized(...))`が806msにわたりconcurrent writeをblockすることを実測。class fixtureの`DisposeAsync`例外が`[Test Class Cleanup Failure]`としてrunを失敗させることまでprobeで確認（当方はsource読解で確認した項目であり、実測はこちらの方が強い）。「PostgreSQL stepは非ゼロ件数を実行したのでfilter空振りではない」という排除論法も正確。`ParseJsonLogLines`が全行を`JsonDocument.Parse`する点に触れ、Console collectionの隔離が単なる作法ではなくassertionの前提であることまで結び付けている。
- **Severity accuracy**: 10/10。唯一のFindingを正しくMinor / non-blockingとし、AC-02は明確にPASSと分離。G-04については「注入点はconnection側というのが唯一の正直な限界。観測されるlease contractは同一なのでFindingとしない」と明示的に判断を述べており、見落としではなく判断である。
- **Signal-to-Noise**: 高い。分量は最大（15KB）だが、AC逐条・probe・cleanup swallowing auditのいずれも判定に効いている。ごく僅かに冗長。
- **実行時間**: 12分
- **今回観測されたReviewer type**: Deep Technical / Precision / Specification
- **総評**: 「重大問題がない」ことを**主張ではなく実験で確定**した唯一のreviewer。Referenceが挙げた最重要Minor（G-01）を同一証拠で独立検出し、G-03に相当する残存窓についても宣言順序を根拠に正しく否定している。Formal Agent B / merge gateとして今回最も信頼できる結果。

### 2. GPT-5.6 Sol / Codex

- **Score**: 97.0 / **Grade**: S
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: 最上位。PR eventのcheckout対象が一時merge commit `da2f915…`であり、その**parentsが指定Base / Head**であることまで確認している（当方も独立に同一結論）。Headを一時ディレクトリへ展開してrestore / build（warning 0 / error 0）/ non-PG 3+27 / PG 7/7 / full 3+34を実行。`docker image inspect`でimage IDとRepoDigestがpin digestと一致することを確認しており、これは**testが提供していないdaemon-side evidenceをreviewer自身が補った**形になる。
- **Test / CI / runtime verification**: xUnit公式の並列実行ドキュメントと、.NET runtimeの`TextWriter.cs`（`SyncTextWriter`）を一次資料として参照し、「同一のreentrant monitorなのでlock-order deadlockを新たに導入しない」と正しく結論。実行後のPostgreSQL container残存0件、Testcontainers resource leak 0件も確認。
- **Severity accuracy**: 9.5/10。Findingが0件のためReferenceのMinor 2件は未記録。ただし内容的にはdigest確認をdaemon側で自ら実施しており、実質的な穴は小さい。
- **Signal-to-Noise**: 7.0/7。今回最も密度が高い。4.4KBに、target identity・CI・local実行・runtime semantics・scopeがすべて検証可能な形で収まっている。冗長な指摘はゼロ。
- **実行時間**: 11分
- **今回観測されたReviewer type**: Deep Technical / Precision
- **総評**: 検証の実行力では1位と互角。差がついたのは、G-01（digest assertionのtautology性）を**自分で強い証拠を取りに行った結果、artifact側の弱さを指摘対象として言語化しなかった**点のみ。merge gate運用としてはほぼ理想的な出力形式。

### 3. Claude Sonnet 5 / Claude Code

- **Score**: 95.5 / **Grade**: S
- **Verdict**: APPROVE（0 / 0 / 2 / 0）— Reference一致
- **Findings**: F-01 Minor（cleanup injectionはconnection open時に失敗）、F-02 Minor（container `DisposeAsync` failureの専用testなし）
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: 非常に高い。worktreeを作って正確なHeadで restore / build / non-PG 30/30（3回反復）/ PG 7/7 / full 37/37 を実行。`docker image inspect`によるdigest照合も実施。特筆すべきは、**失敗した先行run `31277607769`のlogを自分で取得してroot causeを確認**し、さらに**同一SHAのPR event run `31277639955`がsuccessだったこと**まで突き止め、「flaky」というPRの説明を独立に裏取りした点。当方も両runを確認し、記述はすべて正確だった。
- **Test / CI / runtime verification**: `TextWriter.Synchronized`のmonitor共有を、2000ms保持で~2009msブロックという実測で確定（当方の1515ms実測と整合）。xUnitのXMLドキュメントを引いて`DisableParallelization`のcollection間semanticsを確認。`Console.Out`/`Error`利用箇所がsolution全体で`ApiRuntimeContractTests`のみであることをgrepで確認しており、parallel-safety auditの完全性まで検証している。
- **Severity accuracy**: 9.0/10。F-01はReferenceでNit相当をMinorとしており僅かに過大。F-02は「専用testがない」というcoverage gapで、PR本文の自己申告と重なる。実害の証明はないため、Minorとしてはやや強い。ただしいずれも`Blocking: false`であり、merge判断を歪めていない。
- **Signal-to-Noise**: 6.5/7。F-02は「もっとtestを書くべき」に近い一般論の境界にあるが、対応するdatabase側pathがtest済みという対比を示しているため情報価値はある。
- **実行時間**: 7分
- **今回観測されたReviewer type**: Deep Technical / Specification / Precision
- **総評**: 7分でこの検証量に到達した点が際立つ。G-04を独立検出しつつblocking扱いしない判断も正しい。G-01を逃した（むしろ`docker image inspect`で自分で digest を確認しながら、testのassertionの弱さには触れなかった）ことだけが1〜2位との差。

### 4. ChatGPT Opus 5.6 Sol / Browser

- **Score**: 90.5 / **Grade**: A+
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: local実行不可というHarness制約下で、documentary evidenceを最大化した。PR metadata、`ahead_by = 2`のcommit比較、**primary run `31277771209`とpush run `31277769431`の両方**、merge commit `da2f915`の親子関係、そして失敗run `31277607769`のstack traceまで独立に確認している。当方の確認とすべて一致し、誤りは検出されなかった。
- **Test / CI / runtime verification**: `cleanupGate`によるcleanup直列化、`disposed`遷移条件、`AggregateException`によるprimary / cleanup failure両保持、prefix guardといったコードpath semanticsを正確に記述。**READMEのparallel claimと実際のevidenceの一致を明示的に検証**しており、Issue #41 §11のAgent B focusに正面から答えている。
- **Severity accuracy**: 9.5/10。Findingは0件だが、digest assertionについて「fixtureが正確なpin imageで`PostgreSqlBuilder`を構築する」と書くにとどめ、**testのassertionをruntime evidenceだと誤って主張していない**点は他のOpen Code勢と明確に異なる。
- **Signal-to-Noise**: 6.5/7。長いが全項目が検証項目に対応している。
- **実行時間**: 7分
- **今回観測されたReviewer type**: Specification / Precision / Broad
- **総評**: 「実行できない環境でどこまでやれるか」の上限に近い。accidental exposure（Base commitのdiff展開によりFND-03実装benchmark評価文書の一部が表示された）を自ら明記した点も評価に値する。local runtime probeがないためC / F軸で上位3件に届かない。

### 5. Grok 4.5 / Cursor

- **Score**: 90.0 / **Grade**: A+
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: detached worktreeで正確なHeadを取得し、restore / build（0 warning / 0 error）/ non-PG 30/30 / PG 7/7 / full（Unit 3 + Integration 34）を実行。CIのstep単位結果とskip 0も確認。**workspace tip（`5ac5e436…`）をreview対象と誤認しなかった**ことを明記しており、target identityの規律が高い。
- **Test / CI / runtime verification**: catch pathがすべてrethrowであること、Npgsql / TestcontainersがIntegrationTestsに限定されること、package versionの解決結果を確認。runtime probe（xUnit semantics / monitor identity）までは踏み込んでいない。
- **Severity accuracy**: 9.5/10。container `DisposeAsync` failure injectionについて「未testだがproduction pathは握り潰さずhandleを保持するのでFindingとしない」と明示的判断を残している。
- **Signal-to-Noise**: 7.0/7。2.7KBで必要事項が過不足なく揃う。今回最もtriageしやすい出力の一つ。
- **実行時間**: 6分
- **今回観測されたReviewer type**: Precision / Broad
- **総評**: 6分で実runtime証拠まで到達し、誤りゼロ。深掘り（framework semanticsの実測）がない分だけ上位に届かないが、**quality / timeでは今回最上位クラス**。

### 6. GPT-5.6 Terra / Codex

- **Score**: 89.5 / **Grade**: A
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: `git archive`でHeadを一時展開し、restore / build / non-PG / PG / full suiteを実行（件数の明記はなし）。Issue #3 / #33 / #41、実装計画、Accepted ADR-0001 / 0003 / 0004 / 0005 / 0009を明示的に確認しており、**正本side の網羅性は上位**。PR event checkoutがmerge ref `da2f915…`である点にも触れ、run metadataでHead紐付けを確認している。
- **Test / CI / runtime verification**: xUnit公式ドキュメントを引いてcollection-level serializationの整合を確認。`ConsoleCapture`について「read / write / disposeと整合するsynchronized writer lockを使用する」と正しく評価。runtime probeは実施していない。
- **Severity accuracy**: 9.5/10。誤りなし。Referenceの2 Minorは未記録。
- **Signal-to-Noise**: 6.5/7。簡潔で読みやすい。ただしlocal実行の件数が書かれていないため、読み手が追試しにくい。
- **実行時間**: 8分
- **今回観測されたReviewer type**: Specification / Precision
- **総評**: 正本理解とscope判断は上位陣と同等。accidental exposure（Commit APIの応答にbase commitのdiffが含まれ、禁止対象のbenchmark評価文書の一部が表示された）を自ら報告した点も誠実。runtime証拠の粒度がGrok / Solより粗いことがC / F軸の差。

### 7. DeepSeek V4 Pro / Open Code

- **Score**: 85.0 / **Grade**: A
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: AC逐条に**ファイル名 + 行番号**を付けており、今回最も追跡可能性の高い記述形式。local実行はrestore / build（0 warning / 0 error）/ Unit 3/3 / PG 7/7（12s）まで到達。ただし**non-PG integration testがWSL/Windows環境でhang**したことを正直に報告しており、local evidenceは部分的。CIは PR event / push event の両runを確認。
- **Test / CI / runtime verification**: Notesで「`TextWriter.Synchronized`は内部で`lock(this)`を使うため、同一instanceへのexternal lockはwriteとread/disposeの正しい相互排他になる」と**正確に**述べている。これは今回誤答が多かった論点であり、記憶ベースの記述であっても結論は正しい。`Console.SetOut` / `SetError`をlockの外に置いた理由づけも妥当。
- **Severity accuracy**: 9.0/10。誤りはないが、AC-02の記述が「`Image.FullName`と`Image.Digest`をconstantおよび期待digest文字列と照合」という表現に留まり、それがtautologyである点には踏み込んでいない。
- **Signal-to-Noise**: 5.5/7。AC逐条の情報量は多いが、20分という時間に対して新規の洞察は少なく、記述の反復も目立つ。
- **実行時間**: 20分
- **今回観測されたReviewer type**: Specification / Broad
- **総評**: 行番号付きevidenceとlocal実行の正直な限界報告により、監査可能性が高い。JSONにschema外fieldを含むが、内容はMarkdownと一致しており情報量はむしろ多い。schema不適合自体は減点対象にしていない。

### 8. ChatGPT GPT 5.5 / Browser

- **Score**: 84.0 / **Grade**: B+
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: local実行不可（sandboxに`dotnet` / `docker`がなく、DNSも解決不能）を明記した上で、GitHub connector経由で PR metadata、compare API（`ahead_by = 2` / `behind_by = 0`）、job `93154058679`のstep結果、build warning 0 / error 0、非PG 3+27、PG 7/7 skip 0 を確認。Issue #41 / #3 / #33 / `AGENTS.md` / 実装計画 / ADR 5本を正本として明示的に確認しており、**specification sideは上位陣と同水準**。
- **Test / CI / runtime verification**: コードの構造的確認は正確だが、runtime semanticsの検証はない。「`ConsoleCapture`のrace fixは`Flush` / `ToString` / `Dispose`をsynchronized writer上で同期する」という記述にとどめ、**誤った断定を避けている**点は良い。
- **Severity accuracy**: 9.5/10。誤りなし。
- **Signal-to-Noise**: 6.0/7。
- **実行時間**: 6分
- **今回観測されたReviewer type**: Specification / Broad
- **総評**: 6分でspecification / CI両面を押さえた効率的なreview。ただしPR探索時にPR #105のbody（benchmark ranking / score情報）へaccidental exposureしたことを報告しており、独立性の観点では今回のexposure事例の中で最も影響が大きい部類。判定内容に汚染の痕跡は見当たらないが、Limitationとして記録する。

### 9. Composer 2.5 / Cursor

- **Score**: 83.5 / **Grade**: B+
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: 3分で、`gh pr view`によるBase / Head照合、CI run全step確認（PG 7 passed / 0 skipped）、diff精読、local restore / build、non-PG 30 passed、PG integration 7 passedまで到達。**workspaceに残存していたbenchmark candidateの未追跡ファイル（`SharedPostgreSqlContainer.cs`等）に気づき、`git ls-tree HEAD`でHead `91e3fca`の正本が3ファイルだけであることを確認してからreviewした**という記述は、今回のtarget identity規律として特筆に値する。
- **Test / CI / runtime verification**: 実行はしているが、何をもってdigest / version検証が成立したかの内訳は書かれていない。runtime semanticsの検証なし。
- **Severity accuracy**: 9.5/10。誤りなし。
- **Signal-to-Noise**: 6.5/7。noiseはないが、AC逐条のevidenceがないため読み手が判定根拠を追試できない。
- **実行時間**: 3分（今回最速）
- **今回観測されたReviewer type**: Precision / Surface
- **総評**: 今回最速でありながら誤りゼロ・実runtime証拠あり。監査文書としての厚みがないためE / F軸で伸びないが、**高速一次reviewとしての実用性は高い**。

### 10. GPT-5.6 Luna / Codex

- **Score**: 82.5 / **Grade**: B+
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: local checkoutを意図的にBase SHAのまま維持する方針を採り、`git show` / GitHub file・diff APIで読み取り専用reviewを実施。**local build/test/probeは`NO`**。diff規模（10 files / +607 / −9）、run `31277771209`のheadSha一致、non-PG 27/27、PG 7/7を確認。CI logがmerge refをcheckoutすること、run metadataのheadShaがtarget Headと一致することの両方に触れている点は正確。
- **Test / CI / runtime verification**: xUnit公式ドキュメントと.NETの`TextWriter.cs`を参照リンクとして提示している。ただし実測ではなく参照にとどまる。
- **Severity accuracy**: 9.5/10。誤りなし。
- **Signal-to-Noise**: 6.0/7。
- **実行時間**: 11分
- **今回観測されたReviewer type**: Specification / Precision
- **総評**: 誤りは一切ないが、11分という時間に対してlocal実行を行わない方針を採ったため、得られた証拠量が同harnessのSol（11分、full local実行 + docker inspect）と大きく開いた。accidental exposure（repository-wide static searchでbenchmark文書の該当行が表示された）を自ら報告している。

### 11. MiMo-V2.5-Pro / Open Code

- **Score**: 80.5 / **Grade**: B+
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: local実行あり。restore / build（0 warning / 0 error）/ PG 7/7（19.8s）に加え、**実際に生成されたcontainer ID（`b2038d905b1d` / `893f634c26f1`）とDocker server version 29.6.2を記録**しており、実行証拠としては具体性が高い。`UnreachableDockerEndpoint` testが約5秒で完了（20秒timeout内）という観測も有用。
- **Test / CI / runtime verification**: AC逐条の記述は正確。ただしAC-02で「test asserts `Fixture.Container.Image.Digest` equals the expected digest **at runtime, not only a constant comparison**」と述べており、**これはReference G-01で反証された誤り**。証拠強度の評価を誤っている。
- **Severity accuracy**: 9.0/10。Findingは0件で誤検知はないが、上記のevidence強度誤認によりB軸で減点。
- **Signal-to-Noise**: 5.5/7。AC逐条が冗長で、`Task.WhenAll`に関するNoteなど既にREADMEに書かれている内容の再掲が多い。
- **実行時間**: 7分
- **今回観測されたReviewer type**: Broad / Specification
- **総評**: 実行証拠の具体性は評価できるが、「runtimeで検証している」と「reference stringをparseした値を突き合わせている」の区別ができていない。今回の中位群に共通する弱点。

### 12. MiMo-V2.5 / Open Code

- **Score**: 78.0 / **Grade**: B
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: local実行なし（HEADがBaseのままで、repository stateを変更せずには実行できないと判断）。その代わりCI側は丁寧で、`statusCheckRollup`、run全step、job logの Restore / Build / non-PG（Unit 3/3 + Integration 27/27）/ PG 7/7、push run `31277769431`まで確認。**merge commit `da2f915`がhead `91e3fca`をbase `7946cc5`へmergeしたものであることを正しく説明**している。
- **Test / CI / runtime verification**: cleanup path・`cleanupGate`のfinally解放・例外握り潰しの不在を正確に記述。ただしAC-02で「`Image.FullName`と`Image.Digest`をruntimeでcontainer objectに対してassert」としており、証拠強度を誤認。
- **Severity accuracy**: 9.0/10。
- **Signal-to-Noise**: 6.0/7。4分の割に情報密度は高い。
- **実行時間**: 4分
- **今回観測されたReviewer type**: Broad / Surface
- **総評**: 4分でCI側をここまで押さえたのは効率的。local実行を諦めた判断自体は保守的だが妥当（他のreviewerは`git archive`やworktreeでrepository stateを変えずに実行できていたため、手段の引き出しの差が出た）。

### 13. Qwen3.7 Plus / Open Code

- **Score**: 76.5 / **Grade**: B
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: local実行なし（WSL環境に`dotnet` SDK 10.0.302が未インストール）。CIは`gh run view`とlogで確認し、non-PG 30 / PG 7 / 12sを取得。AC逐条にEvidence行を付ける形式は良い。
- **Test / CI / runtime verification**: 2点で精度に差が出た。
  - AC-02で「`Fixture.Container.Image.Digest`をassert — **runtime evidence from the Testcontainers library, not just a constant comparison**」と明示的に述べており、Reference G-01の反証対象そのもの。**今回の誤認の中で最も断定的**。
  - 一方Notesでは「lock object（`synchronizedWriter`）は`TextWriter.Synchronized()`が内部で使うものと一致し、deadlock riskなく適切な相互排他を提供する」と**正しく**述べている。
- **Severity accuracy**: 9.0/10。
- **Signal-to-Noise**: 6.0/7。
- **実行時間**: 10分
- **今回観測されたReviewer type**: Specification / Broad
- **総評**: framework semantics（`TextWriter.Synchronized`）は正解、evidence semantics（Testcontainersのdigest）は不正解という、判定精度の混在が特徴。10分でlocal実行なしという時間効率も上位群に劣る。

### 14. GPT-5.6 Luna / Open Code

- **Score**: 72.0 / **Grade**: C
- **Verdict**: APPROVE（0 / 0 / 0 / 0）— Reference一致
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: 今回最も薄い完遂review。2.0KBで、Verification summaryが実質1段落。CIのrun一致・非PG 30件・PG 7件・`git diff --check`は確認しているが、AC逐条にevidenceがなく、10項目すべてがPASSラベルのみ。local実行なし。
- **Test / CI / runtime verification**: 「fixtureは指定digest、`SHOW server_version_num`、test単位database、cleanup retry、失敗の明示化を実装している」という要約のみ。semanticsの検証は行われていない。
- **Severity accuracy**: 9.0/10。誤った主張はしていない（断定を避けたことがむしろ誤りを防いだ）。
- **Signal-to-Noise**: 5.5/7。noiseはないが、merge gateの根拠文書としては情報が不足する。
- **実行時間**: 7分
- **今回観測されたReviewer type**: Surface
- **総評**: 結論は正しいが、**読み手が判定を追試できない**。同一modelのCodex実行（82.5）およびSol / Codex（97.0）との差は、今回はHarnessと実行方針の差として現れている。「APPROVEと書いたから高得点」にはしないという方針をそのまま適用した結果。

### 15. MiniMax M3 / Open Code

- **Score**: 70.0 / **Grade**: C
- **Verdict**: APPROVE（0 / 0 / 1 / 0）— verdict自体はReference一致
- **Findings**: F-01 Minor —「`TextWriter.Synchronized(buffer)`が返す`SyncTextWriter`は内部で`lock(_out)`（= `buffer`）を取るため、outer lock（`synchronizedWriter`）とinner lockは別objectであり、`buffer.ToString()`は内部lockと排他にならず微小なdata raceが残存する」
- **True Positive**: 0 / **False Positive（blocking）**: 0 / **False Negative**: 0
- **Invalid non-blocking finding**: 1件。上記F-01は**一次証拠で反証される**。`SyncTextWriter`の各methodは`MethodImplAttributes.Synchronized`（= `lock(this)`）であり、`lock(sync)`保持中にconcurrent `sync.Write`が1515msにわたりblockされることを実測で確認した。さらにF-01が提案する代替案「`lock(buffer)`を取る」は、`SyncTextWriter`の同期単位と一致しないため**現行実装より弱くなる**。
- **Evidence quality**: 実行量そのものは中位以上。local restore / build / PG 7/7（10s）/ 単一test指定実行 / `--list-tests`によるdiscovery件数確認まで行い、CI logもstep単位で読んでいる。AC逐条は行番号付きで詳細。
- **Test / CI / runtime verification**: CI / test件数の把握は正確。しかし本PRの中核である同期semanticsで誤り、`ApiRuntimeContractTests`のlocal失敗を「base commitでも再現するpre-existingなWindows環境問題」と結論づけている（当方の同一OS上での実行では非PG suite 27/27が3回とも成功しており、環境固有の事象と考えられる）。
- **Severity accuracy**: 6.0/10。存在しないraceをMinorとして記録した点で、Severity判断の基礎となる事実認識が誤っている。
- **Signal-to-Noise**: 5.0/7。13KBと大部だが、唯一のFindingが誤りであるためtriage負荷に対する見返りが低い。
- **実行時間**: 36分（今回最長）
- **今回観測されたReviewer type**: Broad / Unreliable（今回の実行における同期semantics判断について）
- **総評**: 最長時間を投じ、実行証拠も揃えながら、最も検証が必要な1点で誤った。**時間投入が精度に直結しなかった**今回の代表例。verdictとscope判断は正しいため、補助reviewとしては使えるが、単独のmerge gateには向かない。

### 16. DeepSeek V4 Flash / Open Code

- **Score**: 65.0 / **Grade**: C
- **Verdict**: APPROVE（0 / 0 / 0 / 1）— verdict自体はReference一致
- **Findings**: F-01 Nit —「.NET 10の`SyncTextWriter`はMonitorを一切使用しない純粋なlock-less decoratorであり（runtime probeで確認）、`lock(synchronizedWriter)`はwrite側と排他されない。raceの実質的な解決は`[Collection(ConsoleSensitive)]`による直列化とCIのcategory分割による」
- **True Positive**: 0 / **False Positive（blocking）**: 0 / **False Negative**: 0
- **Invalid non-blocking finding**: 1件。**二重に誤っている**。
  1. 同期semanticsが逆。同一runtime（.NET 10.0.302）で`Write(Char)`の`implFlags = Synchronized`であり、`lock(sync)`はconcurrent writeを実際にblockする。IL中に`Monitor.Enter`が現れないのは`MethodImplOptions.Synchronized`がruntime側で実装されるためであり、「lock-less」の根拠にはならない。しかもF-01は「`lock(sync)`保持中も`sync.Write`がblockされないことを動作検証で確認（S1/A probe）」と、**実測結果として反対の事実を主張**している。
  2. 因果関係も誤り。`[Collection(TestExecutionCollections.ConsoleSensitive)]`は**失敗commit `e769447`に既に存在していた**（`git diff e769447..91e3fca`はConsoleCaptureの2箇所のみ）。したがってraceを解決したのはcollection属性ではない。
- **Evidence quality**: 実行の**幅**は上位級。detached worktreeでrestore / build / non-PG 30 / PG 7/7（2回）/ `ApiRuntimeContractTests` 25/25（4回）/ `git diff --check` / local imageのdigest照合まで実施している。にもかかわらず、中核の技術判断が誤っている。
- **Test / CI / runtime verification**: CI件数・skip 0の把握は正確。runtime semanticsの結論が反対。
- **Severity accuracy**: 6.5/10。誤ったFindingを`Nit` / `Blocking: false` / 「実害なし」と最小severityに置いた自制は評価できるが、事実認識の誤りは残る。
- **Signal-to-Noise**: 4.5/7。唯一のFindingがnoiseであり、しかも「修正は無意味である」と読める内容なので、後続の判断を誤らせる方向のnoiseである。
- **実行時間**: 13分
- **今回観測されたReviewer type**: Broad / Unreliable（今回の実行におけるruntime probe報告について）
- **総評**: **実行しない誤りより、実行したと主張して誤った結論を出す方が、merge gateには危険**という点が今回最も明確に表れた結果。verdict・scope・AC判定はすべて正しいため成果物が無価値ではないが、「runtime probeで確認した」という記述の信頼性が担保されない以上、evidence artifactとしては採用できない。

### 17. ChatGPT o3 / Browser

- **Score**: 28.0 / **Grade**: F
- **Verdict**: **INCOMPLETE**（Blocker 0 / Major 0 / Minor 0 / Nit 0、AC 10項目すべて UNCERTAIN、Scope drift UNCERTAIN）
- **Findings**: なし
- **True Positive / False Positive / False Negative**: 0 / 0 / 0
- **Evidence quality**: Base / Head / PR #104の存在確認のみ。「GitHub search APIはdefault branchしかindexしておらず、PR branchのfile pathは事前知識なしには発見できない。directory listingも利用できない」として、diffと新規test infrastructureにアクセスできずreviewを打ち切った。**CI independently checked: NO**。
- **Test / CI / runtime verification**: 実施なし。
- **Severity accuracy**: 4.0/10。severity判断の対象が存在しない。
- **Signal-to-Noise**: 3.0/7。出力は短く、できなかったことを明確に述べている点は誠実。
- **実行時間**: 5分
- **今回観測されたReviewer type**: Incomplete
- **総評**: **できなかったことを「PASS」と書かずにUNCERTAIN / INCOMPLETEとした点は、precision behaviorとして正しい**。推測でAPPROVEを出していれば、blocking FPは無くとも根拠のない承認になっていた。その一点でB軸に部分点を与えている。一方、reviewとして成立していない以上、A / C / E / F / H軸はほぼ得点しない。Reference verdictがAPPROVEであっても、INCOMPLETEを偶然の一致として加点しない方針を適用した。なおJSONの`outcome: "incomplete"`は現行schemaのenum外だが、schema不適合自体は減点していない。

---

## 8. Reviewer type / 観測傾向

以下は**今回のexecutionの事後分類**であり、モデル一般の性質ではない。

| Type | 定義 | 今回該当（Model + Harness） |
| --- | --- | --- |
| Deep Technical | frameworkやruntimeの挙動を、記憶や文書ではなく実測 / source で確定させた | Claude Opus 5 / Claude Code、GPT-5.6 Sol / Codex、Claude Sonnet 5 / Claude Code |
| Specification | Issue・ADR・WP・AGENTS.mdを正本として逐条参照し、scope境界を明示的に判定した | Claude Sonnet 5 / Claude Code、GPT-5.6 Terra / Codex、ChatGPT GPT 5.5 / Browser、ChatGPT Opus 5.6 Sol / Browser、GPT-5.6 Luna / Codex、Qwen3.7 Plus / Open Code、DeepSeek V4 Pro / Open Code |
| Precision | 支持されない主張を出さず、判断の限界を明示した | Claude Opus 5 / Claude Code、GPT-5.6 Sol / Codex、Grok 4.5 / Cursor、Composer 2.5 / Cursor、ChatGPT Opus 5.6 Sol / Browser、GPT-5.6 Terra / Codex、GPT-5.6 Luna / Codex |
| Broad | 広く網羅するが、深さは中程度 | DeepSeek V4 Pro / Open Code、MiMo-V2.5-Pro / Open Code、MiMo-V2.5 / Open Code、MiniMax M3 / Open Code、DeepSeek V4 Flash / Open Code、Grok 4.5 / Cursor |
| Surface | 結論は正しいが、判定根拠を読み手が追試できない | GPT-5.6 Luna / Open Code、Composer 2.5 / Cursor（速度側）、MiMo-V2.5 / Open Code |
| Over-strict | 実害のない事項をblockerへ昇格させた | **今回は該当なし** |
| Incomplete | reviewを完遂できなかった | ChatGPT o3 / Browser |
| Unreliable | 実測を主張しながら一次証拠と矛盾する結論を出した | DeepSeek V4 Flash / Open Code、MiniMax M3 / Open Code（いずれも同期semanticsの1点について） |

今回の全体傾向として特筆すべきは、**Over-strictが1件も出なかった**ことである。FND-02では実在するBlocker / Majorを巡って検出力の差が現れたが、FND-03では「clean な実装を過剰にblockしない」方向のprecisionが全reviewerで保たれた。差はもっぱら「cleanであることをどこまで自分で確かめたか」と「framework semanticsを誤らなかったか」に現れた。

---

## 9. 実行時間と品質

実行時間は`run.json`を正本とする。品質100点には含めず、事後の参考軸として扱う。

| 観点 | 該当 |
| --- | --- |
| 最速 | Composer 2.5 / Cursor（3分、83.5点） |
| 高品質かつ高速 | Claude Sonnet 5 / Claude Code（7分、95.5点）、Grok 4.5 / Cursor（6分、90.0点）、ChatGPT Opus 5.6 Sol / Browser（7分、90.5点） |
| 深いが時間が掛かった | Claude Opus 5 / Claude Code（12分、99.0点）、GPT-5.6 Sol / Codex（11分、97.0点）、DeepSeek V4 Pro / Open Code（20分、85.0点） |
| 時間を投じたが精度に結びつかなかった | MiniMax M3 / Open Code（36分、70.0点）、DeepSeek V4 Flash / Open Code（13分、65.0点）、GPT-5.6 Luna / Codex（11分、82.5点。local実行を行わない方針を採った） |

参考値としての `Score / 分`（優劣判定には使用しない）:

```text
Composer 2.5 / Cursor            27.8   (83.5 / 3)
MiMo-V2.5 / Open Code            19.5   (78.0 / 4)
Grok 4.5 / Cursor                15.0   (90.0 / 6)
ChatGPT GPT 5.5 / Browser        14.0   (84.0 / 6)
Claude Sonnet 5 / Claude Code    13.6   (95.5 / 7)
ChatGPT Opus 5.6 Sol / Browser   12.9   (90.5 / 7)
MiMo-V2.5-Pro / Open Code        11.5   (80.5 / 7)
GPT-5.6 Terra / Codex            11.2   (89.5 / 8)
GPT-5.6 Luna / Open Code         10.3   (72.0 / 7)
GPT-5.6 Sol / Codex               8.8   (97.0 / 11)
Claude Opus 5 / Claude Code       8.3   (99.0 / 12)
Qwen3.7 Plus / Open Code          7.7   (76.5 / 10)
GPT-5.6 Luna / Codex              7.5   (82.5 / 11)
ChatGPT o3 / Browser              5.6   (28.0 / 5)
DeepSeek V4 Flash / Open Code     5.0   (65.0 / 13)
DeepSeek V4 Pro / Open Code       4.3   (85.0 / 20)
MiniMax M3 / Open Code            1.9   (70.0 / 36)
```

この指標は「短時間で軽く済ませた」ことを機械的に優遇するため、単独では使わない。実際、`Score / 分`最上位のComposer 2.5は3分で誤りゼロという優れた結果だが、AC判定のevidenceを残していないためformal merge gateの証拠文書としては上位3件に置き換えられない。

### 今回のHarness別観測

**同一modelで比較できる唯一のペア**は GPT-5.6 Luna である。

| Model + Harness | Score | local実行 | 時間 |
| --- | ---: | --- | ---: |
| GPT-5.6 Luna / Codex | 82.5 | NO（checkoutをBaseのまま維持する方針） | 11分 |
| GPT-5.6 Luna / Open Code | 72.0 | NO | 7分 |

同一modelでも10.5点の差が出ており、**評価単位をModel単体にできない**ことを示す。一方、同じCodex harnessでもSol（97.0、full local実行 + docker inspect）とLuna（82.5、local実行なし）で14.5点差があるため、差はHarnessだけでも説明できない。

- **Codex**（3件、平均89.7）: 正本参照とmerge ref識別が安定。Solのみfull local実行に到達。
- **Claude Code**（2件、平均97.3）: 2件ともrepository stateを壊さずにHeadを実行し、さらにruntime probeで framework semantics を確定させた。今回最も一貫して深い。
- **Browser**（3件、平均67.5 / 完遂2件の平均87.3）: local実行不可という制約が共通。完遂した2件はdocumentary evidenceを最大化して上位に入ったが、1件はfileへ到達できずINCOMPLETE。**Harness側のrepository読み取り能力が結果を直接左右した**。
- **Cursor**（2件、平均86.8）: 2件ともlocal実行に到達し、誤りゼロ。出力は簡潔で速い。深掘りはしない。
- **Open Code**（7件、平均75.3）: 分散が最大（85.0〜65.0）。local実行に到達した件数は多いが、framework semanticsの誤りが2件ともこのharnessから出た。

---

## 10. 実務上の使い分け

以下は**今回の1 executionの結果だけ**を根拠とする。

### Formal Agent B / merge gate向き

- **Claude Opus 5 / Claude Code**（99.0）
- **GPT-5.6 Sol / Codex**（97.0）
- **Claude Sonnet 5 / Claude Code**（95.5）

3件とも、(1) repository stateを変えずにHeadを実行、(2) CI logをstep単位で読解、(3) framework semanticsをprobeまたは一次source で確定、(4) blocking FPゼロ、を満たした。Issue #41 §10が要求する「Agent B独立レビュー結果 / Blocker・Major 0」のevidenceとして、そのまま添付できる品質にある。

### adversarial探索向き

- **Claude Opus 5 / Claude Code**: 「testが証明していると主張する内容」と「testが実際にassertしている内容」の乖離（G-01）を、独立probeで暴いた唯一のreviewer。false assurance探索に最も適する。
- **Claude Sonnet 5 / Claude Code**: failure injectionの実際の到達点（G-04）を突き止めた。test semanticsの精査に適する。

### specification review向き

- **GPT-5.6 Terra / Codex**、**ChatGPT GPT 5.5 / Browser**、**ChatGPT Opus 5.6 Sol / Browser**: Issue / WP / ADR / AGENTS.mdを逐条で参照し、scope境界とout-of-scope不在を明示的に判定した。実行環境を持たない状況でのspecification gateとして機能する。

### 高速一次review向き

- **Composer 2.5 / Cursor**（3分）、**MiMo-V2.5 / Open Code**（4分）、**Grok 4.5 / Cursor**（6分）: いずれも誤りゼロで正しいverdictへ到達。Draft PR段階の早期feedbackに向く。ただしComposer / MiMo-V2.5はAC判定のevidenceが薄いため、最終gateには別reviewerを重ねる必要がある。

### 補助review向き

- **DeepSeek V4 Pro / Open Code**、**MiMo-V2.5-Pro / Open Code**、**Qwen3.7 Plus / Open Code**、**GPT-5.6 Luna / Codex**: 正しい結論に到達しており、AC網羅の観点で他reviewの抜けを補える。ただしevidence強度の評価（特にdigest verification）を単独では信頼しない運用が必要。

### 単独では使用しない

- **MiniMax M3 / Open Code**、**DeepSeek V4 Flash / Open Code**: 今回、同期semanticsについて一次証拠と矛盾する結論を出した。他reviewerとのcross-checkを前提とすれば使えるが、単独のgateにすると誤った技術的結論がmerge判断へ流れ込む。
- **ChatGPT o3 / Browser**: 今回のHarness構成ではPR branchのfileへ到達できず、reviewが成立しなかった。tool surfaceの改善なしには適用できない。

---

## 11. FND-02との比較

FND-02（Issue #40 / PR #83）のhistorical benchmarkと**同一の8軸100点満点**を使用しているため縦比較が可能である。ただし次の前提を必ず添えること。

> **FND-02はReference Blocker 1 / Major 3、FND-03はReference Blocker 0 / Major 0である。**
> FND-02は「実在する重大bugを検出できるか」、FND-03は「重大bugがない実装をblockせずに、どこまで確かめられるか」を測っている。したがって**絶対scoreの直接比較は無意味**であり、比較対象は順位傾向・reviewer typeの再現性・Harness差に限る。FND-03の採点をFND-02結果へ合わせて調整することは行っていない。

| Model + Harness | FND-02 順位 / Score | FND-03 順位 / Score | 傾向 |
| --- | --- | --- | --- |
| GPT-5.6 Sol / Codex | 1位 / 100.0 | 2位 / 97.0 | **再現**。両benchmarkで最上位。検出benchmarkでも精度benchmarkでも安定 |
| Claude Opus 5 / Claude Code | 2位 / 92.5 | **1位 / 99.0** | **改善**。FND-02ではMinor 6 / Nit 3の過剰指摘が減点だったが、FND-03ではMinor 1に絞り、Signal-to-Noiseが大きく改善 |
| ChatGPT Opus 5.6 Sol / Browser | 4位 / 87.5 | 4位 / 90.5 | **再現**。framework解析の強さが両回で一貫 |
| GPT-5.6 Terra / Codex | 7位 / 75.5 | 6位 / 89.5 | 安定〜改善 |
| Claude Sonnet 5 / Claude Code | 8位 / 60.0 | **3位 / 95.5** | **大幅改善**。FND-02では検出漏れが多かったが、FND-03では独立probeとCI incident再検証まで到達 |
| Grok 4.5 / Cursor | 10位 / 54.0 | **5位 / 90.0** | **大幅改善**。local実行に到達したことが最大の差 |
| Composer 2.5 / Cursor | 9位 / 54.5 | 9位 / 83.5 | 順位は再現。相対位置が安定 |
| DeepSeek V4 Pro / Open Code | 12位 / 47.5 | 7位 / 85.0 | 改善 |
| GPT-5.6 Luna / Codex | 5位 / 82.0 | 10位 / 82.5 | **低下**（相対順位）。FND-03ではlocal実行を行わない方針を採ったことがC / F軸に直結 |
| DeepSeek V4 Flash / Open Code | 6位 / 77.0 | **16位 / 65.0** | **大幅低下**。誤ったruntime probe主張が原因 |
| GPT-5.6 Luna / Open Code | 3位 / 88.0 | **14位 / 72.0** | **大幅低下**。FND-02ではTestServer / Kestrel差まで到達していたが、今回は要約のみ |
| MiMo-V2.5-Pro / Open Code | 14位 / 19.0 | 11位 / 80.5 | 上昇（ただしbenchmark難易度構造の差が大きい） |
| MiMo-V2.5 / Open Code | 15位 / 7.0 | 12位 / 78.0 | 上昇（同上） |
| Qwen3.7 Plus / Open Code | 16位 / 2.0 | 13位 / 76.5 | 上昇（同上） |
| MiniMax M3 / Open Code | 17位 / 0.0 | 15位 / 70.0 | 上昇（同上） |
| ChatGPT GPT 5.5 / Browser | — | 8位 / 84.0 | 新規（FND-02は `Chatgpt Opus 5.5 xhigh` 11位 / 53.0 で別条件） |
| ChatGPT o3 / Browser | — | 17位 / 28.0 | 新規（FND-02は `chatgpt o2` 13位 / 35.0 で別モデル） |

### 一貫して観測されたこと

1. **GPT-5.6 Sol / Codex と Claude Opus 5 / Claude Code は2回とも上位2枠**。検出benchmark・精度benchmークの双方で機能した唯一の2件。
2. **ChatGPT Opus 5.6 Sol / Browser は2回とも4位**。local実行不可という制約下でdocumentary evidenceを最大化するtypeが安定して再現した。
3. **下位群のscore上昇は能力向上を意味しない**。FND-02では実在Blocker / Majorの見逃しが直接減点になったが、FND-03にはGold Blocker / Majorが存在しないため、A軸の下限が構造的に上がっている。MiMo / Qwen / MiniMaxの順位上昇は主にこの構造差による。
4. **Harnessによる差は今回も明確**。Claude Code 2件がともにlocal実行 + runtime probeに到達し、Browser 3件はいずれもlocal実行不可、Cursor 2件はともに高速かつlocal実行到達、Open Code 7件は分散最大。FND-02で観測された「Open Codeの分散の大きさ」は今回も再現した。
5. **local実行の有無がC / F軸を通じて総合順位を大きく左右する**という関係は両benchmarkで共通。ただしFND-03では、「実行した」と主張しつつ誤った結論を出す失敗モード（DeepSeek V4 Flash、MiniMax M3）が新たに観測された。

---

## 12. Limitations

1. **1 executionのみ**。各Model + Harnessについてattempt 1のみであり、分散は測定していない。同一構成の再実行で順位が入れ替わる可能性がある。
2. **評価単位はModel + Agent/Harness + Effort + 1 execution**。モデル一般の能力比較として引用してはならない。GPT-5.6 Luna の Codex / Open Code 間で10.5点差が出た事実がこれを示す。
3. **execution timeは環境依存**。network、Docker image cache、tool呼び出し回数、harnessのoverheadを含む。品質100点には算入していない。
4. **Browser harnessはlocal execution不可**。3件中2件はCI / GitHub API evidenceで補ったが、runtime probeを要する論点（G-01 / G-02）では構造的に不利である。1件（ChatGPT o3）はPR branchのfileへ到達できずreview自体が成立しなかった。この差はモデル能力ではなくtool surfaceに起因する部分が大きい。
5. **accidental benchmark exposureが複数のreviewerに存在する**。自己申告分は次の通り。
   - `chatgpt-opus-5.6-sol-browser`: Base commit fetchでdiffが自動展開され、FND-03実装benchmark評価文書の一部が表示された。
   - `gpt-5.6-luna-codex`: repository-wide static searchで`fnd03-model-comparison/summary.md`、`implementation-evaluation.md`、`fnd02-model-comparison/review-benchmark/`配下の該当行が表示された。
   - `gpt-5.6-terra-codex`: Commit APIの応答にbase commitのdiffが含まれ、禁止対象文書の一部が表示された。
   - `chatgpt-gpt-5.5-browser`: PR探索時にPR #105のbody（benchmark ranking / score情報）が表示された。**今回のexposureの中で内容的な影響が最も大きい。**
   いずれも「判定根拠に使用していない」と申告しており、判定内容に汚染の痕跡は見つからなかったが、独立性が完全であったとは言えない。
6. **schema invalid raw JSON 2件**（`deepseek-v4-pro-opencode` / `chatgpt-o3-browser`）。raw immutabilityを優先して補正していない。schema不適合そのものはreview品質のFalse Positive扱いにしていないが、`chatgpt-o3-browser`のreview未完遂はperformance評価へ反映した。
7. **Reference Reviewも完全な真理ではない**。一次証拠に基づくbenchmark基準であり、次の限界がある。
   - local実行はWindows 11 + Docker Desktop 29.6.2上であり、CIのLinux環境とは異なる。timing依存の残存raceを当方のlocal実行だけで否定はできない（そのためxUnit sourceと`TextWriter.Synchronized`の実測というplatform非依存のsemanticsを根拠とした）。
   - G-03は Reference Review lock後に、自身の再確認（`ConsoleCapture`宣言順序）に基づきMinor → Nitへ降格した。この再分類はいずれのreviewerのscoreにも影響しない（G-03を指摘したreviewerは0件）が、lock時点の判定と最終判定が一致していない箇所であることを明記する。
   - Testcontainersの`ResourceReaper`がprocess-wide singletonである点、container起動に全体timeoutがない点は、投機的であるためFindingとしなかった。将来これらが顕在化する可能性は否定しない。
8. **採点は0.5点単位**。同点近傍（84.0 / 83.5など）の順位差は、記述の監査可能性という定性的判断に依存しており、±1点程度の不確かさがある。

---

## 13. 結論

1. **Issue #41 FND-03 Final Synthesis（Head `91e3fca`）は、Acceptance Criteria 10項目すべてを満たし、Blocker 0 / Major 0でmerge可能である。** Reference VerdictはAPPROVE、Minor 2 / Nit 3。Minorはいずれも実装の欠陥ではなく、verification artifactの強度（digest assertionのtautology性）と、文書化されていないBCL実装詳細への依存に関するものである。

2. **17 reviewerのうち16件が正しいAPPROVEへ到達し、blocking False Positiveは全体でゼロだった。** 「cleanな実装を過剰にblockしない」という精度は今回すべてのreviewerで保たれた。

3. **順位を決めたのは、Findingの件数でも文章量でもなく、一次証拠の取り方だった。** 上位3件（Claude Opus 5 / Claude Code、GPT-5.6 Sol / Codex、Claude Sonnet 5 / Claude Code）は共通して、repository stateを壊さずにHeadを実行し、CI logをstep単位で読み、xUnitと.NETのsemanticsを実測または一次sourceで確定させた。

4. **今回最も価値のあった単一のFindingは、`Image.Digest` assertionがconstant同士の比較でしかないという指摘（G-01）である。** これを明示できたのは`claude-opus-5-claude-code`のみだった。`gpt-5.6-sol-codex`と`claude-sonnet-5-claude-code`は`docker image inspect`で自らdaemon-side evidenceを取得したことで実質的に同じ穴を埋めたが、artifact側の弱さとしては言語化しなかった。

5. **新たに観測された失敗モードは「実行したと主張して誤る」ことである。** `deepseek-v4-flash-opencode`と`minimax-m3-opencode`は、いずれも`TextWriter.Synchronized`の同期semanticsについて一次証拠と矛盾する結論を出した。特に前者は「runtime probeで確認した」と明記しながら実測と反対の結果を報告している。merge gate運用では、検証しなかったことより、検証したと称して誤ることの方が危険である。

6. **Harnessは結果を直接左右した。** Browser harnessはlocal実行不可という制約下でdocumentary evidenceを最大化して上位に入る一方、1件はPR branchのfileに到達できずreviewが成立しなかった。Open Codeは分散が最大で、上位（85.0）から下位（65.0）まで広がった。したがってModel名だけでreviewerを選定してはならない。

7. **FND-02との比較では、GPT-5.6 Sol / Codex と Claude Opus 5 / Claude Code の上位2枠、および ChatGPT Opus 5.6 Sol / Browser の4位が再現した。** 一方で下位群のscore上昇は、主にbenchmarkの難易度構造（Gold Blocker / Majorの有無）の差によるものであり、能力向上の証拠として扱ってはならない。

8. **運用上の推奨（今回の結果のみを根拠とする）**: formal Agent B / merge gateには上位3件のいずれかを充て、高速一次reviewにはCursor 2件またはMiMo-V2.5を、specification gateにはBrowser完遂2件またはTerra / Codexを充てる。Open Code下位2件は単独gateには使用せず、cross-check前提の補助に限定する。
