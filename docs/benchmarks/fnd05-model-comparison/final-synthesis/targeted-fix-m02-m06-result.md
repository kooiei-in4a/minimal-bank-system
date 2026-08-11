# FND-05 Targeted Fix Result — M-02 / M-06

## OLD_HEAD

`59aa87f9c6c4c581a56257caef738318e8d09ec3`

## NEW_HEAD

`9e704f53911be3fdf0d09538424d3bcd9012f96a`

## SOURCE_FINDING_REFS

- `docs/benchmarks/fnd05-model-comparison/reviews/fnd05-conditional-judge-composer-2.5.md@fb0b2f81e4817b494e2167547f537c1e774e919d#sha256:ce44323a2728f0d6ca2dde3d28040074e77d8b59c96ae65bbd528080600f64bb`
- `run.json@5540f44bcc59a6581129ca8c4d6ffb043f61dbbc#sha256:71f852e7397031b91d2f41a20116f18d40e99a4dbd687c0d2a4ce2b065e9936f`
- `pr:153@9e704f53911be3fdf0d09538424d3bcd9012f96a`

Judge control branch を明示 fetch し、lock commit、producer commit、Phase A commit、tree path、両 SHA-256、`stage_artifacts.conditional_judge` の全 lock field を照合してから実施した。

## FINDING_DISPOSITION

| Finding | Mutation | Disposition |
| --- | --- | --- |
| H2-MAJ-02 | M-02 | FIXED |
| H2-MAJ-01 | M-06 | FIXED |

## ROOT_CAUSE

- M-02 は intended real Migrator failure を確認する前に、exit 0 だけから `migrator-nonzero-masked` を出していた。そのため mask-only の通常成功と実 failure を区別できなかった。
- M-06 は mutation の inline config self-check だけで KILLED とし、shipped named-volume policy oracle を実行していなかった。

## CHANGE_SURFACE

許可された変更範囲だけを使用した。

## CHANGED_FILES

- `tests/fnd05/verify-mutations.sh`
- `tests/fnd05/static-gate.sh`

## BEHAVIOR_CHANGE

- M-02 は mask-only control を成功 migration path で実行し、`m02-intended-failure-not-reached` を確認する。実 mutation では Migrator failure marker と `FND05_M02_MASKED_NONZERO=<non-zero>` を machine-readable に確認した後、exit-0 masking と API startability に対して `migrator-nonzero-masked-after-intended-failure` を出す。
- M-06 は detached worktree の named-volume mutation を、baseline、Static Gate RED、restore Static Gate GREEN、residue 0 の順に実行する。
- Static Gate は既存の複合設定判定を維持したまま、resolved PostgreSQL named-volume policy 違反だけに `named-volume-policy-violation` を付与する。

## M02_EVIDENCE

```yaml
BASELINE_GREEN: PASS
MASK_ONLY_CONTROL_EXECUTED: PASS
MASK_ONLY_VALID_KILL_REJECTED: PASS
INTENDED_FAILURE_REACHED: PASS
MACHINE_READABLE_PRECONDITION: PASS
MASKED_NONZERO_OBSERVED: PASS
EXPECTED_RED: PASS
EXPECTED_FAILURE_SIGNATURE: migrator-nonzero-masked-after-intended-failure
SIGNATURE_DISCRIMINATES: PASS
RESTORED_GREEN: PASS
RESIDUE_ZERO: PASS
VALID_KILL: YES
```

## M06_EVIDENCE

```yaml
BASELINE_GREEN: PASS
MUTATION_PRECONDITION: PASS
MUTATION_APPLIED: PASS
SHIPPED_ORACLE_EXECUTED: PASS
EXPECTED_RED: PASS
EXPECTED_FAILURE_SIGNATURE: named-volume-policy-violation
FAILURE_REASON_MATCHED: PASS
RESTORED_GREEN: PASS
RESIDUE_ZERO: PASS
VALID_KILL: YES
```

## MUTATION_RESULTS

```text
M-01 PASS
M-02 PASS — valid new kill evidence recorded above
M-03 PASS
M-04 PASS
M-05 PASS
M-06 PASS — valid new kill evidence recorded above
M-07 PASS
M-08 PASS
M-09 PASS
M-10 PASS
MUTATION_SUITE: PASS
```

## ADJACENT_REGRESSION

No adjacent mutation semantics were changed. Full `tests/fnd05/verify-mutations.sh` completed with exit code 0 after the M-02/M-06 targeted runs.

## DIRECT_HEAD_CI

| Workflow | Direct push run | Result | Head / actual checkout |
| --- | --- | --- | --- |
| Build and Test | [31515332416](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31515332416) | SUCCESS | push event head SHA `9e704f53911be3fdf0d09538424d3bcd9012f96a`; default checkout uses that push SHA |
| FND-05 Compose verification | [31515332435](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31515332435) | SUCCESS | `fnd05-compose` and `fnd05-mutations` each logged `ACTUAL_CHECKOUT_SHA=9e704f53911be3fdf0d09538424d3bcd9012f96a` |

`31515332416` と `31515332435` はいずれも `event=push`、`headSha=9e704f53911be3fdf0d09538424d3bcd9012f96a` であり、PR merge-ref evidence は使用していない。

## NEW_REGRESSIONS

None observed.

## KNOWN_CONCERNS

None.

## UNVERIFIED

Targeted re-review は本作業の範囲外であり、開始していない。

## RE_REVIEW_SCOPE

- finding_owner
- lightweight_mutation_verifier

Full H1/H2 rerun is not required.

## ARTIFACT_LOCK

This artifact is committed on the control branch only. The following registry-lock commit records its exact SHA-256 and the NEW_HEAD reference without merging or cherry-picking the product/test fix into this branch.

## NEW_HEAD_LOCK

LOCKED
