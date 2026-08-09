# Issue #41 FND-03 — 17モデル独立レビュー性能評価

## 1. Executive Summary

Issue #41 `[FND-03] 実PostgreSQL integration test基盤を確立する` のFinal Synthesis（PR #104、Base `7946cc55e49c0c6e21ad7b86c20a8435b4976269`、Head `91e3fca181558cd1523390347f4f2f80d6014d26`）について、17件の `Model + Agent/Harness + Effort + 1 execution` を比較した。

Reference / Gold相当 Reviewの結論は **REQUEST CHANGES** である。

| Reference項目 | 結果 |
| --- | --- |
| Blocker | 0 |
| Major | **1** |
| Minor | **1** |
| Nit | 0 |
| Merge readiness | **NOT MERGE READY** |

merge-blocking root causeは、Testcontainers .NET 4.13.0のcontainer disposal stateにある。`PostgreSqlContainerFixture`は、最初の`DisposeAsync()`が失敗してもcontainer handleを保持すればretryできると実装・説明している。しかしTestcontainersの`Resource.Disposed`は、Docker resource削除が成功する前に内部disposed flagをlatchする。最初の削除が失敗した後、同じcontainerへの2回目の`DisposeAsync()`はno-opになり、wrapperはそれを成功と解釈してhandleを`null`へ落とす。その結果、実containerが残ったままdeterministic ownerを失う可能性がある。

**17 ReviewerすべてがこのMajor root causeを見逃した。** したがって全Reviewerが `TP=0 / FN=1` であり、16件の`APPROVE`はReference Verdictと不一致、残る1件は`INCOMPLETE`である。unsupportedなBlocker/Majorを出したReviewerはいないため、blocking FPは全員0とした。

総合1位は **Claude Opus 5 / Claude Code（65.5点、C）**、2位は **Claude Sonnet 5 / Claude Code（65.0点、C）**、3位は **GPT-5.6 Sol / Codex（59.5点、D）** である。1位もmerge blockerを検出していない。Claude Opus 5は、digest assertionがdaemon-side evidenceではないというReference Minorを唯一正確に特定し、最も深いruntime/source probeを行ったため首位になった。Claude Sonnet 5はcontainer disposal未検証の危険領域まで到達したが、Testcontainers内部stateを追わず、機能欠陥ではなくcoverage gapとして止まった。

今回の最大の識別点は、**repositoryのwrapper codeとgreen CIだけで終了せず、Testcontainers 4.13.0の`Resource` / `DockerContainer`実装まで追ったか**である。17件のうち、このmerge判断に必要なdependency source auditを完遂したReviewerはいなかった。

処理時間は品質点へ含めていない。最速はComposer 2.5 / Cursorの3分だが、Gold Majorを見逃しておりformal gate品質とは別である。品質と速度の均衡ではClaude Sonnet 5 / Claude Codeの7分が最も良かった。最長はMiniMax M3 / Open Codeの36分で、誤った`.NET SyncTextWriter` findingを出しており、長時間が高品質を保証しない結果となった。

---

## 2. 評価対象と方法

### 2.1 評価単位

評価単位はモデル単体ではなく、次の組合せによる今回の1 executionである。

```text
Model + Agent / Harness + Effort + Attempt 1
```

モデル一般、Harness一般、公開benchmark、価格、過去の評判はFND-03の採点根拠に使用していない。

### 2.2 二段階評価

1. **Phase A — Reference Review固定**
   - 17件のraw reviewを読む前に、Issue #41、Parent #3、WP #33、`AGENTS.md`、実装計画、Accepted ADR、Base-to-Head diff、Head code/test、CI Run `31277771209`、公式一次sourceだけから独立判定した。
   - Blocker / Major / Minor / Nit、Acceptance Criteria、root cause、Verdictを固定した。
2. **Phase B — Reviewer比較**
   - Reference固定後にのみ`reviews/*.md`と`reviews/*.json`を読んだ。
   - Markdownを人間向けの正本、JSONをstructured補助情報として扱った。
   - 文言一致ではなくroot cause単位でnormalizationした。

### 2.3 採点軸

FND-02 review benchmarkと同じ8軸を使用した。

| 軸 | 配点 | 主な観点 |
| --- | ---: | --- |
| A. 重大問題検出 | 25 | Gold Blocker/Major root causeの検出、重大問題がないとの誤判断回避 |
| B. 誤検知抑制・Precision | 20 | unsupported finding、framework誤読、speculationの抑制 |
| C. 一次証拠・技術検証品質 | 15 | exact target、diff、CI、local test、runtime probe、公式source |
| D. Severity精度 | 10 | merge blockerとnon-blocking issueの区別 |
| E. 仕様・Issue・Scope理解 | 10 | AC、FND-03/FND-04境界、ADR、ownership理解 |
| F. Test / CI / runtime評価力 | 8 | testが証明する範囲、failure point、framework semantics |
| G. Signal-to-Noise | 7 | triage価値、重複・嗜好・無関係なNitの抑制 |
| H. 最終Verdict精度 | 5 | Reference Verdictとの整合、review完遂性 |

唯一のGold Majorを全員が見逃したため、AとHで大きな差を付けた。単に`APPROVE`したこと、Findingが多いこと、文章が長いことには加点していない。

### 2.4 Schema anomalyと時間

- `deepseek-v4-pro-opencode.json`のschema外fieldはrawのまま扱い、review findingのFPにはしない。
- `chatgpt-o3-browser.json`の`outcome: "incomplete"`はenum外だが、Markdownと合わせて**未完遂**として評価した。
- 実行時間は`run.json`を正本とし、`gpt-5.6-luna-codex = 11分`、`minimax-m3-opencode = 36分`を使用した。
- 時間は100点へ加算・減算せず、品質確定後の別軸として分析した。

---

## 3. Reference / Gold相当 Review

### 3.1 Target identity

| 項目 | 固定値 / 確認結果 |
| --- | --- |
| Repository | `kooiei-in4a/minimal-bank-system` |
| Issue | #41 `[FND-03] 実PostgreSQL integration test基盤を確立する` |
| PR | #104 `[FND-03] 実PostgreSQL integration test基盤を確立する — Final Synthesis` |
| Base SHA | `7946cc55e49c0c6e21ad7b86c20a8435b4976269` |
| Head SHA | `91e3fca181558cd1523390347f4f2f80d6014d26` |
| CI target SHA | `91e3fca181558cd1523390347f4f2f80d6014d26` |
| Primary CI | Run `31277771209`, completed / success |
| Diff | 10 files、`+607 / -9` |
| PostgreSQL image | `postgres:18.4@sha256:3a82e1f56c8f0f5616a11103ac3d47e632c3938698946a7ad26da0df1334744a` |
| Package | `Testcontainers.PostgreSql 4.13.0`、`Npgsql 10.0.3` |

Primary CIの`pull_request` checkoutはsynthetic merge commit `da2f91588acb049322d1479547dde8494749e00d`だが、指定Headとのfile diffは0であり、tested treeは対象Headと同一である。CI logではRestore、Build、non-PostgreSQL tests、real PostgreSQL testsが成功し、real PostgreSQL categoryは7件、skip 0だった。

