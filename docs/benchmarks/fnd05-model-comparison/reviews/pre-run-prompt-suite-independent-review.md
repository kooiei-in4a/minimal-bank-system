# FND-05 Pre-Run Prompt Suite — Independent Review

```yaml
DOCUMENT_STATUS: "PLACEHOLDER — REPLACE WITH COMPLETED REVIEW"
REVIEW_TARGET_PR: 145
REVIEW_TARGET_HEAD: "57df6ae1a30ac23151fbcd707f191f5d26dba029"
REVIEW_MANIFEST: "pre-run-prompt-suite-review-manifest.md"
REVIEWER_MODEL: "<MODEL>"
REVIEWER_HARNESS: "<HARNESS>"
REVIEWER_EFFORT: "<EFFORT>"
REVIEWER_SLUG: "<SLUG>"
```

このファイル全体を、独立reviewの完成結果へ置き換えてください。

Target 22 filesは変更しません。

## Required report structure

### 1. Executive Verdict

```text
VERDICT: READY_FOR_LOCK_WORK / FIX_REQUIRED / REDESIGN_REQUIRED / BLOCKED
BLOCKER_COUNT:
MAJOR_COUNT:
MINOR_COUNT:
NIT_COUNT:
TARGET_HEAD_VERIFIED:
```

### 2. Target Verification

- repository
- PR #145
- base / head full SHA
- changed-file count
- exact 22-file scope
- Draft / OPEN state

### 3. Reference Review

Target suiteを読む前に固定した、Issue #43 / ADR / AGENTS.mdからのReferenceを記載する。

### 4. Fixed Policy Assessment

Kooが固定したpolicyがsuite全体で一貫しているか評価する。好みによる再設計はしない。

### 5. Cross-File Consistency Matrix

| Contract / Identity | Source of truth | Referencing files | Result | Gap |
| --- | --- | --- | --- | --- |

最低限:

- model / harness configuration
- no OpenCode
- no separate Formal Self-Review / H1
- process stage order
- Light / Heavy responsibility
- Heavy non-goals
- Judge triggers
- re-review scope
- revision IDs
- D-01〜D-08
- metrics / gates

### 6. File-by-File Assessment

22 filesすべてについて、最低限次を記録する。

| File | Role | Clarity | Consistency | Executability | Required change |
| --- | --- | --- | --- | --- | --- |

評価は`PASS / MODIFY / REDESIGN`を使用する。

### 7. Findings

Finding形式:

```text
ID:
SEVERITY: Blocker / Major / Minor / Nit
CATEGORY:
AFFECTED_FILES:
ROOT_CAUSE:
PROBLEM:
FAILURE_OR_CONFUSION_PATH:
IMPACT:
EVIDENCE:
RECOMMENDED_CHANGE:
CROSS_FILE_UPDATES:
FIXED_POLICY_AFFECTED: YES / NO
```

同じroot causeを複数Findingへ分割しない。

### 8. Role Separation Assessment

- Static vs Composer
- Composer vs Luna
- Light vs Heavy
- Sol vs Opus
- Heavy non-goals and exceptions
- Heavy budget / re-review
- Conditional Judge

Heavyの除外項目がblind spotを作る場合は、具体的なfailure pathで示す。

### 9. Self-Review Replacement Assessment

- implementation Completion Checksの十分性
- separate SRを廃止してもquality gateが成立するか
- implementation promptの負荷
-自己正当化 / checklist theater / evidence launderingのrisk

### 10. Project Rule Catalog Assessment

- enforceability
- placement rules
- static ownership
- reviewer ownership
- false positive / false negative risk
- design contractとの重複・矛盾

### 11. Mutation and Test-Oracle Assessment

M-01〜M-10について:

| Mutation | Defect class valid | Expected RED reliable | False-positive risk | Execution cost | Required change |
| --- | --- | --- | --- | --- | --- |

### 12. Open Decision Assessment

D-01〜D-08について:

| Decision | Correctly open | Evidence required is sufficient | Candidate leakage | Missing dependency | Required change |
| --- | --- | --- | --- | --- | --- |

未確定値を推測で埋めない。

### 13. Simplification Opportunities

- 削除可能な重複
- 共通block化できる記述
- single source化すべき値
- promptが長いために重要事項が埋没する箇所

削減により失われないcontractを説明する。

### 14. Consolidated Change Plan

修正順にまとめる。

```text
P0 — Blocker / Major
P1 — Minor before lock
P2 — Optional polish
```

各項目へaffected filesとdependencyを付ける。

### 15. Exact Rewrite Proposals

必要な箇所だけ、copy-paste可能なreplacement textまたはpseudodiffを提示する。

22 filesの全文再掲はしない。

### 16. KEEP / MODIFY / DROP

- KEEP
- MODIFY
- DROP
- ADD

### 17. Final Lock Recommendation

次に進める工程を明記する。

- D-01〜D-08 lockへ進める
- prompt suite修正後に再review
-根本再設計
- evidence不足でblocked

### 18. Operation Confirmation

```text
TARGET_FILES_CHANGED: NO
TARGET_PR_CHANGED: NO
ISSUE_CHANGED: NO
FND05_IMPLEMENTATION_STARTED: NO
OUTPUT_FILE_ONLY: YES
```

### 19. Final one-line assessment

1文で結論を書く。
