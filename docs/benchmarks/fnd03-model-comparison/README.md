# FND-03 Model Comparison

Target Issue: #41 `[FND-03] 実PostgreSQL integration test基盤を確立する`

Status: **PREPARED / NOT STARTED**

FND-03ではFND-02と同様に、同一common baseから複数のModel + Agent/Harnessへ独立実装させ、candidate比較、Final synthesis、独立レビューbenchmarkを実施する。

## Governing documents

- `../model-implementation-benchmark-methodology.md`
- `../archive-conventions.md`
- `../independent-review-benchmark-protocol.md`

## Planned artifacts

```text
fnd03-model-comparison/
├── README.md
├── analysis.md
├── implementation-evaluation.md
└── review-benchmark/
    ├── README.md
    ├── run.json
    ├── gold-review.md
    ├── gold-review.json
    ├── manifest.json
    ├── summary.md
    ├── full-evaluation.md
    └── reviews/
        ├── <reviewer-slug>.md
        └── <reviewer-slug>.json
```

`analysis.md`以降はbenchmark実行時に生成する。空の結果ファイルを事前作成しない。

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

Preparatory protocol PR merge後の`main` full SHAをbenchmark開始時に固定し、全candidateへ同一に使用する。

## Independent review artifact rule

FND-03から、benchmark reviewerごとにraw Markdownとstructured JSONを1組で保存する。

Formal Agent B reviewはFinal synthesis PRへ記録し、benchmark用の複数reviewは`review-benchmark/reviews/`へ保存する。

Gold Reviewはbenchmark reviewerへ非公開とし、raw review固定後にCollectorがfinding normalizationとscoringへ使用する。