### 3.2 Acceptance Criteria判定

| AC | 判定 | Reference evidence / 判断 |
| --- | :---: | --- |
| AC-01 PostgreSQL 18を実際に起動 | PASS | Testcontainersで実containerを起動し、Npgsql経由の`SHOW server_version_num`を`180004`と照合。CIでPG 7/7。 |
| AC-02 image digest固定 | PASS | digest-qualified referenceを唯一のimage inputとしてbuilderへ渡す。G-02はverification artifactの弱さであり、pin自体の不成立ではない。 |
| AC-03 database lifecycle自動化 | PASS | xUnit `IAsyncLifetime`でFactごとのdatabase create/drop。unique name、`template0`、`Pooling=false`。 |
| AC-04 状態非共有・相互干渉防止 | PASS | test単位database ownershipと実PGのprobe table非共有test。 |
| AC-05 並列範囲と直列条件 | PASS | assembly parallelization有効、Console-sensitive collectionは`DisableParallelization=true`。READMEは`Task.WhenAll`をxUnit scheduler proofとは主張していない。 |
| AC-06 cleanup failureを無視しない | **FAIL** | G-01。最初のcontainer delete failureは可視化されるが、同じhandleのretryがTestcontainers内部でno-opとなり、その後wrapperがhandleを失う。最終cleanup保証が成立しない。 |
| AC-07 startup / connection failureが明確 | PASS | unreachable Docker / PostgreSQL test、primary+cleanup exception aggregation、fallbackなし。 |
| AC-08 CIで実PostgreSQL実行 | PASS | Run `31277771209`の独立PG stepで7/7、skip 0。Docker不在をsuccess化する分岐なし。 |
| AC-09 InMemory / SQLite不使用 | PASS | Npgsql + Testcontainersのみ。fallback providerなし。 |
| AC-10 business table / migrationなし | PASS | application DbContext、migration、business schema/table、Docker Compose、production wiringの追加なし。 |

### 3.3 Reference Findings

#### G-01 — Major — Testcontainers disposal failure後、保持したcontainer handleは実際にはretryできない

- **Severity:** Major
- **Blocking:** YES
- **Root cause key:** `RC-01-testcontainers-dispose-state-poisons-retry`
- **Affected component:**
  - `tests/MinimalBankSystem.IntegrationTests/PostgreSql/PostgreSqlContainerFixture.cs`
  - Testcontainers .NET 4.13.0 `src/Testcontainers/Resource.cs`
  - Testcontainers .NET 4.13.0 `src/Testcontainers/Containers/DockerContainer.cs`
- **一次証拠:**
  1. fixtureは`candidate.DisposeAsync()`が失敗すると例外を再throwし、field `container`を保持する。「fixture retains ownership so cleanup can be retried」と明示する。
  2. Testcontainersの`Resource.Disposed`は`Interlocked.CompareExchange(ref _disposed, 1, 0)`を評価する。最初の評価時点で、resource削除の成否より前に`_disposed=1`へlatchする。
  3. `DockerContainer.DisposeAsyncCore()`は冒頭で`if (Disposed) return;`を実行し、その後にDocker removeを行う。removeがthrowしても`_disposed`は1のまま戻らない。
  4. 同じcontainer instanceに対する2回目の`DisposeAsync()`は即returnする。wrapperはこのno-opを成功と扱い、`container = null`へ遷移する。
- **影響:**
  - 最初のDocker remove failure後、実containerが残っていてもdeterministic ownerを失い得る。
  - ordinary fixture teardownだけでなく、startup途中failureでpartial resource cleanupも失敗した場合に同じroot causeが発生する。
  - resource reaperが後で回収する可能性は、xUnit lifecycleだけで成立するdeterministic teardownと同義ではない。
  - cleanup failureが一度見えるだけでは不十分で、その後のretry/final cleanupを保証するという実装・README・review claimがfalse assuranceになる。
- **Merge判断:** Issue #41のcleanup ownershipとfailure visibilityの中心契約に影響するため、merge前修正または契約の再設計が必要。

#### G-02 — Minor — digest assertionはrunning containerのdaemon-side evidenceではない

- **Severity:** Minor
- **Blocking:** NO
- **Affected component:** `PostgreSqlFixtureTests.PinnedPostgreSql184ContainerProvidesTheTestDatabase`
- **一次証拠:** Testcontainers 4.13.0の`DockerContainer.Image`は`_configuration.Image`を返し、`DockerImage.FullName` / `Digest`はbuilderへ渡したreferenceをparseして構成する。Docker daemonのcontainer inspect結果ではない。
- **影響:** testは「digest-qualified referenceがcode上で固定され、Testcontainers configurationへ渡された」ことを確認するが、「running containerがdaemon側でそのdigestから作られた」ことまでは単独で証明しない。
- **Merge判断:** digest pin自体は成立し、実PG 18.4も起動している。verification wording/evidenceの精度問題としてMinorに留める。

### 3.4 Reference Verdict

**REQUEST CHANGES / NOT MERGE READY**

```text
Blocker: 0
Major:   1
Minor:   1
Nit:     0
```

CI green、正常系container cleanup、database-level retry testは有効な証拠である。しかし、G-01はそれらが通る経路とは異なる「container removal自体が失敗した後」のstate transitionであり、green CIでは反証されない。

---

## 4. Finding normalization

### 4.1 Gold root causes

| Root cause | Gold severity | Blocking | 内容 |
| --- | :---: | :---: | --- |
| RC-01 | Major | YES | Testcontainersが最初のDispose開始時にdisposed stateをlatchするため、Docker remove failure後の同一handle retryがno-opになり、wrapperが未回収resourceのhandleを失い得る。 |

G-02は有効なnon-blocking findingだが、TP/FNの母数には含めない。

### 4.2 TP / FP / FNルール

- **TP:** RC-01を実質的に検出。SeverityがMinor/Blockerでもroot causeが同じならTPとする。
- **FN:** RC-01を見逃す、または「未テストだがretry可能」と誤認する。
- **FP:** ReviewerがBlocker/Majorとして提示したが、一次証拠で支持されないroot cause。
- **non-blocking unsupported finding:** blocking FP件数には入れず、B、C、D、F、Gで減点する。
- **adjacent observation:** container dispose pathが未検証という指摘だけでは、内部state defectを検出していないためTPにしない。

