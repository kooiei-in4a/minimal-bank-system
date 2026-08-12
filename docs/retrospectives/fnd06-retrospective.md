# FND-06 Retrospective

## Status

RETROSPECTIVE: KOO DECISIONS APPROVED / LIGHT REVIEW PENDING

FND-06 product implementation, review chain, merge, post-merge CI, retrospective evidence preflight, and Koo final retrospective decisions are complete.

This document records the approved FND-06 retrospective decisions and the proposed FND-07+ process baseline. It does not authorize WP-2 or later implementation by itself.

```yaml
RETROSPECTIVE:
  STATUS: KOO_DECISIONS_APPROVED
  LIGHT_REVIEW: PENDING

KOO_FINAL_APPROVAL:
  STATUS: APPROVED
  DATE: 2026-08-13

FND06_PRODUCT:
  ISSUE: 44
  FINAL_PR: 160
  FINAL_PRODUCT_HEAD: bebe05a2e583ff05def01422f65e9fd1e1717a7b
  MERGE_COMMIT: acd0896119ab254ad652b8c362b46a9e75417340
  ISSUE_STATE: CLOSED
  POST_MERGE_BUILD_AND_TEST_RUN: 31620556972
  POST_MERGE_BUILD_AND_TEST_RESULT: SUCCESS

REVIEW_CHAIN:
  LIGHT_REVIEW:
    INITIAL_VERDICT: FIX_REQUIRED
    BLOCKER: 0
    MAJOR: 1
  MAJ01_TARGETED_REREVIEW:
    RESULT: FIXED
  HEAVY_H1:
    VERDICT: APPROVE
    BLOCKER: 0
    MAJOR: 0
    MINOR: 0
  HEAVY_H2:
    VERDICT: APPROVE_WITH_MINOR
    BLOCKER: 0
    MAJOR: 0
    MINOR: 5
  CONDITIONAL_JUDGE:
    REQUIRED: false

WP1:
  ISSUE: 33
  FOUNDATION_READY: PASS
  PRODUCT_FOUNDATION: COMPLETE
  ISSUE_STATE: OPEN_PENDING_RETROSPECTIVE_AND_NEXT_AUTHORIZATION

NEXT_STEP_AUTHORIZATION:
  WP2_OR_LATER: NOT_GRANTED_BY_THIS_DOCUMENT
```

---

## 1. Overall Assessment

FND-06 is assessed as a successful iteration both as a product implementation and as a process-simplification pilot.

The product contract was merged successfully, post-merge CI passed, and the review chain ended with Blocker / Major = 0 / 0. At the same time, the simplified process still detected a real Major during Light Review: the critical mutation harness proved that the target test became RED, but did not yet prove that it failed for the intended semantic reason.

This is important process evidence. FND-06 reduced candidate and review overhead compared with heavier previous runs, while the retained review gates still found a meaningful false-assurance risk before closure.

The approved direction is therefore not to restore the heavier FND-05 process. Instead, FND-06 becomes the baseline for FND-07 and later work, with additional risk-based simplification.

---

## 2. Fixed Facts

- Target Issue: #44 `[FND-06] live／ready health contractを実装する`
- Final Synthesis PR: #160
- Final Product Head: `bebe05a2e583ff05def01422f65e9fd1e1717a7b`
- Merge commit / current retrospective snapshot main: `acd0896119ab254ad652b8c362b46a9e75417340`
- Issue #44: CLOSED / COMPLETED
- post-merge `Build and Test`: run `31620556972` / SUCCESS
- WP-1 #33: Foundation Ready PASS / product foundation COMPLETE / Issue OPEN
- Light Review: initial Major 1
- Major-01 targeted fix and targeted re-review: FIXED
- Heavy H1: APPROVE / Blocker 0 / Major 0 / Minor 0
- Heavy H2: APPROVE_WITH_MINOR / Blocker 0 / Major 0 / Minor 5
- Conditional Judge: NOT REQUIRED

### Candidate pilot

FND-06 used three independent candidates:

| Candidate | Model / Harness | Selection |
| --- | --- | --- |
| C1 | GPT-5.6 Sol / Codex | Rank 2 |
| C2 | Claude Opus 5 / Claude Code | Rank 1 / selected primary |
| C3 | Grok 4.5 / Cursor | Rank 3 |

