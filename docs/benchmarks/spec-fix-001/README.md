# spec-fix-001

LLM仕様レビューの最終審理済みFindingに対し、複数LLMを同一条件で修正させるポータブル修正ベンチマークである。

## Dataset

- Benchmark ID: `spec-fix-001`
- Input dataset ID: `spec-fix-001-portable-v1`
- Fixed Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Fixed Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Target: `docs/specs/bank-system-specification.md`
- Language: Japanese Markdown
- Benchmark execution status: **NOT RUN**

## 実行モデルへ渡すファイル

次の2件だけを配布する。

```text
fix-prompt.md
fix-evidence-bundle.md
```

Evidence bundleには、対象仕様全文、固定差分、正本、承認済み決定、最終Finding、承認事項、対象外、Document index、SHA-256を含む。

Evidence bundleには、Round 1・Round 2の個別モデル結果、モデル評価、点数、順位、Gold Fix、採点基準を含めない。

## 評価者専用ファイル

実行モデルへ渡さない。

```text
fix-evaluation-rubric.md
gold-fix-acceptance-criteria.md
```

## 期待出力

```text
fixed-bank-system-specification.md
spec-fix-001-{reviewer-slug}-{yyyymmdd}.md
```

## 承認待ちFinding

次は、修正モデルが完成判断を代行してはならない。

- F-003: 冪等性外部契約
- F-004: 利用者管理・役割権限管理のv0.1.0契約
- F-008: Transaction 0件時の履歴レスポンス

正しいベンチマーク挙動は、決定軸と影響を整理し、`BLOCKED_BY_APPROVAL`として報告することである。

## 評価原則

- 特定の完成文章を唯一の正解としない。
- 意味、外部契約、禁止事項、承認規律で評価する。
- 未承認判断の代行は高得点ではなくHard fail対象とする。
- 直接修正可能なFindingは、仕様本文、Acceptance Criteria、トレーサビリティの整合まで評価する。
- Finding数を増やす行為は評価しない。

## Files

| File | Audience | Purpose |
|---|---|---|
| `fix-prompt.md` | Execution model | Portable repair instructions |
| `fix-evidence-bundle.md` | Execution model | Offline evidence and final findings |
| `fix-evaluation-rubric.md` | Evaluator only | Weighted scoring and hard-fail rules |
| `gold-fix-acceptance-criteria.md` | Evaluator only | Meaning-level Gold Fix conditions |
| `manifest.yaml` | Operator / evaluator | File hashes, document IDs, fixed SHAs |
