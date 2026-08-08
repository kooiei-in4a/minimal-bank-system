# FND-03 Model Comparison

Target Issue: #41 `[FND-03] 実PostgreSQL integration test基盤を確立する`

Status: **IMPLEMENTATION BENCHMARK EVALUATED**

FND-03では、同一common baseから複数のModel + Agent/Harnessへ独立実装させ、GitHub上の実コード・テスト・CIを一次証拠として比較評価する。

## 実装比較結果

まず全体像を確認する場合は要約版、採点根拠や設計差まで確認する場合は最終評価を参照する。

- [`summary.md`](./summary.md)
  - **Issue #41 FND-03 — AIコーディングモデル実装比較・要約**
  - 総合ランキング、主要な差、品質と速度、実装方式、Final synthesisへの推奨を短く整理
- [`implementation-evaluation.md`](./implementation-evaluation.md)
  - **Issue #41 FND-03 — AIコーディングモデル実装比較・最終評価**
  - 13 Model + Agent/Harnessの採点、カテゴリ別評価、Acceptance Criteria、設計比較、処理効率、CI証跡を収録

実装比較は14候補で開始し、13候補を採点した。MiniMax M3 / Open Codeは `stopped / no-change` のためランキング対象外としている。

## Governing documents

- `../model-implementation-benchmark-methodology.md`
- `../archive-conventions.md`
- `../independent-review-benchmark-protocol.md`

## FND-03 specific review focus

Issue #41の正本に従い、少なくとも次を比較・レビュー対象とする。

- 実PostgreSQL 18を確実に使用しているか
- container imageがdigest固定されているか
- lifecycle / isolation / cleanupが再現可能か
- 複数testがshared stateで干渉しないか
- parallel policyが明示され実証されているか
- cleanup failureやcontainer起動失敗を成功扱いしないか
- CIで同じ実PostgreSQL integration testを実行するか
- InMemory / SQLiteへprovider固有検証を逃がしていないか
- DbContext / migration / business schemaを先取りしていないか

## Common base

Implementation benchmark common base:

```text
95a8e50e6b68025e3386fdd0672bd73bcbaa60a0
```

## Independent review artifact rule

FND-03から、benchmark reviewerごとにraw Markdownとstructured JSONを1組で保存する。

Formal Agent B reviewはFinal synthesis PRへ記録し、benchmark用の複数reviewは`review-benchmark/reviews/`へ保存する。

Gold Reviewはbenchmark reviewerへ非公開とし、raw review固定後にCollectorがfinding normalizationとscoringへ使用する。
