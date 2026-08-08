# Independent Review Benchmark Artifact Protocol

- Status: Active
- Applies from: FND-03 onward
- Parent methodology: `model-implementation-benchmark-methodology.md`
- Related archive policy: `archive-conventions.md`

## 1. Purpose

同一のFinal synthesis / target Headへ複数のModel + Agent/Harnessで独立レビューを実行した結果を、手動転記に依存せず、再現可能かつ機械集計可能なbenchmark artifactとして保存する。

このProtocolは通常のAgent Bレビューを置き換えない。正式なmerge gateとなるAgent Bレビューは対象PRへ記録し、benchmark用の多数レビューは`docs/benchmarks/`へ分離して保存する。

## 2. Standard artifact set

Issue別の独立レビューbenchmarkは次を標準とする。

```text
docs/benchmarks/<issue-slug>-model-comparison/review-benchmark/
├── README.md
├── run.json
├── gold-review.md
├── gold-review.json
├── manifest.json
├── summary.md
├── full-evaluation.md
└── reviews/
    ├── <reviewer-slug>.md
    ├── <reviewer-slug>.json
    └── ...
```

役割:

- `reviews/*.md`: 各モデルが提出した人間向けのraw review。一次成果物。
- `reviews/*.json`: 同一reviewの機械集計用structured result。
- `gold-review.md`: Reference / Gold Reviewの人間向け根拠。
- `gold-review.json`: root cause、Severity、blocking判定の正規データ。
- `run.json`: target Issue / PR / Base SHA / Head SHA / prompt revision / reviewer集合等のrun identity。
- `manifest.json`: 全artifactのpath、bytes、SHA-256、attempt、status、score等の索引。
- `summary.md`: 共有用の短い結論。
- `full-evaluation.md`: scoring、TP/FP/FN、finding normalization、用途別比較を含む最終評価。

## 3. Separation from formal Agent B review

- **Formal Agent B review**: Final synthesis PRへ記録する。merge gateの正本。
- **Benchmark reviews**: 原則としてPRへ17件等を大量投稿しない。benchmark artifactとして保存する。
- benchmark scoreや多数決はFormal Agent B approvalの代替にしない。
- Final synthesis PRのconversationをbenchmark raw dataで埋めない。

## 4. Capture contract per reviewer

各review attemptは、同一内容についてMarkdownとJSONを1組で保存する。

命名:

```text
reviews/<reviewer-slug>.md
reviews/<reviewer-slug>.json
```

再実行が必要な場合は上書きせず、attempt suffixを付ける。

```text
reviews/<reviewer-slug>-attempt-2.md
reviews/<reviewer-slug>-attempt-2.json
```

`reviewer-slug`はrun内で一意かつ安定した`model + harness`表記とする。

## 5. Required structured fields

`reviews/*.json`は少なくとも次を持つ。

- schema version
- benchmark ID / run ID
- target Issue / PR / Base SHA / Head SHA
- model / harness / effort
- attempt
- outcome
- target verification結果
- verdict
- severity counts
- findings
- CI verification
- local verificationの有無
- completion timestampまたは記録時刻

`outcome`は原則として次から選ぶ。

- `completed`
- `failed`
- `stopped`
- `wrong_target`
- `no_result`

レビュー未完了やtarget誤認もbenchmark結果であり、成功したreviewだけを残す運用にしない。

## 6. Finding representation

各findingは少なくとも次を持つ。

- reviewer-local finding ID
- severity
- blocking
- title
- concise description
- evidence references
- affected path / component（取得可能な場合）
- proposed root-cause key（未確定ならnull）

SeverityはFormal Reviewと同じ語彙を使用する。

- Blocker
- Major
- Minor
- Nit

改善提案だけをblocking findingへ昇格させない。

## 7. Target identity gate

レビュー開始時に次を固定する。

```text
Repository
Target Issue
Target PR
Base SHA
Head SHA
CI target SHA
```

structured resultにはtarget verification結果を必ず記録する。

指定Headを取得できない、別branch/baseを見ている、CI SHAが一致しない場合は、内容レビューを継続してもbenchmark上は`wrong_target`または明示的なfailureとして扱う。

