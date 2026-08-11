# FND-04 Independent Review — Reviewer Pool Revision 2

Status: **LOCKED BEFORE REVIEWER EXECUTION**

```yaml
BENCHMARK_ID: fnd04-final-synthesis-independent-review
RUN_ID: fnd04-final-review-20260810
POOL_REVISION: fnd04-reviewer-pool-v2
TARGET_PR: 140
HEAD_SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
REVISED_AT: 2026-08-10T12:20:00+09:00
RAW_CAPTURE_AT_REVISION: 0
```

## Reason for revision

Reviewer execution開始前に、review品質と実運用コストのバランスを再検討した。

旧poolは6枠だったが、raw reviewが1件も開始されていないため、比較結果を汚さずpoolだけを改訂する。

方針:

- Claude Opus 5は高コストでもdeep technical / test assuranceのピンポイント用途として維持する。
- Claude Sonnet 5はreview poolから外す。specification / scope枠はGPT-5.6 Luna / Codexへ置換する。
- Open Codeはこのreview phaseでは使用しない。
- Grok 4.5固定ではなくCursor標準のAuto modeを実務運用枠として使用する。
- 6枠を維持するためだけの冗長reviewer追加は行わず、5枠へ削減する。

## Revised reviewer pool

| Slot | Model / Harness | Primary role |
| --- | --- | --- |
| R1 | GPT-5.6 Sol / Codex | runtime / failure-path |
| R2 | Claude Opus 5 / Claude Code | deep technical / test assurance |
| R3 | GPT-5.6 Luna / Codex | specification / scope |
| R4 | ChatGPT Opus 5.6 Sol / Browser | framework / official-source cross-check |
| R5 | Cursor Auto / Cursor | fast independent review / practical broad scan |

## Interpretation

R3と旧R5を統合する形で、GPT-5.6 LunaはCodexからspecification / scopeを担当する。

これによりOpen Code harness比較は今回のFinal Synthesis review benchmarkの目的から外れる。過去candidate実装benchmarkのOpen Code結果は変更しない。

R5 Cursor Autoは特定modelの性能測定ではなく、**Cursorの標準Auto routingを使った実務review execution**として扱う。

- reviewer model fieldは原則`Cursor Auto`とする。
- product UI等で実際のrouted modelが明示される場合は追加記録する。
- routed modelが表示されない場合は推測せず`NOT_EXPOSED`とする。
- Cursor Autoの結果をGrok 4.5単体の結果として扱わない。

## Independence / scoring impact

- Target PR / Base / Headは変更しない。
- Common review prompt `fnd04-final-review-v1`も変更しない。
- Goldは未公開。
- raw reviewは0件のため、旧poolでのexecution artifactは存在しない。
- revised runは5/5 raw captureをcompletion conditionとする。

このrevisionはreviewer構成だけを変更し、Final Synthesis snapshot、Implementation Evaluation、Selection / Adjudication、candidate rankingを変更しない。
