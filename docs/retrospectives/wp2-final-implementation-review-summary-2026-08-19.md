# minimal-bank-system WP-2 最終総括 — 実装モデル / レビューモデル評価

- Repository: `kooiei-in4a/minimal-bank-system`
- Work Package: WP-2 Security and Audit
- 対象Leaf: #164〜#171
- 評価日: 2026-08-19

## 1. 結論

WP-2では8個のleafに対して独立実装Candidate、Final Synthesis、Light Review、Heavy Review、Targeted Re-reviewを繰り返した。

最終的な結論は次の通り。

- 実装モデルは、今回使った全モデルが概ね独立Candidateとして比較に参加できる基準ラインへ到達した。
- モデルごとに得意領域が分かれた。
- 3つの独立実装Candidate方式はR&Dとして有効だったが、今後の通常leafで毎回3実装する必要性は下がった。
- 一方、Reviewer diversityは実際に重大欠陥・false assuranceの検出に寄与したため、BuilderとReviewerのモデル分離は維持する価値が高い。
- 今後は「1 Production Candidate + Independent Review」を標準とし、必要に応じて2候補、未知・高リスク時のみ3候補へ増やす方式が妥当。

## 2. WP-2 対象Leaf

| Issue | Leaf |
|---|---|
| #164 | WP2-DB-01 |
| #165 | WP2-ID-01 |
| #166 | WP2-AUD-01 |
| #167 | WP2-AUTHN-01 |
| #168 | WP2-AUTHZ-01 |
| #169 | WP2-OPR-QRY-01 |
| #170 | WP2-OPR-CREATE-01 |
| #171 | WP2-OPR-MUT-01 |

## 3. 実装Candidate総括

8 leaf × 3 Candidate = 24実装。

| モデル | 実装回数 | Primary Base採用 |
|---|---:|---:|
| Claude Sonnet 5 | 7 | 2 |
| GPT-5.6 Luna | 6 | 2 |
| GPT-5.6 Sol | 6 | 3 |
| Grok 4.6 | 4 | 1 |
| DeepSeek V4 Flash | 1 | 0 |

AUD-01は3 CandidateすべてGPT-5.6 Solだったため、Solの1勝は他モデルとの直接比較ではない。

Leaf別の最終方向:

| Leaf | Primary Base / Final方向 |
|---|---|
| DB-01 | SonnetをPrimary Base、他Candidate要素も採用 |
| ID-01 | GrokをPrimary Base、他Candidate要素を採用 |
| AUD-01 | Sol C2をPrimary |
| AUTHN-01 | LunaをBase、Sonnet/Sol要素を輸入 |
| AUTHZ-01 | SolをBase、他Candidate要素を輸入 |
| OPR-QRY-01 | SolをBase |
| OPR-CREATE-01 | SonnetをBase |
| OPR-MUT-01 | Luna production + Sonnet verification |

## 4. 実装モデル別評価

### GPT-5.6 Luna

強み:
- contractが閉じた後のproduction implementation
- transaction / PostgreSQL / concurrency
- 既存capabilityを使った具体実装

特にOPR-MUTでは複雑なactive-administrator concurrency contractを実装し、production codeがFinal Synthesisでも維持された。

弱み:
- verification designではSonnetに負ける場合がある
- synthetic test surfaceへ寄る場合がある

推奨役割: **Primary Builder**

### Claude Sonnet 5

強み:
- semantic verification
- mutation test
- race condition
- positive control
- false assurance検出
- feature implementationも強い

OPR-MUTではproduction baseはLunaだったが、verificationはSonnetが採用された。

弱み:
- 少し広めのsurfaceや余計なexternal choiceを入れる場合がある
- authority上まだ決まっていないことを実装可能な選択として前に進める場合がある

推奨役割: **Verifier / Adversarial Builder**

### GPT-5.6 Sol

強み:
- architecture
- responsibility boundary
- 複数contractの統合
- Candidate Selection
- Final Synthesis

弱み:
- frameworkの具体挙動では外すことがある
- runtime detailは別Reviewerが必要

推奨役割: **Architect / Synthesizer / Selection Reviewer**

### Grok 4.6

強み:
- alternative architecture
- second opinion
- simplification pressure
- ID-01ではPrimary Base

弱み:
- 少し複雑な仕組みや抽象化を持ち込む場合がある

推奨役割: **Alternative Builder / Architecture Exploration**

### DeepSeek V4 Flash

