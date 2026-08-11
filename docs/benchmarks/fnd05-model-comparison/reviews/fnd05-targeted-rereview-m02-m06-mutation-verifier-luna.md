# FND-05 M-02 / M-06 Targeted Re-Review — Lightweight Mutation Verifier

ROLE: `lightweight_mutation_verifier`

REVIEWER_IDENTITY:

```yaml
MODEL: GPT-5.6 Luna
HARNESS: Codex
EFFORT: xHigh
CONTEXT: Fresh Context
ROLE: lightweight_mutation_verifier
PROMPT_REVISION: fnd05-targeted-re-review-v2
```

TARGET_VERIFICATION:

```yaml
TARGET_ISSUE: 43
TARGET_PR: 153
OLD_HEAD_SHA: 59aa87f9c6c4c581a56257caef738318e8d09ec3
NEW_HEAD_SHA: 9e704f53911be3fdf0d09538424d3bcd9012f96a
TARGET_BRANCH: agent/issue-43-fnd-05-final-code
CONTROL_HEAD: 5f37fe98eb819f7333afe817ce6d6bde8c6fc990
PASS: YES
```

SOURCE_FINDING_REFS:

- Conditional Judge: `docs/benchmarks/fnd05-model-comparison/reviews/fnd05-conditional-judge-composer-2.5.md@fb0b2f81e4817b494e2167547f537c1e774e919d#sha256:ce44323a2728f0d6ca2dde3d28040074e77d8b59c96ae65bbd528080600f64bb` (`H2-MAJ-02=M-02`, `H2-MAJ-01=M-06`)
- run registry control: `docs/benchmarks/fnd05-model-comparison/run.json@5f37fe98eb819f7333afe817ce6d6bde8c6fc990#sha256:c110a26dcf345010b85dd5723a03fe79d23235dff386184428b3b93df2d3a339`

FIX_ARTIFACT_REF:

- `docs/benchmarks/fnd05-model-comparison/final-synthesis/targeted-fix-m02-m06-result.md@a2e97d3baefb386a0a825a9a79e751ead4124016#sha256:53e8800472db7ba999abd713b5cc7171f6f42c96becc65f951a6924b76e40cce`

FINDING_OWNER_REF:

- `docs/benchmarks/fnd05-model-comparison/reviews/fnd05-targeted-rereview-m02-m06-finding-owner-opus.md@9bfda5c11cd728cace7134f8e07598e4778c5d32#sha256:058d7516cb004070cdce8a2f7f1d059c6ac0aea93e4fe14b0a65c651d36a4993` (`VERDICT: FIXED`, treated as auxiliary input only)

CHANGE_SURFACE:

- Exact comparison: `59aa87f9c6c4c581a56257caef738318e8d09ec3` -> `9e704f53911be3fdf0d09538424d3bcd9012f96a`
- Changed files: `tests/fnd05/verify-mutations.sh`, `tests/fnd05/static-gate.sh`
- No other Targeted Fix files changed.

DIRECT_HEAD_CI:

- Build and Test run `31515332416`: event `push`, result `success`, head `9e704f53911be3fdf0d09538424d3bcd9012f96a`; `build-test` succeeded. Checkout log resolved `9e704f53911be3fdf0d09538424d3bcd9012f96a`.
- FND-05 Compose run `31515332435`: event `push`, result `success`, head `9e704f53911be3fdf0d09538424d3bcd9012f96a`.
- `fnd05-compose`: success; `ACTUAL_CHECKOUT_SHA=9e704f53911be3fdf0d09538424d3bcd9012f96a`; `STATIC_GATE: PASS`.
- `fnd05-mutations`: success; `ACTUAL_CHECKOUT_SHA=9e704f53911be3fdf0d09538424d3bcd9012f96a`; `MUTATION_SUITE: PASS`.

H2_MAJ_02_RESULT: FIXED

M02_MUTATION_VERIFICATION:

```yaml
BASELINE_GREEN: YES
MASK_ONLY_CONTROL:
  EXECUTED: YES
  SUCCESS_PATH_CONFIRMED: YES
  INTENDED_FAILURE_REACHED: NO
  VALID_KILL_SIGNATURE_REJECTED: YES
REAL_FAILURE_PLUS_MASK:
  REAL_FAILURE_INJECTED: YES
  INTENDED_FAILURE_REACHED: YES
  MACHINE_READABLE_FAILURE_MARKER: FND05_M02_MASKED_NONZERO=[1-9][0-9]*
  ORIGINAL_NONZERO_OBSERVED: YES
  MASKED_EXIT_ZERO_OBSERVED: YES
  API_STARTABILITY_OBSERVED: YES
  EXPECTED_RED: YES
  EXPECTED_SIGNATURE: migrator-nonzero-masked-after-intended-failure
SIGNATURE_DISCRIMINATES: YES
RESTORED_GREEN: YES
RESIDUE_ZERO: YES
```

The mask-only control uses the shipped success oracle and is rejected by `m02_failure_oracle` with `m02-intended-failure-not-reached`; it is not counted as a valid M-02 kill. The real-failure case injects a deterministic PostgreSQL port failure, requires the migrator's intended failure marker and the machine-readable non-zero marker, observes a masked migrator exit code of zero with API startability, and is then killed by the required signature.

H2_MAJ_01_RESULT: FIXED

M06_MUTATION_VERIFICATION:

```yaml
BASELINE_GREEN: YES
MUTATION_PRECONDITION: named postgres volume `postgres_data:/var/lib/postgresql` present
SINGLE_MUTATION: YES
MUTATION_APPLIED: YES
SHIPPED_ORACLE_EXECUTED: YES
EXPECTED_RED: YES
EXPECTED_SIGNATURE: named-volume-policy-violation
FAILURE_REASON_MATCHED: YES
MUTATION_REVERTED: YES
RESTORED_GREEN: YES
RESIDUE_ZERO: YES
```

The mutation is applied in a detached worktree by replacing the named volume with an anonymous/contract-out storage entry. The same shipped `tests/fnd05/static-gate.sh` used by the normal `tests/fnd05/verify-compose.sh` path is executed against that worktree and returns the required `named-volume-policy-violation` signature. No M-06-only private or fake oracle is used.

EXPECT_RED_ASSESSMENT:

`expect_red` captures command output, requires a non-zero command status, requires the exact expected `ORACLE_SIGNATURE`, and rejects an unexpected signature. The M-02 post-assertion markers are emitted only after their corresponding assertions succeed. The verdict does not depend on raw `ORACLE_SIGNATURE` text being surfaced by an outer GitHub Actions log.

SHIPPED_ORACLE_ASSESSMENT:

`tests/fnd05/verify-compose.sh` invokes `bash tests/fnd05/static-gate.sh`, and the `fnd05-compose` job runs `verify-compose.sh`. The M-06 mutation path invokes that same Static Gate with `FND05_SOURCE_ROOT` set to the mutated detached worktree. This satisfies the shipped-oracle requirement.

ADJACENT_REGRESSION: PASS

NEW_BLOCKER_MAJOR_IN_CHANGED_SURFACE: 0

The changed-surface checks are supported by direct-head `STATIC_GATE: PASS`, direct-head `MUTATION_SUITE: PASS`, successful cleanup markers for M-02/M-06, and the exact two-file change surface. No new Blocker or Major was found within the allowed adjacent-regression boundary.

RESIDUE:

- CI evidence: M-02 and M-06 each report `RESTORED_GREEN` and `RESIDUE_ZERO`.
- No additional isolated probe was required because exact code inspection plus direct-head CI provides the required discriminating evidence.

FINAL_VERDICT: FIXED

MERGE_READY_FROM_THIS_SCOPE: YES

This is limited to the FND-05 M-02/M-06 targeted re-review and is not an authorization for overall PR #153 merge, PR state changes, Issue changes, or release actions.

UNVERIFIED:

- Full Light Review / Heavy Review, full H1/H2, and mutations outside M-02/M-06 were not re-run.
- No local Docker probe was performed; the existing direct-head CI evidence was sufficient under the probe policy.
- Overall PR #153 merge authorization remains outside this verifier's scope.

ARTIFACT_LOCK:

- Output branch base: `5f37fe98eb819f7333afe817ce6d6bde8c6fc990`
- Historical `stage_artifacts.targeted_re_review` slot: unchanged.
- Finding Owner `stage_artifacts.targeted_re_review_m02_m06_finding_owner` slot: unchanged.
- New registry slot will be added only in Commit B after this artifact is committed.