The selected candidate was not merged directly. Final Synthesis was reconstructed from the common base.

A separate non-normative implementation-only observation recorded C2=97, C1=94, C3=91. This was not the canonical overall candidate score.

---

## 3. What Worked Well

### Three candidates gave enough comparison without turning comparison into the main job

Three independent implementations were sufficient to expose different implementation choices and support a meaningful selection. Compared with larger candidate pools used in earlier experiments, the operator burden was lower while comparative value remained.

The approved baseline is therefore three candidates for benchmark-oriented implementation work. Candidate count is not expanded merely to increase sample size unless there is a specific experimental reason.

### Final Synthesis remained valuable

The best-ranked candidate was not treated as merge-ready by definition. Final Synthesis separated candidate selection from production integration and allowed the final implementation to be reconstructed against the exact common base.

This distinction is retained. When candidate comparison is performed, Final Synthesis remains required.

### One Light Review was enough to find a meaningful Major

The Light Review found MAJ-01 in the critical mutation harness. The problem was not the `/health/live` or `/health/ready` implementation itself. The problem was false assurance in verification: the harness checked that the target test became RED and that the test name appeared, but did not yet prove that the intended defect caused the failure.

This is strong evidence that one focused semantic Light Review has high cost-effectiveness and should remain mandatory.

### Targeted fix and targeted re-review avoided a full restart

After MAJ-01, the changed surface was fixed and re-reviewed directly. The resulting exact Head was then locked for Heavy Review. There was no need to restart the whole review chain.

This preserves quality while reducing repeated review cost.

### Critical mutation became materially stronger

The final mutation flow verifies:

```text
BASELINE_GREEN
→ mutation applied
→ MUTATION_RED
→ mutation-specific semantic signature confirmed
→ source restored
→ RESTORE_GREEN
```

The semantic signature step is the important improvement. A RED result alone is no longer treated as proof that the intended failure class was detected.

### Diverse review perspectives still produced useful information

H1 Architecture / Contract / Integration found no remaining findings. H2 Adversarial Failure / False Assurance / Operational Safety found five additional Minor findings.

The second perspective was therefore not useless, but all five findings were non-blocking. This supports a risk-based policy: one Heavy Review by default, a second Heavy Review only when risk or evidence justifies it.

### Fail-closed behavior worked as intended

The first Final Synthesis attempt stopped before implementation because GitHub control metadata still contained stale prohibition state. The agent preferred GitHub authority over the prompt's optimistic state and stopped rather than silently continuing.

That STOP created operator cost, but it prevented execution under contradictory authorization. The fail-closed rule is therefore retained.

### Historical handoff and current target identity were successfully separated

The original Final Synthesis → Light Review handoff remained immutable after the target Head advanced. A separate current exact target record was created for Heavy Review.

This preserved history without allowing reviewers to accidentally use an obsolete Head.

---

## 4. What Did Not Work Well

### Control state was discovered too late

The stale control metadata was found only after an agent had been launched. The agent stopped correctly, but the work could have been avoided with a small stage-entry preflight.

The process should detect authorization and identity drift before an expensive agent run begins.

### Current control state is still distributed

Parent #3 contains historical body text that no longer represents the current WP-1 state, while later current-state comments supersede that operational metadata. WP-1 #33 and Issue #44 contain more current information.

This can be reconstructed correctly, but a human operator must know which record is current. The problem is not preservation of history; the problem is that current authority requires navigation across multiple records.

### Heavy Review should not automatically mean two full reviews

H2 found five useful Minors, but none were merge-blocking. Requiring two full Heavy Reviews for every future low- or medium-risk change would create cost that is not always justified by the additional findings.

The number of Heavy Reviews should therefore depend on risk, not on a fixed ritual.

### Detailed double scoring can become its own workload

Separating implementation quality from overall candidate quality is useful, but producing two elaborate 100-point scorecards for every run would recreate the process overhead that FND-06 is trying to reduce.

The distinction should be retained; detailed numeric scoring should be used only when benchmark value justifies the cost.

---

## 5. Approved Process Decisions

