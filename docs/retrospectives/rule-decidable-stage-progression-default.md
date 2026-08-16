# Rule-Decidable Stage Progression — Permanent Default

- Status: **Koo-approved process decision — repository synchronization pending**
- Decision ID: `RULE_DECIDABLE_STAGE_PROGRESSION_DEFAULT_V1`
- Decision date: 2026-08-16
- Process Issue: #209
- Parent / Control Issue: #3
- Work Package control: #34
- Historical evidence: #191, #197, #203

## 1. Purpose

WP2-AUTHN-01 and WP2-AUD-01 demonstrated that human involvement can be reduced without allowing AI to silently make product/specification/ADR decisions.

The permanent default is therefore:

> Human involvement is for semantic decisions, not routine confirmation of rule-decidable stage progression.

This document promotes that principle from a bounded pilot rule to the repository-wide default for current and future work.

## 2. Permanent default

```yaml
PERMANENT_STAGE_PROGRESSION:
  STATE: ACTIVE
  SCOPE: REPOSITORY_WIDE_CURRENT_AND_FUTURE_WORK

  RULE_DECIDABLE_STAGE_PROGRESSION: AI_DECIDES
  DERIVED_AUTHORITY_MATERIALIZATION: COORDINATOR_ALLOWED
  AUTHORITY_RECORDS: REQUIRED
  TRANSITION_BUNDLE: REQUIRED

  HUMAN_DECISION_ESCALATION: CONDITION_BASED
  ROUTINE_STAGE_APPROVAL: NOT_REQUIRED

  AUTOMATIC_AGENT_LAUNCH: false
  MANUAL_AGENT_TRANSPORT: true
  JIT_HANDOFF: COPY_PASTE_READY

  FINAL_PRODUCT_MERGE_APPROVAL: HUMAN_REQUIRED
  FINAL_RELEASE_GO_NO_GO: HUMAN_REQUIRED

  DIRECT_MAIN_WRITE: PROHIBITED
  FORCE_PUSH: PROHIBITED
  SILENT_RETARGET: PROHIBITED
```

## 3. Rule-decidable progression

When the next stage follows uniquely from approved authority and GitHub primary evidence, the Coordinator must not ask a human whether it may proceed.

Examples include:

- all required gate conditions are PASS;
- Blocker / Major count is zero;
- required CI is PASS;
- target Issue / branch / PR / exact SHA match the current authority;
- dependencies are complete;
- Individual Issue Ready is PASS and Product Implementation remains within the already approved leaf scope;
- a review finding has one correction uniquely derivable from existing approved authority;
- a Transition Bundle needs mechanical synchronization without changing semantics.

For these cases, the Coordinator may materialize the required stage result, Current Authority records, derived implementation/review authority, and next-agent handoff.

A routine transport action performed by a human does not become a semantic approval merely because the human performs the copy/paste or launch action.

## 4. Human Decision Escalation

Human decision is required when the next action cannot be uniquely derived from existing approved authority, including:

- multiple reasonable interpretations remain;
- a new material product, API, security, Audit, or operational semantic choice is required;
- a material design decision requires a new ADR;
- approved Issue scope must change or expand;
- alternatives have material trade-offs that objective evidence cannot resolve;
- an irreversible or materially high-impact action requires human judgment;
- independent AI conclusions remain materially split after reasonable additional verification;
- an approved specification, ADR, or process decision must change.

`HUMAN_DECISION_REQUIRED` must not be used for routine stage approval.

## 5. Permanent Transition Bundle

The v2 pilot coherence mechanism becomes the permanent transition materialization contract for important next-agent launches.

```yaml
TRANSITION_BUNDLE:
  STAGE_RESULT_EVIDENCE:
  PARENT_CURRENT_AUTHORITY:
  WP_CURRENT_AUTHORITY:
  NEXT_AGENT_HANDOFF:
```

The required members must agree on:

```text
same target Issue
same branch / PR when applicable
same exact base / Head identity
same completed stage
same next authorized stage
```

Because GitHub writes are not transactional, `NEXT_AGENT_HANDOFF` is the bundle finalization record and must be generated last.

A partial or inconsistent bundle is a mechanical `STOP / repair` condition. Repair does not require a human decision unless the repair would change product/specification/ADR/scope/process semantics.

## 6. Preserved controls

This decision does **not** remove or weaken:

- prompt-is-not-authority;
- Fresh Context primary-evidence preflight;
- Single Current Authority;
- Write Preflight;
- direct-main-write prohibition;
- candidate count policy;
- Light / Heavy Review policy;
- targeted fix / targeted re-review;
- Critical Mutation policy;
- independent reviewer role separation;
- Human Decision Escalation for genuine semantic decisions;
- final product merge human approval;
- final release Go / No-Go;
- manual agent transport.

Derived authority may advance a stage only inside already approved semantics and scope. It cannot approve a scope expansion, specification change, ADR change, new material trade-off, or final merge.

## 7. Historical pilot status

The following remain immutable historical evidence:

- `docs/retrospectives/wp2-human-decision-handoff-pilot.md`
- `docs/retrospectives/wp2-human-decision-handoff-pilot-v2.md`
- Issue #191
- Issue #197
- Issue #203

Their bounded `ACTIVE / INACTIVE` pilot states describe those historical runs only. They no longer control whether routine rule-decidable progression requires human approval after this permanent decision becomes effective.

## 8. First application — WP2-OPR-QRY-01

WP2-OPR-QRY-01 / Issue #169 is the first leaf to use the permanent default after repository synchronization.

At decision time:

```yaml
TARGET:
  ISSUE: 169
  LEAF_ID: WP2-OPR-QRY-01
  INDIVIDUAL_ISSUE_READY: PASS
  ISSUE_READY_FORMALIZATION: 5304818598
```

After this process decision is merged, if current target/control identity remains exact and no `HUMAN_DECISION_REQUIRED` condition exists, the Coordinator must **not** request a separate Product Implementation approval. It should materialize Product Implementation authority for the already approved Issue #169 scope and continue the existing WP-2 implementation process.

## 9. Effectivity

The semantic decision is human-approved by Koo through Process Issue #209.

Repository guidance becomes fully synchronized when the associated process PR is independently reviewed and merged. Until merge, no product implementation is authorized by this document alone.
