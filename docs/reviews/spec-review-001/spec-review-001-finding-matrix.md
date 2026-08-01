# spec-review-001 Finding 審理マトリクス

## 1. 対象

- Repository: `kooiei-in4a/minimal-bank-system`
- Review target: Draft PR `#9`
- Base SHA: `dedbcaf31fd4c40b966facd1829c7535b8d0e4ba`
- Head SHA: `4944fb22806526f9e92dc47b516b57431c6c7f0a`
- Target file: `docs/specs/bank-system-specification.md`
- Review artifacts: 7件
- Adjudication date: 2026-08-02
- Review targetへの変更: なし
- 本成果物の格納先: `main` / `docs/reviews/spec-review-001/`

## 2. 審理方法

各レビューのFindingをモデル名から切り離し、根本原因単位へ正規化した。その後、次の正本順で採否を判定した。

1. `AGENTS.md`
2. Issue #3
3. Issue #5 Koo決定 B-01〜B-06
4. Issue #8 Koo決定 D-01〜D-17
5. `docs/project/simulation-charter.md`
6. `docs/requirements/bank-system-requirements.md`
7. `docs/reviews/requirements-review-001.md`
8. `docs/traceability/requirements-register.md`
9. Issue #7
10. Issue #10
11. PR #9および対象仕様書

判定区分:

- **Valid**: 問題、根拠、影響が成立する
- **Partially valid**: 問題検出は正しいが、重大度、根拠、修正案の一部が誤る
- **Invalid**: 正本に反する、問題が成立しない、または単なる任意改善
- **Duplicate**: 別Findingと同一の根本原因
- **Out of scope**: 今回のSpecification Ready判定に不要
- **Needs Koo decision**: 正本だけでは外部契約を一意に確定できない

## 3. Gold Finding

| ID | 最終重大度 | 論点 | 審理 | Koo判断 | 主な根拠 |
|---|---|---|---|:---:|---|
| F-001 | Major | 解約後の口座基本情報参照・現在残高直接参照が未定義 | Valid | 不要 | B-01は解約後に許可する操作を顧客情報参照と履歴閲覧「のみ」に限定する。仕様§5.4は両操作を欠落させ、§6.3は一般権限として許可している |
| F-002 | Major | 必須Acceptance CriteriaとREQ追跡が実質的に不足 | Valid | 不要 | Issue #7は正常系・主要異常系・検証可能なACを要求し、解約では既解約・不存在・権限不足を明示的に要求する。§18と§19には複数の欠落・名目的対応がある |
| F-003 | Major | 冪等キーの結果固定と競合後再試行の外部契約が未確定 | Valid | **必要** | D-13の「最初と同じ業務結果」とD-17の「競合後に同じキーで安全に再実行」の両立条件、scope、要求同一性、結果確定、処理中、保持期間が未定義 |
| F-004 | Major | 利用者管理・役割権限管理のv0.1.0製品範囲が未定義 | Valid | **必要** | B-04と§6.3では管理者の許可操作だが、§2、機能章、AC、Out of scopeに機能契約がない。API機能、seed運用、将来機能の複数解釈が成立する |
| F-005 | Minor | 固定エラーコードとACの原因対応が一意でない | Valid | 既存§22.1判断内 | 負残高、状態不整合、既解約、再有効化、複数原因を一つのACへまとめたケースなどで期待codeが一意でない |
| F-006 | Minor | Audit Logとシステム障害ログの責務・検証範囲が不十分 | Valid | 不要 | D-12は操作・障害ログを要求するが、§14はAudit Log中心で、障害ログの対象と証拠条件が明確でない |
| F-007 | Minor | ADR-CANDIDATE-003の文言が入金にもDB行ロックを固定したように読める | Valid | 不要 | §17は出金・振込の行ロックと入金の方式選択を分離するが、§21の固定要求欄は包括的 |
| F-008 | Minor | 取引0件時の履歴照会結果が未定義 | Valid | **推奨** | 新規口座では正常にTransaction 0件が成立するが、成功時のHTTP状態・空collectionが明示されない |
| F-009 | Minor | §7.3が氏名・メール制約の委譲を先取りしている | Valid | 既存§22.1判断内 | §7.3はAPI・データ設計で定義すると記す一方、§22.1は委譲するか自体を未決としている |
| N-001 | Nit | 主インターフェースがREST APIである独立宣言が弱い | Valid | 不要 | B-05では明示されるが、仕様では§6.1・§16・Out of scopeからの読み取りが中心 |
| N-002 | Nit | 「追加ADR候補」に安定IDがない | Valid | 不要 | 後続Issue・ADRからの参照性が低い |

