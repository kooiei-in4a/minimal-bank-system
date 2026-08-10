# FND-04 Final Synthesis Independent Review Benchmark

Status: **READY FOR REVIEWER EXECUTION / RAW RESULTS NOT STARTED**

```yaml
BENCHMARK_ID: fnd04-final-synthesis-independent-review
RUN_ID: fnd04-final-review-20260810
POOL_REVISION: fnd04-reviewer-pool-v2
PROMPT_REVISION: fnd04-final-review-v1
TARGET_PR: 140
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
HEAD_SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
RAW_CAPTURE: 0 / 5
```

このdirectoryは、FND-04 Final Synthesisのrole-diverse independent review raw artifactと後続adjudicationを管理する。

## Review target

- Real target only: PR #140 / Head `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- Final Synthesis snapshot: `../results/final-synthesis-snapshot.md`
- Common reviewer prompt: `../prompts/final-synthesis-independent-review.md`

Reviewerへsnapshot / evaluation / selection結果を答えとして見せない。reviewerはIssue #42、authority、実diff、test、CIから独立に結論を出す。

## Reviewer pool — Revision 2

Reviewer execution開始前、raw capture 0件の時点でpoolを6枠から5枠へ改訂した。改訂理由と旧poolとの差分は`reviewer-pool-revision-2.md`を正本とする。

| Slot | Expected Model + Harness | Primary role | Effort |
| --- | --- | --- | --- |
| R1 | GPT-5.6 Sol / Codex | runtime / failure-path | reverify at execution |
| R2 | Claude Opus 5 / Claude Code | deep technical / test assurance | reverify at execution |
| R3 | GPT-5.6 Luna / Codex | specification / scope | reverify at execution |
| R4 | ChatGPT Opus 5.6 Sol / Browser | framework / official-source cross-check | reverify at execution |
| R5 | Cursor Auto / Cursor | fast independent review / practical broad scan | Auto |

方針:

- Claude Opus 5は高コストでもdeep technical / test assuranceへピンポイント投入する。
- Claude Sonnet 5はreview poolでは使用しない。
- Open Codeはこのreview phaseでは使用しない。
- R3はGPT-5.6 Luna / Codexへ変更する。
- R5はGrok 4.5固定ではなくCursor標準Auto modeを使用する。
- 6枠維持のための冗長なreplacementは追加せず、5 reviewerで完了とする。

product-visible identity / effortは各実行直前に確認し、実際の値をraw resultへ記録する。silent substitutionは禁止。

Cursor Autoについては特定modelのreviewとして扱わない。routed modelがproduct上で明示される場合だけ追加記録し、表示されない場合は推測せず`NOT_EXPOSED`とする。

## Independence rules

- reviewerはcandidate ranking / scoreを読まない
- Implementation Evaluation / Selection-Adjudicationを読まない
- 他reviewerの結果を読まない
- Gold / Judge結果を読まない
- review中にrepositoryを変更しない
- PRへ大量のbenchmark reviewを投稿しない

## Raw capture

各reviewerについて次をCollectorが保存する。

```text
reviews/<reviewer-slug>.md
reviews/<reviewer-slug>.json
```

reviewer自身にはbenchmark control branchへのcommitを要求しない。

再実行時は上書きせず`-attempt-2` suffixを使用する。

Completion conditionは**5 / 5 raw pair captured**とする。

## CI identity note

Known CI run `31350916189` is successful for the PR evaluation state. The checkout log uses PR merge ref `d12de2ae07003a10d19d576808cf88ec7796da23`, representing Head `99cee...` merged into exact Base `38c07...`.

Reviewers must not silently relabel this as a direct branch-Head checkout run. A separately resolved direct-head push run may be recorded if independently verified.

## Gold isolation

Gold / Reference Review content is **not published in this reviewer-visible directory before raw capture**.

The current coordinator Final Synthesis snapshot is only a target/gate record and is not reviewer Gold. Review scoring against normalized root causes is performed only after raw review artifacts are fixed.

## Controlled Mutant

Not started for this run. The current phase reviews the real Final Synthesis only.

A Controlled Mutant, if later authorized, must use a separate target / run identity and must not contaminate product branch or this real-target raw capture.

## Next steps

1. Execute R1-R5 independently against exact Head.
2. Capture each Markdown + JSON raw pair without semantic editing.
3. Verify target identity and artifact integrity.
4. Only after raw capture, establish/publish adjudicated Reference / Gold for scoring.
5. Normalize TP / FP / FN and severity accuracy.
6. Run Judge A / Judge B; add Judge C only if quorum conditions require it.
7. Formal Agent B review remains the actual product merge gate.
