# Round 1・Round 2から最終Findingへのトレーサビリティ

## 1. 固定対象

- PR: `#9`
- Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Target file: `docs/specs/bank-system-specification.md`
- Round 1 review artifacts: 7
- Round 2 review artifacts: 16
- Round 2 unique model/configuration labels: 15
- Round 2 duplicate execution label: Claude Sonnet 5 High（通常 / browser）

本表は、検出回数をFindingの正しさの根拠に使用しない。回数は再現性と見逃し傾向の記録にだけ使用する。

## 2. 最終Finding対応

| Final ID | Round 1審理 | Round 2 Full / Partial / Miss | 最終処理 |
|---|---|---:|---|
| F-001 | 複数レビューの解約後参照指摘を統合。一部の「許可する」修正案を却下 | 2 / 0 / 14 | Major維持。B-01の「のみ」を厳格適用 |
| F-002 | AC欠落、並行AC、401、解約済み操作、REQ名目追跡を統合 | 6 / 8 / 2 | Major維持。個別Majorを一つの根本原因へ収束 |
| F-003 | scope、結果固定、競合後再送、処理中、保持期間を統合 | 6 / 4 / 6 | Major維持。新規Koo承認軸を明確化 |
| F-004 | 利用者管理、役割管理、管理AC、初期運用経路を統合 | 6 / 1 / 9 | Major維持。製品範囲の選択はKoo承認 |
| F-005 | code使い分け、複数原因AC、再解約codeを統合 | 4 / 4 / 8 | Minor維持。全評価順固定は却下 |
| F-006 | Audit Log対象範囲と障害ログ不足を統合 | 4 / 1 / 11 | Minor維持。閲覧API必須化は却下 |
| F-007 | ADR-CANDIDATE-003の入金方式先取り | 1 / 0 / 15 | Minor維持。出金・振込の行ロック要求は保持 |
| F-008 | 0件履歴正常系 | 1 / 0 / 15 | Minor維持。`200`空collectionの確定はKoo確認待ち |
| F-009 | §7.3と§22.1の承認状態不整合 | 3 / 3 / 10 | Minor維持。具体制約値は追加しない |
| N-001 | REST API主宣言 | 1 / 0 / 15 | Nit維持 |
| N-002 | 追加ADR候補ID | 0 / 0 / 16 | Nit維持。Round 2全件未検出でも正本上の問題は成立 |

## 3. Round 2モデル別カバレッジ

記号:

- `●`: 根本原因を完全検出
- `◐`: 一部検出
- `—`: 未検出

| Review artifact label | F-001 | F-002 | F-003 | F-004 | F-005 | F-006 | F-007 | F-008 | F-009 | N-001 | N-002 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| ChatGPT 5.6 Sol High | ● | ● | ● | ● | ● | — | — | — | ● | — | — |
| ChatGPT 5.6 Sol XHigh | ● | ● | ● | ● | ● | ● | — | — | — | — | — |
| ChatGPT 5.6 Sol Middle | — | ● | ● | ● | — | ● | — | ● | — | — | — |
| ChatGPT 5.6 Sol Fast | — | ● | ● | ● | — | — | — | — | — | — | — |
| Gork 4.5 High Fast | — | ● | ● | ● | ◐ | ● | — | — | ● | — | — |
| ChatGPT 5.6 Luna XHigh | — | — | ● | ● | — | — | — | — | ◐ | — | — |
| Claude Sonnet 5 High（browser） | — | ◐ | — | — | ● | — | — | — | ● | — | — |
| Kimi K3 | — | ● | ◐ | — | — | — | ● | — | — | — | — |
| Composer 2.5 Fast | — | ◐ | ◐ | ◐ | ● | ● | — | — | — | ● | — |
| GLM 5.2 High | — | ◐ | ◐ | — | — | ◐ | — | — | ◐ | — | — |
| Claude Sonnet 5 High（通常） | — | ◐ | — | — | — | — | — | — | — | — | — |
| DeepSeek V4 Pro | — | ◐ | — | — | — | — | — | — | — | — | — |
| DeepSeek V4 Flash | — | ◐ | — | — | ◐ | — | — | — | — | — | — |
| Gemini 3.6 Flash | — | ◐ | — | — | ◐ | — | — | — | ◐ | — | — |
| Gemini 3.6 Pro | — | — | ◐ | — | — | — | — | — | — | — | — |
| Gemini 3.6 Thinking | — | ◐ | — | — | ◐ | — | — | — | — | — | — |

## 4. Round 1代表統合

| Round 1原指摘 | Final ID | 審理結果 |
|---|---|---|
| 解約後の口座情報・残高参照 | F-001 | Valid。Minor扱いは過小。「許可する」修正はInvalid |
| 主要異常系AC不足 | F-002 | Valid。複数の個別Findingを統合 |
| 並行振込AC不足 | F-002 | Validだが独立Majorではなく統合 |
| 冪等性と競合後再試行 | F-003 | Valid。最終契約はKoo承認必要 |
| 利用者・役割管理範囲 | F-004 | Valid。Nit扱いは過小 |
| error code原因対応 | F-005 | Valid。Major扱いは過大 |
| 操作ログと障害ログ | F-006 | Valid |
| ADR-CANDIDATE-003 | F-007 | Valid |
| 0件履歴 | F-008 | Valid。Major扱いは過大 |
| §7.3と§22.1 | F-009 | Valid |
| REST宣言 | N-001 | Valid |
| 追加ADR候補ID | N-002 | Valid |

## 5. Round 2追加候補

| Round 2追加候補 | 最終処理 |
|---|---|
| 振込履歴の相手口座番号は未承認露出 | 最終Finding不採用。原始要件のTransaction属性に対当口座番号があり、禁止根拠が不足 |
| 振込・解約競合の専用AC | F-002へ統合 |
| 解約・更新の入力識別子 | Optional。API入力契約の明確化として後続検討 |
| 顧客ID採番ADR | Optional / ADR選別 |
| PATCH部分更新意味論 | API設計へ委譲 |
| `no_balance_to_withdraw`不存在 | 事実誤認として却下 |
| 全額出金負符号の重複記載 | Optional |

## 6. 成果物参照

### Round 1

- `docs/reviews/spec-review-001/spec-review-001-finding-matrix.md`
- `docs/reviews/spec-review-001/spec-review-001-final.md`
- 個別レビュー成果物

### Round 2

- `docs/reviews/spec-review-002/results/spec-review-002-round-2-complete.zip`
- `docs/reviews/spec-review-002/analysis/spec-review-002-analysis-16.zip`
- `docs/reviews/spec-review-002/round-2-manifest.yaml`

### Final

- `docs/reviews/spec-review-final/final-adjudicated-findings.md`
- `docs/reviews/spec-review-final/rejected-and-merged-findings.md`