```yaml
FND06_RETROSPECTIVE_DECISIONS:
  STATUS: KOO_APPROVED

  OVERALL:
    RESULT: SUCCESS
    DIRECTION: USE_FND06_AS_NEXT_BASELINE

  CANDIDATES:
    DECISION: KEEP
    POLICY: 3_INDEPENDENT_CANDIDATES

  FINAL_SYNTHESIS:
    DECISION: KEEP
    POLICY: REQUIRED_WHEN_CANDIDATE_COMPARISON_IS_USED

  LIGHT_REVIEW:
    DECISION: KEEP
    POLICY: ONE_INDEPENDENT_SEMANTIC_REVIEW_REQUIRED

  HEAVY_REVIEW:
    DECISION: SIMPLIFY
    DEFAULT: ONE_REVIEW
    SECOND_REVIEW: RISK_BASED_CONDITIONAL

  CRITICAL_MUTATION:
    DECISION: KEEP
    MAX: 3
    TARGET: HIGHEST_RISK_FAILURE_CLASSES_ONLY

  SEMANTIC_SIGNATURE:
    DECISION: ADOPT
    REQUIREMENT: EXPECTED_FAILURE_REASON_MUST_BE_CONFIRMED

  STAGE_ENTRY_CHECK:
    DECISION: ADOPT
    TYPE: LIGHTWEIGHT_MECHANICAL_PREFLIGHT
    FULL_SEMANTIC_REVIEW: false
    FULL_GATE_REEVALUATION: false

  JIT_HANDOFF:
    DECISION: KEEP_AND_SIMPLIFY
    HISTORICAL_HANDOFF: IMMUTABLE
    CURRENT_EXACT_TARGET: SEPARATE_RECORD

  IMPLEMENTATION_ONLY_EVALUATION:
    DECISION: ADOPT_LIGHTWEIGHT
    AXES:
      - IMPLEMENTATION_QUALITY
      - OVERALL_CANDIDATE_QUALITY
    DETAILED_NUMERIC_SCORING: BENCHMARK_ONLY_WHEN_USEFUL

  CONTROL_STATE:
    DECISION: ADOPT_SINGLE_CURRENT_AUTHORITY
    REQUIRED_FIELDS:
      - CURRENT_STAGE
      - AUTHORIZATION_STATE
      - EXACT_HEAD
      - NEXT_ACTION
```

---

## 6. Stage Entry Check — Approved Shape

The Stage Entry Check is intentionally small. It is not another semantic review and not another gate ceremony.

Before a new major stage or agent launch, check only:

```yaml
STAGE_ENTRY_CHECK:
  - PREVIOUS_STAGE_COMPLETE
  - NEXT_STAGE_AUTHORIZED
  - CONTROL_STATE_CONSISTENT
  - TARGET_SHA_EXACT
  - NO_UNRESOLVED_BLOCKER_MAJOR
```

If the check passes, proceed. If it detects a contradiction, stop and reconcile control metadata before launching the expensive stage.

The business purpose is simple: detect a missing work permit before sending the worker to the site.

---

## 7. H2 Minor Findings — Final Disposition

The five H2 Minor findings remain non-blocking. They do not reopen FND-06 product implementation.

### MIN-01 — Compose log disclosure oracle is negative-only

Finding: the oracle proves that prohibited disclosure is absent, but lacks a positive control proving that the inspected path would actually observe the sentinel / relevant output if present.

**Disposition: ADOPT_PROCESS_IMPROVEMENT / FIX_BEFORE_ORACLE_REUSE**

For future security / non-disclosure oracles, a negative assertion should have a positive control when a vacuous pass is plausible. FND-06 is not reopened solely to retrofit this harness after product closure.

### MIN-02 — Run B empty-schema check can vacuously pass if `psql` fails

**Disposition: FIX_BEFORE_HARNESS_REUSE**

A verification command failure must be distinguished from an intentionally empty result. Before the Run B harness is reused as future evidence, the command success path must be asserted explicitly.

This is a verification-harness hardening item, not a reason to reopen the already-completed FND-06 product implementation.

### MIN-03 — Slow / hung PostgreSQL timeout behavior is not empirically verified

The implementation has a bounded readiness budget, but slow / hung PostgreSQL behavior was not empirically exercised and production Npgsql timeout / command-timeout policy was not explicitly pinned as part of FND-06.

**Disposition: DEFER_TO_OPERATIONAL_OR_RELEASE_READINESS**

This risk is acceptable for the completed WP-1 internal foundation scope. It must be reconsidered before Release Ready or any production-like deployment claim. FND-06 does not expand scope into production operations retroactively.

