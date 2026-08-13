# WP-1 Retrospective Decision Package

## Status

```yaml
DOCUMENT:
  STATUS: KOO_APPROVED
  DATE: 2026-08-13

WP1:
  ISSUE: 33
  PRODUCT: SUCCESS
  PROCESS: SUCCESS
  FOUNDATION_READY: PASS
  PRODUCT_FOUNDATION: COMPLETE

CURRENT_MAIN_AT_APPROVAL:
  7ff5d65c80e9901e76afd92b3b6f58ad3a803718

WP2:
  ISSUE: 34
  LEAF_DECOMPOSITION: AUTHORIZED
  ISSUE_SET_CREATION: NOT_AUTHORIZED
  PRODUCT_IMPLEMENTATION: PROHIBITED
```

This document is the Koo-approved WP-1 retrospective decision package. It synthesizes the completed WP-1 Foundation work and its process experiments and defines the baseline and limited expansion pilots to carry into WP-2.

It does not reopen FND-01 through FND-06, authorize WP-2 leaf Issue creation, or authorize WP-2 product implementation.

---

## 1. Executive Summary

WP-1 Foundation is assessed as **SUCCESS** both as a product outcome and as a process experiment.

Product-wise, WP-1 established a reusable foundation covering solution/project boundaries, build/test CI, common API execution contracts, real PostgreSQL integration testing, EF Core migration machinery, an explicit one-shot Migrator, Docker Compose lifecycle and migration ordering, and live/ready health semantics.

Process-wise, WP-1 did not preserve one fixed workflow. It reduced process weight as evidence accumulated:

- FND-03 used a research-heavy benchmark/review structure.
- FND-04 reduced candidate and review volume and shifted toward role diversity and targeted re-review.
- FND-05 reduced to three candidates and validated candidate comparison, Final Synthesis, perspective-diverse Heavy Review, exact identity, and targeted fix/re-review.
- FND-06 retained the high-value controls while simplifying the rest; its single semantic Light Review still found a real Major before closure.

The WP-1 conclusion is therefore not "more review is always safer." The adopted principle is:

> Keep controls that can explain which failure they prevent; reduce mechanical, duplicated, and weakly differentiated work.

The largest technical gain was the establishment of a real PostgreSQL-to-migration-to-Compose-to-readiness verification path, together with the lesson that `CI GREEN` or a merely RED mutation does not prove oracle correctness.

The largest process gain was retaining semantic review, critical mutation, exact identity, and targeted re-review while reducing candidate count, review count, and rerun scope.

The largest operator cost was manual SHA transcription, handoff reconstruction, repeated target identity checking, and current-state reconstruction across distributed records.

---

## 2. Fixed Facts / Evidence Identity

```yaml
REPOSITORY:
  kooiei-in4a/minimal-bank-system

CURRENT_MAIN_AT_PACKAGE_APPROVAL:
  7ff5d65c80e9901e76afd92b3b6f58ad3a803718

WP1:
  ISSUE: 33
  STATE: CLOSED
  STATE_REASON: completed
  FOUNDATION_READY: PASS
  PRODUCT_FOUNDATION: COMPLETE

FND01:
  ISSUE: 39
  STATUS: COMPLETE

FND02:
  ISSUE: 40
  STATUS: COMPLETE

FND03:
  ISSUE: 41
  STATUS: COMPLETE

FND04:
  ISSUE: 42
  STATUS: COMPLETE
  RETROSPECTIVE_PR: 144

FND05:
  ISSUE: 43
  STATUS: COMPLETE
  RETROSPECTIVE_PR: 154

FND06:
  ISSUE: 44
  STATUS: COMPLETE
  FINAL_PRODUCT_PR: 160
  PRODUCT_MERGE_SNAPSHOT: acd0896119ab254ad652b8c362b46a9e75417340

FND06_RETROSPECTIVE:
  PR: 161
  REVIEWED_HEAD: 39ae899e7dc72e0c667fbf4960db9e77f037f2d6
  MERGE_COMMIT: 7ff5d65c80e9901e76afd92b3b6f58ad3a803718
  LIGHT_REVIEW:
    RESULT: PASS
    BLOCKER: 0
    MAJOR: 0
    MINOR: 2
  POST_MERGE_BUILD_AND_TEST:
    RUN: 31624413715
    RESULT: SUCCESS

WP2_CURRENT_AUTHORITY_AT_PACKAGE_APPROVAL:
  ISSUE: 34
  COMMENT_ID: 5274312846
  LEAF_DECOMPOSITION: GRANTED_BY_KOO
  ISSUE_SET_CREATION: NOT_YET_GRANTED
  PRODUCT_IMPLEMENTATION: PROHIBITED
```

