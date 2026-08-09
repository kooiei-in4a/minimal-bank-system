# FND-03 Final Code Major Fix — Adjudicated Evaluation

Status: **CANONICAL / 3-JUDGE ADJUDICATED**

この文書は、[`judges/synthesis.md`](./judges/synthesis.md)を正本sourceとする読みやすいentry pointである。scoreの再計算やraw Judge結果の平均化は行っていない。

## Target

- Issue: #41
- Target PR: #104 Final Synthesis
- Common base: `91e3fca181558cd1523390347f4f2f80d6014d26`
- Candidate count: 14
- Exact Head CI: 14 / 14 SUCCESS
- Judge count: 3

## Judge identity

| Judge | Artifact identity |
| --- | --- |
| GPT-5.6 Sol / Codex / xHigh | `gpt-5.6-sol-codex/` |
| Claude Opus 5 / Claude Code / xhigh | `claude-opus-5-claude-code/` |
| GPT-5.6 Pro / Browser / Pro | `gpt-5.6-pro-browser/` |

3件目はユーザー上の呼称にかかわらず、artifact自身のself-reported identityを維持する。

## Final adjudicated ranking

| Rank | Model + Harness | Final score | Merge-ready |
| ---: | --- | ---: | :---: |
| 1 | **GPT-5.6 Sol / Codex** | **94** | **YES** |
| 2 | Claude Opus 5 / Claude Code | 80 | NO |
| 3 | GPT-5.6 Luna / Codex | 77 | NO |
| 4 | GPT-5.6 Terra / Codex | 77 | NO |
| 5 | GPT-5.6 Luna / Open Code | 76 | NO |
| 6 | DeepSeek V4 Flash / Open Code | 74 | NO |
| 7 | Grok 4.5 / Cursor | 73 | NO |
| 8 | Claude Sonnet 5 / Claude Code | 67 | NO |
| 9 | DeepSeek V4 Pro / Open Code | 62 | NO |
| 10 | Composer 2.5 / Cursor | 58 | NO |
| 11 | MiniMax M3 / Open Code | 48 | NO |
| 12 | Qwen3.7 Plus / Open Code | 42 | NO |
| 13 | MiMo-V2.5-Pro / Open Code | 38 | NO |
| 14 | MiMo-V2.5 / Open Code | 34 | NO |

## Adjudicated decision

```text
Final merge-ready: 1 / 14

PR #108:
  production architecture base

PR #113:
  actual Testcontainers latch / second-no-op testをtest-onlyで統合

Base:
  unreachable-Docker regressionを維持

PR #109:
  transport-fault proofはoptional / test-only
```

G-01のpartial-create pathは、Docker create成功後・inspect結果格納前に失敗すると実resourceが存在してもcandidate IDが取得できず、native Disposeがno-opになり得るという問題である。PR #108のcreate前ownership labelと、native結果に依存しないlabel query / remove / re-queryがこのpathを構造的に閉じるため、唯一merge-readyと裁定された。

## Provenance

- [`run.json`](./run.json): 14候補のcanonical registry
- [`judges/manifest.json`](./judges/manifest.json): Judge raw source hash、bytes、lines、part ordering
- [`judges/synthesis.md`](./judges/synthesis.md): full adjudication source
