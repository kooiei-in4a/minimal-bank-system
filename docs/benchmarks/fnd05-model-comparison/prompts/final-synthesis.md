# FND-05 Curated Final Synthesis Prompt

Revision: `fnd05-final-synthesis-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-05 Final Synthesis Author / Agent A** です。

候補比較とSelection / Adjudicationで固定された要素を用い、current mainから製品用実装を新規構築してください。

## 0. Identity

```yaml
MODEL: "<EXACT_PRODUCT_LABEL>"
HARNESS: "<HARNESS>"
EFFORT: "<EFFORT>"
ROLE: "FND-05 Final Synthesis Author"
TARGET_ISSUE: 43
BASE_BRANCH: main
BASE_SHA: "<CURRENT_MAIN_FULL_SHA>"
TARGET_BRANCH: "<PRECREATED_FINAL_BRANCH>"
TARGET_PR: <PRECREATED_DRAFT_PR>
EVALUATION_REVISION: "<LOCKED>"
SELECTION_REVISION: "<LOCKED>"
PROMPT_REVISION: "fnd05-final-synthesis-v1"
```

Default author候補はfresh-context GPT-5.6 Luna / Codexですが、実行前にKooがexact identityを固定します。

## 1. Authority

1. Approved specification
2. ADR-0001 / 0008 / 0009
3. Issue #43
4. `AGENTS.md`
5. FND-05 pre-run design / rules / mutation
6. locked Implementation Evaluation
7. locked Selection / Adjudication

candidateのPR説明やscoreは上位正本を変更しません。

## 2. Prohibited integration methods

- candidate branchをmergeしない
- candidate commitをcherry-pickしない
- candidate PRをReady / mergeしない
- candidate benchmark artifactを変更しない
- rankingを変更しない

current mainから必要な実装を再構成します。

## 3. Task

Selectionで採用された要素を必要十分に統合します。

最低限:

- root `compose.yaml`
- API / Migrator Dockerfile
- `.dockerignore`
- secret injection support
- canonical lifecycle documentation
- Compose production-path tests / validators
- clean start / failure / rerun / reset
- project rule tests
- mandatory mutation-sensitive test oracle

## 4. Completion Checks

Candidate implementation promptのC-01〜C-11をすべて実行します。

独立Formal Self-Review phaseはありません。

### Additional Final Synthesis checks

- [ ] Selectionの採用要素を全件反映
- [ ] reject patternを持ち込んでいない
- [ ] candidate-specific workaroundを持ち込んでいない
- [ ] Final diffがIssue #43へ限定
- [ ] static project rule gate PASS
- [ ] mandatory mutation baseline GREEN
- [ ] M-01〜M-10でtarget RED
- [ ] restore後GREEN / residue 0
- [ ] direct-head CI SUCCESS
- [ ] merge-ref CI identityを分離記録

## 5. Mandatory mutation execution

`reference/mandatory-mutations.md`のM-01〜M-10を原則すべて実行します。

各mutationについて記録:

```text
MUTATION_ID:
BASELINE_RESULT:
INJECTED_CHANGE:
MUTATED_RESULT:
EXPECTED_RED_OBSERVED:
FAILURE_REASON_MATCHED:
RESTORED_RESULT:
RESIDUE:
```

実行不能なmutationを成功扱いしません。Issue / contractの変更が必要なら停止します。

## 6. Required verification

- Compose version / Engine version
- `docker compose config --quiet`
- resolved config / services / images / volumes
- restore
- build 0 warnings / 0 errors
- existing tests
- clean start
- Migrator exit / timestamps
- API state / timestamps
- migration history
- migration failure / API non-start
- existing-volume rerun
- stop / start / restart
- down retain data
- clean reset / resource absence
- secret sentinel
- API no-auto-migration
- scope scan
- `git diff --check`
- direct-head CI
- PR merge-ref CI

## 7. Light review handoff

この実行ではHeavy Reviewへ進みません。

Final Synthesis initial Headを固定し、次へ渡します。

- L1 Composer Project Quality Review
- L2 Luna Contract Conformance Review

PR本文へLight reviewerが必要とするevidence indexを記載してください。

## 8. Duration

```text
STARTED_AT_LOCAL:
FINISHED_AT_LOCAL:
DURATION_MINUTES:
```

## 9. Final report

```text
## FND-05 Final Synthesis Initial Result

AUTHOR_MODEL:
AUTHOR_HARNESS:
AUTHOR_EFFORT:
BASE_SHA:
INITIAL_HEAD_SHA:
DRAFT_PR:

SELECTION_APPLICATION:
- adopted:
- rejected:
- mandatory guards:

CHANGED_FILES:

RUNTIME_DESIGN:
SECRET_DESIGN:
LIFECYCLE_DESIGN:
TEST_DESIGN:

COMPLETION_CHECKS:

MUTATION_RESULTS:
- M-01:
...
- M-10:

VERIFICATION:

DIRECT_HEAD_CI:
MERGE_REF_CI:

KNOWN_CONCERNS:
UNVERIFIED:
STARTED_AT_LOCAL:
FINISHED_AT_LOCAL:
DURATION_MINUTES:

INITIAL_HEAD_LOCK: LOCKED / NOT LOCKED
NEXT_STAGE: LIGHT_REVIEW
```

## 10. Operation permissions

- Final Synthesis branch以外へpushしない
- new PRを作らない
- PRをReady化しない
- mergeしない
- Issueをcloseしない
- candidateを変更しない
- Heavy Reviewを開始しない
