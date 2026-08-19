# minimal-bank-system — WP-2 Security and Audit 取り組み評価レポート

- Repository: `kooiei-in4a/minimal-bank-system`
- 対象: WP-2 Security and Audit
- 参照起点: Discussion #176「WP-2 Security and Audit 開始前の準備と、これから取り組むこと」
- 評価日: 2026-08-19
- 評価対象期間: WP-2開始準備〜`Security and Audit Ready` 完了

> **参照上の注意**
>
> GitHub connectorはRepository Discussion本文の取得に対応しておらず、Discussion #176本文そのものは今回直接取得できなかった。
> そのため本レポートでは、Discussion #176と同時期の正式なGitHub一次証拠である
> Work Package Issue #34、実装Issue分割計画、WP-1からのprocess baseline、各leaf Issue/PR、process retrospective、
> 最終`Security and Audit Ready` Gateを用いて、WP-2開始時の狙いと実績を評価する。
> Discussion #176にだけ存在し、他の正本へ反映されていない独自記述は評価対象外である。

---

# 1. 総合評価

## 結論

**WP-2は、R&D Work Packageとして非常に成功した。**

製品面ではSecurity / Authentication / Authorization / Audit / Operator managementの基盤を成立させ、
最終的に8個のapproved leafすべてを完了し、独立Gate Reviewで
`Security and Audit Ready: PASS` に到達した。

同時に、AI Agent開発プロセスについても大きな学習が得られた。

特に重要なのは次の4点。

1. **複数のAIモデルがproduction-levelの実装Candidateを作れることを確認できた**
2. **Light Reviewだけでは拾えない重大な欠陥をHeavy Reviewが実際に検出した**
3. **Humanはroutineな工程承認ではなく、semantic decisionへ集中できることを実証した**
4. **毎回3実装する方式はR&Dとして有効だったが、通常運用には重すぎることも分かった**

したがってWP-2は、

> 「この工程をそのままWP-3でも繰り返す」

ための成功ではなく、

> **「十分に試した結果、次から工程を簡略化できる」**

という意味で成功したWork Packageと評価する。

---

# 2. 開始時の設計は妥当だったか

## 評価: 高い

Phase 4の実装Issue分割計画では、WP-2以降を最初から固定せず、
前段の実装・レビュー結果を受けて詳細化する**ローリングウェーブ方式**を採用していた。

主な考え方は以下だった。

- 1 Issue = 1つのClose条件
- 1 Issue = 1つの主責任
- Scope / Out of scopeを明確化
- Acceptance CriteriaとVerificationを先に定義
- Issue Readyを通過するまで実装禁止
- Work Package開始前にIssue Setを独立レビュー
- WP-1の実績をWP-2設計へ反映

これは実際のWP-2に非常によく機能した。

特にSecurity/Auditは、最初から細かい実装Issueを固定していた場合、
AUTHN/AUTHZ、Audit ownership、Operator mutation、concurrencyなどの責務境界を
後から大幅に直す可能性が高かった。

WP-2では、先にWP-1の実装形を確認してからleafを確定したため、
**ローリングウェーブ方式を採用した判断は正しかった**。

---

# 3. WP-2開始時のprocess baseline

WP-2開始時のCurrent Authorityでは、
WP-1から以下を引き継ぐ方針だった。

- Rolling Wave
- Issue Single Primary Responsibility
- Stage Entry Check
- 高価値比較時の3 Candidate
- Candidate比較時のFinal Synthesis
- Light Semantic Review
- Risk-based Heavy Review
- Critical Mutation 最大3件
- Semantic Failure Signature
- Targeted Re-review
- Single Current Authority
- Direct main write禁止
- Write Preflight

開始時点では、

- automatic agent launch: false
- human approval: required

としていた。

## 評価

**開始時の安全側の設定として妥当。**

Security/Auditという高リスク領域で、
いきなり自動化を最大化しなかった点は適切だった。

一方、この設計は後半では明らかに重くなり、
WP-2自身の実験結果を使って改善された。

この「途中でprocessを変えたこと」は失敗ではなく、
WP-2のR&D成果と評価する。

---

# 4. Product実装前にprocessを整えた点

WP-2ではproduct implementationへ入る前に、
Process Baseline Sync Issue #172を完了した。

主な内容:

