# FND-05 Light Contract Review

REVIEWER_IDENTITY:

    MODEL: GPT-5.6 Luna
    HARNESS: Codex
    EFFORT: xhigh
    CONTEXT: Fresh Context
    ROLE: adr_issue_ac_contract_conformance
    SLOT: L2
    PROMPT_REVISION: fnd05-light-contract-v2

レビュー対象は、PR本文・merge-ref・candidateではなく、指定されたFinal Synthesis exact Headのみとした。

TARGET_VERIFICATION:

    repository: kooiei-in4a/minimal-bank-system
    target_issue: 43
    target_pr: 153
    base_sha: ee8abbb15758c1a2cfb624791482b755be578da2
    head_sha: be45366af18e55a5f8dd8af932518b690c7a36c0
    pr_state: OPEN
    pr_draft: YES
    pr_merged: NO
    head_branch: agent/issue-43-fnd-05-final-code
    pr_reported_merge_ref: a8f1bc658651758c23562ca46de2f68d8ac4dc58
    direct_head_ci:
      build_and_test:
        run: 31491089738
        job: 93777527104
        result: SUCCESS
        checkout_sha: be45366af18e55a5f8dd8af932518b690c7a36c0
      fnd05_compose_verification:
        run: 31491089797
        jobs: 93777527169, 93777527186
        result: SUCCESS
        checkout_sha: be45366af18e55a5f8dd8af932518b690c7a36c0
    final_synthesis:
      path: docs/benchmarks/fnd05-model-comparison/final-synthesis/initial-result.md
      producer_commit: be45366af18e55a5f8dd8af932518b690c7a36c0
      sha256: 87cc9fef65bf88c02be0405303f40e0cc9bf00762965c48b633cf9f05723cc42
      identity: GPT-5.6 Terra / Codex / xHigh / Fresh Context
    static_gate:
      producer_commit: 259f1f83b25e0e3d9b6b8256bccf7a838400a2c7
      sha256: e104c9be357e9617ff8526bf1c2d75f45809f09ed8a0812556ee58422ad760ee
      target_head: be45366af18e55a5f8dd8af932518b690c7a36c0
    l1:
      producer_commit: c0f0710c3c6dff7855140040b5701243a661b44a
      lock_commit: 9e6e632a594a127a1d96fff1602878086586981f
      sha256: 36da84a82482694deb1260eef35532030fdd78a9cba7189a7d8976c695bcda67
      target_head: be45366af18e55a5f8dd8af932518b690c7a36c0
    run_registry:
      lock_commit: 9e6e632a594a127a1d96fff1602878086586981f
      sha256: 5401ae6830dec2a5fb3e7206d4b2937561767c0c66929911857c9e62630ea8f1
      light_l1_status: locked

PR metadata contains a merge_commit_sha, but PR #153 is still open and unmerged. That ref was not used as the product review target. The direct-head workflow logs show checkout of be45366af18e55a5f8dd8af932518b690c7a36c0 and assert it equals GITHUB_SHA.

VERDICT:

FIX_REQUIRED

The product implementation covers the main FND-05 runtime contract, and the exact-head runtime runs are successful. The mandatory D-06 mutation evidence is not complete: M-05 is reported as killed without executing the required digest/image-policy oracle. D-03's missing-host-secret negative runtime path is also unverified.

TRACEABILITY_MATRIX:

