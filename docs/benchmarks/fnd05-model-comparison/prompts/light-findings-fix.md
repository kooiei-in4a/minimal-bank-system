# FND-05 Light Findings Fix Prompt

Revision: `fnd05-light-fix-v2`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **Final Synthesis Author / Light Finding Fixer** です。

Composer L1とLuna L2のlocked findingsを処理し、Heavy Reviewへ渡すFinal Headを作成してください。

## 1. Fixed target / artifact identity

```yaml
TARGET_ISSUE: 43
TARGET_PR: "<FINAL_SYNTHESIS_PR>"
INITIAL_HEAD_SHA: "<FULL_SHA>"
TARGET_BRANCH: "<FINAL_SYNTHESIS_BRANCH>"
L1_ARTIFACT_PATH: "<PATH>"
L1_ARTIFACT_SHA256: "<SHA256>"
L2_ARTIFACT_PATH: "<PATH>"
L2_ARTIFACT_SHA256: "<SHA256>"
RUN_REGISTRY_SHA: "<RUN_JSON_SHA256>"
PROMPT_REVISION: "fnd05-light-fix-v2"
```

`run.json.stage_artifacts.light_l1/light_l2`とtarget Headが一致しない場合は停止してください。

## 2. Scope

このphaseで扱える入力はL1 / L2 findingsだけです。

- accepted findingを必要最小限で修正
- rejected findingは上位正本と一次証拠で理由を記録
- findingにない新設計を追加しない
- Heavy review相当の自由探索をしない
- unrelated refactorをしない

## 3. Disposition

各finding:

```text
FINDING_ID:
SOURCE: L1 / L2
SEVERITY_CANDIDATE:
DISPOSITION: ACCEPTED_FIXED / REJECTED / DUPLICATE / NOT_APPLICABLE / UNRESOLVED / ESCALATED
REASON:
EVIDENCE:
FILES_CHANGED:
TESTS:
```

Blocker / Major candidateをREJECTEDにしても解消済み扱いにしません。

## 4. Required Heavy handoff

```text
HEAVY_HANDOFF:
  resolved_and_verified_findings:
  rejected_or_unresolved_blocker_major_candidates:
  escalated_blocker_major_candidates:
  evidence_incomplete_findings:
```

REJECTED / UNRESOLVED / ESCALATED Blocker・Major candidateは、該当Heavy reviewerのprimary scopeに入る場合、Heavyが独立再確認します。

## 5. Required verification

- static project rule gate
- D-01でlockしたCompose/config validation
- restore / build / existing tests
- affected runtime tests
- clean start / migration failure / API non-start
- secret sentinel if affected
- mutation baseline if affected
- `git diff --check`
- direct-head CI

## 6. Final Head lock

修正後のfull Head SHAを固定し、次のartifact identityを`run.json.stage_artifacts.light_fix`へ記録する。

```text
ARTIFACT_LOCK:
  stage: light_fix
  artifact_path:
  content_sha256:
  prompt_revision: fnd05-light-fix-v2
  target_head_sha:
  source_artifact_refs:
  producer_slot:
  producer_commit_sha:
STATUS: LOCKED
```

## 7. Output

```text
# FND-05 Light Findings Fix Result

INITIAL_HEAD:
FINAL_HEAD:

L1_DISPOSITION:
L2_DISPOSITION:
HEAVY_HANDOFF:

CHANGED_FILES:
VERIFICATION:
DIRECT_HEAD_CI:
MUTATION_BASELINE:
NEW_REGRESSIONS:
KNOWN_CONCERNS:
UNVERIFIED:

ARTIFACT_LOCK:
FINAL_HEAD_LOCK: LOCKED / NOT_LOCKED
NEXT_STAGE: SOL_AND_OPUS_HEAVY_REVIEW
```

## 8. Prohibited operations

- new PR作成
- Ready化 / merge
- Issue変更
- candidate変更
- Heavy Review開始
- branch削除
