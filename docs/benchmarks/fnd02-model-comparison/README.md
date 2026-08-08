# FND-02 Model Comparison Results

Issue #40 `[FND-02] 共通API実行契約を確立する` で実施した、AIモデル実装比較および独立第三者レビュー性能比較の成果物を保存します。

## Documents

- `analysis.md`
  - 14 candidateのarchive manifest。candidate Head / tag / PR / CIの再現用索引。
- `implementation-evaluation.md`
  - 14 Model + Agent/HarnessによるFND-02実装比較・採点・Final synthesis recommendation。
- `review-benchmark/full-evaluation.md`
  - PR #83のbenchmark対象Headに対する17 Model + Agent/Harnessの独立レビュー性能評価。Reference / Gold Review、100点評価、TP/FP/FN、用途別ランキングを含む。
- `review-benchmark/summary.md`
  - 独立レビュー性能評価の共有用要約。
- `review-benchmark/raw-results.md`
  - 各モデルへ同一レビュー依頼を投入した際の提出結果をまとめた比較資料。

## Snapshot scope

レビュー性能benchmarkは、PR #83の当時のHead `2306c634abc40b4e5330c9492c8bcee8c0d6a5cc` を対象にした**歴史的benchmark snapshot**です。

その後、指摘事項はPR #83で修正され、最終Head `d987733d1a606b21c971860565c687e4ba47ff8a` がAgent B再レビューを通過してmainへmergeされ、Issue #40はCompletedとなっています。

したがって、`review-benchmark/` 配下のREQUEST CHANGESやfindingは、現在のmainに未解消問題が残っていることを意味しません。モデルのレビュー能力を比較した当時の一次記録として扱います。

## Methodology

- `../model-implementation-benchmark-methodology.md`
- `../archive-conventions.md`
