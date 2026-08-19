# minimal-bank-system — WP-3 動的Agent / Review配分 改善提案

- Repository: `kooiei-in4a/minimal-bank-system`
- 対象: WP-3 Customer Vertical Slice
- 日付: 2026-08-19
- 目的: WP-2で固定的だったモデル構成を、Issueの難しさと実装結果に応じて動的に決める方式へ移行し、WP-2相当の品質を維持しながらAI実行数・レビュー数・Human操作を削減できるかを検証する。

---

## 1. 提案の要点

WP-3では、WP-2で固定的だったモデル構成を、**Issueの難しさと実装結果に応じて動的に決める方式**へ変更する。

```text
Issue Ready
   ↓
① 実装前 Agent 判断
   ↓
1 / 2 / 3 Candidateを決定
   ↓
Implementation
   ↓
CI / Test
   ↓
② 実装後 Reviewer 判断
   ↓
Lightのみ / Heavy H1追加 / H2追加
   ↓
必要ならNarrow Fix
   ↓
Final Approval
```

具体的には次のようにする。

| 改善点 | WP-3での変更 |
|---|---|
| **実装前判断** | Issue Ready時に、難易度・新規性・security・concurrency・設計選択肢・影響範囲を評価 |
| Candidate数 | LOW → 1、MEDIUM → 2、HIGH → 3 |
| Builder選択 | 通常はLuna。設計比較が必要ならSonnet/Grok等を追加 |
| **実装後判断** | diff、変更範囲、CI、テスト、mutation、残存リスクを見て必要なReviewerを決定 |
| Light Review | 原則実施。Sonnet/Terra等 |
| Heavy H1 | security、DB、transaction、concurrency、false assurance等がある場合だけ追加 |
| Heavy H2 | H1で重大Findingが出た、cross-leaf riskがある、証拠に不確実性が残る場合だけ追加 |
| Human | semantic choiceと最終merge判断に集中 |
| 記録 | Candidate数とReviewer数を決めた**理由**、Actual Model/Harnessを記録 |

---

## 2. 実装前 Agent 判断

ここは既存の`Issue Ready`を拡張する。

これまでの、

> 「このIssueは実装開始できるか」

に加えて、

> **「このIssueには何Candidate必要か、誰に実装させるか」**

まで決める。

通常の既存patternなら1実装。新しい設計や重要なsecurity boundaryなら2実装。未知で影響の大きいものだけ3実装とする。

### Candidate数の基準

#### LOW — 1 Candidate

- 既存patternの再利用
- CRUD中心
- 既存transaction / Audit / AUTHZ primitiveの利用
- schema変更なし、または既知pattern内
- 新しいsecurity semanticなし
- contractが十分閉じている
- 影響範囲が局所的

#### MEDIUM — 2 Candidate

- 新しいDB transaction
- schema設計
- 新しいsecurity boundary
- concurrencyを含む
- 既存patternをそのまま使えない
- 複数の自然なarchitectureがある
- cross-leaf影響が一定程度ある

#### HIGH — 3 Candidate

- repository初のarchitecture
- irreversible / high-impact migration
- 高リスクsecurity境界
- 重大concurrency
- 正解が事前に読みづらい
- 2 Candidateが大きく対立
- 後続leaf全体へ大きく影響する
- R&D自体が目的の一部である

原則は、

> **1 → 必要なら2 → 本当に必要な場合だけ3**

とする。

---

## 3. Builder選択

通常のPrimary BuilderはGPT-5.6 Lunaを第一候補とする。

WP-2の実測では、Lunaはcontractが閉じた後のproduction implementation、transaction、PostgreSQL、concurrencyで高い適性を示した。

必要に応じて追加Candidateを選ぶ。

- Claude Sonnet 5: verificationを意識した実装、race / mutation / positive control
- Grok 4.6: alternative architecture、simplification、framework依存へのsecond opinion
- GPT-5.6 Sol: architecture / boundaryが主課題で、実装比較自体に高い価値がある場合

Candidate数だけでなく、**なぜそのモデルを追加したか**を記録する。

---

## 4. 実装後 Reviewer 判断

同じ考え方をレビュー側にも適用する。

これまではLight / Heavyを比較的固定的に運用していたが、WP-3では、

> **「この実装結果には、どの深さのレビューが必要か」**

を実装後に判断する。

判断材料:

- diffの大きさ
- production surfaceの変更範囲
- schema / migration変更
- transaction boundary
- concurrency
- security / authorization / Audit
- failure injection
- Critical Mutation
- CI / test結果
- 新しいframework behaviorへの依存
- cross-leaf影響
- reviewerが確認できるsemantic oracleの直接性
- unresolved risk / uncertainty

---

## 5. Light Review

Light Reviewは原則実施する。

候補:

- Claude Sonnet 5
- GPT-5.6 Terra

主目的:

- scope / contract逸脱
- obvious correctness defect
- response / Audit / authorization ownership
- test completeness
- evidenceの基本妥当性
- severity calibration