## 4. F-002で追加すべき最低限のAcceptance Criteria

網羅的な組合せ試験ではなく、正本とIssue #7が要求する主要契約を直接検証する最低限の集合とする。

1. 有効Customerの氏名・メールアドレス更新成功
2. 既解約状態での再解約拒否
3. 解約対象不存在
4. 解約権限不足
5. 解約済みAccountへの入金・通常出金・全額出金拒否
6. 解約後の顧客情報参照・履歴閲覧成功
7. 解約後の口座基本情報・現在残高直接参照拒否
8. 通常出金の0円・負数拒否
9. 振込の残高不足
10. 振込の出金元・振込先不存在
11. 顧客ID／口座番号の不一致
12. 未認証401と認証済み権限不足403
13. 閲覧者の口座基本情報拒否と履歴上残高閲覧許可
14. 同時振込または出金・振込競合
15. 同一冪等キーの同時到着と競合後再送
16. 履歴表示必須項目の検証
17. Audit Logの必須項目、成功・失敗、secret非記録

## 5. モデル別Finding対応

### Kimi K3

| 原Finding | Canonical | 審理 | コメント |
|---|---|---|---|
| m-1 解約後参照 | F-001 | Valid | 検出・修正方向は正しい。Minor判定は過小 |
| m-2 冪等キー失敗・保証期間 | F-003 | Valid | 主要論点を検出。ただし「新規Koo判断不要」は不適切 |
| m-3 同時実行敗北時code | F-003/F-005 | Partially valid | 有効な下位論点だが独立Findingとしては重複がある |
| m-4 エラー優先順位・再解約 | F-005 | Valid | 全体優先順位の固定は過剰になり得るため、原因別ACの分離を優先 |
| m-5 AC不足 | F-002 | Valid | Major相当をMinorとしている |
| n-1 REST宣言 | N-001 | Valid | 軽微 |
| n-2 AC期待結果不統一 | F-005 | Valid | 原因別AC分離で対応 |
| n-3 利用者管理・ログイン契約 | F-004 | Valid | Nitではなく製品範囲のMajor |
| Verdict / Ready | - | Partially valid | Required fixesを認めながら`READY FOR KOO APPROVAL`としており内部整合性が弱い |

### Gork-4.5 High Fast（成果物表記）

| 原Finding | Canonical | 審理 | コメント |
|---|---|---|---|
| M-1 主要異常系AC不足 | F-002 | Valid | 重大度も妥当 |
| M-2 利用者・役割管理契約なし | F-004 | Valid | 少数モデルが検出した重要論点 |
| m-1 閲覧者の口座基本情報AC | F-002 | Valid | F-002の一部 |
| m-2 並行振込ACなし | F-002 | Valid | REQ-CON-001追跡不足 |
| m-3 通常出金0円・負数ACなし | F-002 | Valid | 正本に直接対応 |
| m-4 同一キー異内容の導出根拠 | F-003 | Valid | D-13だけでは異内容拒否まで確定していない |
| n-1 追加ADR候補ID | N-002 | Valid | 軽微 |
| n-2 再有効化AC | F-005 | Valid | 操作面と期待codeの明確化が必要 |
| 見逃し | F-001/F-003中核 | - | 解約後参照とD-13/D-17の衝突を十分に捉えていない |

### Composer 2.5 Fast

| 原Finding | Canonical | 審理 | コメント |
|---|---|---|---|
| M-1 解約後参照 | F-001 | Valid | 検出・重大度・修正方向が正しい |
| m-1 未認証401 AC | F-002 | Valid | F-002の一部 |
| m-2 解約済み入出金AC | F-002 | Valid | F-002の一部 |
| m-3 監査ログ対象範囲 | F-006 | Partially valid | 「全参照操作を必ず記録」は要確認だが、記録対象の境界明示は必要 |
| n-1 AC-CLS-003 code | F-005 | Valid | 正しい |
| 見逃し | F-003/F-004 | - | 冪等性契約と利用者管理範囲を未検出 |

### GPT-5.6 Pro

