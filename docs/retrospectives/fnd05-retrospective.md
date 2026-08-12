# FND-05 Retrospective

## Status

RETROSPECTIVE: IN PROGRESS

FND-05 product implementation, candidate archive, and pre-retrospective repository cleanup are complete.

This document is the working record for the FND-05 retrospective.

No FND-06 process change is authorized by this document alone.

---

## 1. Retrospective Scope

この振り返りで確認する対象を記載する。

現時点では評価を行わない。

- FND-05 product implementation
- candidate implementation / evaluation
- Selection / Adjudication
- Final Synthesis
- Light Review
- Heavy Review
- Conditional Judge
- targeted fix / targeted re-review
- artifact / SHA / handoff management
- repository cleanup / archive
- operator workload / process complexity

---

## 2. Fixed Facts / Timeline

GitHub一次証拠から確認する事実を後で整理するための領域。

現時点では主要な確定identityのみ記録する。

- Issue #43
- Final Synthesis PR #153
- Final Product Head
- Final Merge Commit
- candidate archive
- final post-merge state

詳細な評価や解釈はまだ書かない。

---

## 3. What Worked Well

TBD

---

## 4. What Did Not Work Well

TBD

---

## 5. Quality Gain vs Process Cost

TBD

以下を後で比較する。

- 品質向上に実際に寄与した工程
- 同じ品質を得るために簡略化できそうな工程
- 重複していたreview / handoff / verification
- 人間の判断負荷
- prompt / artifact / branch数
- STOP / reworkの原因

---

## 6. Candidate / Review Lessons

TBD

candidate比較やreview funnelから得られた知見を後で整理する。

---

## 7. Operational Observation Review

FND-05実行中に記録されたnon-normative Observation Ledgerを、
retrospectiveで個別に評価する。

Observation Ledger:

`docs/retrospectives/fnd05-operational-observations.md`

各Observationは後で次のいずれかに分類する。

- ADOPT
- DEFER
- REJECT

現時点では採否を決定しない。

### O-01 — Execution prompt + handoff contract

STATUS: UNREVIEWED

### O-02 — Model identity authority

STATUS: UNREVIEWED

### O-03 — Non-normative Observation Ledger

STATUS: UNREVIEWED

### O-04 — Exact Git blob handoff hash verification

STATUS: UNREVIEWED

### O-05 — Artifact production commit / registry lock commit separation

STATUS: UNREVIEWED

### O-06 — Just-in-Time Spec / CI Rule Check experiment

STATUS: UNREVIEWED

### O-07 — Windows / WSL Git EOL contract

STATUS: UNREVIEWED

必要に応じてLedgerに存在する追加Observationも、
GitHub一次証拠を確認したうえでこのsectionへ追加する。

---

## 8. Keep / Simplify / Remove

最終的にFND-05のprocessを以下へ分類する。

### KEEP

TBD

### SIMPLIFY

TBD

### REMOVE

TBD

---

## 9. Candidate Improvements for FND-06

TBD

重要:

ここには最初から改善案を大量に入れない。

FND-05 retrospectiveの結果として、
FND-06で実際に試す変更を少数へ絞る。

---

## 10. Decisions

まだ決定しない。

```yaml
RETROSPECTIVE_DECISIONS:
  STATUS: NOT_YET_DECIDED

FND06_PROCESS_CHANGES:
  STATUS: NOT_AUTHORIZED
```

---

## 11. Next Step

次工程はCoordinatorとKooが振り返り方法を確定してから開始する。

この初期workspace作成だけでは、
Observation採否やFND-06変更を開始しない。