Light ReviewでBlocker / Major 0かつ、Heavyへの明確なrisk signalがなければ、そのままFinal Approvalへ進めることを許容する。

---

## 6. Heavy H1

Heavy H1は常時必須とせず、次のrisk signalがある場合に追加する。

- security boundary
- database privilege / persistence safety
- transaction atomicity
- concurrency / race condition
- fail-closed
- complex Audit semantics
- critical mutation
- false assuranceの可能性
- framework-specific behavior
- cross-leaf reachability
- Light Reviewでは証明力が十分でない

Reviewer候補:

- GPT-5.6 Sol: false assurance / adversarial review / semantic evidence
- GPT-5.6 Luna: runtime / DB / transaction / integration / concurrency

実装内容に応じて役割適性で選ぶ。

---

## 7. Heavy H2

Heavy H2は高リスク時のみ追加する。

追加条件の例:

- H1で重大Findingが発生した
- 修正後に別視点の独立確認が有効
- cross-leaf latent riskがある
- authority / contract / repository evidenceの深い確認が必要
- semantic oracleの証拠に不確実性が残る
- security / Audit / concurrencyの残余リスクが高い

第一候補:

- Claude Opus 5

必要に応じてLuna等を追加する。

単に「Heavyだから念のためH2」では実施しない。

---

## 8. Humanの役割

Humanは以下へ集中する。

- 承認済みauthorityから一意に決まらないsemantic choice
- product / API / security / Auditの新しい意味判断
- Accepted ADRにない重要設計判断
- scope変更
- materially high-impact / irreversible choice
- AI間の結論が追加検証でも解消しない場合
- final product merge approval
- final release Go / No-Go

以下はroutine human approvalへ戻さない。

- Candidate数のrule-decidable判定
- Reviewer深度のrule-decidable判定
- CI PASS
- exact target一致
- dependency complete
- Blocker / Major 0
- ruleから一意に決まるNarrow Fix

---

## 9. 記録要件

WP-3では、model evaluationを後から正確に行えるよう、stage result evidenceへ以下を必須記録する。

```yaml
PRE_IMPLEMENTATION_RISK:
  LEVEL: LOW | MEDIUM | HIGH
  CANDIDATE_COUNT: 1 | 2 | 3
  REASONS:

IMPLEMENTATION:
  ACTUAL_MODEL:
  ACTUAL_HARNESS:
  ROLE:

POST_IMPLEMENTATION_REVIEW_RISK:
  LIGHT_REQUIRED: true
  H1_REQUIRED:
  H2_REQUIRED:
  REASONS:

REVIEW:
  ACTUAL_MODEL:
  ACTUAL_HARNESS:
  REVIEW_STAGE:
```

planned identityとactual identityを分離する。

Candidate数 / Reviewer数そのものより、**なぜその数・モデルを選んだか**を残す。

---

## 10. WP-3の中心テーマ

WP-3では、

> **Agentを最初から固定配置するのではなく、前後のリスク評価で必要なAgentだけ投入する。**

ことを試す。

WP-2が、

> `固定的に厚い工程で品質を確認する`

段階だったのに対し、WP-3は、

> **`必要なところだけ工程を厚くして、WP-2品質を維持できるか`**

を確認する段階とする。

この変更により、WP-2で確立した安全性を大きく捨てずに、AI実行数・レビュー数・Human操作を削減できる可能性がある。

---

## 11. WP-3で計測する指標

最低限、次を記録する。

- Candidate数
- Candidate数を増減した理由
- actual Model / Harness
- first-pass implementation success
- required CI first-pass success
- Light Review Finding
- Heavy H1実施率 / Major検出率
- Heavy H2実施率 / Major検出率
- Narrow Fix回数
- Targeted Re-review回数
- Final Synthesisが必要になった割合
- Human Decision発生数
- Agent実行数
- elapsed time
- rework量
- WP-2相当の品質Gateを維持できたか

中心となる問いは、

> **「少ないAI実行数で、WP-2相当の品質を維持できるか」**

である。

---

## 12. 暫定運用モデル

```text
Issue / Contract
      ↓
Issue Ready
      ↓
Pre-Implementation Risk Assessment
      ↓
1 / 2 / 3 Candidate
      ↓
Implementation
      ↓
Build / Test / CI
      ↓
Independent Light Review
      ↓
Post-Implementation Risk Assessment
      ↓
 ┌───────────────┬───────────────────┐
 │ Low residual  │ Material risk      │
 │ risk          │ signal             │
 ↓               ↓
Final Approval   Heavy H1
                 ↓
                 必要ならNarrow Fix
                 ↓
                 Targeted Re-review
                 ↓
                 H2 risk?
                 ├─ No → Final Approval
                 └─ Yes → Heavy H2 → Final Approval
      ↓
Human merge approval
      ↓
Merge
```

この方式をWP-3で検証し、WP-2品質を維持したまま工程を軽量化できるかを評価する。