- `AGENTS.md`
- PR template
- main direct write禁止
- 最小Ruleset
- WP-1で採用したprocess controlsのrepositoryへの同期

さらにCIで既存dependencyのHigh severity advisoryが発見され、
Issue #174 / PR #175としてproduct workと分離して修復した。

## 評価

**非常に良い。**

特に評価できるのは、
security remediationをWP2-DB-01等へ紛れ込ませず、
独立Issueとして処理したこと。

これにより、

- product change
- process/control change
- pre-existing security remediation

が混ざらなかった。

AI Agent開発ではscope creepが起きやすいため、
この分離は実務的に重要である。

---

# 5. Issue Set設計の評価

最終的にWP-2は8 leafへ分解された。

- WP2-DB-01
- WP2-ID-01
- WP2-AUD-01
- WP2-AUTHN-01
- WP2-AUTHZ-01
- WP2-OPR-QRY-01
- WP2-OPR-CREATE-01
- WP2-OPR-MUT-01

Issue Set Reviewでは複数モデルを使い、
単なる重複レビューではなく異なる観点が得られた。

観測された特徴:

- GPT-5.6 Sol: Issue topology / decomposition
- Claude Opus 5: repository evidence / existing asset collision
- Grok 4.6: lightweight second opinion / simplification

具体的には、

- AUTHをAUTHNとAUTHZへ分割
- Operator queryとcreateを分割
- AUTHN独立verification surface
- WP-1 verification asset ownershipの取り込み

などが改善された。

## 評価

**非常に成功。**

3モデルレビューを最も有効に使えた場面の一つ。

実装後に責務を分けるより、
Issue Set段階で分割を修正できたため、
後続の手戻りを抑えられた。

---

# 6. 3 Candidate実装方式の評価

8 leaf × 3 Candidateで、合計24 Candidateを実装した。

実際に使用したモデル:

| Model | 実装回数 | Primary Base |
|---|---:|---:|
| Claude Sonnet 5 | 7 | 2 |
| GPT-5.6 Luna | 6 | 2 |
| GPT-5.6 Sol | 6 | 3 |
| Grok 4.6 | 4 | 1 |
| DeepSeek V4 Flash | 1 | 0 |

Primary Baseは4種類のモデルへ分散した。

これは、

> 「最強モデルを1つ選べば全部解決する」

という結果にはならなかったことを示す。

またBaseにならなかったCandidateからも、
verification、test pattern、architecture要素をFinal Synthesisへ取り込んだ。

代表例:

`OPR-MUT = Luna production + Sonnet verification`

## 評価

### R&Dとして: 非常に高い

3 Candidate方式により、

- モデル差
- architecture差
- verification差
- over-engineering傾向
- Final Synthesis方法

を実測できた。

### 通常運用として: 重すぎる

毎回3つ完成実装を作る方式は、

- Agent実行回数
- CI
- Candidate comparison
- Final Synthesis
- 証拠管理

の負荷が大きい。

したがって、

> **WP-2で3 Candidate方式を十分試したからこそ、WP-3では減らせる**

と評価する。

---

# 7. 実装モデルから得られた知見

## GPT-5.6 Luna

強かった領域:

- production implementation
- transaction
- PostgreSQL
- concurrency
- contract-driven implementation

暫定役割:

**Primary Builder**

---

## Claude Sonnet 5

強かった領域:

- semantic verification
- mutation
- positive control
- race / false assurance
- feature implementation

暫定役割:

**Light Reviewer / Verifier / Secondary Builder**

---

## GPT-5.6 Sol

強かった領域:

- architecture
- responsibility boundary
- cross-contract integration
- Candidate Selection
- Final Synthesis

暫定役割:

**Architect / Synthesizer / Adversarial Reviewer**

---

## Grok 4.6

強かった領域:

- alternative architecture
- framework依存を疑う
- simplification / second opinion

暫定役割:

**Alternative Builder / Second Opinion**

---

## DeepSeek V4 Flash

1サンプルのみ。

Candidateとして成立し、有効な局所案も提供したが、
一般的なモデル特性評価にはサンプル不足。

---

# 8. Review工程は本当に必要だったか

## 結論: 必要だった

WP-2で最も価値の高かった実験の一つ。

Light ReviewやCIがPASSしていても、
Heavy Reviewが実際にMajor defectを発見した。