## 8. Raw artifact immutability

Collectorがraw reviewを受領した後、内容を読みやすくする目的で書き換えない。

許可する変更:

- ファイル名の標準化
- line endingの統一が必要な場合の明示的・一括変換
- structured JSONのschema validationに必要な機械的整形

意味内容を変更する修正は行わない。

誤りを訂正する必要がある場合は旧artifactを保持し、新attemptとして追加する。

## 9. Integrity manifest

`manifest.json`には各artifactについて最低限次を記録する。

- path
- artifact type
- reviewer slug / attempt（該当時）
- bytes
- SHA-256
- capture status

これにより、外部Harnessから回収したreviewとrepositoryへ保存したreviewが同一であることを検証可能にする。

Git commit SHAも履歴証拠になるが、collector前後の改変検知用としてartifact hashを別に保持する。

## 10. Gold Review isolation

Gold Reviewはbenchmark reviewerの出力と分離する。

ルール:

1. benchmark reviewerへGold Reviewを見せない。
2. 他reviewerのfinding / score / verdictも見せない。
3. Gold Reviewは一次証拠から独立に作成する。
4. reviewer raw artifactsを固定した後に公開・集計へ利用する。
5. Gold Reviewを後から変更した場合はrevisionと理由を記録し、全reviewerへ同一基準を再適用する。

`gold-review.json`ではmerge-blocking root causeに安定したIDを割り当てる。

例:

```text
G-01
G-02
G-03
```

## 11. Finding normalization and scoring

Collector / Benchmark Judgeはreviewerのfinding文言ではなくroot cause単位でGold Reviewと照合する。

- TP: Gold root causeを実質的に検出
- FN: Gold root causeを未検出
- FP: 正本・コード・runtime証拠で支持されないblocking finding
- Severity差: root cause検出自体とは分けてSeverity軸で評価

同じroot causeを複数findingへ分割してもTPを水増ししない。

単に`REQUEST CHANGES`が一致しただけで高得点にしない。根拠が誤っていればAccuracy / Evidence / Signal-to-Noiseで減点する。

## 12. Collector responsibilities

Collectorは各モデルのreviewを生成しない。回収・検証・正規化・集計だけを行う。

標準手順:

1. expected reviewer一覧をrun identityから取得
2. Markdown / JSON pairの存在確認
3. JSON Schema validation
4. target identity確認
5. bytes / SHA-256計算
6. completed / failed / stopped / wrong_target分類
7. Gold root causeとのfinding normalization
8. TP / FP / FN算出
9. scoring算出
10. `manifest.json`生成
11. `full-evaluation.md`生成
12. `summary.md`生成
13. benchmark results branchへ格納
14. Benchmark Results PR作成

Collectorはraw Markdownを手動で一つの巨大ファイルへコピーすることを前提にしない。

## 13. Publication workflow

reviewer自身にbenchmark結果branchへのcommitを要求しない。

推奨flow:

```text
Reviewer execution
  -> raw .md + structured .json
  -> temporary collection area
  -> Collector validation
  -> docs/benchmarks/<issue>/review-benchmark/
  -> agent/<issue-slug>-benchmark-results
  -> Benchmark Results PR
  -> main
```

これによりReviewerとCollectorの責任を分離し、独立レビュー中のrepository mutationを避ける。

## 14. FND-03 adoption

FND-03では本Protocolを初回正式適用する。

予定path:

```text
docs/benchmarks/fnd03-model-comparison/review-benchmark/
```

FND-02の`review-benchmark/raw-results.md`は移行前のhistorical artifactとして維持する。FND-03ではモデル別raw Markdown + structured JSONへ分割し、手動の巨大Markdown集約を標準手順にしない。

## 15. Schemas and template

共通schema / template:

- `schemas/review-result.schema.json`
- `schemas/gold-review.schema.json`
- `schemas/review-benchmark-manifest.schema.json`
- `templates/review-result-template.md`

schema revisionを変更する場合、run identityへ使用schema versionを記録する。
