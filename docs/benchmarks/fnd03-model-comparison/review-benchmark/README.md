# FND-03 Final Synthesis Independent Review — Raw Capture

- Status: **RAW ARTIFACTS CAPTURED / ANALYSIS NOT STARTED**
- Benchmark ID: `fnd03-final-synthesis-independent-review`
- Run ID: `fnd03-final-91e3fca-20260809`
- Target Issue: #41
- Target PR: #104
- Base SHA: `7946cc55e49c0c6e21ad7b86c20a8435b4976269`
- Head SHA: `91e3fca181558cd1523390347f4f2f80d6014d26`
- Primary CI Run: `31277771209`

このディレクトリは、Final Synthesisに対して実行した17件の独立レビューをraw artifactとして固定する。

## Capture scope

- `reviews/*.md`: reviewerが提出したMarkdown review
- `reviews/*.json`: reviewerが提出したstructured result
- `run.json`: run identity、reviewer identity、実行時間
- `manifest.json`: artifactのbytes、SHA-256、capture status

この段階では以下を実施していない。

- Gold / Reference Review
- finding normalization
- TP / FP / FN
- reviewer scoring
- reviewer ranking
- `summary.md`
- `full-evaluation.md`

## Raw immutability

reviewerの意味内容、Finding、Severity、Verdict、Notesは変更していない。

combined captureから個別ファイルへ分割する際、transport envelopeである `=== BEGIN/END ... ===` と、artifact全体を囲っていたMarkdown code fenceだけを機械的に除去し、UTF-8 / LFで保存した。

実行時間はraw review本文ではなく`run.json`のrun metadataとして保存する。確定値として `gpt-5.6-luna-codex = 11分`、`minimax-m3-opencode = 36分` を使用している。

## Schema validation at capture

現行 `docs/benchmarks/schemas/review-result.schema.json` に対して、17 JSONのうち15件がvalid、2件がinvalidだった。raw artifact immutabilityを優先し、invalid JSONも意味内容を修正せず保存する。

- `deepseek-v4-pro-opencode.json`
  - schemaにない `ac_assessment` / `scope_drift` / `out_of_scope_detected` を含む
- `chatgpt-o3-browser.json`
  - `outcome: "incomplete"` が現行schemaのoutcome enum外

これらは`manifest.json`で `capture_status: "invalid"` と記録する。後続分析で補正値が必要な場合もraw artifact自体は上書きしない。