代表例:

### AUD-01

Heavy H1:

`Audit Down empty-history guard concurrent INSERT race`

Migration rollbackの証拠保持にraceが存在した。

---

### AUTHN-01

Heavy H1:

invalid JWT non-disclosure testが、
送信したJWTが本当にauthentication failure pathを通ったことを証明していなかった。

つまりgreen testが**false assurance**だった。

---

### AUTHZ-01

Heavy H1:

404/405/415 routing fault preservationのうち、
mandatory verification gapを発見。

---

### OPR-QRY-01

AUTHZ H2でMinorとして残したAudit cancellation riskが、
実際のprotected endpoint導入によりreachableになりMajorへ昇格。

---

### OPR-CREATE-01

Light ReviewはPASS。

しかしHeavy H1で、

Audit failure injectionが実PostgreSQL persistence failureへ到達せず、
test doubleのmethod入口throwだけで成立していることをMajorとして検出。

---

## 評価

**Heavy Reviewは形式的な二重レビューではなく、実際の品質向上に寄与した。**

これはWP-2の最重要成果の一つ。

---

# 9. Reviewerモデルから得られた知見

## Claude Opus 5

得意:

- Issue Ready
- authority completeness
- repository evidence
- latent/cross-leaf risk
- H2

暫定役割:

**Deep Reviewer**

---

## GPT-5.6 Sol

得意:

- false assurance
- adversarial review
- semantic evidence
- Heavy H1

暫定役割:

**Adversarial Heavy Reviewer**

---

## GPT-5.6 Luna

得意:

- runtime reachability
- DB
- transaction
- integration
- concurrency

暫定役割:

**Runtime Heavy Reviewer**

---

## Claude Sonnet 5

得意:

- Light Review
- Targeted Re-review
- verification completeness

暫定役割:

**Light Reviewer**

---

# 10. Human Decisionの扱い

開始時は多くのstage transitionでhuman approvalを求めていた。

WP-2途中の実験から、

> **Human involvement is for semantic decisions, not routine confirmation of rule-decidable stage progression.**

という恒久ルールへ進化した。

以後、

- CI PASS
- dependency complete
- Blocker/Major 0
- exact target一致
- ruleから一意に決まるnarrow fix

などはAI Coordinatorが進められるようになった。

Humanへ残したもの:

- 複数の合理案が残るsemantic choice
- product/API/security/Auditの新しい意味判断
- ADR変更
- scope変更
- final product merge
- release Go / No-Go

## 評価

**大きな成功。**

WP-2はSecurity/Audit機能を作っただけでなく、
Humanの役割を「工程承認者」から「意味判断者」へ狭める実験に成功した。

---

# 11. Control / Evidence設計の評価

## 強かった点

- exact SHA
- Single Current Authority
- Write Preflight
- direct main write禁止
- Transition Bundle
- Fresh Context
- prompt-is-not-authority
- Primary evidence再確認
- Targeted fix / Targeted re-review
- Final gate

これらにより、
AI Agentが古い状態や別branchを暗黙に扱う危険をかなり抑えた。

## 問題点

**記録量が非常に多い。**

GitHub commentsへ、

- stage evidence
- Current Authority
- handoff
- review result
- fix authority
- targeted rereview

を多数materializeしたため、
人間が追跡するには重い。

WP-2ではR&D証拠として価値が高かったが、
通常運用では圧縮すべき。

## 評価

- Safety / Auditability: 非常に高い
- Human readability / simplicity: 改善余地大

---

# 12. WP-2の弱点

成功したWPではあるが、改善すべき点も明確。

## 12.1 Agent実行数が多い

3 Candidate + Final Synthesis + Light + H1 + H2 + rereviewは高コスト。

通常leafへそのまま適用すべきではない。

---

## 12.2 Control metadataが重い

安全性は高いが、
Current Authority / Transition Bundle / stage commentsが増えすぎた。

機械処理向けの情報と、
Humanが読む情報を分ける余地がある。

---

## 12.3 Actual Model identity記録が一部不統一

planned modelとactual modelが異なるケースがあった。

例:

- AUTHZ C2 Luna予定 → DeepSeek V4 Flash
- OPR-CREATE C3 old branch naming → actual Grok 4.6
- reviewer assignmentとactual executionのずれ

今後はstage resultで、