### 4.3 Reviewer finding対応表
| Reviewer | Raw finding | Normalized result | TP / FP / FN treatment |
| --- | --- | --- | --- |
| `claude-opus-5-claude-code` | F-01 Minor: digest assertionはconfig parse | G-02に一致。RC-01は未検出 | TP 0 / FP 0 / FN 1 |
| `claude-sonnet-5-claude-code` | F-01 pre-cancel limitation; F-02 container dispose未test | 有効なnon-blocking観測。RC-01隣接だがroot cause未検出 | TP 0 / FP 0 / FN 1 |
| `gpt-5.6-sol-codex` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `chatgpt-opus-5.6-sol-browser` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `deepseek-v4-pro-opencode` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `grok-4.5-cursor` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `gpt-5.6-terra-codex` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `chatgpt-gpt-5.5-browser` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `mimo-v2.5-pro-opencode` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `composer-2.5-cursor` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `gpt-5.6-luna-codex` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `qwen3.7-plus-opencode` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `mimo-v2.5-opencode` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `gpt-5.6-luna-opencode` | No findings | RC-01未検出 | TP 0 / FP 0 / FN 1 |
| `deepseek-v4-flash-opencode` | F-01 Nit: SyncTextWriterはlockless | 公式.NET 10 sourceと矛盾するunsupported non-blocking finding | TP 0 / FP 0 / FN 1; B/C/F/G減点 |
| `minimax-m3-opencode` | F-01 Minor: SyncTextWriterはinner bufferをlock | 公式.NET 10 sourceと矛盾するunsupported non-blocking finding | TP 0 / FP 0 / FN 1; B/C/F/G減点 |
| `chatgpt-o3-browser` | No findings / INCOMPLETE | 実装・CIを未評価。RC-01未検出 | TP 0 / FP 0 / FN 1; completion減点 |

---
## 5. 総合ランキング

| Rank | Model | Harness | Effort | Score | TP | FP | FN | Verdict精度 | Grade | 時間(分) |
| ---: | --- | --- | --- | ---: | ---: | ---: | ---: | --- | :---: | ---: |
| 1 | **Claude Opus 5** | Claude Code | xhigh | **65.5** | 0 | 0 | 1 | APPROVE → 不一致 | C | 12 |
| 2 | **Claude Sonnet 5** | Claude Code | xhigh | **65.0** | 0 | 0 | 1 | APPROVE → 不一致 | C | 7 |
| 3 | **GPT-5.6 Sol** | Codex | xHigh | **59.5** | 0 | 0 | 1 | APPROVE → 不一致 | D | 11 |
| 4 | **ChatGPT Opus 5.6 Sol** | Browser | xhigh | **57.5** | 0 | 0 | 1 | APPROVE → 不一致 | D | 7 |
| 5 | **DeepSeek V4 Pro** | Open Code | 指定値 | **57.0** | 0 | 0 | 1 | APPROVE → 不一致 | D | 20 |
| 6 | **Grok 4.5** | Cursor | high fast | **56.5** | 0 | 0 | 1 | APPROVE → 不一致 | D | 6 |
| 7 | **GPT-5.6 Terra** | Codex | xHigh | **56.0** | 0 | 0 | 1 | APPROVE → 不一致 | D | 8 |
| 8 | **ChatGPT GPT 5.5** | Browser | xhigh | **55.0** | 0 | 0 | 1 | APPROVE → 不一致 | D | 6 |
| 9 | **MiMo-V2.5-Pro** | Open Code | 未指定 | **51.5** | 0 | 0 | 1 | APPROVE → 不一致 | D | 7 |
| 10 | **Composer 2.5** | Cursor | 未指定 | **50.0** | 0 | 0 | 1 | APPROVE → 不一致 | D | 3 |
| 11 | **GPT-5.6 Luna** | Codex | xHigh | **47.5** | 0 | 0 | 1 | APPROVE → 不一致 | F | 11 |
| 12 | **Qwen3.7 Plus** | Open Code | MAX | **46.0** | 0 | 0 | 1 | APPROVE → 不一致 | F | 10 |
| 13 | **MiMo-V2.5** | Open Code | 未指定 | **45.5** | 0 | 0 | 1 | APPROVE → 不一致 | F | 4 |
| 14 | **GPT-5.6 Luna** | Open Code | Xhigh | **41.5** | 0 | 0 | 1 | APPROVE → 不一致 | F | 7 |
| 15 | **DeepSeek V4 Flash** | Open Code | 指定値 | **35.5** | 0 | 0 | 1 | APPROVE → 不一致 | F | 13 |
| 16 | **MiniMax M3** | Open Code | 指定値 | **34.5** | 0 | 0 | 1 | APPROVE → 不一致 | F | 36 |
| 17 | **ChatGPT o3** | Browser | Medium | **14.0** | 0 | 0 | 1 | INCOMPLETE → 未完遂 | F | 5 |

全ReviewerがRC-01を見逃したため、S/A/B gradeは0件である。上位2件のC gradeは、merge blocker検出ではなく、有効なnon-blocking findingと一次証拠の深さによる。

---

## 6. 評価軸別スコア

| Model / Harness | A /25 | B /20 | C /15 | D /10 | E /10 | F /8 | G /7 | H /5 | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Claude Opus 5 / Claude Code | 1.5 | 19.5 | 15.0 | 5.0 | 10.0 | 8.0 | 6.5 | 0.0 | **65.5** |
| Claude Sonnet 5 / Claude Code | 3.0 | 18.5 | 14.5 | 4.5 | 10.0 | 8.0 | 6.5 | 0.0 | **65.0** |
| GPT-5.6 Sol / Codex | 0.5 | 19.0 | 14.0 | 2.0 | 10.0 | 7.5 | 6.5 | 0.0 | **59.5** |
| ChatGPT Opus 5.6 Sol / Browser | 0.5 | 18.5 | 13.5 | 2.0 | 10.0 | 7.0 | 6.0 | 0.0 | **57.5** |
| DeepSeek V4 Pro / Open Code | 0.5 | 18.5 | 12.5 | 2.0 | 10.0 | 7.0 | 6.5 | 0.0 | **57.0** |
| Grok 4.5 / Cursor | 0.5 | 18.5 | 12.5 | 2.0 | 9.5 | 7.0 | 6.5 | 0.0 | **56.5** |
| GPT-5.6 Terra / Codex | 0.5 | 18.0 | 12.5 | 2.0 | 10.0 | 6.5 | 6.5 | 0.0 | **56.0** |
| ChatGPT GPT 5.5 / Browser | 0.5 | 18.5 | 12.0 | 2.0 | 10.0 | 6.0 | 6.0 | 0.0 | **55.0** |
| MiMo-V2.5-Pro / Open Code | 0.0 | 17.5 | 11.0 | 1.5 | 9.5 | 6.0 | 6.0 | 0.0 | **51.5** |
| Composer 2.5 / Cursor | 0.0 | 18.0 | 10.0 | 1.5 | 9.0 | 5.5 | 6.0 | 0.0 | **50.0** |
| GPT-5.6 Luna / Codex | 0.0 | 17.0 | 9.0 | 1.5 | 9.5 | 4.5 | 6.0 | 0.0 | **47.5** |
| Qwen3.7 Plus / Open Code | 0.0 | 16.0 | 9.0 | 1.0 | 9.5 | 5.0 | 5.5 | 0.0 | **46.0** |
| MiMo-V2.5 / Open Code | 0.0 | 16.5 | 8.5 | 1.0 | 9.0 | 5.0 | 5.5 | 0.0 | **45.5** |
| GPT-5.6 Luna / Open Code | 0.0 | 16.5 | 6.5 | 1.0 | 8.5 | 3.5 | 5.5 | 0.0 | **41.5** |
| DeepSeek V4 Flash / Open Code | 0.0 | 10.5 | 8.0 | 0.5 | 9.5 | 4.0 | 3.0 | 0.0 | **35.5** |
| MiniMax M3 / Open Code | 0.0 | 9.5 | 8.5 | 0.5 | 9.5 | 4.0 | 2.5 | 0.0 | **34.5** |
| ChatGPT o3 / Browser | 0.0 | 7.0 | 2.0 | 0.0 | 2.0 | 0.0 | 3.0 | 0.0 | **14.0** |