Two non-blocking evidence-state drifts are retained as historical/current-state distinctions:

1. the FND-04 retrospective document header retains an older draft-state label although PR #144 is merged;
2. the FND-06 retrospective document body retains an intermediate Light Review pending label although PR #161 and the WP-1 closure record establish retrospective completion.

These are evidence-pointer/status presentation gaps, not unresolved product or process Blocker/Major findings.

---

## 3. WP-1 Product Outcome

WP-1 Foundation Ready is PASS.

The completed foundation establishes the following reusable product capabilities:

- solution/project responsibility boundaries;
- build/test CI;
- common API error contract;
- correlation ID;
- TimeProvider;
- JSON technical logging and prohibited-log-field policy;
- real PostgreSQL integration fixture and lifecycle;
- EF Core/Npgsql migration machinery;
- explicit Migrator;
- API startup no-auto-migration behavior;
- Docker Compose runtime with migration-to-API ordering;
- migration failure preventing API start;
- secret external injection;
- live/ready health contract including PostgreSQL readiness and migration completeness semantics.

WP-2 must build on this foundation rather than reconstruct it.

---

## 4. WP-1 Process Evolution

### FND-01/02

FND-01 exposed a process deviation: Issue Ready was confirmed after implementation had already started rather than before start.

The retained lesson is:

> Do not discover start authorization after work has begun.

FND-01/02 also established the baseline product and execution contracts reused by later leaves.

### FND-03

FND-03 was valuable as a research benchmark but excessively heavy for routine development. Its instrumentation included a large candidate pool, many independent reviews, Major-fix comparisons, multiple judges, and archive work.

The research value is retained; the routine process weight is not.

### FND-04

FND-04 reduced candidate count from 14 to 8, replaced large volumes of similar reviews with role-diverse review, made the third Judge conditional, and used targeted fix / targeted re-review instead of full reruns.

A key technical lesson became explicit:

> A green test does not prove that the test detects the defect class it claims to protect against.

Negative tests and mutations therefore need evidence that the intended path was reached and that failure occurred for the expected semantic reason.

### FND-05

FND-05 reduced implementation comparison to three candidates.

The following elements demonstrated clear value:

- candidate comparison;
- Selection / Adjudication;
- Final Synthesis;
- perspective-diverse Heavy Review;
- exact target identity;
- targeted fix / targeted re-review;
- Observation Ledger separation between current-run conditions and future improvements.

FND-05 also exposed significant operator cost:

- canonical registry drift;
- repeated manual SHA transcription;
- hand-built handoffs;
- distributed current state;
- mechanical checks delegated to semantic reviewers.

### FND-06

FND-06 used a limited simplification pilot with:

- three candidates;
- Final Synthesis;
- one semantic Light Review;
- two Heavy Review perspectives;
- critical mutation capped at three;
- small mechanical checks;
- generate-only JIT handoff.

The semantic Light Review found a real Major: the mutation harness proved a RED state but had not yet proved that the intended semantic failure caused it. Targeted fix and targeted re-review resolved the finding.

Heavy H1 ended with Blocker/Major 0. Heavy H2 ended with Blocker/Major 0 and Minor 5. Conditional Judge was not required.

The important observation is:

> Even after reducing process volume, the retained review gate still found a meaningful false-assurance risk.

---

## 5. What Worked Well

### Rolling Wave

