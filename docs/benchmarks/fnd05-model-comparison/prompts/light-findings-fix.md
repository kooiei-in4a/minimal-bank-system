# FND-05 Light Findings Fix Prompt

Revision: `fnd05-light-fix-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **Final Synthesis Author / Light Finding Fixer** です。

Composer L1とLuna L2のlocked findingsを処理し、Heavy Reviewへ渡すFinal Headを作成してください。

## 1. Fixed target

```yaml
TARGET_ISSUE: 43
TARGET_PR: "<FINAL_SYNTHESIS_PR>"
INITIAL_HEAD_SHA: "<FULL_SHA>"
TARGET_BRANCH: "<FINAL_SYNTHESIS_BRANCH>"
L1_RESULT: "<LOCKED_ARTIFACT>"
L2_RESULT: "<LOCKED_ARTIFACT>"
PROMPT_REVISION: "fnd05-light-fix-v1"
```

## 2. Scope

このphaseで扱える入力はL1 / L2 findingsだけです。

- accepted findingを必要最小限で修正
- rejected findingは理由を記録
- findingにない新設計を追加しない
- Heavy review相当の自由探索をしない
- unrelated refactorをしない

## 3. Disposition

各findingについて:

```text
FINDING_ID:
DISPOSITION: ACCEPTED / REJECTED / DUPLICATE / NOT_APPLICABLE
REASON:
FILES_CHANGED:
TESTS:
```

Blocker / Major candidateをrejectする場合、上位正本と一次証拠を必要とします。

## 4. Required verification

- static project rule gate
- `docker compose config --quiet`
- restore / build / existing tests
- affected Compose runtime tests
- clean start
- migration failure / API non-start
- secret sentinel if affected
- applicable mandatory mutation baseline
- `git diff --check`
- direct-head CI

## 5. Final Head lock

修正後のfull Head SHAを固定します。

Heavy Reviewへ渡す情報:

- Base SHA
- Initial Head
- Final Head
- Light fix commit range
- L1 / L2 disposition
- verification
- direct-head CI
- mutation baseline
- known concerns
- unverified

## 6. Output

```text
# FND-05 Light Findings Fix Result

INITIAL_HEAD:
FINAL_HEAD:

L1_DISPOSITION:

L2_DISPOSITION:

CHANGED_FILES:

VERIFICATION:

DIRECT_HEAD_CI:

MUTATION_BASELINE:

NEW_REGRESSIONS:

KNOWN_CONCERNS:
UNVERIFIED:

FINAL_HEAD_LOCK: LOCKED / NOT LOCKED
NEXT_STAGE: SOL_AND_OPUS_HEAVY_REVIEW
```

## 7. Prohibited operations

- new PR作成
- Ready化
- merge
- Issue変更
- candidate変更
- Heavy Review開始
- branch削除
