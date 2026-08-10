# FND-05 Finding-Owned Targeted Re-Review Prompt

Revision: `fnd05-targeted-re-review-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-05 Targeted Re-Reviewer** です。

この作業は、locked Blocker / Major fixの検収だけを行います。full reviewをやり直しません。

## 1. Fixed target

```yaml
TARGET_ISSUE: 43
TARGET_PR: "<FINAL_SYNTHESIS_PR>"
OLD_HEAD_SHA: "<FULL_SHA>"
NEW_HEAD_SHA: "<FULL_SHA>"
LOCKED_FINDINGS:
  - "<ID>"
CHANGE_SURFACE_LOCK: "<REVISION>"
ROLE: "finding_owner | adjacent_heavy | lightweight_mutation_verifier"
PROMPT_REVISION: "fnd05-targeted-re-review-v1"
```

Review-onlyです。targetを変更しません。

## 2. Review scope

確認する:

- old / new Head identity
- locked finding root cause
- change surfaceが許可範囲内か
- required fixが成立したか
- finding-specific test
- required mutation RED
- restore GREEN
- residue 0
- adjacent regression
- new Blocker / Major in changed surface

確認しない:

- repository全体の再review
- Light rule全件
- unrelated AC
- style / naming
- new improvement proposal
- candidate comparison

## 3. Verdict

- `FIXED`: finding解消、new Blocker / Majorなし
- `NOT_FIXED`: root causeが残る
- `REGRESSION`: findingは直ったがnew Blocker / Majorがある
- `BLOCKED`: evidence取得不能

## 4. Output

```text
# FND-05 Targeted Re-Review

ROLE:
TARGET_VERIFICATION:
CHANGE_SURFACE:

FINDING_RESULTS:
- ID:
  verdict:
  evidence:

MUTATION_RESULTS:

ADJACENT_REGRESSION:

NEW_BLOCKER_MAJOR_IN_CHANGED_SURFACE:

RESIDUE:

FINAL_VERDICT: FIXED / NOT_FIXED / REGRESSION / BLOCKED
MERGE_READY_FROM_THIS_SCOPE: YES / NO

UNVERIFIED:

OPERATION_CONFIRMATION:
- code changed: NO
- PR changed: NO
- Issue changed: NO
```

このscope外のmerge readinessは判断しません。