AUTHZで局所的に有効な設計要素を提供したが、1サンプルのみで一般評価は保留。

推奨役割: **Experimental Alternative**

## 5. レビューモデル総括

レビューは次の役割へ分けて評価する。

1. Issue Ready Review
2. Light Semantic Review
3. Heavy H1
4. Targeted Re-review
5. Heavy H2
6. Candidate Selection / Adjudication

単純なMajor検出数だけでは比較しない。

## 6. Claude Opus 5 — Deep Reviewer

強み:
- Issue Readyで契約不足を止める
- repository-level evidence depth
- authority completeness
- latent risk / cross-leaf risk

代表例:
- AUD-01 Issue Ready: Major 3
- AUTHN-01 Issue Ready: Major 2
- OPR-QRY-01 Issue Ready: Major 3
- AUTHZ H2でRequestAbortedによるmandatory Audit cancellation riskをMinorとして保持し、後続leafで実際にreachable Majorへ昇格

弱み:
- 重い
- Minor/Nitまで多く拾いやすい
- routine Light Reviewには過剰

推奨役割: **Issue Ready / H2 / Deep Evidence / Authority Completeness**

## 7. GPT-5.6 Sol — Adversarial Heavy Reviewer

Heavy H1で特に強かった。

代表例:
- AUD-01: Audit Downのconcurrent INSERT raceをMajor検出
- AUTHN-01: invalid JWT non-disclosure testのfalse assuranceをMajor検出
- AUTHZ-01: 415 routing fault pathでcurrent-Operator DB lookup非発生の証拠不足をMajor検出

特徴:
- 「テストがgreenか」ではなく、「そのgreenが要求された意味を本当に証明しているか」を見る。

推奨役割: **Heavy H1 / False Assurance / Adversarial Review**

## 8. GPT-5.6 Luna — Runtime Heavy Reviewer

BuilderだけでなくReviewerとしても高評価。

代表例:
- OPR-QRY H1: AUTHZ latent riskが実endpoint導入でreachableになったことをMajor化
- OPR-CREATE H1: Audit failure injectionが実PostgreSQL persistence failureまで到達していないfalse assuranceをMajor化
- OPR-MUT H1: 重大欠陥がなければ無理にFindingを増やさずPASS

推奨役割: **Runtime / DB / Integration / Concurrency Heavy Review**

## 9. Claude Sonnet 5 — Light / Targeted Reviewer

強み:
- Light Review
- Targeted Re-review
- severity calibration
- MinorとMajorの切り分け

代表例:
- OPR-QRY Light: Major 0でPASSしつつreal endpoint coverage不足をMinorとして保持
- OPR-CREATE Issue Ready: Major 3を正しく発見

弱み:
- OPR-CREATEでは、Major自体は正しかったが、いくつかのsemantic choiceをAIだけで決められる方向へ寄せたため、CoordinatorがHuman Decisionへ補正した

推奨役割: **Light Semantic Review / Targeted Re-review**

## 10. GPT-5.6 Terra

サンプル不足。

OPR-MUT Light ReviewではPASS。今後はactual reviewer identityをstage result evidenceに必須記録するべき。

推奨役割: **Light Review候補 / H2候補**

## 11. Grok 4.6 — Second Opinion Reviewer

WP-2 Issue Set reviewで、
- lightweight
- proportional
- simplification pressure
- unique second opinion

という価値を示した。

推奨役割: **Alternative / Simplification Review**

## 12. Cursor Auto

OPR-MUT Issue Ready Reviewで、
- API shape
- no-op semantics
- concurrency strategy / conflict
- Audit operation identifiers

の4つのmaterial semantic choiceを検出し、Human Decision Requiredへ正しくエスカレーション。

ただしactual underlying modelが不明なため、モデル比較には使わない。

## 13. レビュー工程の有効性

WP-2では、Light Review PASS後にHeavy ReviewがMajorを見つけた実例がある。

### OPR-QRY
Light:
- PASS
- Blocker 0
- Major 0

Heavy H1:
- Major 1
- AUTHZ latent riskが実endpointでreachableになった

### OPR-CREATE
Light:
- PASS

Heavy H1:
- Major 1
- Audit failure testのfalse assurance

このため、高リスクleafではLight Reviewだけでは不十分な場合がある。

## 14. 3 Candidate方式の最終評価

WP-2では有効だった。

得られたもの:
- model差
- best productionとbest verificationが別になること
- rejectすべきarchitecture
- Final Synthesis methodology
- model-role mapping

