# FND-05 Opus Heavy Final Review

REVIEWER_IDENTITY:

    MODEL: Claude Opus 5
    HARNESS: Claude Code
    EFFORT_REQUESTED: xhigh
    EFFORT_ACTUAL_LABEL: not exposed by this Claude Code session; no effort selector
        value was surfaced to the model, so no exact label can be attested. The session
        ran with extended reasoning enabled throughout.
    CONTEXT: Fresh Context
    ROLE: adversarial_failure_and_false_assurance_final_gate
    SLOT: H2
    PROMPT_REVISION: fnd05-heavy-opus-v2
    FULL_REVIEW_BUDGET: 1
    FULL_REVIEW_USED: 1

H1_INDEPENDENCE:

    `docs/benchmarks/fnd05-model-comparison/reviews/fnd05-heavy-h1-sol-gpt-5.6-sol-codex.md`
    was NOT read at any point before or during this review. The only H1-derived facts
    consumed were `run.json.stage_artifacts.heavy_sol.status = locked` and
    `heavy_sol.target_head_sha = 59aa87f9c6c4c581a56257caef738318e8d09ec3`, used solely
    as stage-progression confirmation. No H1 verdict, finding, merge-readiness judgement
    or architecture assessment informed any conclusion below. No mechanical H1/H2
    comparison is recorded in this artifact; that reconciliation belongs to the
    Coordinator.

---

## TARGET_VERIFICATION

    TARGET_ISSUE: 43
    TARGET_PR: 153
    BASE_SHA: ee8abbb15758c1a2cfb624791482b755be578da2          (git object: commit — OK)
    FINAL_HEAD_SHA: 59aa87f9c6c4c581a56257caef738318e8d09ec3    (git object: commit — OK)
    TARGET_BRANCH: agent/issue-43-fnd-05-final-code
    origin/agent/issue-43-fnd-05-final-code = 59aa87f9c6c4c581a56257caef738318e8d09ec3  MATCH

    PR #153: state=OPEN  isDraft=true  mergedAt=null
             headRefName=agent/issue-43-fnd-05-final-code
             headRefOid=59aa87f9c6c4c581a56257caef738318e8d09ec3  MATCH
             baseRefName=main

    Product target used for this review: exact Final Head 59aa87f only.
    PR merge-ref, main, candidate branches, Light control branch and the H1 output
    branch were NOT used as product target.

CONTROL_REGISTRY_VERIFICATION:

    CONTROL_HEAD: 84c50824d274815e52f6459e689b74acb2c6bbd5   (git object: commit — OK)
    RUN_REGISTRY_PATH: docs/benchmarks/fnd05-model-comparison/run.json
    RECOMPUTED_SHA256 (git show 84c5082:...run.json | sha256sum):
        3eeedcfc07e33d5661b09c404763156c3871acb78e78e7d37b0d5927761ec017
    EXPECTED_SHA256:
        3eeedcfc07e33d5661b09c404763156c3871acb78e78e7d37b0d5927761ec017
    MATCH: YES

LIGHT_FIX_ARTIFACT_VERIFICATION:

    PATH: docs/benchmarks/fnd05-model-comparison/final-synthesis/light-findings-fix-result.md
    SHA256 @2251adc9ba502ff81569295e05c74d1282901f5e:
        927deabfc98708219896815f856f8b61a7d60551aeb201640e67a2f3c73df17b   MATCH
    SHA256 @84c50824d274815e52f6459e689b74acb2c6bbd5:
        927deabfc98708219896815f856f8b61a7d60551aeb201640e67a2f3c73df17b   MATCH
    LIGHT_FIX_TARGET_HEAD recorded = 59aa87f9c6c4c581a56257caef738318e8d09ec3  MATCH

MUTATION_REPORT_INPUT:

    Per prompt Section 5, the same immutable artifact is consumed under the
    mutation-report logical role:
        MUTATION_REPORT_ARTIFACT_PATH:
            docs/benchmarks/fnd05-model-comparison/final-synthesis/light-findings-fix-result.md
        MUTATION_REPORT_SHA256:
            927deabfc98708219896815f856f8b61a7d60551aeb201640e67a2f3c73df17b   MATCH
    Sections consumed: M05_EVIDENCE, MUTATION_BASELINE, VERIFICATION, DIRECT_HEAD_CI.
    No new mutation-report artifact was created.

DETERMINISM_CONTRACT_VERIFICATION:

    run.json.revisions.mutation_determinism_contract = fnd05-mutation-determinism-v1   MATCH
    run.json.revisions.mandatory_mutations          = fnd05-mutations-v2                MATCH
    reference/mutation-determinism-contract.md revision header
                                                    = fnd05-mutation-determinism-v1     MATCH
    reference/mandatory-mutations.md revision header = fnd05-mutations-v2                MATCH
    run.json.gates.mutation_determinism_locked = true
    run.json.gates.mutations_locked            = true
    Issue #43 Section 13 D-06 restates the same lock at Product-authority level.

---

## ENTRY_CONDITIONS

| Condition | Result | Primary evidence |
| --- | --- | --- |
| Static Gate PASS | PASS | `stage_artifacts.static_gate.status = locked`; `STATIC_GATE: PASS` observed in direct-head CI job 93825335500 at 15:07:39Z |
| L1 LOCKED | PASS | `stage_artifacts.light_l1.status = locked` |
| L2 LOCKED | PASS | `stage_artifacts.light_l2.status = locked` |
| Light findings disposition COMPLETE | PASS | light-findings-fix-result.md: L2-D06-M05 and L2-D03-SECRET-MISSING both ACCEPTED_FIXED; all four handoff buckets for unresolved/escalated/incomplete are empty |
| Light Fix LOCKED | PASS | `stage_artifacts.light_fix.status = locked`, `target_head_sha = 59aa87f` |
| Final Head locked | PASS | `light_fix.target_head_sha` and `heavy_sol.target_head_sha` both `59aa87f`; PR #153 head identical |
| direct-head Build/Test SUCCESS | PASS | run 31505330867, see DIRECT_HEAD_CI below |
| direct-head Compose/Mutation SUCCESS | PASS | run 31505330990, see DIRECT_HEAD_CI below |
| mandatory mutation baseline/report available | PASS (with defect) | `tests/fnd05/verify-mutations.sh` at Final Head + job 93825335505 log. See MUTATION_ASSESSMENT — the report is available but M-06's entry is not a valid kill |
| D-06 mutation determinism lock = true | PASS | `run.json.gates.mutation_determinism_locked = true` |
| artifact path / SHA256 / target Head match | PASS | see above |
| PR #153 OPEN / DRAFT / NOT MERGED / Head = 59aa87f | PASS | `gh pr view 153` |

    ENTRY_CONDITIONS_PASS: YES

    Stage progression consumed from H1 (permitted scope only):
        run.json.stage_artifacts.heavy_sol.status = locked
        run.json.stage_artifacts.heavy_sol.target_head_sha = 59aa87f9c6c4c581a56257caef738318e8d09ec3

