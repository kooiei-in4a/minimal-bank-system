# FND-05 Issue Ready Gate Review Prompt

Revision: `fnd05-issue-ready-review-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-05 Pre-Run Gate Reviewer** です。

この作業はReview-onlyです。Issue、branch、PR、docsを変更しないでください。

## 1. Target

```yaml
TARGET_ISSUE: 43
PREPARATION_PR: "<PR>"
PREPARATION_HEAD: "<FULL_SHA>"
EXPECTED_MAIN_SHA: "<FULL_SHA>"
PROMPT_REVISION: "fnd05-issue-ready-review-v1"
```

## 2. Authority

- Parent Issue #3
- WP-1 Issue #33
- Issue #43
- `AGENTS.md`
- ADR-0001 / 0008 / 0009
- FND-04 merge / close evidence
- FND-05 pre-run files

## 3. Required checks

### Dependency / gate

- #42 COMPLETE / MERGED
- current main contains FND-04 final implementation
- WP-1 / Implementation Ready state is consistent
- Issue #43 Scope / AC / stop conditions are current

### Design lock

- assumption ledger `TO_LOCK` = 0
- implementation / test design contract locked
- secret design fixed
- image digests fixed
- lifecycle commands fixed
- failure injection fixed
- API ordering observation fixed

### Process lock

- candidate 3 fixed
- OpenCode 0
- independent Formal Self-Review 0
- Light 2 fixed
- Heavy 2 fixed
- Heavy non-check lists fixed
- Judge conditional
- scoring fixed
- prompts fixed

### Experiment identity

- common base full SHA fixed
- candidate branch names fixed
- 3 branches created from same SHA
- 3 / 3 Heads identical to common base
- exact model / harness / effort verified
- candidate output / PR diff 0 before execution

### Safety / scope

- health / business / backup / production deployment先取りなし
- secret / credential未保存
- no candidate execution started

## 4. Verdict

### PASS

全必須項目が一次証拠で確認でき、implementationを開始できる。

### FAIL

未解決項目があり、implementation禁止を維持する。

### BLOCKED

外部tool / access / dependencyにより検証不能。

## 5. Output

```text
# FND-05 Issue Ready Gate Review

TARGET_VERIFICATION:

DEPENDENCY_GATE:
DESIGN_LOCK:
PROCESS_LOCK:
EXPERIMENT_IDENTITY:
SAFETY_SCOPE:

OPEN_ITEMS:

VERDICT: PASS / FAIL / BLOCKED
IMPLEMENTATION_PERMITTED: YES / NO

REQUIRED_ACTIONS:

OPERATION_CONFIRMATION:
- Issue changed: NO
- code changed: NO
- branch changed: NO
```

PASSでもcandidate実行はKooの明示開始指示まで行いません。
