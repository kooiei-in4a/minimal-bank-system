# Benchmarks

このdirectoryは、AIモデル比較実験の方法論、Issue別report、独立レビュー成果物、archive運用規約を管理します。

## Primary documents

- `model-implementation-benchmark-methodology.md`
  - benchmark全体の共通方法論
  - candidate条件、採点、Final synthesis、archiveの基本方針
- `independent-review-benchmark-protocol.md`
  - 複数Model + Agent/Harnessによる独立レビューbenchmarkの成果物標準
  - raw Markdown + structured JSON + Gold Review + manifest + aggregate report
  - FND-03から正式適用
- `archive-conventions.md`
  - candidate archive時の命名・配置・実行順序
  - tag、archive branch、report path、archive PR titleの標準形

## Schemas / templates

- `schemas/review-result.schema.json`
- `schemas/gold-review.schema.json`
- `schemas/review-benchmark-manifest.schema.json`
- `templates/review-result-template.md`
- `templates/review-benchmark-prompt-preamble.md`
  - 他reviewer / Gold Reviewからの情報漏れを防ぎ、target SHA確認を強制する共通preamble

## Issue-specific reports

- `fnd01-model-comparison/analysis.md`
- `fnd02-model-comparison/README.md`
  - archive manifest
  - 14モデル実装比較
  - 17モデル独立第三者レビュー性能比較
  - 各モデルのレビュー結果
- `fnd03-model-comparison/README.md`
  - FND-03 benchmarkの事前scaffold
  - 新しい独立レビューartifact protocolを初回適用

新しいimplementation benchmarkを開始する場合は`model-implementation-benchmark-methodology.md`を確認する。
独立レビューbenchmarkを行う場合は`independent-review-benchmark-protocol.md`と共通preambleを併用する。
archive時は`archive-conventions.md`に従う。