ただし今後も毎回3実装すべきではない。

## 15. 今後のCandidate数

### 1 Candidate — 標準
- 既存pattern
- CRUD
- contractが閉じている
- schema変更なし
- security semantic新規判断なし

### 2 Candidate — 重要
- 新transaction
- concurrency
- schema
- security boundary
- 複数の自然なarchitecture

### 3 Candidate — 例外
- repository初
- 高リスク
- irreversible
- R&D
- 2 Candidateが大きく対立
- 後続leaf全体へ影響

原則:

> **1 → 必要なら2 → 本当に必要な場合だけ3**

## 16. 今後の標準モデル配置

| 役割 | 第一候補 | 第二候補 |
|---|---|---|
| Issue / Architecture設計 | GPT-5.6 Sol | Claude Opus 5 |
| Issue Ready Review | Claude Opus 5 | Claude Sonnet 5 |
| Production Builder | GPT-5.6 Luna | Claude Sonnet 5 |
| Light Semantic Review | Claude Sonnet 5 | GPT-5.6 Terra |
| Heavy H1 / false assurance | GPT-5.6 Sol | GPT-5.6 Luna |
| Runtime / DB Heavy | GPT-5.6 Luna | GPT-5.6 Sol |
| H2 / cross-leaf risk | Claude Opus 5 | GPT-5.6 Luna |
| Candidate Selection | GPT-5.6 Sol / Claude Opus 5 | — |
| Alternative / simplification | Grok 4.6 | — |

## 17. 推奨標準フロー

```text
Issue / Contract
     ↓
Issue Ready Review
Opus 5
     ↓
Production Implementation
Luna
     ↓
Build / Tests / CI
     ↓
Light Semantic Review
Sonnet 5
     ↓
Risk判断
     ↓
必要なら Heavy H1
Sol または Luna
     ↓
必要なら Narrow Fix
     ↓
Targeted Re-review
別Fresh Reviewer
     ↓
高リスク時のみ H2
Opus / Luna
     ↓
Final Approval
Human
     ↓
Merge
```

## 18. WP-2から得た主要知見

1. 複数モデルがproduction-level Candidateを作れる
2. モデルごとに得意roleが異なる
3. best implementationとbest verificationは同じモデルとは限らない
4. Light ReviewとHeavy Reviewは異なる価値を持つ
5. green CIだけではsemantic correctnessは証明できない
6. mutation / positive control / non-vacuous oracleが有効
7. latent riskはdownstream leafでreachableになり得る
8. Reviewer model diversityは実際に欠陥検出へ寄与する
9. 3 Candidate方式はR&Dとして有効だった
10. 学習後は1 Candidate標準へ縮小可能

## 19. WP-2終了時点の最終判断

### Implementation
**PASS**

複数モデルで十分な実装能力を確認。通常実装をLuna中心へ寄せられる見込みが立った。

### Review
**PASS**

Opus / Sol / Luna / Sonnetに異なるReviewer価値を確認。Heavy Reviewは実際にMajor defect / false assuranceを複数検出した。

### Process
**ADOPT_WITH_SIMPLIFICATION**

- 1 Candidate default
- 2 Candidate escalation
- 3 Candidate exceptional
- independent review remains mandatory
- heavy review is risk-based

## 20. 次段階でのR&Dテーマ

WP-3以降ではモデル比較そのものを主目的にせず、次を計測する。

- 1 Candidateでのfirst-pass success率
- Light Review Major検出率
- Heavy Review Major検出率
- Narrow Fix回数
- Final Synthesisが必要になった割合
- Human Decision発生率
- model/harness別cost
- elapsed time
- rework量

次の問いは、

> 「複数AIが書けるか」

ではなく、

> **「少ないAI実行数で、WP-2相当の品質を維持できるか」**

である。

## 21. 最終要約

WP-2は、

> 3つのAIに同じコードを書かせる実験

から始まり、

> **AIごとの得意役割を使い分け、必要最小限のAgent数で品質を確保する工程**

へ進化した。

現時点での推奨:

```text
Sol    = Architect / Adversarial Review / Selection
Opus   = Deep Issue Ready / H2
Luna   = Production Builder / Runtime Heavy Review
Sonnet = Light Review / Verification
Grok   = Alternative / Simplification
Terra  = Light Reviewer候補
```

今後は、

> **1 Builder + Independent Review**

を標準とする。

難しい時だけCandidateを増やす。

これがWP-2の実測結果から導ける、現時点で最も合理的な運用方針である。
