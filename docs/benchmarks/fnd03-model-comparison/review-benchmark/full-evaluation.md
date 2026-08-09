# FND-03 Independent Review Benchmark — Full Evaluation

Status: **CANONICAL / POST-HOC ADJUDICATED**

この文書は、既存のJudge evaluation・synthesis・summaryをCollector形式のentry pointへ整理したものである。新しい採点は計算していない。

## Target identity

| Field | Value |
| --- | --- |
| Repository | `kooiei-in4a/minimal-bank-system` |
| Issue | #41 |
| PR | #104 |
| Base SHA | `7946cc55e49c0c6e21ad7b86c20a8435b4976269` |
| Head SHA | `91e3fca181558cd1523390347f4f2f80d6014d26` |
| Primary CI | `31277771209` |
| Raw review pairs | 17 Markdown + 17 JSON |
| Raw JSON schema status | 15 valid / 2 invalid but preserved |

## Artifact roles

- [`manifest.json`](./manifest.json): raw capture integrity manifest。raw artifactのbytes / SHA-256 / capture statusを保持する。
- [`collector-results.json`](./collector-results.json): post-hoc Collector scoreとblocking-Gold alignmentの機械可読結果。
- [`gold-review.md`](./gold-review.md) / [`gold-review.json`](./gold-review.json): protocol-compatible Gold。
- 本書: 人間向けcanonical aggregate evaluation。

raw capture integrityと後付けadjudication結果を同じmanifestへ混ぜず、責務を分離する。

## Gold and methodology

Final Gold: `REQUEST CHANGES / NOT MERGE READY`、Blocker 0 / Major 1 / Minor 1。root causeは[`gold-review.md`](./gold-review.md)のG-01 / G-02を使用する。

- **G-01 — Major / blocking:** Testcontainers 4.13.0のdispose state latchとsame-instance retry no-opによるcleanup ownership loss。
- **G-02 — Minor / non-blocking:** digest assertionのdaemon-side evidence不足。

`post_hoc_adjudication: true`。Gold Majorは最初のReference lock後に追加一次source突合で明確化されたため、完全blindな事前locked Goldの結果として解釈しない。

## Blocking-Gold TP / FP / FN scope

本benchmarkのTP / FN表は、**merge-blocking root cause G-01の検出性能**を表す。G-02はnon-blocking MinorとしてGoldに保持するが、このTP / FN denominatorには含めない。

理由:

- protocol上、merge-blocking root causeの検出がmerge verdict accuracyを左右する。
- G-02を同じTP/FN母数へ混ぜると、「merge blocker検出」と「non-blocking evidence weakness検出」を同じ指標で表現することになる。
- G-02への到達度は既存JudgeのEvidence / Severity / Review Quality score側で評価済みであり、このarchiveで再採点しない。

全17 reviewerについて、G-01を実質検出したreviewerは0件、unsupported blocking FPは0件、G-01未検出のFNは各1件である。`chatgpt-o3-browser`はINCOMPLETE / no_resultだがbenchmark結果から除外しない。

| Reviewer | Outcome / Verdict | TP(G-01) | Blocking FP | FN(G-01) |
| --- | --- | ---: | ---: | ---: |
| Claude Opus 5 / Claude Code | completed / APPROVE | 0 | 0 | 1 |
| Claude Sonnet 5 / Claude Code | completed / APPROVE | 0 | 0 | 1 |
| GPT-5.6 Sol / Codex | completed / APPROVE | 0 | 0 | 1 |
| ChatGPT Opus 5.6 Sol / Browser | completed / APPROVE | 0 | 0 | 1 |
| Grok 4.5 / Cursor | completed / APPROVE | 0 | 0 | 1 |
| GPT-5.6 Terra / Codex | completed / APPROVE | 0 | 0 | 1 |
| DeepSeek V4 Pro / Open Code | completed / APPROVE | 0 | 0 | 1 |
| ChatGPT GPT 5.5 / Browser | completed / APPROVE | 0 | 0 | 1 |
| Composer 2.5 / Cursor | completed / APPROVE | 0 | 0 | 1 |
| MiMo-V2.5-Pro / Open Code | completed / APPROVE | 0 | 0 | 1 |
| GPT-5.6 Luna / Codex | completed / APPROVE | 0 | 0 | 1 |
| MiMo-V2.5 / Open Code | completed / APPROVE | 0 | 0 | 1 |
| Qwen3.7 Plus / Open Code | completed / APPROVE | 0 | 0 | 1 |
| GPT-5.6 Luna / Open Code | completed / APPROVE | 0 | 0 | 1 |
| MiniMax M3 / Open Code | completed / APPROVE | 0 | 0 | 1 |
| DeepSeek V4 Flash / Open Code | completed / APPROVE | 0 | 0 | 1 |
| ChatGPT o3 / Browser | no_result / INCOMPLETE | 0 | 0 | 1 |

