# Issue #41 FND-03 — GPT + Claude 統合Judge評価

- Status: **POST-HOC ADJUDICATED SYNTHESIS**
- Benchmark ID: `fnd03-final-synthesis-independent-review`
- Run ID: `fnd03-final-91e3fca-20260809`
- Repository: `kooiei-in4a/minimal-bank-system`
- Target Issue: #41
- Target PR: #104
- Base SHA: `7946cc55e49c0c6e21ad7b86c20a8435b4976269`
- Head SHA: `91e3fca181558cd1523390347f4f2f80d6014d26`
- Primary CI Run: `31277771209`

> この文書は、ChatGPT JudgeとClaude Opus 5 / Claude Code Judgeの2評価を、一次証拠で不一致裁定した統合結果である。
> 元の2評価は監査用にそのまま保持し、上書きしない。

---

## 1. Executive Summary

2つのJudgeは、PostgreSQL 18.4の実行、database isolation、digest pin、scope、CI、`TextWriter.Synchronized`の現runtime semanticsについて概ね一致した。

唯一merge判断を反転させた争点は、**Testcontainers .NET 4.13.0でcontainer deleteが失敗した後に、同じcontainer instanceを再度`DisposeAsync()`して本当にretryできるか**である。

一次sourceを裁定根拠とした最終Goldは次の通り。

```text
Reference Verdict: REQUEST CHANGES / NOT MERGE READY
Blocker: 0
Major:   1
Minor:   1
Nit:     0（採点対象外の観察事項は別記）
```

Gold Majorは、TestcontainersがDocker resource削除成功前に内部disposed stateをlatchするため、最初のremove failure後の同一instanceへの2回目の`DisposeAsync()`がno-opになり得ること。repository fixtureはそのno-opを成功と区別できず、`container = null`へ遷移してdeterministic ownerを失い得る。

17 reviewerのうち、このroot causeを実質的に検出したreviewerは0件。したがって最終Goldに対するTP/FNは全件`TP=0 / FN=1`となる。

ただし、これを理由にreviewerの証拠収集能力まで低く評価するのは不適切である。そこで統合採点では、**Goldに依存する40点**と**Judge間で比較的安定するReview Quality 60点**を明確に分離した。

最終順位は1位 Claude Opus 5 / Claude Code 66.0、2位 Claude Sonnet 5 / Claude Code 65.5、3位 GPT-5.6 Sol / Codex 60.5。

---

## 2. 入力となった2つのJudge評価

| 文書 | Reference Verdict | 上位3件 |
| --- | --- | --- |
| `implementation-evaluation.md` | REQUEST CHANGES / Major 1 | Claude Opus 65.5 / Claude Sonnet 65.0 / GPT-5.6 Sol 59.5 |
| `implementation-evaluation-claude-opus-5.md` | APPROVE / Major 0 | Claude Opus 99.0 / GPT-5.6 Sol 97.0 / Claude Sonnet 95.5 |

絶対点が大きく違った主因は、採点基準ではなく**Gold Majorが存在するか否か**の判断差だった。

---

## 3. 不一致の裁定

### 3.1 共通認定

- PostgreSQL 18.4 real container / PG category 7件 / skip 0は成立。
- image referenceはdigest-qualifiedで固定。
- test単位database isolation、GUID名、`template0`、`Pooling=false`は成立。
- InMemory / SQLite fallbackなし。
- FND-04以降へのscope creepなし。
- `Fixture.Container.Image.FullName` / `Digest`はdaemon inspectではなくconfiguration image referenceのparse結果。
- .NET 10の`SyncTextWriter`は現runtimeでwrapper instance自身をmonitorとして同期し、`lock(synchronizedWriter)`はwriteと相互排他になる。
- DeepSeek V4 Flash / MiniMax M3の当該同期semantics findingは支持されない。
- ChatGPT o3 / BrowserはINCOMPLETE。

### 3.2 最終Gold Major

Testcontainers 4.13.0のsourceでは、`Resource.Disposed`が`Interlocked.CompareExchange(ref _disposed, 1, 0)`を評価することで、最初のdispose開始時に`_disposed`をlatchする。`DockerContainer.DisposeAsyncCore()`はその後にDocker removeを行う。