| AC / Requirement | Implementation | Test / validator | Runtime evidence | Result | Gap |
| --- | --- | --- | --- | --- | --- |
| ADR-0001: Docker Compose v2, PostgreSQL 18, single application runtime | compose.yaml defines PostgreSQL 18.4, API, and Compose project; approved digests and linux/amd64 are present. | Consumed Static Gate; verify-compose.sh runs Compose config and start path. | Run 31491089797, jobs 93777527169/93777527186, checked out exact Head and completed successfully. | PASS | None for FND-05 scope. |
| AC: Docker Compose can start PostgreSQL | postgres service has the approved image, healthcheck, and database configuration. | config --quiet, up, state polling, and success assertions. | Compose log shows PostgreSQL started; job ended COMPOSE_RUNTIME_VERIFICATION: PASS. | PASS | None. |
| AC: API starts in the same Compose project | api service is in the same named project and has a runtime image/entrypoint. | docker inspect asserts API running and com.docker.compose.project/service labels. | Exact-head Compose job observes API start and running state. | PASS | None. |
| AC: FND-04 explicit Migrator is connected to Compose | migrator is a one-shot service using MinimalBankSystem.Migrator; FND-04 MigrateAsync path is reused. | Migrator exit code, log marker, and migration-history assertions. | Exact-head Compose log observes Migrator start and exit before API; mutation job also reaches the Migrator path. | PASS | None. |
| AC: PostgreSQL usable -> Migrator -> API | PostgreSQL health gates Migrator via service_healthy; API gates on Migrator service_completed_successfully. | External state assertions use status, exit code, labels, and timestamps. | Direct-head log observes PostgreSQL start, Migrator exit, then API start; runtime validator passes. | PASS | None. |
| AC: clean DB migration completes before API is ready/serving | Migrator applies pending migrations; API listener is checked only after the gate. | State.ExitCode == 0, expected history, API.StartedAt >= Migrator.FinishedAt, and listener probe. | Run 31491089797 succeeds on exact Head; M-01 mutation is killed by the ordering oracle. | PASS | None. |
| AC: migration failure prevents API start | service_completed_successfully blocks API after a failing Migrator; failure fixture makes PostgreSQL unreachable and preserves the failure marker. | assert_failure_contract requires non-zero Migrator and API never-started/created state. | Same exact-head Compose job executes the isolated failure path and returns PASS. | PASS | No stored state snapshot beyond validator assertions. |
| AC: migration-not-run state is not silently accepted | API cannot pass its dependency gate when Migrator does not complete successfully. | Failure path plus M-02/M-07 mutation oracles. | Mutation job records M-02 and M-07 as killed and ends MUTATION_SUITE: PASS. | PASS | None for the baseline contract. |
| AC / ADR-0009: normal API startup does not migrate | API Program.cs only configures the DbContext; no schema-evolution call is in the normal startup path. | Consumed Static Gate source prohibition; M-03 uses a pending migration fixture, first proves baseline state is unchanged, then injects API MigrateAsync and expects a state delta. | Mutation job records M-03 killed at exact Head; the baseline pending-state probe must pass for the job to continue. | PASS | None. |
| AC: PostgreSQL data uses a named volume | postgres_data is a named Compose volume mounted to PostgreSQL. | Static/rendered config check plus retained-stop volume inspection and clean-reset residue assertion. | Exact-head log shows named volume creation/removal; Compose job passes. | PASS | None. |
| D-02: exact image identities and platform | PostgreSQL, SDK, and ASP.NET references are digest-qualified and services/Dockerfiles use linux/amd64. | Consumed locked Static Gate checks source and rendered config. | Exact-head build log resolves the locked SDK/runtime digests; Compose validator passes the locked PostgreSQL/rendered-config checks. | PASS | M-05 mutation oracle gap is recorded separately under D-06. |
| AC / D-03: secret is externally supplied, explicitly granted, and not committed | Top-level secrets.environment uses MBS_DATABASE_PASSWORD; services receive explicit secret grants; wrapper reads the file and constructs the connection string in-process. | Static Gate plus sentinel search in rendered config, logs, docker inspect, and API docker top; source has readable/non-empty fail-closed checks. | Exact-head Compose job runs with an external sentinel and completes without sentinel exposure. | PARTIAL | Missing-host-secret runtime negative path is not exercised or retained as evidence. |
| AC / D-04: reproducible start, stop, restart, clean reset | Operations guide documents canonical commands; verification uses up, retained down, a second up, and down --volumes --remove-orphans. | Volume retention, migration-history stability on restart, and project-scoped residue assertions. | Exact-head Compose job returns PASS after clean start, retained restart, failure path, and cleanup. | PASS | None. |
| D-05: external state evidence | Validator reads Compose JSON, docker inspect, volume labels, timestamped logs, and PostgreSQL __EFMigrationsHistory. | assert_success_contract and assert_failure_contract assert the locked success/failure rules, including started-then-exited versus never-started. | Direct-head jobs execute those external commands and return SUCCESS/PASS; job logs include exact checkout and final validator markers. | PASS | Raw snapshots are asserted in-process rather than persisted as separate evidence files. |
| D-06: deterministic failure injection and mutation contract | Test-only failure fixture and isolated mutation worktrees/overrides exist; production branch is not mutated. | M-01..M-10 job executes one mutation at a time and records kill markers. | Exact-head mutation job records M-01 through M-10 as KILLED and MUTATION_SUITE: PASS. | PARTIAL | M-05 does not run the required static/resolved-image oracle; its KILLED marker only proves the digest string was removed from a temp file. Full baseline/RED/restore evidence is also not retained for M-05. |
| D-07: Linux/amd64 cross-platform contract | Compose services pin linux/amd64; shell validators use Bash and jq and POSIX paths. | Workflow runs on ubuntu-24.04; scripts execute under Bash in both direct-head jobs. | Actions logs identify Ubuntu 24.04.4; exact-head build/Compose/mutation jobs succeed. | PASS | Unsupported Windows/PowerShell/macOS/arm64 targets are not treated as required. |
| AC: no business schema or business data is added | PR diff contains Compose/runtime/test/docs assets only; no business migration/entity/table is introduced. | Diff/file-scope inspection and expected InitialFoundation history assertion. | Exact-head Compose path applies the existing foundation migration; no business-schema artifact is part of the target diff. | PASS | FND-05 does not own later business-schema upgrade evidence. |
| ADR-0008 / ADR-0009 dependency boundary | Named volume, external credential injection, explicit migration, PostgreSQL history, and no API auto-migration align with Accepted ADRs; FND-04 owns migration machinery. | Dependency #42 is COMPLETE/MERGED; FND-05 validators consume the Migrator contract without reimplementing it. | Target-head build and Compose runtime execute the dependency path successfully. | PASS | FND-05 does not claim backup/restore or business migration ownership. |
| Issue #43 scope / out of scope | Changed files are Compose, Dockerfiles, secret wrapper, FND-05 validators/workflow, operations docs, and benchmark evidence. No health endpoint, business endpoint/schema, backup/restore, production deployment, or scheduled service is added. | Exact base-to-head changed-file list and consumed L1 scope result. | Direct-head runtime only exercises the FND-05 runtime path. | PASS | Benchmark evidence files are process artifacts, not product-scope additions. |
| D-08 / review identity separation | Final Synthesis artifact records GPT-5.6 Terra / Codex / xHigh / Fresh Context. | Locked run registry and initial-result SHA identify the producer artifact. | Initial-result artifact at exact Head is hash-verified; no merge-ref is used. | PASS | None. |