| 原Finding | Canonical | 審理 | コメント |
|---|---|---|---|
| M-1 冪等性と競合後再試行 | F-003 | Valid | 最も詳細。新規承認論点の整理も妥当 |
| M-2 解約後参照 | F-001 | Valid | 正しい |
| M-3 利用者・役割管理範囲 | F-004 | Valid | 正しい |
| M-4 AC・追跡不足 | F-002 | Valid | 正しい |
| M-5 並行AC不足 | F-002 | Valid | F-002へ統合。独立Majorは過大 |
| M-6 エラーcode問題 | F-005 | Valid | 問題は正しいがMajorは過大 |
| M-7 0件履歴 | F-008 | Valid | 問題は正しいがMajorは過大 |
| m-1 整数円 | - | Partially valid | 明示改善としては有用だが、Gold Findingには含めない |
| m-2 §7.3と§22.1 | F-009 | Valid | 正しい |
| 総評 | - | - | Recallは最高。ただしFinding分割と重大度が攻撃的で、修正範囲を膨らませる傾向 |

### GLM 5.2 High

| 原Finding | Canonical | 審理 | コメント |
|---|---|---|---|
| m-1 主要異常系AC不足 | F-002 | Valid | Major相当をMinorとしている |
| m-2 操作ログと障害ログ | F-006 | Valid | 正しい |
| m-3 ADR-CANDIDATE-003 | F-007 | Valid | 正しい独自検出 |
| n-1 closed code使い分け | F-005 | Valid | 正しい |
| n-2 顧客ID外部形式 | - | Invalid/Optional | 顧客ID形式は現状のopaque IDで成立し、D-10は口座番号の決定 |
| Verdict / Ready | - | Invalid | F-001、F-003、F-004を見逃し、`PASS WITH FINDINGS`は甘い |

### DeepSeek V4 Pro

| 原Finding | Canonical | 審理 | コメント |
|---|---|---|---|
| m-1 AC-CLS-003 code | F-005 | Valid | 正しい |
| m-2 解約後口座情報 | F-001 | Partially valid | 曖昧さの検出は正しいが、「許可する」修正案はB-01の「のみ」に反する |
| m-3 0件履歴 | F-008 | Valid | 正しい |
| Verdict / Ready | - | Invalid | 重大な欠落をMinor扱いし、誤った修正方向のままReadyと判定 |
| その他 | - | Invalid/Overreach | メール最大長を具体例で確定する推奨は、本レビューでKoo判断を代行し得る |

### GPT-5.6 Thinking

| 原Finding | Canonical | 審理 | コメント |
|---|---|---|---|
| M-1 解約後参照 | F-001 | Valid | 正しい |
| M-2 AC・追跡不足 | F-002 | Valid | 正しい |
| M-3 冪等キー契約 | F-003 | Valid | 正しい |
| m-1 負残高code不整合 | F-005 | Valid | 正しい |
| Verdict / Ready | - | Valid | `FAIL / NOT READY`は妥当 |
| 見逃し | F-004/F-006/F-007/F-008/F-009 | - | Major 1件と複数Minorを未検出 |

## 6. 不採用・統合した代表的指摘

| 指摘 | 判定 | 理由 |
|---|---|---|
| 解約後の口座基本情報参照を許可する | Invalid | B-01の許可操作を2種類「のみ」とする決定に反する |
| 顧客IDにも口座番号と同じ外部形式を定義する | Optional | 顧客IDは一意なopaque IDとして成立し、形式は実装設計へ分離可能 |
| 全エラーのグローバル評価順を必ず固定する | Partially valid | 特定ACの原因分離と期待code固定は必要だが、全API共通の詳細評価順まで製品契約化する必要はない |
| 金額型を直ちに整数型へ固定する | Out of scope | 外部入力で1円未満を許可しない明示は有用だが、物理型はADR対象 |
| Audit Log閲覧APIを必須化する | Invalid | §22.1の既存Koo判断事項であり、レビュー担当が追加を決定できない |

## 7. 最終審理結論

- Blocker: 0
- Major: 4
- Minor: 5
- Nit: 2
- Verdict: `FAIL`
- Specification Ready recommendation: `NOT READY`
- 既存Koo承認事項: 3
- 新たに必要なKoo判断: 冪等性外部契約、利用者・役割管理範囲
- 追加確認を推奨するKoo判断: 取引0件時の履歴レスポンス