したがってremoveがthrowした場合でも`_disposed`は1のまま残り、同じinstanceへの次回`DisposeAsync()`は冒頭の`if (Disposed) return;`で終了する。

repository fixtureは外側のC# referenceを保持しているが、そのreferenceが指すTestcontainers objectは再dispose不能なstateになっている。次回のno-opを成功と判断するとfieldを`null`へ落とすため、未回収resourceのdeterministic ownerを失い得る。

この点は、通常cleanupが成功するgreen CIでは反証されない。

### 3.3 AC-06の精密化

| 論点 | 最終判定 |
| --- | --- |
| cleanup failureを黙って無視しない | PASS — 最初の例外は可視化される |
| failed container dispose後の同一instance retry | FAIL |
| final cleanupのdeterministic成立 | FAIL |
| failed dispose後も有効なowner/handleを維持 | FAIL |

従ってMajorは「例外を握り潰した」問題ではなく、**retry / final cleanup contractとdependency semanticsの不一致**である。

---

## 4. 統合採点方式

元の8軸100点を維持する。

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

ただし統合時は次の2群へ分離する。

### 4.1 Review Quality — 60点

`B + C + E + F + G`。これらはGold Verdictの違いによる影響が比較的小さいため、**ChatGPT JudgeとClaude Judgeの各軸scoreの平均**を採用する。

### 4.2 Gold Alignment — 40点

`A + D + H`。これらはGoldの存在・Severity・最終Verdictへ直接依存するため、2 Judgeの平均は取らない。一次sourceで裁定した最終Gold `REQUEST CHANGES / Major 1` に対して再評価する。

これにより、たとえばClaude Opus 5はReview Qualityでは59.25 / 60と非常に高い一方、Gold Alignmentは6.5 / 40に留まる。**「レビューは深いが、今回の唯一のMajorを見逃した」ことを1つの数字に潰さず読める。**

Totalは8軸合計を0.5点単位へ四捨五入する。順位は丸め前Totalで決定し、表示scoreが同点の場合はReview Qualityをtie-breakとする。

---

## 5. 最終統合ランキング

| Rank | Model + Harness | Review Quality /60 | Gold Alignment /40 | Final Score | Grade | 時間(分) |
| ---: | --- | ---: | ---: | ---: | :---: | ---: |
| 1 | Claude Opus 5 / Claude Code | 59.25 | 6.5 | **66.0** | C | 12 |
| 2 | Claude Sonnet 5 / Claude Code | 57.75 | 7.5 | **65.5** | C | 7 |
| 3 | GPT-5.6 Sol / Codex | 57.75 | 2.5 | **60.5** | D | 11 |
| 4 | ChatGPT Opus 5.6 Sol / Browser | 54.50 | 2.5 | **57.0** | D | 7 |
| 5 | Grok 4.5 / Cursor | 54.25 | 2.5 | **57.0** | D | 6 |
| 6 | GPT-5.6 Terra / Codex | 53.75 | 2.5 | **56.5** | D | 8 |
| 7 | DeepSeek V4 Pro / Open Code | 52.75 | 2.5 | **55.5** | D | 20 |
| 8 | ChatGPT GPT 5.5 / Browser | 51.50 | 2.5 | **54.0** | D | 6 |
| 9 | Composer 2.5 / Cursor | 49.25 | 1.5 | **51.0** | D | 3 |
| 10 | MiMo-V2.5-Pro / Open Code | 48.75 | 1.5 | **50.5** | D | 7 |
| 11 | GPT-5.6 Luna / Codex | 47.75 | 1.5 | **49.5** | F | 11 |
| 12 | MiMo-V2.5 / Open Code | 45.25 | 1.0 | **46.5** | F | 4 |
| 13 | Qwen3.7 Plus / Open Code | 45.00 | 1.0 | **46.0** | F | 10 |
| 14 | GPT-5.6 Luna / Open Code | 42.00 | 1.0 | **43.0** | F | 7 |
| 15 | MiniMax M3 / Open Code | 38.25 | 0.5 | **39.0** | F | 36 |
| 16 | DeepSeek V4 Flash / Open Code | 36.50 | 0.5 | **37.0** | F | 13 |
| 17 | ChatGPT o3 / Browser | 17.00 | 0.0 | **17.0** | F | 5 |

### 解釈