機械可読版は[`collector-results.json`](./collector-results.json)を参照する。

## Review Quality / Gold Alignment / Final score

既存`implementation-evaluation-synthesis.md`のscore semanticsを維持する。Review Qualityは60点、Gold Alignmentは40点で、単純平均や新規補正は行っていない。

| Rank | Model + Harness | Review Quality /60 | Gold Alignment /40 | Final score | Grade |
| ---: | --- | ---: | ---: | ---: | :---: |
| 1 | Claude Opus 5 / Claude Code | 59.25 | 6.5 | **66.0** | C |
| 2 | Claude Sonnet 5 / Claude Code | 57.75 | 7.5 | **65.5** | C |
| 3 | GPT-5.6 Sol / Codex | 57.75 | 2.5 | **60.5** | D |
| 4 | ChatGPT Opus 5.6 Sol / Browser | 54.50 | 2.5 | **57.0** | D |
| 5 | Grok 4.5 / Cursor | 54.25 | 2.5 | **57.0** | D |
| 6 | GPT-5.6 Terra / Codex | 53.75 | 2.5 | **56.5** | D |
| 7 | DeepSeek V4 Pro / Open Code | 52.75 | 2.5 | **55.5** | D |
| 8 | ChatGPT GPT 5.5 / Browser | 51.50 | 2.5 | **54.0** | D |
| 9 | Composer 2.5 / Cursor | 49.25 | 1.5 | **51.0** | D |
| 10 | MiMo-V2.5-Pro / Open Code | 48.75 | 1.5 | **50.5** | D |
| 11 | GPT-5.6 Luna / Codex | 47.75 | 1.5 | **49.5** | F |
| 12 | MiMo-V2.5 / Open Code | 45.25 | 1.0 | **46.5** | F |
| 13 | Qwen3.7 Plus / Open Code | 45.00 | 1.0 | **46.0** | F |
| 14 | GPT-5.6 Luna / Open Code | 42.00 | 1.0 | **43.0** | F |
| 15 | MiniMax M3 / Open Code | 38.25 | 0.5 | **39.0** | F |
| 16 | DeepSeek V4 Flash / Open Code | 36.50 | 0.5 | **37.0** | F |
| 17 | ChatGPT o3 / Browser | 17.00 | 0.0 | **17.0** | F |

Final scoreは既存post-hoc synthesisの値をそのまま使用している。このarchiveでは再計算していない。

## Interpretation

- 1位 Claude Opus 5 / Claude Code: 66.0。Review Quality 59.25 / 60で最高。
- 2位 Claude Sonnet 5 / Claude Code: 65.5。G-01そのものは未検出だが、container dispose failure path未検証へ最も近く到達した。
- 3位 GPT-5.6 Sol / Codex: 60.5。
- Final Gold Major G-01を検出したreviewerは0件だが、これは各reviewerのEvidence / Scope / CI分析能力まで否定するものではない。
- `green CI != failure-path correctness`。正常系CIはG-01の反証にならない。
- G-02はClaude Opus等がevidence weaknessとして捉えており、Review Quality / severity評価へ反映済みである。

## Historical Judge relationship

既存Judgeは同一Goldを前提としていなかったため、最終scoreはJudge scoreの単純平均ではない。

- ChatGPT Judge: `REQUEST CHANGES / Major 1`
- Claude Judge: `APPROVE / Major 0`
- Final synthesis: 一次sourceでTestcontainers semanticsを裁定し、`REQUEST CHANGES / Major 1 / Minor 1`をtechnical Goldとした。

詳細な軸別score、Judge間差、tie-breakは[`implementation-evaluation-synthesis.md`](./implementation-evaluation-synthesis.md)を正本sourceとして参照する。

## Source documents

- [`implementation-evaluation.md`](./implementation-evaluation.md)
- [`implementation-evaluation-claude-opus-5.md`](./implementation-evaluation-claude-opus-5.md)
- [`implementation-evaluation-synthesis.md`](./implementation-evaluation-synthesis.md)
- [`summary.md`](./summary.md)
- [`manifest.json`](./manifest.json)
- [`collector-results.json`](./collector-results.json)
- [`gold-review.md`](./gold-review.md)
- [`gold-review.json`](./gold-review.json)
