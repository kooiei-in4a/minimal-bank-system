# FND-05 Curated Final Synthesis Prompt

Revision: `fnd05-final-synthesis-v2`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-05 Final Synthesis Author / Agent A** です。

Selection / Adjudicationで固定された要素を用い、current mainから製品用実装を新規構築してください。

## 0. Identity / locked inputs

```yaml
MODEL: "<D-08_LOCKED_EXACT_PRODUCT_LABEL>"
HARNESS: "<D-08_LOCKED_HARNESS>"
EFFORT: "<D-08_LOCKED_EFFORT>"
ROLE: "FND-05 Final Synthesis Author"
TARGET_ISSUE: 43
BASE_BRANCH: main
BASE_SHA: "<CURRENT_MAIN_FULL_SHA>"
TARGET_BRANCH: "<PRECREATED_FINAL_BRANCH>"
TARGET_PR: <PRECREATED_DRAFT_PR>
EVALUATION_ARTIFACT_PATH: "<PATH>"
EVALUATION_ARTIFACT_SHA256: "<SHA256>"
SELECTION_ARTIFACT_PATH: "<PATH>"
SELECTION_ARTIFACT_SHA256: "<SHA256>"
RUN_REGISTRY_SHA: "<RUN_JSON_SHA256>"
MUTATION_DETERMINISM_REVISION: "fnd05-mutation-determinism-v1"
PROMPT_REVISION: "fnd05-final-synthesis-v2"
```

D-08が未lock、またはModel / Harness / Effortが`run.json`と一致しない場合は停止する。Default authorを推測しない。

## 1. Authority

1. Koo-approved product policy / approved specification
2. ADR-0001 / ADR-0008 / ADR-0009
3. Issue #43
4. `AGENTS.md`
5. locked D-01〜D-08 / FND-05 contracts
6. locked Evaluation / Selection artifacts

Artifact path / sha256 / source Headsが`run.json.stage_artifacts`と一致しなければ停止する。

## 2. Prohibited integration methods

- candidate branch merge禁止
- candidate commit cherry-pick禁止
- candidate PR Ready / merge禁止
- candidate benchmark artifact変更禁止
- ranking変更禁止

Current mainから必要な実装を再構成する。

## 3. Task — Selectionのobservable contractを統合

Selectionで採用された**behavior / evidence / guard**を必要十分に統合する。

Exact service name、file placement、Compose condition等は、D-01〜D-08またはSelectionで正当にlockされたものだけをMUSTとして扱う。

Final Synthesisは少なくとも次を証明する。

- PostgreSQL usable → explicit Migrator → API
- Migrator success後だけAPI start
- Migrator failure時API never-start
- API no-auto-migration
- D-02 image identities
- named volume
- D-03 secret contract
- D-04 lifecycle contract
- D-05 external state evidence
- D-06 failure injection + deterministic mutation preconditions / barriers / signatures
- D-07 portability
- Issue #43 Scope boundary

## 4. Completion Checks

Candidate prompt v2のC-01〜C-08相当を、Final Synthesisの実Headで一次証拠付きで実行する。

独立Formal Self-Review phaseは作らない。

Additional:

- Selection採用要素を全件反映
- reject patternを持ち込んでいない
- candidate-specific workaroundを無批判に持ち込んでいない
- Static gate PASS
- mandatory mutation baseline GREEN
- applicable mutationごとにdeterministic precondition PASS
- M-01〜M-10 target RED for expected failure signature
- invalid failure signatureをkillに数えていない
- restore GREEN / residue 0
- direct-head CI SUCCESS
- merge-ref CIはdirect-headと区別してidentity記録

## 5. Mandatory mutation execution

`mandatory-mutations.md` v2と`mutation-determinism-contract.md` v1に従いM-01〜M-10を原則すべて実行する。

特に:

- M-01はcontrolled barrierでMigrator successful completionを未完了へ保持してからorderingを弱め、自然raceに依存しない
- M-03はauto-migrationが存在すれば必ずobservable migration-state deltaが出るDB preconditionを成立させる
- M-07はoracle-quality meta mutation
- M-08は**testを壊さず、Migrator exit 0 + expected migration未適用**を作るruntime defect
- M-10はcleanup対象のsame-project resourceがmutation前に実在することを確認する

各mutation:

```text
MUTATION_ID:
CLASS:
PRECONDITION_RESULT:
CONTROLLED_BARRIER_OR_FIXTURE:
INJECTION_POINT_CLASS:
BASELINE_RESULT:
INJECTION_ARTIFACT_REF:
EXPECTED_FAILURE_SIGNATURE:
OBSERVED_FAILURE_SIGNATURE:
INVALID_FAILURE_MATCHED: YES / NO
MUTATED_RESULT:
EXPECTED_RED_OBSERVED:
FAILURE_REASON_MATCHED:
RESTORED_RESULT:
CLEANUP_RESULT:
RESIDUE:
```

`PRECONDITION_RESULT != PASS`の場合は`BLOCKED — PRECONDITION NOT ESTABLISHED`としてKILLED / SURVIVEDへ数えない。`INVALID_FAILURE_MATCHED = YES`またはfailure signature不一致もkillではない。

## 6. Required verification

D-01〜D-07でlockしたexact command / evidence methodを使用する。

最低限:

- Engine / Compose identity
- config validation
- restore / build / existing tests
- clean start
- Migrator exit / completion evidence
- API state / start ordering evidence
- migration history
- failure / API non-start
- existing-volume rerun
- D-04 lifecycle / reset
- secret sentinel
- API no-auto-migration
- scope scan
- `git diff --check`
- direct-head CI
- merge-ref CI identity

## 7. Initial artifact lock / Light handoff

この実行ではHeavy Reviewへ進まない。

Initial Headと成果物を`run.json.stage_artifacts.final_synthesis_initial`へlockする。

```text
ARTIFACT_LOCK:
  stage: final_synthesis_initial
  artifact_path:
  content_sha256:
  prompt_revision: fnd05-final-synthesis-v2
  target_head_sha:
  source_artifact_refs:
    - <evaluation ref>
    - <selection ref>
  producer_slot:
  producer_commit_sha:
STATUS: LOCKED / NOT_LOCKED
```

そのexact HeadをL1 ComposerとL2 Lunaへ渡す。

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
SOURCE_ARTIFACTS:
SELECTION_APPLICATION:
CHANGED_FILES:
RUNTIME_DESIGN:
SECRET_DESIGN:
LIFECYCLE_DESIGN:
TEST_DESIGN:
COMPLETION_CHECKS:
MUTATION_RESULTS:
MUTATION_DETERMINISM:
VERIFICATION:
DIRECT_HEAD_CI:
MERGE_REF_CI:
KNOWN_CONCERNS:
UNVERIFIED:
STARTED_AT_LOCAL:
FINISHED_AT_LOCAL:
DURATION_MINUTES:
ARTIFACT_LOCK:
NEXT_STAGE: LIGHT_REVIEW
```

## 10. Operation permissions

- Final Synthesis branch以外へpushしない
- new PRを作らない
- PRをReady化 / mergeしない
- Issueをcloseしない
- candidateを変更しない
- Heavy Reviewを開始しない