- 1〜3位は両Judgeでも共通して上位3枠だった。統合後もその傾向を維持した。
- Claude Opus 5はReview Quality 59.25 / 60で最高。digest assertionの弱さを唯一Findingとして明示し、runtime/source probeも最深。
- Claude Sonnet 5はGold Majorそのものは未検出だが、container dispose failure pathが未検証であることまで到達したためGold Alignmentが全Reviewer中最も高い。
- GPT-5.6 SolはReview QualityでSonnetと同点級だが、cleanup failureの危険領域への接近が弱く3位。
- DeepSeek FlashとMiniMaxは`SyncTextWriter` semanticsの誤分析によりReview Qualityが大きく低下。
- o3は未完遂のため最下位。

---

## 6. 統合軸別スコア

| Model + Harness | A/25 | B/20 | C/15 | D/10 | E/10 | F/8 | G/7 | H/5 | Total(raw) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Claude Opus 5 / Claude Code | 1.5 | 19.75 | 15.00 | 5.0 | 10.00 | 8.00 | 6.50 | 0.0 | 65.75 |
| Claude Sonnet 5 / Claude Code | 3.0 | 19.00 | 14.50 | 4.5 | 10.00 | 7.75 | 6.50 | 0.0 | 65.25 |
| GPT-5.6 Sol / Codex | 0.5 | 19.50 | 14.25 | 2.0 | 9.75 | 7.50 | 6.75 | 0.0 | 60.25 |
| ChatGPT Opus 5.6 Sol / Browser | 0.5 | 19.25 | 12.25 | 2.0 | 9.75 | 7.00 | 6.25 | 0.0 | 57.00 |
| Grok 4.5 / Cursor | 0.5 | 19.25 | 12.25 | 2.0 | 9.25 | 6.75 | 6.75 | 0.0 | 56.75 |
| GPT-5.6 Terra / Codex | 0.5 | 19.00 | 12.00 | 2.0 | 9.75 | 6.50 | 6.50 | 0.0 | 56.25 |
| DeepSeek V4 Pro / Open Code | 0.5 | 18.50 | 12.00 | 2.0 | 9.50 | 6.75 | 6.00 | 0.0 | 55.25 |
| ChatGPT GPT 5.5 / Browser | 0.5 | 19.25 | 10.75 | 2.0 | 9.75 | 5.75 | 6.00 | 0.0 | 54.00 |
| Composer 2.5 / Cursor | 0.0 | 19.00 | 10.00 | 1.5 | 8.50 | 5.50 | 6.25 | 0.0 | 50.75 |
| MiMo-V2.5-Pro / Open Code | 0.0 | 17.00 | 11.00 | 1.5 | 9.00 | 6.00 | 5.75 | 0.0 | 50.25 |
| GPT-5.6 Luna / Codex | 0.0 | 18.50 | 9.00 | 1.5 | 9.25 | 5.00 | 6.00 | 0.0 | 49.25 |
| MiMo-V2.5 / Open Code | 0.0 | 16.75 | 8.50 | 1.0 | 8.75 | 5.50 | 5.75 | 0.0 | 46.25 |
| Qwen3.7 Plus / Open Code | 0.0 | 16.25 | 8.75 | 1.0 | 9.25 | 5.00 | 5.75 | 0.0 | 46.00 |
| GPT-5.6 Luna / Open Code | 0.0 | 18.00 | 6.50 | 1.0 | 8.25 | 3.75 | 5.50 | 0.0 | 43.00 |
| MiniMax M3 / Open Code | 0.0 | 10.25 | 9.75 | 0.5 | 9.50 | 5.00 | 3.75 | 0.0 | 38.75 |
| DeepSeek V4 Flash / Open Code | 0.0 | 10.25 | 8.75 | 0.5 | 9.25 | 4.50 | 3.75 | 0.0 | 37.00 |
| ChatGPT o3 / Browser | 0.0 | 9.50 | 2.00 | 0.0 | 2.00 | 0.50 | 3.00 | 0.0 | 17.00 |

---

## 7. 元Judge scoreとの関係