Later Work Packages can be decomposed using the actual implementation shape rather than prematurely fixing all leaf work in advance.

### Single Primary Responsibility / Issue Ready

Leaf scope, ownership, authority, dependencies, acceptance criteria, verification, and out-of-scope boundaries remain useful controls.

### Exact Identity

Exact Head / target identity prevents candidate, synthesis, review, and fix artifacts from being confused with one another.

### Candidate Comparison + Final Synthesis

When high-value comparison is justified, candidates provide alternative implementation evidence while Final Synthesis prevents the winning candidate from automatically becoming production acceptance.

### Semantic Light Review

One focused semantic review has demonstrated high cost-effectiveness. FND-06 found a real Major at this stage.

### Perspective Diversity

FND-05 demonstrated that a second review perspective can find Major defects missed by another strong reviewer. Review diversity therefore matters more than simply increasing reviewer count.

### Targeted Fix / Targeted Re-review

When root cause and changed surface are narrow, targeted re-review preserves quality without restarting the entire review chain.

### Critical Mutation

Critical mutation is valuable for testing the verification system itself, particularly false-assurance risks in high-impact failure paths.

---

## 6. What Did Not Work Well

### Excessive candidate volume

FND-03-scale candidate volume is not justified for normal development.

### Excessive similar review

Many reviews with overlapping perspectives produce lower marginal value than fewer deliberately distinct review roles.

### Full rerun after narrow fixes

A narrow finding with a narrow fix does not justify re-running every previous review stage by default.

### Manual SHA transcription

Identity verification is necessary, but repeated manual copying of the same identity across prompts, handoffs, issues, registries, and reviews creates its own error surface.

### Manual handoff construction

Mechanically retrievable metadata should not be repeatedly reconstructed by an operator.

### Distributed current-state authority

Historical records should remain immutable, but current operational authority must be readable from a single current record at each control level.

### Mechanical checks assigned to semantic reviewers

Branch, exact target, required field completeness, CI state, authorization flags, and prohibited file checks should be mechanical when deterministic.

### Direct main write risk

During FND-06 retrospective creation, a GitHub connector file-write operation omitted the branch and briefly committed documentation directly to `main`; the commit was immediately reverted and the net product/file diff was zero.

The incident is sufficient evidence to prohibit direct main writes in the normal process and require a lightweight write preflight.

---

## 7. Quality Gain vs Process Cost

| Process element | Observed quality gain | Process cost | WP-2 direction |
| --- | --- | --- | --- |
| Exact Head | prevents wrong-target review | low | KEEP |
| 3 candidates | meaningful implementation comparison | medium-high | KEEP when high-value comparison is justified |
| Final Synthesis | separates candidate ranking from production acceptance | medium | KEEP when comparison is used |
| 1 semantic Light Review | found real FND-06 Major | medium | KEEP |
| Heavy Review 1 | deep final semantic/adversarial review | high | KEEP |
| Heavy Review 2 | found FND-05 Majors; only Minors in FND-06 | high | risk-based |
| Critical Mutation max 3 | tests highest-risk false-assurance classes | medium | KEEP for high risk |
| Semantic Signature | confirms intended reason for mutation kill | low-medium | ADOPT |
| Stage Entry Check | detects stale authorization before expensive work | low | ADOPT |
| JIT Handoff | reduces repeated operator reconstruction | low | EXPAND_PILOT |
| Mechanical Gate | removes deterministic checks from semantic reviewers | low | EXPAND_PILOT |
| Manual SHA/current-state reconstruction | no unique semantic quality gain | high | REMOVE |

---

## 8. Final WP-1 Decisions

### KEEP

- Rolling Wave;
- single primary responsibility;
- Issue Ready;
- exact Head / exact target identity;
- three independent candidates when benchmark/high-value comparison is justified;
- Final Synthesis when candidate comparison is used;
- one independent semantic Light Review;
- Targeted Fix / Targeted Re-review;
- Critical Mutation max 3 for highest-risk failure classes;
- immutable historical handoff records.

### SIMPLIFY