### MIN-04 — FND-06 workflow does not explicitly establish .NET SDK via setup-dotnet / global.json

**Disposition: ADOPT_CI_PORTABILITY_IMPROVEMENT**

New or materially revised dedicated workflows should make their SDK identity explicit or reuse the repository's canonical setup path. The current successful run remains valid evidence; this does not require reopening FND-06.

### MIN-05 — RestartablePostgreSqlFixture cleanup / fixed-port risk

The fixture does not reuse the existing ownership-label cleanup mechanism and retains abnormal-termination residue / fixed-port TOCTOU risk.

**Disposition: FIX_BEFORE_BROAD_REUSE_OR_PARALLEL_EXPANSION**

The current FND-06 scope remains accepted. Before this fixture pattern is expanded to more concurrent or long-lived test scenarios, cleanup ownership and port allocation should be hardened.

---

## 8. Quality Gain vs Process Cost

| Process element | Observed quality gain | Process cost | Approved direction |
| --- | --- | --- | --- |
| 3 candidates | meaningful implementation comparison | three executions + evaluation | KEEP |
| Final Synthesis | separates candidate rank from production merge | one additional implementation stage | KEEP |
| 1 Light Review | found a real Major / false assurance risk | one independent semantic review | KEEP |
| Heavy H1 | no additional finding in this run | reviewer cost | default Heavy Review remains one |
| Heavy H2 | found 5 distinct Minor risks | second full reviewer cost | conditional only |
| Critical Mutation max 3 | directly tests highest-risk false assurance classes | targeted test complexity | KEEP |
| semantic signature | proves mutation died for intended reason | small oracle implementation cost | ADOPT |
| Stage Entry Check | would have prevented avoidable fail-closed launch | very small mechanical check | ADOPT |
| JIT handoff | reduces manual reassembly of review context | stale identity risk after target moves | KEEP + simplify |
| exact target lock | prevents review of obsolete Head | small metadata cost | KEEP |
| dual quality axes | improves model / harness learning | scoring overhead if overdone | lightweight only |
| distributed control state | preserves history | high operator navigation cost | replace with single current authority |

---

## 9. FND-07+ Baseline

The approved default flow is:

```text
Stage Entry Check
↓
3 Independent Candidates
↓
Simplified Evaluation / Selection
↓
Final Synthesis
↓
1 Light Semantic Review
↓
Targeted Fix / Targeted Re-review if needed
↓
1 Heavy Review by default
↓
Second Heavy Review only when risk justifies it
↓
Critical Mutation: max 3 highest-risk cases
↓
Merge / post-merge verification
```

Additional rules:

- a mutation is not accepted as killed merely because a test is RED;
- the expected semantic failure reason must be confirmed for critical mutations;
- historical handoff records remain immutable;
- the current review target is recorded separately with exact identity;
- current stage / authorization / exact Head / next action should be readable from one current authority;
- implementation quality and overall candidate quality remain conceptually separate;
- detailed numeric scoring is optional and benchmark-driven.

---

## 10. Business Interpretation

The main lesson from FND-06 is not "reduce reviews". It is "retain the controls that can explain what failure they prevent, and remove ritual that does not justify its cost."

The retained controls each have a clear purpose:

- Stage Entry Check prevents work from starting under stale authorization.
- Light Review catches semantic and verification mistakes before expensive review stages.
- Critical Mutation proves that important tests can detect the failures they claim to detect.
- Semantic Signature prevents false confidence from unrelated RED results.
- Final Synthesis prevents candidate ranking from automatically becoming production acceptance.
- risk-based Heavy Review spends the second independent review only where consequence or uncertainty justifies it.

This is the FND-06 direction for balancing quality assurance with operator and agent cost.

---

## 11. Remaining Closure Work

Before this retrospective is marked COMPLETE:

1. perform one lightweight independent review of this document only;
2. confirm that approved decisions, GitHub evidence identity, and H2 Minor dispositions are represented without contradiction;
3. address only Blocker / Major or clear factual inaccuracies found by that review;
4. record final retrospective completion;
5. synchronize WP-1 #33 and Parent #3 current control state;
6. decide next-step authorization separately.

The lightweight review must not redesign FND-06, rerun product implementation, or reopen non-blocking H2 Minor findings by default.