| Model + Harness | ChatGPT Judge | Claude Judge | Final Synthesis |
| --- | ---: | ---: | ---: |
| Claude Opus 5 / Claude Code | 65.5 | 99.0 | **66.0** |
| Claude Sonnet 5 / Claude Code | 65.0 | 95.5 | **65.5** |
| GPT-5.6 Sol / Codex | 59.5 | 97.0 | **60.5** |
| ChatGPT Opus 5.6 Sol / Browser | 57.5 | 90.5 | **57.0** |
| Grok 4.5 / Cursor | 56.5 | 90.0 | **57.0** |
| GPT-5.6 Terra / Codex | 56.0 | 89.5 | **56.5** |
| DeepSeek V4 Pro / Open Code | 57.0 | 85.0 | **55.5** |
| ChatGPT GPT 5.5 / Browser | 55.0 | 84.0 | **54.0** |
| Composer 2.5 / Cursor | 50.0 | 83.5 | **51.0** |
| MiMo-V2.5-Pro / Open Code | 51.5 | 80.5 | **50.5** |
| GPT-5.6 Luna / Codex | 47.5 | 82.5 | **49.5** |
| MiMo-V2.5 / Open Code | 45.5 | 78.0 | **46.5** |
| Qwen3.7 Plus / Open Code | 46.0 | 76.5 | **46.0** |
| GPT-5.6 Luna / Open Code | 41.5 | 72.0 | **43.0** |
| MiniMax M3 / Open Code | 34.5 | 70.0 | **39.0** |
| DeepSeek V4 Flash / Open Code | 35.5 | 65.0 | **37.0** |
| ChatGPT o3 / Browser | 14.0 | 28.0 | **17.0** |

Final Synthesisは2 scoreの単純平均ではない。Goldが矛盾している状態で単純平均すると、「Majorが存在する確率50%」のような意味のない数値になるためである。

---

## 8. Non-scoring observations

以下は技術的に有用だが、最終Gold Blocker/Major/MinorやTP/FN母数には入れない。

- `TextWriter.Synchronized`の返却instance自身がmonitorであることは現runtime実装に依存する。保守性上の観察として有効だが、現Headの不具合ではない。
- pre-cancelled cleanup testは`DROP DATABASE`実行中ではなくconnection open時に失敗する。failure visibility / lease retryの証拠としては有効だが、DROP command途中failureの証明ではない。
- `ConsoleCapture.Dispose`の`Console.SetOut/SetError`復元順には理論的な窓があるが、現使用順ではblocking issueとして実証されていない。
- PR本文の「test failure」という表現は、xUnit上では厳密にはtest class cleanup failure。run失敗にはなるため実害なし。

---

## 9. 手続き上の重要な制約

この統合評価は**post-hoc adjudication**である。

ChatGPT側のTestcontainers disposal Majorは、最初のReference Review lock後の追加一次source突合で明確化された。そのため、このMajorを使って17 reviewerを再採点したFinal Synthesisは、技術的な最終判断としては使用できるが、**元プロトコルの『raw reviewerを見る前にGoldを完全固定する』というblind benchmarkの純粋なReference scoreではない**。

従って成果物の位置づけを分ける。

- 元2 Judge文書: 独立Judge executionの監査記録。
- `implementation-evaluation-synthesis.md`: Judge間不一致を後から一次証拠で裁定した最終技術評価。
- Final Synthesis score: **post-hoc adjudicated score**。モデルのblind benchmark scoreとして引用する場合はこの注記を付ける。

厳密なblind benchmarkとしてMajor 1をGoldに採用したい場合は、修正前Headを固定したまま別RUN_IDで新しいJudgeをraw reviewer非参照で実行し、Referenceを先に固定する必要がある。

---

## 10. 結論

1. GPT版とClaude版は単純平均せず、共通事実を統合し、唯一の争点を一次sourceで裁定した。
2. 最終技術Goldは`REQUEST CHANGES / Major 1 / Minor 1`。
3. reviewer qualityは`Review Quality /60`と`Gold Alignment /40`に分離して再採点した。
4. 最終順位はClaude Opus 5 / Claude Code、Claude Sonnet 5 / Claude Code、GPT-5.6 Sol / Codexの順。
5. 1位でもMajorを検出していないため、Final Scoreは66.0。これは『レビュー品質が低い』のではなく、『証拠品質は最高だが唯一のmerge blockerへのGold Alignmentを失った』ことを示す。
6. 今後のformal Agent B reviewには、wrapperのfailure/retry contractがdependency objectのstate machineへ依存する場合、dependency sourceまたはdeterministic failure injectionを必須checkとして追加する。