DIRECT_HEAD_CI (GitHub primary evidence, re-verified independently):

    BUILD_AND_TEST:
        RUN: 31505330867
        EVENT: push
        STATUS: completed
        CONCLUSION: success
        JOB: build-test (93825335093) — success
        ACTUAL_CHECKOUT_SHA (from job log, `git log -1 --format=%H`):
            59aa87f9c6c4c581a56257caef738318e8d09ec3
        MATCH: YES

    FND05_COMPOSE:
        RUN: 31505330990
        EVENT: push
        STATUS: completed
        CONCLUSION: success
        JOBS:
            fnd05-compose   (93825335500) — all steps completed, conclusion success
            fnd05-mutations (93825335505) — all steps completed, conclusion success
        ACTUAL_CHECKOUT_SHA (workflow "Record actual checkout SHA" step, both jobs):
            ACTUAL_CHECKOUT_SHA=59aa87f9c6c4c581a56257caef738318e8d09ec3
            plus in-workflow assertion `test "$(git rev-parse HEAD)" = "$GITHUB_SHA"`
        MATCH: YES

    NOTE (API artefact, not a finding): the REST job object for
    `fnd05-mutations` (93825335505) currently returns `status: in_progress`,
    `conclusion: null` while every one of its steps is `completed/success`
    (step 4 ran 15:07:38Z → 15:10:43Z) and the parent run is
    `completed/success`. This is a GitHub job-record finalisation artefact, not
    an incomplete run. It is recorded here because a reviewer reading only
    `gh run view --json jobs` would see a misleading empty conclusion.

    PR merge-ref CI was NOT substituted. The pull_request-event run 31505335667
    is still `in_progress` and was deliberately not used.

---

## VERDICT

    CHANGES_REQUIRED

    BLOCKER: 0
    MAJOR: 2

---

## BLOCKERS

    NONE.

    No fail-open path was found in the FND-05 observable contract. Migrator
    non-zero, migration timeout, missing host secret, unreadable/empty secret
    file and missing non-secret connection parameters all converge on a
    non-zero process exit, and API start is gated on
    `service_completed_successfully`, which is exit-code-driven. Every failure
    variant examined fails closed.

---

## MAJORS