- Heavy Review: one by default, second reviewer risk-based;
- detailed numeric scoring: benchmark-only when useful;
- JIT Handoff: generate-only, with historical handoff separated from current exact target.

### REMOVE

- FND-03-scale candidate volume from routine development;
- large volumes of similar reviews;
- standalone Formal Self-Review phase;
- unconditional full rerun after a narrow fix;
- manual replication of current state across multiple registries;
- routine manual SHA/metadata transcription.

### ADOPT

- lightweight Stage Entry Check;
- semantic failure signature for critical mutation;
- single Current Authority per control level;
- Write Preflight;
- lightweight separation of Implementation Quality and Overall Candidate Quality;
- selected mechanical blocking/warning checks.

### PROHIBIT

- direct write to `main` in normal agent/process operation.

Write Preflight must confirm at minimum:

```yaml
WRITE_PREFLIGHT:
  TARGET_REPOSITORY:
  TARGET_BRANCH:
  EXPECTED_BASE_SHA:
  WRITE_SCOPE:
  DIRECT_MAIN_WRITE_ALLOWED: false
```

This is a mechanical preflight, not a new semantic review stage.

### DEFER

- automatic agent launch;
- Release Ready-specific operational verification;
- production-like deployment claims;
- slow/hung PostgreSQL operational timeout validation;
- large-scale automated benchmark scoring/registry infrastructure.

---

## 9. WP-2 Expansion Pilot

FND-06 produced useful observations, but one successful pilot does not establish universal future effectiveness. WP-2 therefore expands the scope slightly and re-evaluates the results.

### E01 Stage Entry Check — EXPAND_PILOT

Expand from limited stage boundaries to important agent launches.

Minimum checks:

```text
PREVIOUS_STAGE_COMPLETE
NEXT_STAGE_AUTHORIZED
CONTROL_STATE_CONSISTENT
TARGET_SHA_EXACT
NO_UNRESOLVED_BLOCKER_MAJOR
```

### E02 Critical Mutation — EXPAND_PILOT

Apply to high-risk Security/Audit leaves only.

- maximum three mutations per leaf;
- select only the highest-risk failure classes;
- do not require mutation for every test.

### E03 Semantic Signature — ADOPT

Critical mutation evidence must establish:

```text
BASELINE_GREEN
-> mutation applied
-> MUTATION_RED
-> expected semantic failure reason confirmed
-> source restored
-> RESTORE_GREEN
```

A merely RED test is not sufficient.

### E04 JIT Handoff — EXPAND_PILOT

Candidate boundaries:

- decomposition -> Issue Ready Review;
- Final Synthesis -> Light Review;
- targeted fix -> targeted re-review.

Constraints:

```yaml
GENERATE_ONLY: true
AUTOMATIC_AGENT_LAUNCH: false
HUMAN_APPROVAL: required
```

### E05 Mechanical Gate — EXPAND_PILOT

Candidate blocking checks:

- exact target mismatch;
- wrong branch when branch identity is contractually fixed;
- authorization mismatch;
- unresolved Blocker/Major;
- required CI failure;
- direct main write attempt;
- prohibited secret file.

Candidate warnings:

- missing evidence pointer;
- missing optional metadata;
- missing optional benchmark score.

Only deterministic conditions that materially invalidate quality, identity, authorization, or required verification should block. Convenience metadata should remain warnings.

---

## 10. WP-1 Foundation Reuse Contract for WP-2

WP-2 must reuse the existing WP-1 foundation unless a concrete defect or new approved requirement requires change.

| Foundation | WP-2 treatment |
| --- | --- |
| Solution / project boundary | REUSE |
| Build / Test CI | REUSE |
| API error contract | REUSE |
| Correlation ID | REUSE |
| TimeProvider | REUSE |
| Technical JSON logging | REUSE |
| Prohibited log policy | REUSE_AND_EXTEND |
| Real PostgreSQL fixture | REUSE |
| EF Core migration machinery | REUSE |
| Explicit Migrator | REUSE |
| API no-auto-migration | PRESERVE |
| Compose secret injection | REUSE |
| Health contract | PRESERVE |