Hが全件0点なのは、ReferenceがREQUEST CHANGESであるのに対し、16件がAPPROVE、1件がINCOMPLETEだったためである。

---

## 7. 各Reviewer詳細評価

### 1. Claude Opus 5 / Claude Code

- **Reviewer slug:** `claude-opus-5-claude-code`
- **Score:** 65.5 / 100
- **Grade:** C
- **Verdict:** APPROVE — 不一致
- **Findings:** Minor 1件。`Fixture.Container.Image.FullName` / `Digest` がdaemon-side evidenceではなく、入力referenceのparse結果であることを正確に指摘した。これはReferenceのG-02と一致する。
- **True Positive:** 0。Gold Major RC-01は未検出。
- **False Positive:** 0。blocking FPはなく、提示したMinorは一次証拠で支持される。
- **False Negative:** 1。Testcontainers `Resource._disposed` の先行latchによりcontainer cleanup retryがno-op化するroot causeを見逃した。
- **Evidence quality:** 17件中もっとも深い。Testcontainersのdigest semanticsをall-zero digestのscratch probeで切り分け、xUnit scheduling、`TextWriter.Synchronized`、fixture teardown visibilityも独立probeした。
- **Test / CI / runtime verification:** Headのbuild、non-PG、PG 7/7、full suite、CI step/count、container残存確認まで実施。
- **Severity accuracy:** G-02をMinorとした判断はReferenceと一致。ただしG-01を「handle保持によりretry可能」と評価したためmerge severity判定は不正確。
- **Signal-to-Noise:** 根拠の薄い指摘はなく、長文だがtriageに必要な検証情報が中心。
- **実行時間:** 12分
- **今回観測されたReviewer type:** Deep Technical / Precision / Runtime-Probe
- **総評:** 総合1位。ただしmerge blockerを発見したわけではない。正確なnon-blocking findingと、最も広いruntime/source verificationで首位になった。Formal merge gateとして単独利用するには不十分。

### 2. Claude Sonnet 5 / Claude Code

- **Reviewer slug:** `claude-sonnet-5-claude-code`
- **Score:** 65.0 / 100
- **Grade:** C
- **Verdict:** APPROVE — 不一致
- **Findings:** Minor 2件。pre-cancelled tokenがDROP実行前に失敗する点と、container `DisposeAsync` failure/retry pathがruntime未検証である点を提示した。前者は有効なevidence limitation、後者はGold Majorに最も近い観測。
- **True Positive:** 0。container disposalの危険領域には到達したが、実際のlibrary state transitionを検出していないためTPにはしない。
- **False Positive:** 0。blocking FPはない。F-02は「test不足」に留まり、機能欠陥を特定していないためTPでもFPでもない。
- **False Negative:** 1。Testcontainers内部のdisposed latchとsecond-dispose no-opを見逃し、「codeはretry可能」と誤認した。
- **Evidence quality:** prior failing CI、TextWriter monitor probe、Docker image inspect、full suite反復、container leak確認まで実施。
- **Test / CI / runtime verification:** exact Headでrestore/build、non-PG反復、PG 7/7、full suite、CIを確認。7分でこの深さに到達した。
- **Severity accuracy:** pre-cancel limitationをMinorに留めた点は適切。真のcontainer cleanup defectをMajor化できなかった。
- **Signal-to-Noise:** 2件とも重点確認事項に関係し、重複や設計嗜好は少ない。
- **実行時間:** 7分
- **今回観測されたReviewer type:** Deep Technical / Adversarial / Broad
- **総評:** Gold Majorの周辺まで最も近づいたReviewer。Source dependencyの`Resource`基底classまで追えば首位かつREQUEST CHANGESに到達できた。品質と速度の均衡は今回最良。

### 3. GPT-5.6 Sol / Codex

- **Reviewer slug:** `gpt-5.6-sol-codex`
- **Score:** 59.5 / 100
- **Grade:** D
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。unsupported blocking findingはない。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** Base/Head、PR merge ref、CI log、local full suite、Docker image inspect、package resolution、xUnit/.NET sourceを確認しており、証拠収集は非常に強い。
- **Test / CI / runtime verification:** real PostgreSQL 7/7、full suite、container残存0、initial CI raceのroot causeと最終fixまで確認。
- **Severity accuracy:** 重大findingを出していないためG-01とのSeverity整合はない。
- **Signal-to-Noise:** Findingなしで低ノイズ。検証記録は豊富だが、依存ライブラリdispose semanticsの監査へ結び付かなかった。
- **実行時間:** 11分
- **今回観測されたReviewer type:** Deep Technical / Specification / Verification-heavy
- **総評:** 実装表面とruntime evidenceの検証は上位。ただし「wrapperがhandleを保持する」というコード上の見た目を超えてTestcontainersの再Dispose挙動を調べず、merge verdictを誤った。

### 4. ChatGPT Opus 5.6 Sol / Browser

- **Reviewer slug:** `chatgpt-opus-5.6-sol-browser`
- **Score:** 57.5 / 100
- **Grade:** D
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** Issue hierarchy、diff、CI merge ref、primary/push CI、initial flaky CI、ConsoleCapture修正までBrowser環境で広く確認した。
- **Test / CI / runtime verification:** local executionは不可。ただしCIのtest count、skip 0、exact Headのpush runまで確認した。
- **Severity accuracy:** container handle保持をretry成立と判断し、Majorを0件とした。
- **Signal-to-Noise:** 情報量は多いが、ほぼ重点項目に沿う。accidental benchmark exposureを自己申告している。
- **実行時間:** 7分
- **今回観測されたReviewer type:** Deep Technical / Specification / Browser-Evidence
- **総評:** Browserだけで高い一次証拠密度を実現したが、dependency implementationの深掘りが不足した。local execution不可そのものより、source-level disposal auditを行わなかった点が敗因。

### 5. DeepSeek V4 Pro / Open Code

- **Reviewer slug:** `deepseek-v4-pro-opencode`
- **Score:** 57.0 / 100
- **Grade:** D
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。schema外fieldはraw transport anomalyでありFPには数えない。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** CI 2 run、local PG 7/7、build、container leak観測、ConsoleCapture monitor解釈まで確認した。non-PG local runはhangし、CIで補完した。
- **Test / CI / runtime verification:** 実PostgreSQL containerを起動。local環境差を明示した点は良い。
- **Severity accuracy:** cleanupを完全にPASSとし、failed dispose後のlibrary stateを評価しなかった。
- **Signal-to-Noise:** No findingsで低ノイズ。ACごとの説明は明確。
- **実行時間:** 20分
- **今回観測されたReviewer type:** Broad / Verification-heavy / Precision
- **総評:** 広く堅実だが20分を使ってもTestcontainers内部へ到達しなかった。schema invalid JSON自体は品質減点の中心ではなく、merge blocker見逃しが支配的。

