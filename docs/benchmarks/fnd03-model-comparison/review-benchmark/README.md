# FND-03 Final Synthesis Independent Review Benchmark

- Status: **COMPLETE / POST-HOC ADJUDICATED**
- Benchmark ID: `fnd03-final-synthesis-independent-review`
- Run ID: `fnd03-final-91e3fca-20260809`
- Target Issue: #41
- Target PR: #104
- Base SHA: `7946cc55e49c0c6e21ad7b86c20a8435b4976269`
- Head SHA: `91e3fca181558cd1523390347f4f2f80d6014d26`
- Primary CI Run: `31277771209`
- Raw reviewers: 17 Markdown / 17 JSON

このディレクトリは、Final Synthesisに対する17件の独立reviewと、その後のGold adjudication・canonical集計を保存する。

## Canonical outputs

- [`run.json`](./run.json): 元のrun identity。raw capture metadataとして保持
- [`manifest.json`](./manifest.json): **raw capture integrity manifest**。raw artifactのbytes、SHA-256、capture statusを保持し、後付けscoreやGold alignmentは混ぜない
- [`collector-results.json`](./collector-results.json): post-hoc Collector結果。final scoreとblocking Gold G-01に対するTP / FP / FNを機械可読化
- [`gold-review.md`](./gold-review.md): protocol-compatible Gold Review
- [`gold-review.json`](./gold-review.json): Gold root causeの正規データ
- [`full-evaluation.md`](./full-evaluation.md): Collector形式の全体評価
- [`summary.md`](./summary.md): source branchのhistorical summary
- [`implementation-evaluation.md`](./implementation-evaluation.md): source branchのhistorical Judge evaluation
- [`implementation-evaluation-claude-opus-5.md`](./implementation-evaluation-claude-opus-5.md): source branchのhistorical Claude Judge evaluation
- [`implementation-evaluation-synthesis.md`](./implementation-evaluation-synthesis.md): source branchのhistorical synthesis

## Target and verdict

最終technical Goldは次のとおりである。

```text
REQUEST CHANGES / NOT MERGE READY
Blocker: 0
Major:   1
Minor:   1
Nit:     0
```

主要Majorは、Testcontainers .NET 4.13.0がDocker resource removal完了前にdisposed stateをlatchし、同じfailed instanceへの2回目の`DisposeAsync()`がno-op成功になり得るため、actual containerのdeterministic ownerを失う問題である。

## Raw artifact policy

- `reviews/*.md` と `reviews/*.json` はraw artifactとして内容を変更していない。
- `run.json`とsource branchのhistorical Judge documentsも意味内容を変更していない。
- `deepseek-v4-pro-opencode.json` は既知のextra fieldsにより schema invalid のまま保存する。
- `chatgpt-o3-browser.json` は `outcome = incomplete` が現行schema enum外のため invalid のまま保存する。
- raw artifactのcapture statusとschema deviationは[`manifest.json`](./manifest.json)で確認できる。
- archive段階で追加したGold / Full Evaluation / score alignmentはraw integrity manifestへ遡及混入せず、`gold-review.*` / `full-evaluation.md` / `collector-results.json`へ分離する。

## TP / FP / FN scope

`collector-results.json`と`full-evaluation.md`のTP / FNは、**merge-blocking Gold root cause G-01のみ**を母数とする。G-02はnon-blocking MinorとしてGold Reviewへ保持し、evidence / severity qualityには反映されるが、このTP / FN denominatorには含めない。

これにより「Gold root cause全件のTP/FN」と「merge blocker検出性能」を混同しない。

## Methodology caveat

このGoldは完全blindな事前locked Goldではない。最初のReference lock後、追加のTestcontainers 4.13.0一次source突合によりMajorを明確化した `post_hoc_adjudication` である。したがって、reviewer raw outputを事前に完全固定したblind benchmarkの純粋なGold scoreとは区別する。