WP-2 adds the following concerns on top of that foundation:

- Identity;
- Authentication;
- Authorization;
- Operator management;
- Audit.

---

## 11. WP-2 Risk-Based Review Policy

WP-2 retains:

```text
Light Review: 1 semantic review
Heavy Review: 1 by default
Second Heavy Review: risk-based
```

Security/Audit leaves are higher risk than WP-1 infrastructure leaves, so the second Heavy Review may be triggered more frequently. It is not mandatory for every WP-2 leaf merely because the Work Package is security-related.

Signals that strongly justify a second Heavy Review include:

- authentication or authorization correctness;
- stale JWT / current authority behavior;
- last-administrator concurrency;
- fail-closed Audit behavior;
- append-only enforcement;
- secret/credential non-disclosure;
- concurrency or external-state race behavior;
- novel or fragile test oracles;
- a Major found during Light Review;
- remaining uncertainty about mutation semantic correctness.

---

## 12. WP-2 Candidate Critical Failure Classes

These are risk-class candidates for decomposition and verification planning, not mutations to implement in this package.

Maximum three critical mutations per leaf remains the rule.

| ID | Candidate failure class |
| --- | --- |
| AUTH-01 | allow an already-issued JWT for a disabled Operator |
| AUTH-02 | ignore authorization-state version mismatch |
| AUTH-03 | authorize from JWT role claim rather than current DB role |
| OP-01 | permit concurrent operations to demote all last administrators |
| AUD-01 | commit the business transaction when required Audit persistence fails |
| AUD-02 | permit application DB role to UPDATE/DELETE Audit Log |
| SEC-01 | write JWT/password/signing key into technical logs |

Leaf decomposition must assign only the risk classes actually owned by each leaf.

---

## 13. Known Carryovers / Deferred Items

FND-06 H2 Minor findings do not reopen FND-06 product work but remain relevant before reuse or expansion.

Before reuse of relevant verification harness patterns:

- use a positive control when a negative non-disclosure oracle could pass vacuously;
- distinguish intentionally empty results from verification-command failure;
- explicitly assert verification command success.

Before broad or parallel reuse of restartable PostgreSQL fixture patterns:

- harden cleanup ownership;
- address abnormal-termination residue and fixed-port/TOCTOU risks.

For new or materially revised dedicated workflows:

- make SDK identity explicit or reuse the repository's canonical setup path.

Deferred to operational or Release Ready evaluation:

- slow/hung PostgreSQL behavior;
- production timeout policy;
- deployment/rollback;
- production-like operational guarantees.

---

## 14. WP-2 Entry Conditions

After this Decision Package is formally recorded, WP-2 leaf decomposition may proceed under the existing Current Authority.

The authority boundary remains:

```yaml
WP2_LEAF_DECOMPOSITION:
  AUTHORIZED

WP2_ISSUE_SET_CREATION:
  NOT_AUTHORIZED

WP2_PRODUCT_IMPLEMENTATION:
  PROHIBITED
```

WP-2 decomposition must use at least:

- ADR-0007;
- ADR-0008;
- `bank-system-specification` §6, §14, §19.9;
- actual WP-1 foundation shape;
- WP-2 completion gate;
- the risk-class catalog in this package.

Leaf count must not be fixed in advance.

After decomposition:

```text
Independent Issue Set Review
-> WP-2 Issue Set Ready evaluation
-> Koo authorization for Issue creation
```

WP-2 product implementation remains a separate later gate.

---

## Business Interpretation

The WP-1 lesson is not "how many times should AI review code?" Each retained control has a specific failure it prevents:

- Stage Entry Check prevents work from starting without valid authorization.
- Exact Head prevents reviewing the wrong code.
- Light Review detects semantic mistakes early.
- Critical Mutation checks whether important tests can detect the failures they claim to detect.
- Semantic Signature prevents unrelated RED results from being treated as successful detection.
- Write Preflight prevents wrong-branch and direct-main writes.
- Targeted Re-review avoids restarting the full process after a narrow fix.