### 6. Grok 4.5 / Cursor

- **Reviewer slug:** `grok-4.5-cursor`
- **Score:** 56.5 / 100
- **Grade:** D
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。container `DisposeAsync` failure injectionが未実施であることはNotesで認識したが、code inspectionだけで問題なしとした。
- **True Positive:** 0。
- **False Positive:** 0。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** detached worktree、restore/build、non-PG 30/30、PG 7/7、full suite、CIを6分で確認。
- **Test / CI / runtime verification:** 実行範囲は広い。runtime成功はnormal pathのみで、cleanup failure時のretry semanticsは証明していない。
- **Severity accuracy:** 危険領域を認識しながらnon-finding扱いしたためMajor判定に失敗。
- **Signal-to-Noise:** 非常に簡潔で低ノイズ。
- **実行時間:** 6分
- **今回観測されたReviewer type:** Fast / Verification-heavy / Precision
- **総評:** 高速一次reviewとして有力。ただし「未テストだがcode上正しい」という判断が依存library source未確認のまま行われた。merge gateには追加のadversarial dependency auditが必要。

### 7. GPT-5.6 Terra / Codex

- **Reviewer slug:** `gpt-5.6-terra-codex`
- **Score:** 56.0 / 100
- **Grade:** D
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** Issue/ADR、exact diff、CI、temporary archiveでのfull suiteを確認。
- **Test / CI / runtime verification:** real PostgreSQL testとresource reaper session残存を確認したが、意図的なdispose failureは検証していない。
- **Severity accuracy:** wrapperの例外伝播とhandle保持のみを見てAC-06 PASSとした。
- **Signal-to-Noise:** 簡潔で重点項目中心。accidental benchmark exposureを自己申告。
- **実行時間:** 8分
- **今回観測されたReviewer type:** Specification / Verification-heavy / Precision
- **総評:** 仕様・scope理解は強い。正常系のclean teardownからfailure retryの正しさを推定したことが誤りで、dependency state machine監査が不足した。

### 8. ChatGPT GPT 5.5 / Browser

- **Reviewer slug:** `chatgpt-gpt-5.5-browser`
- **Score:** 55.0 / 100
- **Grade:** D
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** authority documents、diff、workflow、CI job logを詳細に確認。local dotnet/dockerは利用不可。
- **Test / CI / runtime verification:** CIによりnon-PG/PG件数とskip 0を確認したが、dependency probeはない。
- **Severity accuracy:** container failure pathを静的に「handle retained」と評価し、libraryのsecond-dispose挙動を見なかった。
- **Signal-to-Noise:** Browser制約下として整理は良い。PR #105のranking accidental exposureを明示。
- **実行時間:** 6分
- **今回観測されたReviewer type:** Specification / Broad / Browser-Evidence
- **総評:** 6分のBrowser reviewとしては広いが、実装者wrapperの説明をdependency semanticsで反証する段階へ進めなかった。

### 9. MiMo-V2.5-Pro / Open Code

- **Reviewer slug:** `mimo-v2.5-pro-opencode`
- **Score:** 51.5 / 100
- **Grade:** D
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** restore/build、PG 7/7、container create/delete、CIを確認。
- **Test / CI / runtime verification:** 実PostgreSQL normal pathは確認したが、full non-PG suiteやfailure-state dependency probeは限定的。
- **Severity accuracy:** `Container.Image.Digest`をrunning-image runtime evidenceと扱うなどG-02のsemanticsも見逃した。
- **Signal-to-Noise:** AC checklistは明瞭で過剰指摘はない。
- **実行時間:** 7分
- **今回観測されたReviewer type:** Moderate / Runtime / Checklist
- **総評:** normal-path verificationは成立しているが、テストが何を証明しないか、dependency objectのfailure stateまで評価できていない。

### 10. Composer 2.5 / Cursor

- **Reviewer slug:** `composer-2.5-cursor`
- **Score:** 50.0 / 100
- **Grade:** D
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** PR/CI/diff、local restore/build、non-PG 30、PG 7を3分で確認。workspaceのuntracked candidateを正本から除外した。
- **Test / CI / runtime verification:** 実行効率は最速。ただしsource dependencyの内部状態は未確認。
- **Severity accuracy:** 全AC PASSとし、cleanup failure retryをtest結果だけで受け入れた。
- **Signal-to-Noise:** 非常に低ノイズ。短いが重要なnegative analysisも少ない。
- **実行時間:** 3分
- **今回観測されたReviewer type:** Fast / Surface / Low-noise
- **総評:** 最速の一次screening。単純なscore/timeでは高く見えるが、merge blockerを見逃しているため正式gate用途には使えない。

### 11. GPT-5.6 Luna / Codex

- **Reviewer slug:** `gpt-5.6-luna-codex`
- **Score:** 47.5 / 100
- **Grade:** F
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** read-only diff、PR metadata、CI counts、xUnit/.NET documentationを確認。local executionはなし。
- **Test / CI / runtime verification:** CI evidenceは有効だが、failure injectionやdependency source probeは未実施。
- **Severity accuracy:** failed handleをretryableと断定しておりG-01と反対の結論。
- **Signal-to-Noise:** 簡潔。repository searchで禁止benchmark行が偶発表示された旨を記録。
- **実行時間:** 11分
- **今回観測されたReviewer type:** Specification / CI-centric / Surface
- **総評:** 対象同定とCI確認は正しいが、11分に対してtechnical depthが伸びず、cleanup contractの核心を静的表面で判断した。

### 12. Qwen3.7 Plus / Open Code

- **Reviewer slug:** `qwen3.7-plus-opencode`
- **Score:** 46.0 / 100
- **Grade:** F
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** target identity、diff全10 files、CI logを確認。local SDK不足で実行なし。
- **Test / CI / runtime verification:** CIのPG 7/7を確認した。`Container.Image.Digest`を「runtime evidence」と誤って位置付けた。
- **Severity accuracy:** cleanup wrapperをそのまま正しいと判断し、重大問題なしとした。
- **Signal-to-Noise:** AC別説明は整理されているが、証明力の区別に誤りがある。
- **実行時間:** 10分
- **今回観測されたReviewer type:** Specification / CI-centric / Checklist
- **総評:** FND-02時のtarget取得失敗とは異なり今回は完遂したが、証拠semanticsとdependency sourceの深さが不足した。

### 13. MiMo-V2.5 / Open Code

- **Reviewer slug:** `mimo-v2.5-opencode`
- **Score:** 45.5 / 100
- **Grade:** F
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** PR/CI、merge ref、AC別code evidenceを4分でまとめた。local executionなし。
- **Test / CI / runtime verification:** CI countは確認したが、runtime probeは行っていない。
- **Severity accuracy:** digest propertyをruntime assertion、cleanupGate/handle保持をretry保証と解釈した。
- **Signal-to-Noise:** 比較的簡潔だが、正しいように見える説明の中にsemantics誤認がある。
- **実行時間:** 4分
- **今回観測されたReviewer type:** Broad Checklist / CI-centric / Fast
- **総評:** 高速で網羅的なchecklist reviewだが、深い反証力は弱い。補助的なAC確認には使えるがmerge gateには不足。

