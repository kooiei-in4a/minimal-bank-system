# FND-05 Selection / Adjudication Prompt

Revision: `fnd05-selection-adjudication-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-05 Technical Selection Lead** です。

この作業はReview-onlyです。candidate、branch、PR、Issueを変更してはいけません。

## 1. Inputs

- locked Implementation Evaluation
- exact C1 / C2 / C3 Heads
- common base
- Issue #43 / ADR / design contract
- project rule catalog
- mandatory mutations

## 2. Purpose

単純なwinner mergeではなく、要素単位でFinal Synthesisの入力を固定します。

選択対象:

- Compose topology / ordering
- Dockerfile / image design
- secret injection
- lifecycle commands
- external observation
- failure injection
- test oracle
- mutation sensitivity
- operations documentation

## 3. Rules

- candidate branch merge禁止
- cherry-pick禁止
- score 1位の全要素自動採用禁止
- Scope先取りを採用理由にしない
- code量を採用理由にしない
- PR説明を一次証拠にしない
- rejected patternを明示する
- Final Synthesis required guardをtestableな形で固定する

## 4. Decision format

各要素について:

```text
ELEMENT:
PRIMARY_SOURCE:
PARTIAL_SOURCE:
REJECTED_SOURCES:
DECISION:
RATIONALE:
REQUIRED_TEST:
REQUIRED_MUTATION:
SCOPE_EFFECT:
```

`PRIMARY_SOURCE`は設計参考元であり、そのcandidate codeをmerge / copyする許可ではありません。

## 5. Required decisions

### S-01 Service topology

- services
- dependencies
- health / successful completion conditions

### S-02 Docker images

- Dockerfile structure
- build targets
- digest policy
- runtime user

### S-03 Secret injection

- source
- container mount
- entrypoint behavior
- least grant
- sentinel evidence

### S-04 Lifecycle

- validate
- clean start
- stop / start
- restart
- down
- clean reset

### S-05 Runtime evidence

- state
- exit code
- timestamps
- migration history
- logs

### S-06 Failure injection

- invalid credential
- migration failure
- no production backdoor

### S-07 Test oracle

- intended path marker
- failure reason / state marker
- no `exit != 0` only

### S-08 Mutation

M-01〜M-10のFinal Synthesis requirementを固定する。

### S-09 Scope

health / business / backup / production deployment非混入を固定する。

## 6. Output

```text
# FND-05 Selection / Adjudication

INPUT_LOCK:

PRIMARY_ARCHITECTURE_SOURCE:

ELEMENT_DECISIONS:
- S-01:
...
- S-09:

REJECT_PATTERNS:

FINAL_SYNTHESIS_REQUIRED_GUARDS:

MANDATORY_MUTATIONS:

FINAL_SYNTHESIS_AUTHOR_CONSTRAINTS:

CANDIDATE_MERGE: PROHIBITED
CANDIDATE_CHERRY_PICK: PROHIBITED

SELECTION_LOCK:
- revision:
- candidate Heads:
```

Final Synthesis実装へ進まず、locked inputを作成して停止してください。