`ACTUAL_MODEL / ACTUAL_HARNESS`

を必須にすべき。

---

## 12.4 Human transportが残った

Routine approvalは削減できたが、

- copy/paste
- agent launch
- session transport

はまだManual。

process logicはかなり自律化したが、
execution transportはまだ完全自動ではない。

---

# 13. 最終成果

最終的にapproved WP-2 leaf #164〜#171はすべてclosed/completed。

current main:

`43f22577781ac052a6fba8eb456438ab82a3703b`

独立WP-2 completion review:

`Security and Audit Ready: PASS`

WP-3へ進める状態になった。

これは単なる「8 IssueをCloseした」ではなく、

- Authentication
- Authorization
- Audit
- Operator identity/lifecycle
- DB security boundary
- security-relevant concurrency
- fail-closed behavior
- semantic verification

がWork Packageとして横断的に成立したことを独立Gateで確認した結果である。

---

# 14. 評価スコアカード

| 評価項目 | 評価 | コメント |
|---|---|---|
| Product goal達成 | **A** | 8 leaf完了、最終Gate PASS |
| Security/Audit品質 | **A** | Heavy Reviewが実欠陥を複数検出・修正 |
| Issue decomposition | **A** | Rolling WaveとIssue Set Reviewが有効 |
| Scope control | **A** | process/security remediation/productを分離 |
| Evidence / traceability | **A** | 非常に強い |
| Model R&D成果 | **A** | 実装・レビュー双方で役割差を観測 |
| Human decision削減 | **A-** | rule-decidable progressionを恒久化 |
| Process efficiency | **C+** | R&Dとして意図的に重い |
| Human readability | **B-** | control comment量が多い |
| 今後のscalability | **B** | 簡略化すれば高い、現状そのままでは重い |

## 総合

### R&Dとして

**A / 非常に成功**

### 通常開発プロセスとしてそのまま採用

**B- / 簡略化が必要**

---

# 15. WP-3へ持っていくべきもの

## 残す

- Rolling Wave
- Issue Ready
- Single primary responsibility
- Fresh independent review
- exact target / SHA確認
- CI
- Critical Mutation（高リスク時のみ）
- Targeted fix / rereview
- Human Decision Escalation
- final merge Human approval
- risk-based Heavy Review

## 減らす

- 毎回3 Candidate
- 毎回H2
- routine human stage approval
- 重複するcontrol comments
- model比較そのものを目的とした実行

## WP-3標準案

```text
Issue Ready
   ↓
1 Primary Builder
   ↓
CI
   ↓
Independent Light Review
   ↓
riskあり？
 ├─ No → Final Approval
 └─ Yes
       ↓
     Heavy Review
       ↓
     必要ならNarrow Fix / Targeted Re-review
```

Candidateは、

- 通常: 1
- 設計差がある: 2
- 未知 / 高リスク / R&D: 3

とする。

---

# 16. 最終評価

WP-2で最も評価できる点は、

> **Security and Auditを実装したことだけではない。**

3 Candidate、複数Reviewer、Critical Mutation、Heavy Review、Human Decision pilotを実際に回したことで、

> **「AI Agentをどう組み合わせれば品質を保てるのか」**

について実測データを得たことが最大の成果である。

一方で、同じ工程をWP-3へそのまま持ち込むのは過剰。

WP-2は意図的に厚いR&D工程だったため、
次はそこで得た知見を使ってAgent数とstage数を減らすべき。

したがって最終評価は:

> **WP-2はR&Dとして成功。  
> 次の成功条件は、この品質を保ったまま工程を軽くできるか。**

これがWP-3で検証すべき中心テーマである。

---

# 参考GitHub一次証拠

- Work Package control: Issue #34
- Phase 4 issue decomposition plan: `docs/plans/phase-4-implementation-issue-decomposition.md`
- Process Baseline Sync: Issue #172
- Pre-existing dependency security remediation: Issue #174
- WP-2 leaf issues: #164〜#171
- Rule-Decidable Stage Progression: Process Issue #209 / `docs/retrospectives/rule-decidable-stage-progression-default.md`
- Final WP-2 main: `43f22577781ac052a6fba8eb456438ab82a3703b`
- Final gate: `WP2_SECURITY_AND_AUDIT_READY_GATE_RESULT = PASS`