### H2-MAJ-01 — M-06 mandatory mutation is not a valid kill; it verifies its own mutation instead of the oracle

    ID: H2-MAJ-01
    SEVERITY: Major

    ROOT_CAUSE:
        `tests/fnd05/verify-mutations.sh::run_m06` never executes any FND-05
        oracle. It renders the mutated Compose configuration and then asserts,
        with an inline `jq` predicate of its own, that the named volume is
        gone — i.e. it asserts only that its own mutation was applied — and
        prints `M-06: KILLED` on that basis. There is no baseline GREEN, no
        invocation of `tests/fnd05/static-gate.sh` (the sole shipped oracle for
        the named-volume contract), no expected RED, no expected failure
        signature, no restore GREEN and no residue check.

    FAILURE_SCENARIO:
        A future change (refactor, merge, "simplification", or a deliberate
        weakening) removes or breaks the named-volume clause in
        `tests/fnd05/static-gate.sh`. `static-gate.sh` then reports
        `STATIC_GATE: PASS` on a configuration that no longer enforces the
        named PostgreSQL data volume, and `verify-mutations.sh` still reports
        `M-06: KILLED`. Both CI jobs stay green while the M-06 protected
        contract ("PostgreSQL data is retained in a named volume", Issue #43
        Acceptance Criteria) is completely unguarded. The Compose runtime path
        would then accept an anonymous volume, and `down --remove-orphans`
        would silently destroy the database on every canonical stop.

    EXPECTED:
        Issue #43 Section 9: "mandatory mutationではdeterministic precondition
        成立後にexpected reasonでRED、restore後GREEN、residue 0を確認".
        Issue #43 Section 13 D-06: "baseline GREEN → expected RED → restore
        GREEN → residue 0".
        `reference/mandatory-mutations.md` Section 7 (M-06 Expected detection):
        "volume policy / lifecycle test RED".
        `reference/mandatory-mutations.md` Section 13: `EXPECTED_RED_OBSERVED:
        NO` "は有効なkillとして数えない。Final Synthesisのmandatory mutationで
        発生した場合はmerge-blocking Majorとして扱う。"

    OBSERVED:
        `tests/fnd05/verify-mutations.sh` `run_m06` body at Final Head consists
        of: write override → `set_project` → `compose_run config --format json`
        → inline `jq any(.services.postgres.volumes[]; .type == "volume" and
        .source == "postgres_data")` → `printf 'M-06: KILLED\n'`.
        No `baseline`, no `expect_red`, no `ORACLE_SIGNATURE`, no
        `assert_residue_zero`, no call to `static-gate.sh`.

        Direct-head CI corroboration (job 93825335505):
            15:09:54.1289674Z  M-06: KILLED   <- preceded only by M-05 lines
            15:09:54.2130858Z  M-06: KILLED
        Elapsed for the entire M-06 mutation: 84 ms. No container, network or
        volume was created; the only two `STATIC_GATE: PASS` lines in the whole
        mutation job belong to M-05 (baseline and restore). The mutation job
        log therefore independently confirms no oracle ran for M-06.

        The inline predicate is also strictly weaker than the shipped oracle:
        it omits the `.target == "/var/lib/postgresql"` clause that
        `static-gate.sh` asserts.

    PROBE / MUTATION:
        Probe P-1, executed in an isolated detached `git worktree` at
        59aa87f (no production branch touched, one change at a time,
        `docker compose config` only — no container started):

        1. Baseline: `bash tests/fnd05/static-gate.sh` -> `STATIC_GATE: PASS`, exit 0.
        2. `run_m06` logic verbatim against the intact tree -> `M-06: KILLED`.
        3. Injected ONE defect into the oracle only — deleted
           `tests/fnd05/static-gate.sh:57`:
               any(.services.postgres.volumes[]; .type == "volume" and
                   .source == "postgres_data" and
                   .target == "/var/lib/postgresql") and
           (`git diff` confirms exactly one deleted line.)
        4. `bash tests/fnd05/static-gate.sh` -> `STATIC_GATE: PASS`, exit 0.
           The named-volume contract is now unprotected and the shipped gate
           does not notice.
        5. `run_m06` logic verbatim against the regressed oracle ->
           `M-06: KILLED`  (unchanged).

        Probe P-2 (fairness control, same isolated worktree):
        oracle restored to Final-Head state, then the REAL M-06 defect applied
        to the canonical `compose.yaml`
        (`- postgres_data:/var/lib/postgresql` -> `- /var/lib/postgresql`):
            `bash tests/fnd05/static-gate.sh` -> exit 1.
        The shipped oracle is therefore sound. The defect is in the mutation,
        not in the product oracle.

        Cleanup: worktree removed with `git worktree remove --force`;
        `git status --porcelain` empty; no probe-labelled container, volume or
        network exists. RESIDUE: 0.

    PRECONDITION:
        Established for the probe: Final Head tree, `static-gate.sh` GREEN
        before injection, exactly one defect injected, oracle restored before
        P-2.
        NOT established by `run_m06` itself: it has no precondition step at all.

    FAILURE_SIGNATURE:
        Expected (per D-06 M-06): "resolved config / actual volume identity
        oracle detects non-named storage" — i.e. `static-gate.sh` exits
        non-zero.
        Observed: none. No oracle was executed, so no failure signature was
        produced or matched. `EXPECTED_RED_OBSERVED: NO`.
        Secondary gap: `static-gate.sh`'s `jq --exit-status` block emits no
        `ORACLE_SIGNATURE=...` at all, so even a corrected M-06 would have no
        named-volume signature to match against, unlike the `require_literal`
        checks which do emit signatures.

    IMPACT:
        The mandatory mutation set is the only mechanism the FND-05 contract
        provides for proving the verification suite detects the defect classes
        it claims to protect. For M-06 that proof does not exist, while
        `MUTATION_SUITE: PASS`, `run.json`, the Final Synthesis initial result
        ("M-06 ... Red signal observed") and the locked Light-Fix artifact
        (`MUTATION_BASELINE: M-01_TO_M-10: PASS`) all assert that it does. This
        is false assurance recorded in immutable artifacts and is exactly the
        `EXPECTED_RED_OBSERVED: NO` case that `mandatory-mutations.md`
        Section 13 designates merge-blocking.

    REQUIRED_FIX:
        Rewrite `run_m06` to the same shape already used by `run_m05`:
        1. detached temporary worktree at HEAD;
        2. baseline: run the shipped oracle (`static-gate.sh` with
           `FND05_SOURCE_ROOT` pointing at the worktree) and record
           `BASELINE_GREEN`;
        3. verify the mutation precondition (named volume present in the
           worktree's `compose.yaml`) and apply exactly one mutation to that
           worktree's `compose.yaml`;
        4. `expect_red <named-volume-signature> ... static-gate.sh` — which
           requires adding an explicit signature emission for the
           named-volume assertion in `static-gate.sh` (splitting the monolithic
           `jq --exit-status` block, or wrapping it so it prints
           `ORACLE_SIGNATURE=named-volume-missing` on failure);
        5. remove the worktree, re-run the oracle for `RESTORED_GREEN`;
        6. `assert_residue_zero`.
        Then re-record the M-06 row of the mutation report with
        `PRECONDITION_RESULT`, `BASELINE_RESULT`, `EXPECTED_FAILURE_SIGNATURE`,
        `OBSERVED_FAILURE_SIGNATURE`, `EXPECTED_RED_OBSERVED`,
        `RESTORED_RESULT` and `RESIDUE_CHECK` per `mandatory-mutations.md`
        Section 13.

    WHY_HEAVY_SCOPE:
        This is a test-oracle / false-assurance defect: the suite is GREEN, the
        recorded evidence claims a kill, and no oracle was ever exercised. It
        is invisible to rule-level or happy-path review and only surfaces by
        executing the mutation path and probing the oracle independently.

    LIGHT_GATE_ESCAPE: YES
    SOURCE_LIGHT_FINDING: NONE
        (`run_m06` is unchanged by the Light fix — `git diff be45366..59aa87f
        -- tests/fnd05/verify-mutations.sh` touches only `run_m05`. The
        resolved Light findings were L2-D06-M05 and L2-D03-SECRET-MISSING;
        this is a distinct mutation and a distinct root cause, not a re-raise.)

    RESIDUE: 0

---

### H2-MAJ-02 — M-02's expected failure signature cannot distinguish a real masked failure from a no-op mutation

    ID: H2-MAJ-02
    SEVERITY: Major

    ROOT_CAUSE:
        `run_m02` injects two things at once conceptually — the deterministic
        precondition (`POSTGRES_PORT: "1"`, forcing a real Migrator failure)
        and the defect (a wrapper that converts any non-zero exit to 0) — but
        verifies only the second. `failure_oracle` short-circuits on its very
        first check:

            [[ "$(exit_code migrator)" != 0 ]] || {
                printf 'ORACLE_SIGNATURE=migrator-nonzero-masked\n' >&2
                return 1
            }

        The M-02 wrapper (`dotnet ... || { code=$?; printf ...; exit 0; }`)
        yields exit 0 unconditionally — on genuine success as well as on masked
        failure. Therefore `ORACLE_SIGNATURE=migrator-nonzero-masked` is emitted
        identically whether or not the intended migration failure was ever
        reached, and `expect_red` accepts it as the kill.

    FAILURE_SCENARIO:
        A future change makes the `POSTGRES_PORT: "1"` override ineffective —
        e.g. the base `compose.yaml` switches `environment` from mapping form
        to list form (Compose merges list-form `environment` by replacement, not
        by key), the connection parameters move into the wrapper or a
        `.env`-style source, or the port key is renamed. The migrator then
        connects successfully and applies the migration; the masking wrapper
        still exits 0; `wait_for_state api running` still succeeds; the oracle
        still emits `migrator-nonzero-masked`; `M-02: KILLED` is still printed
        and `MUTATION_SUITE: PASS` is still reported. From that point on M-02
        provides no evidence whatsoever about exit-code masking, and its
        protected contract — "migration failure must not be reported as
        success and must not permit API start" — is unverified while CI is
        green.

    EXPECTED:
        `reference/mandatory-mutations.md` Section 3 (M-02 Expected detection):
        "failure test RED; **intended migration failureへ到達している**;
        expected non-zeroとobserved exit 0の不一致を検出".
        `reference/mandatory-mutations.md` Section 1: "preconditionが成立しない
        runを `KILLED` または `SURVIVED` として数えない."
        `reference/pre-run-decision-locks.md` D-06, M-02 row, deterministic
        precondition column: "intended real Migrator failure reached".
        `reference/mandatory-mutations.md` Section 13:
        `PRECONDITION_RESULT != PASS` "は有効なkillとして数えない ...
        merge-blocking Major".

    OBSERVED:
        The wrapper deliberately emits a precondition marker
        `FND05_M02_MASKED_NONZERO=<code>` into the migrator log, but nothing
        consumes it. Verified two ways:
          - source: `grep -n "FND05_M0" tests/fnd05/verify-mutations.sh` at
            Final Head returns only the *producing* command strings for M-01
            (line 204), M-02 (line 239) and M-09 (line 438), plus the single
            *consuming* assertion for M-01 at line 214
            (`compose_run logs ... | grep --fixed-strings --quiet
            FND05_M01_BARRIER_ESTABLISHED`). There is no consumer for the M-02
            marker.
          - CI: the marker string does not appear anywhere in job 93825335505's
            log (0 occurrences), because the migrator log is never read in
            `run_m02`.
        Contrast with the sibling mutations, which ARE self-guarding: in
        `run_m07` and `run_m08` a no-op mutation produces the *wrong*
        signature or GREEN and `expect_red` fails; M-01 explicitly asserts its
        barrier marker; M-09 explicitly asserts
        `StartedAt != "0001-01-01T00:00:00Z" and Status == "exited"` before
        invoking the oracle. M-02 is the only failure-path mutation with no
        precondition assertion and a non-discriminating signature.

    PROBE / MUTATION:
        No probe was run for this finding, and none is needed: the
        non-discrimination is a closed-form property of the code at Final Head.
        `failure_oracle`'s first predicate is a function of
        `docker inspect .State.ExitCode` alone; the M-02 override forces that
        value to 0 on both branches of its `||`. Both the intended-failure run
        and a hypothetical success run therefore produce the identical
        `ORACLE_SIGNATURE=migrator-nonzero-masked`, and no other step in
        `run_m02` observes the migrator's real outcome. Executing the runtime
        path would not add discriminating evidence, so no Docker resources were
        consumed for this finding.

    PRECONDITION:
        For the 2026-08-11 direct-head run the precondition almost certainly
        held by construction (`POSTGRES_PORT: "1"` is refused deterministically),
        but the run produced no evidence of it. `PRECONDITION_RESULT` is
        therefore UNRECORDED / UNVERIFIABLE from the locked evidence, not PASS.

    FAILURE_SIGNATURE:
        Expected: `migrator-nonzero-masked` observed **after** the intended
        Migrator failure has been shown to be reached.
        Observed: `migrator-nonzero-masked` with the "intended failure reached"
        half of the precondition neither asserted nor logged. The signature is
        satisfied by an invalid state as well as by the valid one.

    IMPACT:
        M-02 protects the single most important FND-05 safety property after
        ordering — that a failed migration is never reported as success. Its
        kill is currently unfalsifiable: the suite cannot tell a real kill from
        a mutation that did nothing. The locked artifacts nevertheless record
        `MUTATION_BASELINE: M-01_TO_M-10: PASS`, which is assurance the
        evidence does not support.

    REQUIRED_FIX:
        In `run_m02`, before calling `expect_red`, assert the precondition
        from primary runtime state — either
            `compose_run logs --no-color migrator |
                 grep --fixed-strings --quiet 'FND05_M02_MASKED_NONZERO='`
        (the marker is already emitted; it only needs to be consumed, and it
        should additionally be asserted to be a non-zero code), or assert the
        intended failure reason marker
        `'Migration failed. The deployment must not continue.'` in the migrator
        log. Then record `PRECONDITION_RESULT: PASS` in the mutation report per
        `mandatory-mutations.md` Section 13. The same one-line pattern already
        exists in `run_m01` (line 214), so this is a low-risk change.

    WHY_HEAVY_SCOPE:
        False assurance in a failure-path oracle: green CI, a recorded kill,
        and an expected signature that a no-op mutation reproduces exactly.
        Detectable only by tracing the injected precondition through to the
        oracle's discriminating predicate.

    LIGHT_GATE_ESCAPE: YES
    SOURCE_LIGHT_FINDING: NONE
    RESIDUE: 0

---

## FAILURE_PATH_MATRIX

| # | Failure path | Product behaviour at Final Head | API start prevented? | Runtime evidence at Final Head |
| --- | --- | --- | --- | --- |
| F-01 | PostgreSQL not yet usable | `depends_on: postgres: condition: service_healthy`; `pg_isready -U $POSTGRES_USER -d $POSTGRES_DB`, interval 2s / timeout 3s / retries 30 / start_period 5s | Migrator does not start until healthy | CI job 93825335500: postgres `Started` 15:08:11.09 → `Healthy` 15:08:16.59 → migrator `Starting` 15:08:16.60 |
| F-02 | Migrator connection failure | `GetPendingMigrationsAsync` throws → generic catch → `MigratorLog.MigrationFailed` → `MigratorExitCode.Failure = 1` | YES — `service_completed_successfully` not satisfied | `tests/fnd05/failure-compose.yaml` (`POSTGRES_PORT: "1"`); `assert_failure_contract` asserts exit != 0, API never started, and the literal marker `Migration failed. The deployment must not continue.` Direct-head CI green |
| F-03 | Migrator credential failure | Npgsql auth exception → same generic catch → exit 1 | YES (same gate) | NOT exercised at runtime. Covered by construction: every non-timeout exception maps to exit 1 |
| F-04 | Migration timeout | `CancellationTokenSource(60s)`; `OperationCanceledException when cancellation.IsCancellationRequested` → `MigrationTimedOut` → `MigratorExitCode.Timeout = 2` | YES (non-zero) | NOT exercised at runtime. Distinct exit code 2 confirmed by source; gate is exit-code-driven so behaviour is identical to F-02 |
| F-05 | Migration itself fails / invalid history | `MigrateAsync` throws → `MigrationFailed` → exit 1 | YES | NOT exercised at runtime as a distinct cause; M-08 exercises the inverse (exit 0 with migration NOT applied) and is detected |
| F-06 | Missing host secret `MBS_DATABASE_PASSWORD` | Compose refuses to start any service requiring the secret | YES — every project container observed with `StartedAt == 0001-01-01T00:00:00Z` | `run_missing_secret_probe` in `verify-compose.sh`; CI 15:08:10.64 `MISSING_SECRET: FAIL_CLOSED_OBSERVED`, `API_NOT_SERVING`, `NO_LEAK`, `RESIDUE_ZERO`, `compose-up-exit=1` |
| F-07 | Secret file unreadable or empty inside the container | `deployment/fnd05/with-database-secret.sh` exits 78 before `exec` | YES (non-zero migrator) | NOT exercised at runtime; source-verified, `set -Eeuo pipefail` + explicit `exit 78` |
| F-08 | Required non-secret connection parameter missing | wrapper loop over `POSTGRES_HOST/PORT/DATABASE/USERNAME` exits 78 | YES | NOT exercised at runtime; source-verified |
| F-09 | Migrator exit code masked to 0 | Contract violation class; detected by `failure_oracle` | n/a | M-02 — see H2-MAJ-02 for the discrimination gap |
| F-10 | Migrator exits 0 without applying the migration | Detected by unchanged `success_oracle` via `public.__EFMigrationsHistory` | n/a | M-08 executed against a detached worktree with the real Migrator runtime mutated; `expect_red expected-migration-absent success_oracle`. Self-guarding: a no-op sed leaves the oracle GREEN and `expect_red` fails |
| F-11 | API starts then exits immediately | `success_oracle` requires `Status == running`, not merely "was started" | n/a | M-09; explicitly asserts `StartedAt != 0001-01-01T00:00:00Z and Status == "exited"` before the oracle, so started-then-exited is not conflated with never-started |
| F-12 | Ordering weakened (API may start before Migrator success) | Compose `condition: service_completed_successfully`; runtime check `api.StartedAt >= migrator.FinishedAt` | n/a | M-01 with a controlled barrier (migrator held in an infinite loop, barrier marker asserted from the migrator log) and API observed `running`; RED with `api-started-before-migrator-success` |
| F-13 | API startup performs schema evolution | `Program.cs` has no migration call; `static-gate.sh` source scan rejects `MigrateAsync\|\.Migrate(\|EnsureCreated` under `src/MinimalBankSystem.Api` | n/a | M-03 with a real pending-migration fixture and verified baseline state invariance (see TEST_ORACLE_ASSESSMENT) |

    Exit masking: none found. `with-database-secret.sh` terminates with
    `exec "$@"`, so the .NET exit code is the container exit code; the migrator
    returns an explicit `int` from top-level statements; `restart: "no"` on the
    migrator prevents a restart loop from converting a failure into an
    eventually-successful completion.

    Success-looking failure: none found on the product path. The one
    success-looking failure found is in the verification layer (H2-MAJ-01,
    H2-MAJ-02).

    "started-then-exited vs never-started": correctly distinguished in both
    `assert_failure_contract` (`StartedAt == "0001-01-01T00:00:00Z" or
    Status == "created"`) and `success_oracle` (`Status == "running"`), and
    mutation-tested by M-09.

---

## LIFECYCLE_ASSESSMENT

    D-04 conformance: the canonical command strings in
    `docs/operations/fnd05-compose-runtime.md` are byte-identical in meaning to
    the D-04 lock (validate / start / stop-retaining-data / restart /
    clean reset), and `verify-compose.sh` executes exactly those forms.

    stop (`down --remove-orphans`):
        Verified. CI 15:08:18.92–15:08:19.21 removes the three containers and
        the network; the named volume is NOT removed. The script then asserts
        the volume still exists and that its labels are
        `com.docker.compose.project = minimal-bank-system-fnd05` and
        `com.docker.compose.volume = postgres_data`.

    start-after-stop / restart:
        Verified. The second `up --build` (15:08:19.37) shows
        `Network ... Creating` but no `Volume ... Creating`, confirming volume
        reuse, and the migrator is recreated and re-runs (`migrator Starting`
        15:08:25.16 → `Exited` 15:08:26.31), so the migration gate is genuinely
        re-evaluated rather than skipped. `assert_success_contract` runs again
        and `read_history` is asserted equal to the first run's history — the
        rerun does not duplicate or mutate migration history.

    retained-volume rerun:
        Verified as above (V-04).

    clean reset (`down --volumes --remove-orphans`):
        Verified. CI 15:08:27.47–15:08:27.76 removes containers, volume and
        network; `assert_no_project_residue` then asserts zero project-labelled
        containers, volumes and networks.

    interrupted cleanup:
        `trap cleanup_safety EXIT` covers normal and error exits, and correctly
        does NOT mask the script's exit status (the trap never calls `exit`, so
        bash preserves the original status). It does not cover an untrapped
        SIGINT/SIGTERM, so an operator Ctrl-C can leave project resources
        behind. The documented clean-reset command recovers this, and FND-05
        scope does not require signal-safe orchestration. Not a finding.

    parallel execution:
        `verify-compose.sh` intentionally uses the D-04 fixed project name, so
        two concurrent runs on one daemon would collide — that is a direct
        consequence of the D-04 lock, not a defect. `verify-mutations.sh`
        isolates every mutation under `...-m0N-$RANDOM$RANDOM`, and the
        missing-secret probe uses `${project_name}-missing-secret-$RANDOM$RANDOM`,
        both registered for trap cleanup. The two CI jobs run on separate
        runners.

    repeated execution:
        Idempotent. Every entry point begins from `up --build` and every exit
        path runs `down --volumes --remove-orphans` plus a residue assertion.

