# Issue #41 FND-03 — 独立レビュー性能評価サマリー

## 1. 最終結論

GPT JudgeとClaude Opus 5 / Claude Code Judgeの2評価を、単純平均ではなく一次証拠で不一致裁定し、`implementation-evaluation-synthesis.md`に統合した。

最終技術Goldは次の通り。

```text
Reference Verdict: REQUEST CHANGES / NOT MERGE READY
Blocker: 0
Major:   1
Minor:   1
```

争点はTestcontainers .NET 4.13.0のcontainer disposal state。最初のDocker remove failure前に内部disposed flagがlatchされるため、同じcontainer instanceへの2回目の`DisposeAsync()`がno-opとなり、repository fixtureが未回収resourceのhandleを失い得る。

---

## 2. 3つの評価文書

| 文書 | 役割 | Verdict |
| --- | --- | --- |
| `implementation-evaluation.md` | ChatGPT独立Judge | REQUEST CHANGES / Major 1 |
| `implementation-evaluation-claude-opus-5.md` | Claude Opus 5独立Judge | APPROVE / Major 0 |
| `implementation-evaluation-synthesis.md` | **GPT + Claude統合Judge / canonical** | **REQUEST CHANGES / Major 1** |

元2文書は監査用として変更せず、統合Judgeを最終synthesisとして追加した。

---

## 3. なぜ元Scoreが大きく違ったか

採点基準は同じだったが、Goldが逆だった。

- Claude Judge: 「Major 0。APPROVEが正解」
- ChatGPT Judge: 「Major 1。REQUEST CHANGESが正解」

この差が`A.重大問題検出 /25`、`D.Severity /10`、`H.Verdict /5`の計40点へ波及し、同じreviewerでも30点以上の差が生じた。

---

## 4. 統合採点の考え方

8軸100点は維持し、Gold依存度で2つに分離した。

| Subscore | 配点 | 軸 | 統合方法 |
| --- | ---: | --- | --- |
| Review Quality | 60 | B + C + E + F + G | GPT JudgeとClaude Judgeの平均 |
| Gold Alignment | 40 | A + D + H | 最終Gold `Major 1 / REQUEST CHANGES`へ再評価 |

このため、レビュー内容そのものが深いReviewerはReview Qualityで高得点を維持しつつ、唯一のMajorを見逃した事実はGold Alignment側で明確に減点される。

例: Claude Opus 5 / Claude Code

```text
Review Quality:  59.25 / 60
Gold Alignment:   6.50 / 40
Final Score:      66.0 / 100
```

つまり66点は「レビュー能力が低い」という意味ではなく、**証拠品質はほぼ満点だが、今回の唯一のmerge blockerを見逃した**という意味である。

---

## 5. 最終統合ランキング

| Rank | Model + Harness | Quality /60 | Gold /40 | Final | Grade | 分 |
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

表示上4位と5位は57.0で同点だが、丸め前TotalはChatGPT Opus 5.6 Sol = 57.00、Grok 4.5 = 56.75のため、この順とした。

---

## 6. 両Judgeで一致した主な技術事項

- PostgreSQL 18.4 real container / PG category 7件 / skip 0は成立。
- digest-qualified image reference、database isolation、fallbackなし、scope管理は成立。
- `Image.FullName` / `Digest` assertionはdaemon-side evidenceではなくconfiguration referenceのparse結果。
- .NET 10現runtimeでは`lock(synchronizedWriter)`はconcurrent writeと相互排他になる。
- DeepSeek V4 Flash / MiniMax M3の`SyncTextWriter` findingは支持されない。
- ChatGPT o3 / BrowserはINCOMPLETE。

---

## 7. 統合Judgeでの使い分け

- **Formal merge-gate候補:** Claude Opus 5 / Claude Code、Claude Sonnet 5 / Claude Code、GPT-5.6 Sol / Codex。ただし今回3件ともGold Majorを見逃したため、dependency lifecycle監査を追加する。
- **高速一次review:** Grok 4.5 / Cursor、Composer 2.5 / Cursor。
- **specification / documentary review:** ChatGPT Opus 5.6 Sol / Browser、GPT-5.6 Terra / Codex、ChatGPT GPT 5.5 / Browser。
- **単独gate非推奨:** DeepSeek V4 Flash、MiniMax M3（framework semantics誤り）、ChatGPT o3（未完遂）。

---

## 8. 手続き上の注意

統合結果は**post-hoc adjudicated synthesis**である。ChatGPT側のTestcontainers Majorは最初のReference lock後の追加一次source突合で明確化されたため、このFinal Scoreを「元プロトコルどおり完全blindに固定されたGoldへのscore」とは扱わない。

技術的なmerge判断としては統合Goldを採用する。一方、厳密なblind benchmarkとして再確定する場合は、同じ修正前Headを別RUN_IDで新しいJudgeに評価させ、raw reviewerを読む前にMajor 1を含むReferenceを固定する。

---

## 9. 結論

最終的なGPT + Claude統合結果は、**REQUEST CHANGES / Major 1**。

Reviewer順位は、1位 Claude Opus 5 / Claude Code、2位 Claude Sonnet 5 / Claude Code、3位 GPT-5.6 Sol / Codex。

今後は単一Totalだけでなく、`Review Quality /60`と`Gold Alignment /40`を併記する。これにより、JudgeのGold判断差で絶対点が大きく動いても、「レビューそのものの深さ」と「今回の正解への一致度」を分けて読める。