MISSING_IMPLEMENTATION:

None identified for the Issue #43 product scope. The observed gaps are in required verification/evidence, not in the baseline Compose implementation.

MISSING_TEST:

- D-06 M-05 lacks an actual static/resolved-image policy oracle invocation. tests/fnd05/verify-mutations.sh::run_m05 only removes the PostgreSQL digest from a temporary file, checks that the literal is absent, and prints M-05: KILLED.
- D-03 has no isolated test that omits MBS_DATABASE_PASSWORD/the mounted secret and verifies fail-closed behavior before application/Migrator work, with no leakage.

MISSING_RUNTIME_EVIDENCE:

- No valid expected-RED runtime evidence exists for M-05. The direct-head mutation log's M-05: KILLED is not evidence that a digest validator detected the defect.
- The missing-secret negative path has no exact-head runtime run/evidence.

SCOPE_DRIFT:

None observed. The PR's 13 changed paths remain within FND-05 runtime/supporting configuration, verification, operations documentation, and benchmark evidence. Exact service names and file placement were not promoted to independent ACs beyond the locked observable contracts.

IDENTITY_GAPS:

None for the required target. Base, Head, PR state, direct-head checkout, Final Synthesis, Static Gate, L1, and run registry identities match the locked inputs. PR metadata exposes merge-ref a8f1bc658651758c23562ca46de2f68d8ac4dc58, but it is not the product review target and PR #153 is not merged.

FINDINGS:

ID: L2-D06-M05
CATEGORY: TEST
SEVERITY_CANDIDATE: Major
REQUIREMENT: Locked D-06 / mandatory M-05 must detect removal of digest pinning with the expected static/resolved-image oracle; a valid kill requires baseline GREEN, deterministic mutation, expected RED, restore GREEN, and residue 0.
EXPECTED_TRACE: Digest-qualified baseline -> tag-only mutation -> static/image-policy validator reports the expected digest-absence signature -> mutation is reverted -> validator is GREEN -> cleanup/residue is zero.
OBSERVED_TRACE: run_m05 in tests/fnd05/verify-mutations.sh writes a sed-mutated temporary Compose file, checks only that the digest literal is absent, prints M-05: KILLED, and deletes the temporary file. It does not invoke tests/fnd05/static-gate.sh, a rendered-image validator, or a baseline/restore assertion. CI job 93777527186 records only M-05: KILLED and the aggregate MUTATION_SUITE: PASS.
GAP: The mandatory mutation suite can report a kill without proving that the required protected image-identity contract is detected. This is invalid mutation evidence under the locked D-06 contract and leaves D-02 mutation assurance incomplete.
MINIMAL_FIX: In the evaluator/test-only path, run the unchanged image-policy oracle against an isolated tag-only mutation and assert the expected RED signature; record baseline GREEN, restore GREEN, and cleanup/residue evidence. Do not change product code or disclose the evaluator patch to the candidate.
HEAVY_ESCALATION_REQUIRED: YES

ID: L2-D03-SECRET-MISSING
CATEGORY: TEST
SEVERITY_CANDIDATE: Minor
REQUIREMENT: Locked D-03 requires missing secret input to fail closed before application/Migrator work and to avoid disclosure.
EXPECTED_TRACE: Remove the host secret in an isolated Compose probe -> startup/migration path fails closed -> API is not serving -> no secret is exposed in argv/logs/rendered config -> cleanup succeeds.
OBSERVED_TRACE: The wrapper implements readable/non-empty checks and exits 78, and the success path uses a sentinel non-disclosure check. No exact-head test removes the host secret and verifies the negative runtime boundary.
GAP: The implementation branch is present, but the required negative test and actual runtime evidence are absent.
MINIMAL_FIX: Add an isolated no-secret validator probe with explicit fail-closed and no-leak assertions, then retain its exact-head CI evidence.
HEAVY_ESCALATION_REQUIRED: NO

ESCALATIONS:

- ESCALATION to the D-06 mutation/test owner and coordinator: L2-D06-M05 is a Major root-cause candidate in the mandatory mutation validator. Static Gate baseline evidence remains locked and was consumed, not re-scored.
- The D-03 missing-secret item is a verification gap only; no product-code escalation is raised.

UNVERIFIED:

- D-03 missing-host-secret runtime behavior.
- A valid M-05 expected-RED/restore-GREEN/residue-zero evidence chain.
- M-06 has a direct rendered-config violation assertion, but the script does not retain a separate baseline/restore cycle; this is recorded as an evidence limitation, not a separate product finding in this L2 review.
- Raw external-state snapshots are not retained as standalone CI artifacts; the validator assertions and exact-head job results are available.

ARTIFACT_LOCK:

    artifact_path: docs/benchmarks/fnd05-model-comparison/reviews/fnd05-light-l2-contract-conformance-gpt-5.6-luna.md
    content_sha256: recorded in run.json.stage_artifacts.light_l2 at Commit B
    prompt_revision: fnd05-light-contract-v2
    target_head_sha: be45366af18e55a5f8dd8af932518b690c7a36c0
    source_artifact_refs:
      - docs/benchmarks/fnd05-model-comparison/final-synthesis/initial-result.md@be45366af18e55a5f8dd8af932518b690c7a36c0#sha256:87cc9fef65bf88c02be0405303f40e0cc9bf00762965c48b633cf9f05723cc42
      - docs/benchmarks/fnd05-model-comparison/final-synthesis/static-gate-result.md@259f1f83b25e0e3d9b6b8256bccf7a838400a2c7#sha256:e104c9be357e9617ff8526bf1c2d75f45809f09ed8a0812556ee58422ad760ee
      - docs/benchmarks/fnd05-model-comparison/reviews/fnd05-light-l1-project-quality-composer-2.5.md@c0f0710c3c6dff7855140040b5701243a661b44a#sha256:36da84a82482694deb1260eef35532030fdd78a9cba7189a7d8976c695bcda67
      - run.json@9e6e632a594a127a1d96fff1602878086586981f#sha256:5401ae6830dec2a5fb3e7206d4b2937561767c0c66929911857c9e62630ea8f1
      - github-actions-run:31491089738
      - github-actions-run:31491089797
    producer_slot: L2
    producer_commit_sha: recorded in run.json.stage_artifacts.light_l2 at Commit B
    status: pending_lock_commit