### 14. GPT-5.6 Luna / Open Code

- **Reviewer slug:** `gpt-5.6-luna-opencode`
- **Score:** 41.5 / 100
- **Grade:** F
- **Verdict:** APPROVE — 不一致
- **Findings:** Findingなし。
- **True Positive:** 0。
- **False Positive:** 0。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** CI success、test counts、diff check、fixture要点を短く確認。local Head testなし。
- **Test / CI / runtime verification:** runtime evidenceはCI依存で、framework/library semanticsの検証がない。
- **Severity accuracy:** 全AC PASSの理由が要約レベルで、cleanup retryの実体を評価していない。
- **Signal-to-Noise:** 低ノイズだが情報量不足。
- **実行時間:** 7分
- **今回観測されたReviewer type:** Surface / Checklist / CI-centric
- **総評:** 完遂はしているがsurface reviewに留まる。7分の実行で重大な反証探索がほぼ見られない。

### 15. DeepSeek V4 Flash / Open Code

- **Reviewer slug:** `deepseek-v4-flash-opencode`
- **Score:** 35.5 / 100
- **Grade:** F
- **Verdict:** APPROVE — 不一致
- **Findings:** Nit 1件。`.NET 10 SyncTextWriterはlocklessで、lock(synchronizedWriter)はwriteと排他しない`と指摘したが、.NET 10 sourceでは各methodが`MethodImplOptions.Synchronized`でwrapper instanceをmonitorとして使うため誤り。
- **True Positive:** 0。
- **False Positive:** 0。Nitのためblocking FP件数には入れないが、unsupported technical findingとしてB/C/F/Gを大きく減点。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** local test反復とIL/runtime probeを主張するが、結論が一次sourceと矛盾する。
- **Test / CI / runtime verification:** PG 7/7、ApiRuntimeContractTests反復は確認したが、誤ったframework modelでrace fixを評価した。
- **Severity accuracy:** 誤ったfindingをNitに留めたためblocking damageは小さいが、正しいG-01は未検出。
- **Signal-to-Noise:** 誤った技術説明がreviewerの判断コストを増やす。
- **実行時間:** 13分
- **今回観測されたReviewer type:** Deep-looking / Framework-misread / Unreliable
- **総評:** 実行量はあるが、framework semanticsの誤読により信頼性が低下。今回のexecutionでは補助reviewにも再検証が必須。

### 16. MiniMax M3 / Open Code

- **Reviewer slug:** `minimax-m3-opencode`
- **Score:** 34.5 / 100
- **Grade:** F
- **Verdict:** APPROVE — 不一致
- **Findings:** Minor 1件。`SyncTextWriterはinner bufferをlockするためouter wrapper lockと不一致`と指摘したが、実際はsynchronized methodがwrapper instanceをlockするため誤り。
- **True Positive:** 0。
- **False Positive:** 0。Minorはblocking FPに数えないが、unsupported findingとして大幅減点。
- **False Negative:** 1。RC-01を見逃した。
- **Evidence quality:** local PG 7/7、build、test discoveryは確認した。一方、最重要framework主張が公式sourceと不整合。
- **Test / CI / runtime verification:** 36分で最長。ApiRuntimeContractTestsのWindows crashはbaseでも再現と切り分けたが、Gold rootへ進めなかった。
- **Severity accuracy:** 誤ったframework findingをMinor化し、真のMajorを0件とした。
- **Signal-to-Noise:** 非常に長く、誤ったlock modelが大きなtriage負荷を生む。
- **実行時間:** 36分
- **今回観測されたReviewer type:** Over-strict / Framework-misread / Slow / Unreliable
- **総評:** 時間と深さが品質へ結び付かなかった例。長時間reviewでもdependency sourceの正確な読解が欠けるとformal gate性能は上がらない。

### 17. ChatGPT o3 / Browser

- **Reviewer slug:** `chatgpt-o3-browser`
- **Score:** 14.0 / 100
- **Grade:** F
- **Verdict:** INCOMPLETE — 未完遂
- **Findings:** Findingなし。全ACをUNCERTAINとし、code/diff/CIを確認できず終了。
- **True Positive:** 0。
- **False Positive:** 0。schema enum外の`outcome: incomplete`自体はFPにしない。
- **False Negative:** 1。RC-01を未評価。
- **Evidence quality:** Repository/PR/Base/Headの存在確認に留まり、CI SHAも未確認。
- **Test / CI / runtime verification:** CI independently checked = NO、local execution = NO。
- **Severity accuracy:** reviewを完遂していないためSeverity判定なし。
- **Signal-to-Noise:** 無理な断定を避けた点は良いが、benchmark taskを達成していない。
- **実行時間:** 5分
- **今回観測されたReviewer type:** Incomplete
- **総評:** Harnessとして対象コードへ到達できず最下位。偶然のAPPROVE/REQUEST CHANGESではなく、完遂性そのものをperformanceへ反映した。

---

## 8. Reviewer type / 観測傾向

以下は今回の1 executionに対する事後分類であり、モデル一般の恒常的性質ではない。

| Reviewer | 観測type | 今回の特徴 |
| --- | --- | --- |
| `claude-opus-5-claude-code` | Deep Technical / Precision / Runtime-Probe | 正確なdigest semanticsと多面的probe。dependency disposal rootだけ未到達。 |
| `claude-sonnet-5-claude-code` | Deep Technical / Adversarial / Broad | cleanup failure testの限界を発見。Gold Major周辺まで最接近。 |
| `gpt-5.6-sol-codex` | Deep Technical / Specification / Verification-heavy | target/CI/local/runtime evidenceは強いがclean approvalへ収束。 |
| `chatgpt-opus-5.6-sol-browser` | Deep Technical / Specification / Browser-Evidence | Browserで広い一次証拠。local不可でもCI incidentを深掘り。 |
| `deepseek-v4-pro-opencode` | Broad / Verification-heavy / Precision | 広いAC検証とlocal PG。dependency state machineは未監査。 |
| `grok-4.5-cursor` | Fast / Verification-heavy / Precision | 高速・低ノイズ・実行範囲広い。adversarial depth不足。 |
| `gpt-5.6-terra-codex` | Specification / Verification-heavy / Precision | 仕様・scope・runtimeを均衡確認。failure retryを表面判断。 |
| `chatgpt-gpt-5.5-browser` | Specification / Broad / Browser-Evidence | 文書・diff・CI中心の広いBrowser review。 |
| `mimo-v2.5-pro-opencode` | Moderate / Runtime / Checklist | normal-path runtime中心。証拠semanticsの区別が弱い。 |
| `composer-2.5-cursor` | Fast / Surface / Low-noise | 最速のsurface screening。低ノイズだが深い反証なし。 |
| `gpt-5.6-luna-codex` | Specification / CI-centric / Surface | CI/source checklist中心。local/probeなし。 |
| `qwen3.7-plus-opencode` | Specification / CI-centric / Checklist | target完遂とAC整理は良いがdigest/runtime解釈に誤り。 |
| `mimo-v2.5-opencode` | Broad Checklist / CI-centric / Fast | 高速で網羅的なchecklist。dependency depth不足。 |
| `gpt-5.6-luna-opencode` | Surface / Checklist / CI-centric | 短いsurface review。 |
| `deepseek-v4-flash-opencode` | Deep-looking / Framework-misread / Unreliable | 深いprobeを主張するが.NET semanticsを誤読。 |
| `minimax-m3-opencode` | Over-strict / Framework-misread / Slow / Unreliable | 長時間・広範だが誤ったframework findingでnoise増。 |
| `chatgpt-o3-browser` | Incomplete | 対象code/CIへ到達できず未完遂。 |