WP-2 therefore keeps controls that demonstrated quality value and targets operator copy/paste, duplicate review, and state reconstruction for reduction.

---

## 15. Decision Register

| ID | Topic | Decision | Evidence / Rationale | WP-2 application | Recheck |
| -- | ----- | -------- | -------------------- | ---------------- | ------- |
| WP1-D01 | Rolling Wave | KEEP | staged decomposition worked | continue | WP-2 retrospective |
| WP1-D02 | Single primary responsibility | KEEP | prevents scope overlap | all leaves | WP-2 retrospective |
| WP1-D03 | Issue Ready | KEEP | start/scope control | all leaves | WP-2 retrospective |
| WP1-D04 | Exact Head | KEEP | prevents wrong-target review | reviews/gates | continue |
| WP1-D05 | 3 candidates | KEEP | comparison value observed | benchmark/high-value only | WP-2 retrospective |
| WP1-D06 | Final Synthesis | KEEP | candidate != production | when comparison used | WP-2 retrospective |
| WP1-D07 | Semantic Light Review | KEEP | real Major found in FND-06 | one review | WP-2 retrospective |
| WP1-D08 | Heavy Review | SIMPLIFY | marginal value depends on risk | 1 default + 2nd risk-based | WP-2 retrospective |
| WP1-D09 | Targeted Re-review | KEEP | avoids full rerun | narrow fixes | continue |
| WP1-D10 | Critical Mutation | KEEP | oracle verification value | high risk, max 3 | WP-2 retrospective |
| WP1-D11 | Detailed scoring | SIMPLIFY | high operator cost | benchmark only | when needed |
| WP1-D12 | Formal Self-Review phase | REMOVE | standalone phase too heavy | embed DoD/checks in execution | when needed |
| WP1-D13 | Large candidate pools | REMOVE | FND-03 cost too high | not routine | research only |
| WP1-D14 | Large similar review sets | REMOVE | low marginal value | prefer perspective diversity | WP-2 retrospective |
| WP1-D15 | Full rerun after narrow fix | REMOVE | targeted review sufficient when bounded | blast-radius based | when findings occur |
| WP1-D16 | Stage Entry Check | ADOPT | detects stale control before launch | important launches | WP-2 pilot |
| WP1-D17 | Semantic Signature | ADOPT | prevents RED false assurance | critical mutations | WP-2 pilot |
| WP1-D18 | Single Current Authority | ADOPT | reduces state reconstruction | one per control level | WP-2 retrospective |
| WP1-D19 | Write Preflight | ADOPT | direct-main incident evidence | all GitHub writes | WP-2 retrospective |
| WP1-D20 | Direct main write | PROHIBIT | branch omission incident | prohibited by default | only explicit future policy change |
| WP1-D21 | Automatic agent launch | DEFER | generate-only pilot boundary | human approval required | WP-2 retrospective |
| WP1-E01 | Stage Entry expansion | EXPAND_PILOT | usefulness observed in FND-06 | important launches | WP-2 retrospective |
| WP1-E02 | Critical Mutation expansion | EXPAND_PILOT | high-risk verification value | Security/Audit leaves | WP-2 retrospective |
| WP1-E03 | Semantic Signature | ADOPT | MAJ-01 improvement | default for critical mutation | WP-2 retrospective |
| WP1-E04 | JIT Handoff expansion | EXPAND_PILOT | operator-cost reduction candidate | three handoff boundaries | WP-2 retrospective |
| WP1-E05 | Mechanical Gate | EXPAND_PILOT | remove deterministic work from semantic review | selected blocking/warning checks | WP-2 retrospective |

---

```yaml
WP1_RETROSPECTIVE_RECOMMENDATION:
  WP1:
    PRODUCT: SUCCESS
    PROCESS: SUCCESS

  DECISION_PACKAGE:
    KOO_APPROVED: true
    FORMAL_RECORD_READY: true

  WP2:
    LEAF_DECOMPOSITION: READY
    ISSUE_CREATION: NOT_AUTHORIZED
    PRODUCT_IMPLEMENTATION: PROHIBITED
```