---

## OWNERSHIP_ASSESSMENT

    Project scoping: correct. Every residue and identity assertion filters on
    `label=com.docker.compose.project=<exact project name>` for containers,
    volumes and networks, so resources belonging to other projects on the same
    daemon can neither satisfy nor break the assertions. `assert_success_contract`
    additionally asserts `Config.Labels["com.docker.compose.project"]` and
    `["com.docker.compose.service"]` on the migrator and API containers, and
    `docker volume inspect` asserts
    `Labels["com.docker.compose.volume"] == "postgres_data"`.

    Cross-project misidentification (the M-10 invalid-kill class "他projectの
    resourceを誤検出"): not present. The one place where a wider view is taken
    is `docker ps -aq --filter label=...project=$missing_project_name` in the
    missing-secret probe, which is likewise project-scoped and uses a random
    suffix.

    Volume ownership: `postgres_data:/var/lib/postgresql` matches the
    postgres:18 image layout (`PGDATA=/var/lib/postgresql/18/docker`, image
    `VOLUME /var/lib/postgresql`), and `static-gate.sh` pins the mount target.
    Anonymous volumes and bind mounts are rejected by the same assertion.

    Container/process ownership: the wrapper `exec`s the .NET process, so the
    container's PID 1 is the application, not a shell — signal handling and
    exit-code propagation are correct.

    Not requested and correctly absent: production orchestration, restart
    policies beyond `restart: "no"` on the migrator, HA, zero-downtime.

---

## SECRET_PATH_ASSESSMENT

    D-03 conformance: full.
      - source: host env `MBS_DATABASE_PASSWORD` -> top-level Compose secret
        (`secrets.database_password.environment`), asserted both literally and
        in the rendered JSON by `static-gate.sh`.
      - grant: explicit per service; `static-gate.sh` asserts
        `(.services.migrator.secrets | length) == 1` and the same for the API.
      - postgres reader: `POSTGRES_PASSWORD_FILE: /run/secrets/database_password`.
      - api/migrator reader: `deployment/fnd05/with-database-secret.sh` reads
        the mounted file, builds `ConnectionStrings__Database` in-process,
        exports it, `unset`s the local, then `exec`s. The value never reaches
        argv and never reaches `Config.Env` in the image or the Compose
        definition.
      - missing secret: fail-closed, negative-probed at runtime (F-06).

    Observation surfaces actually scanned for the sentinel at Final Head:
      success path (`assert_success_contract`): rendered `config --format json`,
        `compose logs --no-color --timestamps`, `docker inspect` of all three
        containers, `docker top` of the API.
      missing-secret path: Compose `up` combined output, rendered config,
        `docker inspect` of all project containers.
      mutation suite (`secret_oracle`): `docker top` of the API only — which is
        the correct surface for the M-04 defect class ("secret enters process
        arguments").

    In CI the sentinel and the password are the same value
    (`${{ runner.temp }}-fnd05-${{ github.run_id }}`), so the scan is
    meaningful rather than vacuous. No real credential is used.

    Residual, accepted by the lock: the constructed connection string lives in
    the application process environment (`/proc/<pid>/environ` inside the
    container). D-03 explicitly locks this reader design
    ("...constructs ConnectionStrings__Database ..., exports it, then execs
    dotnet"), so it is not a finding.

    Repository: `static-gate.sh` rejects tracked `.env`-family and `*.local`
    files; `git ls-files` scan is project-root scoped via `FND05_SOURCE_ROOT`.

    No secret leak path found.

---

## HIDDEN_DEPENDENCIES

| Dependency | Declared / checked? | Assessment |
| --- | --- | --- |
| `docker`, `jq`, `bash` | `require_command` in `verify-compose.sh` (exit 69) | OK |
| `docker`, `git`, `jq`, `grep` | `require_command` in `static-gate.sh` (exit 69) | OK |
| `git` in `verify-mutations.sh` (`git rev-parse --show-toplevel`, `git worktree`) | not in a `require_command` list | Fails loudly at line 4; acceptable |
| `seq`, `mktemp`, `sed`, `awk`, `mapfile`, `printf` | not checked | GNU coreutils / bash builtins; present on every D-07 target. `mapfile` and `$RANDOM` require bash >= 4, D-07 requires >= 5.2 |
| CWD must be the repository root | implicit — `verify-compose.sh` calls `bash tests/fnd05/static-gate.sh` and `-f compose.yaml -f tests/fnd05/failure-compose.yaml` by relative path | Real but fail-loud; CI `run:` executes from the repo root and the operations guide documents repo-root usage. `static-gate.sh` and `verify-mutations.sh` themselves are CWD-independent (`FND05_SOURCE_ROOT` / `--project-directory`) |
| `MBS_DATABASE_PASSWORD` from the host | `: "${MBS_DATABASE_PASSWORD:?...}"` in `static-gate.sh`; defaulted to a sentinel in the two verify scripts | OK, documented in `docs/operations/fnd05-compose-runtime.md` |
| Pre-existing Docker resources | none assumed; every path starts from `up --build` and ends with `down --volumes` | OK |
| Local-only paths | none; no host bind mounts, no absolute host paths in `compose.yaml` | OK |
| Timing / sleep assumptions | only bounded polling (`for attempt in $(seq 1 90)` with a 1s sleep and a state predicate). No fixed sleep is used as ordering evidence; M-01 uses an asserted barrier marker instead of a sleep | OK — conforms to D-06 "no natural race or fixed-sleep coincidence" |
| Docker Desktop vs Linux engine | `platform: linux/amd64` pinned on all three services; digests are index digests | OK for D-07 targets |
| `linux/amd64` assumption | explicit and required by D-07 | OK |
| CRLF / `core.autocrlf` | `static-gate.sh` runs `git -c core.autocrlf=true diff --check`; D-07 declares local `core.autocrlf=true` is not an execution contract | OK. Observed during probing that on a Windows checkout the shell assets materialise with CRLF; this is explicitly out of the D-07 required-target set and must not be raised as a new MUST |

    No dependency on an unsupported platform is imposed. No new MUST is
    proposed for Windows containers, PowerShell-only execution, macOS or arm64.

---

## TEST_ORACLE_ASSESSMENT

    Reachability of the intended failure path:
      - F-02 is reached through the real Migrator production path and asserted
        by the positive reason marker `Migration failed. The deployment must
        not continue.`, not by `exit != 0` alone. `failure_oracle` and
        `assert_failure_contract` both require exit != 0 AND API-never-started
        AND the reason marker.
      - M-07 is a genuine oracle-quality meta mutation: replacing the migrator
        command with `/fnd05/missing-executable` produces a non-zero exit that
        never reaches the intended path, and the oracle correctly rejects it
        with `intended-failure-marker-absent`. It is self-guarding: if the
        override failed to apply, the first predicate would emit
        `migrator-nonzero-masked` and `expect_red` would reject the wrong
        signature.

    Runtime vs source scan:
      - The V-07 no-auto-migration property is enforced in the shipped gate by
        a source scan (`grep -rE 'MigrateAsync|\.Migrate\(|EnsureCreated'` over
        `src/MinimalBankSystem.Api`). Standing alone that would be exactly the
        `mandatory-mutations.md` M-03 invalid-kill pattern.
      - It does not stand alone: `run_m03` establishes a real runtime fixture —
        a genuinely pending migration compiled into the API image — and first
        proves that the *unmutated* API rebuild leaves both
        `__EFMigrationsHistory` and `information_schema.tables` unchanged, then
        injects `MigrateAsync` into `Program.cs` in a detached worktree and
        observes the state delta. This satisfies the determinism contract's
        requirement that the DB not already be at latest. This is the
        best-constructed mutation in the suite.

    Actual process / container state:
      - All ordering and lifecycle assertions read `docker inspect`
        `State.Status`, `State.ExitCode`, `State.StartedAt`, `State.FinishedAt`
        and Compose labels, per D-05.
      - API readiness is proven by an actual TCP connect from inside the
        container (`exec 3<>/dev/tcp/127.0.0.1/8080`), not by container status
        alone, and the poll aborts early if the container leaves `running`.
      - Migration state is read from the real database
        (`psql ... SELECT "MigrationId" FROM public."__EFMigrationsHistory"`),
        not from logs.

    Correspondence between runtime evidence and assertions: good on the product
    path. The two defects found are in the mutation layer (H2-MAJ-01,
    H2-MAJ-02).

    Oracle-signature discipline: `require_literal` in `static-gate.sh` and
    every oracle in `verify-mutations.sh` emit an `ORACLE_SIGNATURE=` line, and
    `expect_red` requires the exact expected signature — a genuinely good
    design. Two gaps: the `jq --exit-status` block in `static-gate.sh` emits no
    signature at all (see H2-MAJ-01 REQUIRED_FIX), and `expect_red` swallows
    the captured output on success, so the CI log contains zero
    `ORACLE_SIGNATURE=` lines and the signatures are only verifiable by reading
    the script rather than from the run record.

---

## MUTATION_ASSESSMENT

| ID | Baseline GREEN | Deterministic precondition asserted | Barrier / fixture | One mutation | Expected RED | Signature matched | Invalid-failure guarded | Restore GREEN | Residue 0 | Verdict |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| M-01 | YES (`baseline`) | YES — `wait_for_state migrator running` + barrier marker asserted from the migrator log | controlled infinite-loop barrier, not a sleep or race | YES | YES | `api-started-before-migrator-success` | YES — a hung migrator with API not running returns 1 without the signature | via `clean_current` + override removal | YES | VALID |
| M-02 | YES | **NO** — marker emitted, never consumed | port-1 fixture + masking wrapper | YES (two coupled edits: fixture + defect) | YES | `migrator-nonzero-masked` | **NO** — a no-op mutation yields the same signature | override removed | YES | **INVALID — see H2-MAJ-02** |
| M-03 | YES (`baseline`) + explicit "pending migration present, state unchanged" check | YES | detached worktree + real pending migration | YES | YES | `api-migration-state-delta` | YES — build failure aborts under `set -e` | worktree removed | YES | VALID (strongest in the suite) |
| M-04 | YES | YES (baseline success path proves argv is clean) | evaluator-only command override expanding the sentinel into argv | YES | YES | `secret-in-actual-argv` | YES | override removed | YES | VALID |
| M-05 | YES (`STATIC_GATE: PASS`, logged) | YES (`MUTATION_PRECONDITION`, digest present in the worktree) | detached worktree | YES | YES (`EXPECTED_RED`) | `postgres-image-digest-missing` | YES | YES (`RESTORED_GREEN`) | YES (`RESIDUE_ZERO`) | VALID — the only mutation that exercises a shipped oracle |
| M-06 | **NO** | **NO** | none | mutation applied but never evaluated | **NO — no oracle executed** | none produced | n/a | **NO** | not checked | **INVALID — see H2-MAJ-01** |
| M-07 | NO | partial — relies on the base image/entrypoint being intact | command override to a missing executable | YES | YES | `intended-failure-marker-absent` | YES — self-guarding via the exit-code predicate | override removed | YES | VALID (missing baseline noted as a non-blocking concern) |
| M-08 | NO | implicit — fresh project ⇒ fresh volume ⇒ empty DB; not asserted | detached worktree, runtime-only `sed` on `Migrator/Program.cs`; **oracle unchanged** | YES | YES | `expected-migration-absent` | YES — a failed `sed` leaves the oracle GREEN and `expect_red` fails; a build break aborts under `set -e`; test/validator untouched | worktree removed | YES | VALID |
| M-09 | NO | YES — asserts `StartedAt != 0001-01-01T00:00:00Z and Status == "exited"` before the oracle | command override that starts, proves the listener, then exits 37 | YES | YES | `api-not-running-after-migrator` | YES | override removed | YES | VALID |
| M-10 | YES (`baseline`) | YES — `docker volume ls --filter label=...project=<m10 project>` asserted non-empty before the reset | same-project named volume, machine-verified | YES (cleanup responsibility weakened to `down` without `--volumes`) | YES — `assert_residue_zero` returns non-zero | residue-remains (no explicit signature string) | YES — the filter is project-scoped, so other projects cannot satisfy it | YES (`down --volumes` then `assert_residue_zero`) | YES | VALID |

    Focus-mutation conclusions requested by the prompt:

    M-01: PASS. The barrier is a controlled, asserted state (migrator held
    `running`, marker `FND05_M01_BARRIER_ESTABLISHED` grepped from the
    migrator log), not a natural race or a fixed sleep. Ordering is weakened
    only after the barrier is confirmed.

    M-03: PASS. A known pending migration is compiled into the API image and
    the baseline API rebuild is proven to leave both migration history and the
    table set unchanged before the mutation is injected. The "DB already at
    latest" invalid-kill class is excluded by construction and by assertion.

    M-05: PASS. All eight required evidence items are present and logged
    (`BASELINE_GREEN`, `MUTATION_PRECONDITION`, `MUTATION_APPLIED`, static-gate
    oracle executed against the isolated worktree, `EXPECTED_RED`,
    `EXPECTED_FAILURE_SIGNATURE=postgres-image-digest-missing`,
    `RESTORED_GREEN`, `RESIDUE_ZERO`). Not re-raised as a quality finding, and
    no new residual false-assurance root cause was found in it.

    M-07: PASS as an oracle-quality meta mutation. It breaks execution before
    the intended path and the oracle rejects the resulting non-zero exit for
    the right reason. It is correctly not counted as a product defect.

    M-08: PASS. The oracle is not modified; only the Migrator runtime is
    (`MigrateAsync` -> `await Task.CompletedTask` in a detached worktree). The
    kill is `exit 0 + expected migration absent from
    public.__EFMigrationsHistory`, observed by the unchanged `success_oracle`.
    Build failure, test modification and validator modification are not
    counted: a build break aborts the script under `set -e`, and a `sed` that
    matched nothing would leave the oracle GREEN, which `expect_red` rejects.
    Residual weakness only: the "expected migration state absent before the
    run" precondition is implied by the fresh project volume rather than
    asserted.

    M-10: PASS. The precondition — a same-project named volume actually
    existing before the clean reset — is machine-verified with a
    project-scoped label filter, so a non-existent cleanup target cannot be
    counted as a kill and another project's resources cannot be
    misattributed.

---

## MUTATION_DETERMINISM_ASSESSMENT

    Contract revisions in force and mutually consistent:
        MUTATION_DETERMINISM_REVISION: fnd05-mutation-determinism-v1
        MANDATORY_MUTATIONS_REVISION:  fnd05-mutations-v2
        MUTATION_KILL_REQUIRES_DETERMINISTIC_PRECONDITION: true
    Confirmed identical in `run.json.revisions`, in the two locked reference
    documents' revision headers, and restated by Issue #43 Section 13.

    Determinism outcome, by contract clause:
      - "no natural race or fixed-sleep coincidence": satisfied everywhere.
        Ordering is always established by an asserted state or a barrier, never
        by elapsed time.
      - "one mutation at a time": satisfied. Each `run_mNN` runs in its own
        Compose project (`...-$RANDOM$RANDOM`) and, where source is touched, in
        its own detached worktree.
      - "unrelated build/YAML/CLI/image failures are invalid kills": satisfied
        structurally — `up --build` is not wrapped in `set +e` except in
        `run_m07`, where the wrong-signature check rejects unrelated failures.
      - "baseline GREEN, expected RED, restore GREEN, residue 0 are required
        for all mutations": **NOT satisfied for M-06** (none of the four), and
        baseline GREEN is absent for M-07, M-08 and M-09.
      - "PRECONDITION_PROPERTY ... confirmable from primary evidence, otherwise
        BLOCKED — PRECONDITION NOT ESTABLISHED": **NOT satisfied for M-02**,
        whose precondition is neither asserted nor observable from the run
        record.

    Report format (`mandatory-mutations.md` Section 13 / determinism contract
    Section 9): only M-05 emits per-field evidence. M-01–M-04 and M-06–M-10
    emit a single `M-0N: KILLED` line, and the locked mutation-report input
    records only `MUTATION_BASELINE: M-01_TO_M-10: PASS`. That is why M-06's
    non-kill and M-02's unverifiable precondition were not visible from the
    artifacts and required reading the suite and the raw job log.

---

## FALSE_ASSURANCE

    1. `M-06: KILLED` is printed on the strength of the mutation script
       confirming its own mutation. Probe P-1 shows the line is emitted
       unchanged after the only oracle protecting the named-volume contract has
       been deleted, and that `static-gate.sh` stays GREEN in that state.
       (H2-MAJ-01)

    2. `M-02: KILLED` is printed on a signature that a no-op mutation
       reproduces exactly, because `failure_oracle` short-circuits on an exit
       code the mutation forces to 0 unconditionally, and the wrapper's own
       precondition marker is discarded. (H2-MAJ-02)

    3. Recorded claims that the primary evidence does not support:
       `final-synthesis/initial-result.md` states for M-06 "Red signal
       observed; baseline uses the named retained volume" — no red signal and
       no baseline exist for M-06. `light-findings-fix-result.md` records
       `MUTATION_BASELINE: M-01_TO_M-10: PASS` and `KNOWN_CONCERNS: NONE`.
       These are consequences of (1); they are listed as evidence of the escape,
       not as separate findings.

    4. Structural risk (not a finding, see NON_BLOCKING_HEAVY_CONCERNS_MAX_3
       item 1): nine of ten mutations are evaluated against oracle copies that
       live inside `verify-mutations.sh`, not against the shipped verification
       in `verify-compose.sh`. Only M-05 exercises a shipped oracle.

    5. Checked and NOT false assurance:
       - `exit != 0` is never sufficient for a PASS anywhere; every negative
         path additionally requires a positive reason marker or a state
         predicate.
       - The V-07 source scan is backed by a real runtime fixture (M-03).
       - `docker top`, `docker inspect` and `psql` are used as primary state,
         not logs or self-reported success.
       - The EXIT traps do not mask the scripts' exit status.
       - Direct-head CI checked out the exact Final Head in both workflows;
         no PR merge-ref evidence was substituted.

---

## REJECTED_UNRESOLVED_LIGHT_RECHECK

    The Light handoff carries:
        rejected_or_unresolved_blocker_major_candidates: []
        escalated_blocker_major_candidates: []
        evidence_incomplete_findings: []

    Therefore no mandatory Light re-check was required, and none was
    performed as a re-check.

    Resolved Light findings were verified as still resolved at Final Head and
    are NOT re-raised:
      - L2-D06-M05 — `run_m05` at Final Head performs baseline GREEN,
        precondition, single mutation, oracle execution, expected RED with
        `postgres-image-digest-missing`, restore GREEN and residue 0; all eight
        markers appear in job 93825335505 at 15:09:53.9–15:09:54.1.
      - L2-D03-SECRET-MISSING — `run_missing_secret_probe` exists at Final Head
        and executed in job 93825335500 at 15:08:10.6 with fail-closed,
        API-not-serving, no-leak and residue-zero all observed.

    Neither is reported as a Heavy finding. H2-MAJ-01 and H2-MAJ-02 are
    independent root causes in code paths the Light fix did not touch
    (`git diff be45366..59aa87f -- tests/fnd05/verify-mutations.sh` modifies
    only `run_m05`).

---

## MERGE_READY

    NO

    Rationale: `mandatory-mutations.md` Section 13 designates an invalid
    mandatory-mutation kill in Final Synthesis as merge-blocking, and Issue #43
    Section 10 requires Blocker/Major = 0 for close. Two mandatory mutations
    (M-06, M-02) do not establish a valid kill. The product implementation
    itself is not the obstacle: no product Blocker or Major was found, and both
    required fixes are confined to `tests/fnd05/verify-mutations.sh` plus a
    signature emission in `tests/fnd05/static-gate.sh`.

---

## LIGHT_GATE_ESCAPES

    LGE-01 — H2-MAJ-01 (M-06 not a valid kill). Should have been caught by the
        same L2 D-06 conformance pass that produced L2-D06-M05; the M-05 and
        M-06 defects are the same class (mutation does not exercise the oracle)
        and M-06 is the more severe of the two.

    LGE-02 — H2-MAJ-02 (M-02 precondition not asserted).

    LGE-03 — PR #153 body still reads "Product implementation has not started."
        and describes the PR as "Final Synthesis execution-control preparation
        only", while the head contains the full implementation. Known process /
        documentation concern per prompt Section 20. NOT treated as a Heavy
        finding and NOT modified during H2.

    LGE-04 — `docs/benchmarks/fnd05-model-comparison/run.json` registry-state
        staleness, present at both the Final Head and the control head:
            status: issue_ready_pass_waiting_for_koo_authorization
            implementation_permitted: false
            gates.koo_start_authorized: false
            gates.common_base_locked: false
            gates.candidate_branches_created: false
            gates.candidate_pull_requests_created: false
            gates.candidate_branch_identity_verified: false
            gates.exact_model_harness_effort_locked: false
            gates.candidate_output_zero_confirmed: false
        while `stage_artifacts` records candidates, both Light reviews, the
        Light fix and `heavy_sol` as locked, and
        `evidence/koo-start-authorization-20260811.md` records the granted
        authorization. The registry's own gate block therefore contradicts its
        stage block. Documentation/process only — it does not affect any
        runtime, failure or false-assurance property, so it is recorded here
        rather than as a Heavy finding, and was not modified during H2.

---

## NON_BLOCKING_HEAVY_CONCERNS_MAX_3

    NBC-01 — Duplicated oracles between the shipped verification and the
        mutation suite. `verify-mutations.sh` defines its own
        `success_oracle`, `failure_oracle`, `secret_oracle` and
        `assert_residue_zero`, which mirror `verify-compose.sh`'s
        `assert_success_contract`, `assert_failure_contract`, its sentinel scan
        and `assert_no_project_residue`. Nine of ten mutations kill the copies;
        only M-05 exercises a shipped oracle. Nothing keeps the copies in sync,
        so a future regression in `verify-compose.sh` would not be detected by
        any mutation. The copies are currently faithful for the mutated
        properties, which is why this is a concern rather than a Major.
        Suggested direction: have `verify-mutations.sh` source the oracle
        definitions from a single shared file that `verify-compose.sh` also
        sources.

    NBC-02 — Missing baseline GREEN for M-07, M-08 and M-09
        (`mandatory-mutations.md` Section 1 requires it for every mutation).
        Risk is contained because all three are self-guarding — a no-op
        mutation produces a wrong signature or leaves the oracle GREEN, either
        of which fails `expect_red`. M-06's missing baseline is covered by
        H2-MAJ-01 and is not double-counted here.

    NBC-03 — Runtime failure-path coverage is single-cause. Only connection
        failure is exercised at runtime (`tests/fnd05/failure-compose.yaml`,
        `POSTGRES_PORT: "1"`). Credential failure, migration timeout
        (`MigratorExitCode.Timeout = 2`), invalid migration history and the
        wrapper's own configuration failures (`exit 78`) are covered only by
        construction. The `service_completed_successfully` gate is exit-code
        driven, so all of them behave identically to the exercised path and
        this is fail-closed in every variant — hence a coverage concern, not a
        finding. Related and also fail-closed: `pg_isready` in the postgres
        healthcheck can report ready against the entrypoint's temporary
        initialisation server; the observed margin in CI was postgres
        `Started` 15:08:11.09 → `Healthy` 15:08:16.59 (5.5 s) against
        `start_period: 5s`. If that ever fired early, the migrator would fail
        to connect and the API would not start — a flake, never a false pass.

---

## UNVERIFIED

    U-01 Local WSL2 / Docker Desktop execution of `verify-compose.sh` and
        `verify-mutations.sh` was not reproduced by H2. The direct-head CI
        evidence on GitHub-hosted Ubuntu 24.04 was used instead. H2's own
        probes ran `docker compose config` and `static-gate.sh` only, from a
        Windows checkout, and started no container.

    U-02 The runtime behaviour of failure paths F-03 (credential failure),
        F-04 (migration timeout), F-05 (invalid history), F-07 and F-08
        (wrapper `exit 78`) is inferred from source plus the exit-code-driven
        Compose gate; it was not executed. No probe was run for these because
        the gate does not distinguish causes.

    U-03 The `ORACLE_SIGNATURE=` strings asserted by `expect_red` do not appear
        in the CI job log, because `expect_red` captures and discards the
        output on success. Signature matching is therefore verified by reading
        the suite at Final Head, not from the run record.

    U-04 H2-MAJ-02's failure scenario (an override that silently stops taking
        effect) was reasoned from the Compose merge semantics for list-form vs
        mapping-form `environment`; it was not reproduced. The finding itself
        does not depend on it — the non-discrimination of the signature is a
        closed-form property of the code at Final Head.

    U-05 Whether the H1 reviewer reached the same or different conclusions is
        unknown to H2 by design; the H1 artifact was not read. Reconciliation
        is the Coordinator's step.

---

## REVIEW_BUDGET

    full review used: 1 / 1

---

## ARTIFACT_LOCK

    This Commit A is the producer commit for the H2 Heavy review artifact. The
    exact Git blob SHA-256 of this file at Commit A is recorded by the
    following `run.json`-only lock commit (Commit B), which sets
    `stage_artifacts.heavy_opus` to `locked`.

    Boundary honoured during H2: no push to
    `agent/issue-43-fnd-05-final-code`, `codex/fnd05-heavy-h1-sol` or `main`;
    no change to PR #153, no Ready-for-review, no merge, no Issue change, no
    candidate change, no new PR. Only `claude/fnd05-heavy-h2-opus` is pushed.