今回、`Deep Technical`に分類されたReviewerも全員RC-01を見逃した。したがって、単にprobe数が多い、local testを実行した、文章が詳細というだけではformal review性能を保証しない。

---
## 9. 実行時間と品質

実行時間は採点外である。以下の`Score / 分`は参考値であり、merge gate品質の代替指標ではない。

| 時間順位 | Reviewer | 時間(分) | Score | Score / 分（参考） | 解釈 |
| ---: | --- | ---: | ---: | ---: | --- |
| 1 | `composer-2.5-cursor` | 3 | 50.0 | 16.67 | 最速。一次screening効率は高いがFN 1。 |
| 2 | `mimo-v2.5-opencode` | 4 | 45.5 | 11.38 | 参考値。Gold Major検出なし。 |
| 3 | `chatgpt-o3-browser` | 5 | 14.0 | 2.80 | 5分で停止し未完遂。 |
| 4 | `grok-4.5-cursor` | 6 | 56.5 | 9.42 | 参考値。Gold Major検出なし。 |
| 5 | `chatgpt-gpt-5.5-browser` | 6 | 55.0 | 9.17 | 参考値。Gold Major検出なし。 |
| 6 | `claude-sonnet-5-claude-code` | 7 | 65.0 | 9.29 | 上位品質と速度の均衡が最良。 |
| 7 | `chatgpt-opus-5.6-sol-browser` | 7 | 57.5 | 8.21 | 参考値。Gold Major検出なし。 |
| 8 | `mimo-v2.5-pro-opencode` | 7 | 51.5 | 7.36 | 参考値。Gold Major検出なし。 |
| 9 | `gpt-5.6-luna-opencode` | 7 | 41.5 | 5.93 | 参考値。Gold Major検出なし。 |
| 10 | `gpt-5.6-terra-codex` | 8 | 56.0 | 7.00 | 参考値。Gold Major検出なし。 |
| 11 | `qwen3.7-plus-opencode` | 10 | 46.0 | 4.60 | 参考値。Gold Major検出なし。 |
| 12 | `gpt-5.6-sol-codex` | 11 | 59.5 | 5.41 | 参考値。Gold Major検出なし。 |
| 13 | `gpt-5.6-luna-codex` | 11 | 47.5 | 4.32 | 参考値。Gold Major検出なし。 |
| 14 | `claude-opus-5-claude-code` | 12 | 65.5 | 5.46 | 最上位品質。深いprobeに相応の時間。 |
| 15 | `deepseek-v4-flash-opencode` | 13 | 35.5 | 2.73 | 参考値。Gold Major検出なし。 |
| 16 | `deepseek-v4-pro-opencode` | 20 | 57.0 | 2.85 | 参考値。Gold Major検出なし。 |
| 17 | `minimax-m3-opencode` | 36 | 34.5 | 0.96 | 最長だが誤ったfinding。時間効率・品質とも低い。 |

### 9.1 最速

- **Composer 2.5 / Cursor — 3分:** exact target、CI、local testを短時間で確認した。ただしdependency source auditはなく、正式なmerge gateには不足する。
- **MiMo-V2.5 / Open Code — 4分:** AC checklistを高速に作成したが、digestとcleanup semanticsを誤認した。
- **ChatGPT o3 / Browser — 5分:** 速度ではなく未完遂であり、fast reviewとしても成立していない。

### 9.2 高品質かつ高速

- **Claude Sonnet 5 / Claude Code — 7分、65.0点:** Gold Majorの隣接領域まで到達し、failure injectionの正確な範囲を切り分けた。
- **ChatGPT Opus 5.6 Sol / Browser — 7分、57.5点:** local環境なしでもCI incidentとtarget evidenceを深く追った。
- **Grok 4.5 / Cursor — 6分、56.5点:** local full suiteを含む高速verification。ただしadversarial dependency analysisは不足。

### 9.3 深いが時間が掛かったReviewer

- **Claude Opus 5 / Claude Code — 12分:** 最も深く正確なnon-blocking analysis。
- **GPT-5.6 Sol / Codex — 11分:** runtime/CI/source evidenceは強いがGold Major未検出。
- **DeepSeek V4 Pro / Open Code — 20分:** 広く検証したがdependency stateへ進めなかった。
- **MiniMax M3 / Open Code — 36分:** 最長だがframework誤読を含み、深さと正確さが一致しなかった。

### 9.4 Harness別に今回観測された差

- **Claude Code:** 2件とも上位。probe設計とfailure semanticsの分解が強かったが、Testcontainers基底classのdispose stateは両方とも未確認。
- **Codex:** target identity、CI、local execution、spec/scopeの整合が安定していた。一方、3件すべてclean approvalへ収束し、dependency内部の反証探索が不足。
- **Browser:** 2件はlocalなしでも詳細なGitHub一次証拠を構築し、1件はtool制約で未完遂。Harness内varianceが大きい。
- **Cursor:** 3分・6分と最速群。一次screeningには効率的だが、今回のdeep dependency root causeには到達しなかった。
- **Open Code:** local executionを伴うreviewからsurface checklist、framework誤読、長時間低効率までvarianceが最大。Harness名だけで品質を推定できない。

---

## 10. 実務上の使い分け

### Formal Agent B / merge gate向き

**今回のraw resultだけでは、単独でformal merge gateを任せられるReviewerはいない。** 全員が唯一のGold Majorを見逃したためである。

この中から人間補助付きで主Reviewerを選ぶなら、Claude Opus 5 / Claude CodeまたはClaude Sonnet 5 / Claude Codeが最も近い。ただし次の追加gateを必須にする必要がある。

```text
wrapper codeだけでcleanup retryを判断せず、
dependencyのDispose state machineとsecond-call semanticsを公式sourceで確認する
```

### adversarial探索向き

- **Claude Sonnet 5 / Claude Code:** failure injection pointと未検証pathを見つける力が最も有効だった。
- **Claude Opus 5 / Claude Code:** claimと実証の差をprobeで崩す力が強い。

両者を組み合わせてもRC-01は見逃しているため、dependency source checklistを追加する必要がある。

### specification review向き

- **GPT-5.6 Sol / Codex**
- **ChatGPT Opus 5.6 Sol / Browser**
- **GPT-5.6 Terra / Codex**
- **DeepSeek V4 Pro / Open Code**

Issue/ADR/scope/CI targetの照合は強い。ただし今回のようにspec準拠がdependency semanticsへ依存する場合、仕様確認だけでは不足する。

### 高速一次review向き

