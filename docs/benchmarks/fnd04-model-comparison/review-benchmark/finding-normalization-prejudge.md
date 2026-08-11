# FND-04 Final Synthesis — Pre-Judge Finding Normalization

Status: **RAW REVIEW CAPTURE COMPLETE / JUDGE ADJUDICATION REQUIRED**

```yaml
BENCHMARK_ID: fnd04-final-synthesis-independent-review
RUN_ID: fnd04-final-review-20260810
TARGET_PR: 140
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
HEAD_SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
REVIEWER_POOL: fnd04-reviewer-pool-v2
RAW_RESULTS: 5_of_5
```

この文書はraw reviewer findingをroot cause候補単位に束ねるCollector用pre-judge artifactである。Gold / Reference verdictではない。Severity / blockingの最終裁定はJudge quorumと一次証拠に基づく。

## Reviewer verdicts

| Slot | Reviewer | Verdict | Merge-ready | B | M | m | N |
|---|---|---|---:|---:|---:|---:|---:|
| R1 | GPT-5.6 Sol / Codex | APPROVE_WITH_FINDINGS | YES | 0 | 0 | 1 | 1 |
| R2 | Claude Opus 5 / Claude Code | CHANGES_REQUIRED | NO | 0 | 1 | 1 | 2 |
| R3 | GPT-5.6 Luna / Codex | APPROVE_WITH_FINDINGS | YES | 0 | 0 | 1 | 0 |
| R4 | GPT-5.6 Sol / Browser | APPROVE | YES | 0 | 0 | 0 | 0 |
| R5 | Cursor Auto / Cursor | APPROVE_WITH_FINDINGS | YES | 0 | 0 | 2 | 0 |

## NR-01 — C8-M01 regression test / false assurance

Mapped findings / observations:

- R2-F01 — **Major / blocking**
- R5-F01 — Minor / nonblocking
- R4 — no finding, but explicitly states the committed blocklist-only assertion is weaker when considered alone
- R1 / R3 — no finding

Shared root-cause candidate:

`DesignTimeConnectionSafetyTests` proves only `exit != 0` and absence of a fixed forbidden-string list. It does not positively prove that the production design-time factory / Npgsql connection-required path was reached or that no destination was configured. A nonzero tooling/build failure or an off-blocklist fabricated destination can potentially satisfy the test.

Important distinction:

- Production Head behavior itself is independently observed as correct / fail-closed by multiple reviewers.
- The dispute is whether the **committed regression evidence** is sufficiently sensitive to the prior C8-M01 defect class and whether that assurance gap is merge-blocking.

R2 supplied mutation evidence that:

1. an off-blocklist fabricated destination (`Host=db;Database=ambient_fallback`) can be introduced while the committed regression remains green;
2. preventing the `--no-build` command from reaching the factory can still leave the regression green.

Pre-judge disposition: **VALID ROOT-CAUSE CANDIDATE / SEVERITY DISPUTED / JUDGE REQUIRED**.

Judge questions:

1. Does Issue #42 / locked Final Synthesis contract require this committed regression itself to prove the production factory path and no-destination state, or is correct production behavior plus other evidence sufficient?
2. If the regression can pass when the guarded defect class is reintroduced, is that Major false assurance or Minor test-quality weakness?
3. What is the minimum robust fix: positive failure-origin assertions, direct options/connection inspection, removal of `--no-build`, or another approach?

## NR-02 — 60s CommandTimeout vs CTS exit classification

Mapped findings:

- R1-F01 — Minor
- R5-F02 — Minor
- R2 — no finding; reviewer mutation evidence suggests the committed timeout test is sensitive to both timeout mechanisms and observed exit 2
- R4 / R3 — no finding

Shared root-cause candidate:

Npgsql command timeout and whole-operation CTS both use 60 seconds. `Program.cs` maps CTS-observed `OperationCanceledException` to exit 2 and general exceptions to exit 1. R1/R5 argue two independent timeout mechanisms at the same nominal deadline can make timeout classification nondeterministic; R2 argues observed/mutated behavior supports the current classification and does not raise a finding.

Pre-judge disposition: **DISPUTED MINOR CANDIDATE / JUDGE REQUIRED**.

Judge questions:

- Is there a real reachable ordering that yields a provider timeout as generic Failure=1 before CTS classification?
- Does Issue #42 require timeout-specific exit code 2 or merely bounded nonzero failure?
- Is the current single lock-based test sufficient for the documented taxonomy?

## NR-03 — temporary model-drift negative evidence

Mapped findings / observations:

- R3-F01 — Minor
- R4 — P08 PARTIAL, no finding
- R5 — P08 PARTIAL, no finding
- R1 — independently reproduced temporary model drift -> exit 1 -> clean recovery
- R2 — independently reproduced temporary model drift -> exit 1 -> clean recovery

Pre-judge disposition: **EVIDENCE LIMIT RESOLVED BY INDEPENDENT REPRODUCTION / LIKELY NOT A PRODUCT FINDING**.

The original author evidence was local-only, but two independent reviewers subsequently reproduced the negative probe against the locked Head without retaining synthetic artifacts.

## NR-04 — CI identity wording

Mapped findings:

- R1-F02 — Nit
- R2-F04 — Nit
- R4 — P15 PARTIAL due inability to independently resolve direct-head run
- R3 / R5 — direct-head push run identified

Coordinator independently verified:

- PR merge-ref run `31350916189`: SUCCESS, checkout `d12de2ae07003a10d19d576808cf88ec7796da23`
- direct-head push run `31350870902`: SUCCESS, checkout `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- direct-head run: build 0 warnings / 0 errors, pending-model PASS, non-PG 42 passed, real PG 23 passed

Pre-judge disposition: **VALID NIT / METADATA ONLY / EVIDENCE GAP RESOLVED**.

## NR-05 — Migrator exit-code taxonomy coverage

Mapped finding:

- R2-F02 — Minor

Root-cause candidate:

Negative failure tests assert only nonzero, while README documents 0=success / 1=failure / 2=timeout. A regression that misclassifies ordinary failure as timeout could pass the negative tests.

Pre-judge disposition: **UNIQUE MINOR CANDIDATE / JUDGE MAY CONFIRM OR REJECT**.

## NR-06 — no-information assertions / naming

Mapped finding:

- R2-F03 — Nit

Includes literal/constant assertions and low-information checks where stronger behavioral tests exist elsewhere.

Pre-judge disposition: **NIT CANDIDATE / NONBLOCKING**.

## Pre-Judge merge gate

```text
Raw reviewers completed:              5 / 5
Reviewers with Blocker/Major:         1 / 5
Blocking root-cause candidates:       NR-01 only
Production defect confirmed:          NO
Assurance defect candidate:           YES — NR-01
Direct-head CI:                       VERIFIED SUCCESS
Final merge-ready adjudication:       NOT YET DETERMINED
Next gate:                             Judge A + Judge B
```

Do not modify PR #140 implementation before Judge adjudication solely because one reviewer assigned Major. Judge A/B must independently decide root-cause validity, Severity, blocking status and required fix. Conditional Judge C is used only if the first two Judges disagree on reference verdict, blocking root cause or merge-readiness.