- **Grok 4.5 / Cursor — 6分**
- **Composer 2.5 / Cursor — 3分**
- **ChatGPT GPT 5.5 / Browser — 6分**

明白なtarget mismatch、scope drift、CI未実行、基本的なlifecycle不備を短時間でscreeningする用途には使える。merge承認の最終判断にはしない。

### 補助review向き

MiMo-V2.5-Pro、Qwen3.7 Plus、MiMo-V2.5、GPT-5.6 Luna / Open Codeは、AC checklistやCI確認の補助には使えるが、一次証拠の意味とdependency internalsを別Reviewerが再確認する必要がある。DeepSeek V4 FlashとMiniMax M3は今回、framework semanticsの誤ったfindingを出したため、出力をそのままtriageへ流さず一次source照合を必須とする。ChatGPT o3 / Browserは未完遂のため補助reviewとしても利用できない。

---

## 11. FND-02との比較

FND-02 historical benchmarkは比較材料としてのみ使用し、FND-03 scoreを過去結果へ合わせて調整していない。FND-02はGold Blocker/Majorが4 root causesあり、production Kestrel boundaryなどを検出できたかが主な差だった。FND-03はGold Majorが1件で、Testcontainersのdependency disposal internalsまで追う必要があった。

| Model + Harness | FND-02 Rank / Score | FND-03 Rank / Score | 今回観測された変化 |
| --- | ---: | ---: | --- |
| GPT-5.6 Sol / Codex | 1 / 100.0 | 3 / 59.5 | FND-02では4/4 TP。今回は深いverificationを行ったが唯一のMajorを未検出。 |
| Claude Opus 5 / Claude Code | 2 / 92.5 | 1 / 65.5 | 順位は首位へ上昇。正確なMinorを発見したがMajorは未検出。 |
| GPT-5.6 Luna / Open Code | 3 / 88.0 | 14 / 41.5 | 今回はsurface reviewに留まり大幅悪化。 |
| ChatGPT Opus 5.6 Sol / Browser | 4 / 87.5 | 4 / 57.5 | 順位は同じだが、今回はGold TP 0。Browser evidence構築は一貫。 |
| GPT-5.6 Luna / Codex | 5 / 82.0 | 11 / 47.5 | FND-02の重大問題検出が再現せず、CI-centric reviewに留まった。 |
| DeepSeek V4 Flash / Open Code | 6 / 77.0 | 15 / 35.5 | 今回は.NET synchronization semanticsの誤読で大幅悪化。 |
| GPT-5.6 Terra / Codex | 7 / 75.5 | 7 / 56.0 | 順位は同じ。spec/runtime verificationは安定したがMajor未検出。 |
| Claude Sonnet 5 / Claude Code | 8 / 60.0 | 2 / 65.0 | failure semanticsの分析が改善し、Gold Major周辺まで到達。Verdictは引き続き不一致。 |
| Composer 2.5 / Cursor | 9 / 54.5 | 10 / 50.0 | 高速・低ノイズ傾向は再現。deep root cause検出は弱い。 |
| Grok 4.5 / Cursor | 10 / 54.0 | 6 / 56.5 | 高速verificationで順位改善。ただしGold TPは0。 |
| ChatGPT GPT 5.5 / Browser | 11 / 53.0 | 8 / 55.0 | Browserによる広いevidence確認が概ね再現。 |
| DeepSeek V4 Pro / Open Code | 12 / 47.5 | 5 / 57.0 | complete local verificationで改善したがMajor未検出。 |
| MiMo-V2.5-Pro / Open Code | 14 / 19.0 | 9 / 51.5 | FND-02より大幅に完遂性が改善。normal-path中心でdeep dependency rootは未検出。 |
| MiMo-V2.5 / Open Code | 15 / 7.0 | 13 / 45.5 | 今回はreviewを完遂。checklist品質は上がったがformal gate水準には未達。 |
| Qwen3.7 Plus / Open Code | 16 / 2.0 | 12 / 46.0 | FND-02のtarget誤認から改善し、今回は正しいHead/CIを評価。technical depthは不足。 |
| MiniMax M3 / Open Code | 17 / 0.0 | 16 / 34.5 | 未完遂から完遂へ改善。ただし36分と誤ったframework findingで低位。 |
| ChatGPT o3 / Browser | 比較対象なし（FND-02はo2） | 17 / 14.0 | 同一モデルではないため縦比較しない。今回のBrowser executionは未完遂。 |

FND-02で強かったReviewerがFND-03でも自動的に強いとは限らなかった。特にGPT-5.6 Sol / Codex、GPT-5.6 Luna系、DeepSeek V4 Flashは重大問題検出が再現しなかった。一方、Claude系は今回も深いprobe傾向を示したが、dependency disposal rootまでは届かなかった。これはモデル一般の順位ではなく、task-specificな1 execution差である。

---

## 12. Limitations

- 各Reviewerは1 executionのみであり、再現性・分散を測っていない。
- 評価対象はModel単体ではなくModel + Harness + Effort + executionである。
- 実行時間はmachine、cache、network、Docker image availability、tool latencyに依存する。
- Browser Harnessはlocal repository、.NET、Dockerを利用できない場合があり、CI/source evidence中心になる。
- 一部Reviewerは検索・commit API応答から過去benchmarkやPR #105の情報へaccidental exposureした。自己申告上は判定根拠に使用していないが、完全なblindnessを保証できない。
- raw JSON 2件は現行schemaにvalidではない。rawを変更せずMarkdownと併読した。
- Reference Reviewも完全な真理ではなく、固定Head、CI、framework/library一次sourceに基づくbenchmark基準である。
- Judge環境ではlocal Docker failure injectionを再現していない。G-01はTestcontainers 4.13.0の決定論的なsource-level state transitionから判定した。
- 実際のDocker remove failure後にresource reaperが最終的に回収する環境もあり得る。しかしIssue #41が求めるxUnit lifecycleによるdeterministic ownership/retryとは別である。
- FND-02比較はhistorical scoreの記述的比較であり、FND-03 scoreのcalibrationには使用していない。

---

## 13. 結論

FND-03 Final SynthesisのReference Verdictは **REQUEST CHANGES** である。database単位のisolation、実PostgreSQL 18.4、digest pin、CI実行、parallel policy、scope管理は概ね成立している。一方、container cleanup failure後のretry保証はTestcontainers 4.13.0の内部disposed stateと矛盾し、未回収containerのdeterministic ownerを失う可能性がある。

17 Reviewerのうち、このmerge-blocking root causeを検出したものは0件だった。したがって、今回のbenchmarkで最も重要な結論は「1位のReviewerをそのまま採用すればよい」ではない。**green CI、local test、wrapper code inspectionが揃っていても、contractがdependency state machineへ依存する場合は公式sourceまで監査するreview stageが必要**である。

今回の相対順位では、Claude Opus 5 / Claude Codeが正確なdigest semanticsと最深のevidenceで1位、Claude Sonnet 5 / Claude Codeがcleanup failure領域への接近と速度で2位、GPT-5.6 Sol / Codexが広いverificationで3位となった。ただし3件ともAPPROVEは不正確であり、formal merge gateとしては追加のdependency-source reviewなしに採用できない。
