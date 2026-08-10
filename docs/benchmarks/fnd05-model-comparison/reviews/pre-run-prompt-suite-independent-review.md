# FND-05 Pre-Run Prompt Suite — Independent Review

```yaml
DOCUMENT_STATUS: "COMPLETED INDEPENDENT REVIEW"
REVIEW_TARGET_PR: 145
REVIEW_TARGET_HEAD: "57df6ae1a30ac23151fbcd707f191f5d26dba029"
REVIEW_TARGET_BASE: "a69471578eed12823a1469017dac7fddf32ad41b"
REVIEW_MANIFEST: "pre-run-prompt-suite-review-manifest.md"
OUTPUT_BRANCH: "agent/fnd05-prompt-suite-independent-review"
OUTPUT_PR: 146
OUTPUT_FILE: "docs/benchmarks/fnd05-model-comparison/reviews/pre-run-prompt-suite-independent-review.md"
REVIEWER_MODEL: "GPT 5.6 Sol"
REVIEWER_HARNESS: "Browser"
REVIEWER_EFFORT: "xHigh"
REVIEWER_SLUG: "gpt-5.6-sol-Browser-xhigh"
ATTEMPT: 1
```

## 1. Executive Verdict

```text
VERDICT: FIX_REQUIRED
BLOCKER_COUNT: 0
MAJOR_COUNT: 5
MINOR_COUNT: 4
NIT_COUNT: 0
TARGET_HEAD_VERIFIED: YES
```

全体構造は成立している。3 candidate → Selection → curated Final Synthesis → Static / Light 2 → fixed Head → Heavy 2 → conditional fix / re-review というFND-05の固定方針を再設計する必要はない。

一方、lock前に修正すべき問題がある。最重要は次の5点である。

1. benchmark側のdraft design / ruleが、Issue #43が許可する実装自由度より狭い方式をMUST化している。
2. M-08がtest oracle自身を弱体化するmutationになっており、期待REDを信頼できない。
3. Light findingのREJECTED / UNRESOLVED Blocker・Major候補をHeavyが再確認しない解釈が可能で、blind spotが残る。
4. Evaluation → Selection → Final Synthesis → Light → Heavy → targeted fix間の「locked artifact」を一意に参照するcontrol-plane契約が不足している。
5. `Authority`を名乗る一部promptで、Approved specification / ADR / IssueよりParent / WP等が上に見える並びになっている。

したがって、D-01〜D-08の値をlockする前に、P0 / P1のprompt-suite修正を行い、finding-owned再レビューを1回実施することを推奨する。

---

## 2. Target Verification

GitHub一次証拠で次を確認した。

```yaml
REPOSITORY: "kooiei-in4a/minimal-bank-system"
TARGET_PR: 145
TITLE: "docs(fnd05): prepare ADR-first implementation and review funnel"
STATE: "OPEN / DRAFT"
BASE_BRANCH: "agent/fnd04-final-retrospective-synthesis"
BASE_SHA: "a69471578eed12823a1469017dac7fddf32ad41b"
HEAD_BRANCH: "agent/fnd05-pre-run-preparation"
HEAD_SHA: "57df6ae1a30ac23151fbcd707f191f5d26dba029"
CHANGED_FILES: 22
ADDITIONS: 5006
DELETIONS: 0
```

22 filesはすべて `docs/benchmarks/fnd05-model-comparison/**` 配下で、review manifestのexact listと一致する。対象外ファイルの混入は確認されなかった。

Output PR #146も、baseがPR #145 exact Head `57df6ae1...` であることを確認した。

Target identityは固定条件と一致するため、`BLOCKED — TARGET MOVED` ではない。

---

## 3. Phase A Reference Review

Target suiteを評価する前に、Issue #43、Accepted ADR-0001 / 0008 / 0009、`AGENTS.md`、PR #144 FND-04 final retrospectiveを確認し、次をReferenceとして固定した。

### 3.1 Authority

正本は次の順で扱う。

1. Kooが承認した製品方針・仕様
2. Accepted ADR
3. Target Issue #43
4. code / automated tests
5. PR description / comment

`AGENTS.md`はこのauthorityと開発統制を定義する。Parent #3 / WP #33はphase / gate / progressの統制証拠であり、製品仕様、ADR、Issue AC、コードレベル設計の正本ではない。

`docs/benchmarks/**`は比較実験・再現用artifactであり、製品仕様、ADR、Issue #43を上書きしない。

### 3.2 Issue #43 close condition

FND-05は、Docker Compose v2でPostgreSQL、explicit one-shot Migrator、APIを再現可能に起動・停止し、次を成立させた時にclose可能となる。

```text
PostgreSQL usable
  -> explicit Migrator
     -> success: API may start
     -> failure: API must not start
```

Issueは`one-shot migrator service または同等のCompose正本経路`を許可している。

### 3.3 Scope / Out of scope

Required:

- Docker Compose v2 execution path
- PostgreSQL 18
- API
- FND-04 explicit Migrator connection
- named PostgreSQL volume
- digest pinning
- external secret / connection configuration
- migration success before API start
- migration failure API non-start
- deterministic start / stop / clean reset

Out of scope:

- FND-06 health endpoint logic
- business endpoint / business schema / business data
- backup / restore
- production deployment
- scheduled service / production orchestrator
- API startup auto-migration

### 3.4 ADR contract

ADR-0001 fixes .NET 10 / ASP.NET Core 10 / PostgreSQL 18 / EF Core 10 / Docker Compose v2 and requires implementation-time package / image identity pinning within approved majors.

ADR-0009 requires EF migration application through an explicit migrator command or one-shot Compose path before normal API start, prohibits API startup migration and `EnsureCreated`, and requires migration failure to fail deployment rather than be masked.

ADR-0008 requires secrets not to be stored or logged and permits external secret supply through environment, Docker secret, or protected password file. Credentials must not be placed directly in command arguments. PostgreSQL persistence uses a named Docker volume.

### 3.5 FND-04 / FND-05 / FND-06 boundary

- FND-04 owns DbContext, migration machinery, `InitialFoundation`, explicit `MinimalBankSystem.Migrator`, non-zero failure propagation, API no-auto-migration.
- FND-05 owns Compose wiring, lifecycle, external runtime observation, secret / image / volume integration, migration -> API ordering.
- FND-06 owns `/health/live` and `/health/ready` application semantics.

FND-05 must prove ordering without introducing FND-06 API health endpoints.

### 3.6 Agent / reviewer / merge rules

- Agent A may implement, test, verify, inspect diff, and update Draft PR within authorized scope.
- Agent B / reviewers independently re-evaluate from authority and primary evidence.
- Blocker / Major must be zero before merge gate.
- Issue Ready and Koo start authorization are separate from WP-1-wide Implementation Ready.
- PR #145 itself must not start FND-05 implementation.

### 3.7 FND-04 process policy

The fixed FND-05 policy is valid:

- no OpenCode
- no independent Formal Self-Review / H1 execution
- author-side verification remains embedded as predefined Completion Checks
- 3 independent candidates
- no candidate merge / cherry-pick into Final Synthesis
- curated Final Synthesis from current main
- Static + Composer + Luna before Heavy
- fixed Final Head before Sol / Opus
- Heavy non-goals with Blocker / Major root-cause exception
- Judge conditional only
- re-review by finding owner / blast radius

This review does not reopen those decisions.

---

## 4. Fixed Policy Assessment

| Fixed policy | Assessment | Notes |
| --- | --- | --- |
| C1 Luna / Codex | PASS | consistent across README / run / checklist / prompts |
| C2 Sonnet / Claude Code | PASS | consistent |
| C3 Grok 4.5 / Cursor high | PASS | `high fast` explicitly prohibited |
| OpenCode 0 | PASS | consistent |
| Separate Formal Self-Review 0 | PASS | completion checks preserve Agent A basic verification |
| Light L1 Composer | PASS | role is understandable |
| Light L2 Luna | PASS | AC / evidence traceability role is understandable |
| Heavy H1 Sol | PASS | architecture / contract scope is focused |
| Heavy H2 Opus | PASS | failure / lifecycle / false assurance scope is focused |
| Heavy explicit non-goals | PASS WITH CHANGE | non-goals are safe; unresolved Light finding handling must be narrowed |
| Heavy full review 1 each | PASS | re-review exceptions are blast-radius based |
| Judge conditional | PASS | trigger set is reasonable |
| Final Synthesis curated from current main | PASS | no merge / cherry-pick is explicit |
| Issue Ready + Koo start authorization | PASS | implementation remains prohibited |

No fixed policy requires redesign.

---

## 5. Cross-File Consistency Matrix

| Contract / Identity | Source of truth | Referencing files | Result | Gap |
| --- | --- | --- | --- | --- |
| Model / harness configuration | run.json + fixed policy | README, checklist, implementation, review prompts | PASS | none |
| No OpenCode | fixed policy / run.json | README, checklist, implementation | PASS | none |
| No separate Formal Self-Review | fixed policy / retrospective | README, run, checklist, implementation, final-synthesis | PASS | none |
| Stage order | retrospective / README | matrix, prompts | PASS | control-plane artifact identity incomplete |
| Light responsibility | review matrix | L1 / L2 | MODIFY | L1 rechecks catalog rules owned by other stages |
| Heavy responsibility | review matrix | Sol / Opus | PASS WITH CHANGE | unresolved/rejected Light B/M exception must be explicit |
| Heavy non-goals | retrospective | matrix, Sol, Opus | MODIFY | matrix wording is broader than Heavy prompts |
| Judge triggers | run.json / matrix | conditional-judge | PASS | consistent |
| Re-review scope | retrospective / matrix | targeted-fix / targeted-re-review | PASS | artifact binding should be explicit |
| Revision IDs | run.json | all 22 files | PASS | v1 identifiers match |
| D-01 | open decision registry | ledger / checklist / gate | PASS | evidence definition usable |
| D-02 | open decision registry | ledger / checklist / gate | MODIFY | PostgreSQL digest is split into P-07 instead of the D-02 definition |
| D-03 | open decision registry | ledger / design contract | MODIFY | concrete preferred answer leaks before lock |
| D-04 | open decision registry | ledger / design contract / rules | MODIFY | restart semantics are partly hard-coded before command lock |
| D-05 | open decision registry | ledger / checklist / gate | MODIFY | ledger narrows it to API start timestamp only |
| D-06 | open decision registry | ledger / design contract | PASS WITH CAUTION | examples are acceptable; exact override remains open |
| D-07 | open decision registry | ledger / checklist | PASS | no exact shell/helper locked yet |
| D-08 | open decision registry | ledger / final-synthesis | MODIFY | default Luna/Codex author pre-seeds an explicitly open identity |
| Metrics | run.json | retrospective / final synthesis | PASS | kill rate / residue targets consistent |
| Gate status | run.json | checklist / issue-ready | PASS | implementation_permitted=false is maintained |
| Stage output identity | none | evaluation -> selection -> final -> light -> heavy -> fix | FAIL | fresh context cannot uniquely resolve several `<LOCKED>` inputs |

---

## 6. File-by-File Assessment — 22 files

| File | Role | Clarity | Consistency | Executability | Required change |
| --- | --- | --- | --- | --- | --- |
| `README.md` | process overview | PASS | PASS | PASS | none required; update only if terminology changes |
| `pre-run-checklist.md` | human pre-run gate | PASS | MODIFY | PASS | align D-02 / D-05 / D-08 wording with ledger/run |
| `run.json` | machine-readable registry | PASS | MODIFY | MODIFY | add stage artifact refs and locked decision evidence/value fields |
| `scoring.md` | candidate rubric | PASS | PASS | PASS | none required |
| `reference/assumption-ledger.md` | external/project assumptions + D-01..08 | PASS | MODIFY | MODIFY | keep D values truly open; broaden D-05; unify D-02 |
| `reference/implementation-and-test-design-contract.md` | runtime/test contract | PASS | MODIFY | MODIFY | separate upper-source MUSTs from unapproved implementation-shape preferences |
| `reference/mandatory-mutations.md` | oracle validation | PASS | MODIFY | MODIFY | redesign M-08; clarify candidate-visible vs evaluator injection detail |
| `reference/project-rule-catalog.md` | enforceable project rules | PASS | MODIFY | MODIFY | remove/lock overconstraints; make owner semantics real |
| `reference/review-perspective-matrix.md` | role split | PASS | MODIFY | MODIFY | unresolved/rejected Light B/M handling; reduce L1 overlap |
| `prompts/implementation.md` | candidate implementation | PASS | MODIFY | MODIFY | authority order; overconstraints; mutation visibility contract |
| `prompts/implementation-evaluation.md` | common evaluation | PASS | MODIFY | MODIFY | approved spec authority + canonical output artifact identity |
| `prompts/selection-adjudication.md` | element selection | PASS | MODIFY | MODIFY | exact evaluation artifact ref / output ref contract |
| `prompts/final-synthesis.md` | curated final implementation | PASS | MODIFY | MODIFY | exact Selection/Evaluation artifact refs; remove D-08 default |
| `prompts/light-review-project-quality.md` | Composer Light | PASS | MODIFY | PASS | authority order; only own catalog scope + escalation |
| `prompts/light-review-contract-conformance.md` | Luna Light | PASS | MODIFY | PASS | authority order; consume S0/L1 rather than duplicate rules |
| `prompts/light-findings-fix.md` | Light finding disposition/fix | PASS | MODIFY | MODIFY | mark unresolved/rejected B/M explicitly for Heavy handoff |
| `prompts/heavy-review-sol.md` | architecture final gate | PASS | MODIFY | PASS | explicitly re-open unresolved/rejected relevant Light B/M candidates |
| `prompts/heavy-review-opus.md` | adversarial final gate | PASS | MODIFY | PASS | same; otherwise non-goals are sound |
| `prompts/conditional-judge.md` | disagreement adjudication | PASS | PASS | MODIFY | bind Sol/Opus artifacts by immutable refs, not labels only |
| `prompts/issue-ready-review.md` | pre-execution gate | PASS | MODIFY | MODIFY | verify all D locks via run registry evidence refs; state authority order explicitly |
| `prompts/targeted-fix.md` | B/M minimal fix | PASS | PASS | MODIFY | bind locked finding source + reviewer artifact/ref + target head |
| `prompts/targeted-re-review.md` | finding-owned re-review | PASS | PASS | MODIFY | bind change-surface lock and finding source by immutable artifact refs |

No file requires full redesign. The necessary changes are local and cross-file mechanical once the five Major root causes are resolved.

---

## 7. Findings

### F-01

```text
ID: F-01
SEVERITY: Major
CATEGORY: AUTHORITY / OVERCONSTRAINT / CANDIDATE LEAKAGE
AFFECTED_FILES:
  - reference/implementation-and-test-design-contract.md
  - reference/project-rule-catalog.md
  - prompts/implementation.md
  - prompts/selection-adjudication.md
  - prompts/final-synthesis.md
  - prompts/light-review-project-quality.md
  - prompts/light-review-contract-conformance.md
ROOT_CAUSE:
  Draft benchmark design guidance has been promoted to MUST / acceptance criteria
  without an upper-authority decision that fixes the exact implementation shape.
PROBLEM:
  Issue #43 permits a one-shot migrator service OR an equivalent canonical Compose
  path, but the suite requires exactly postgres/migrator/api, service_healthy +
  service_completed_successfully, exact Dockerfile/document/test placement, a
  specific restart interpretation, non-root runtime assumptions, and other
  implementation details as hard conformance rules.
FAILURE_OR_CONFUSION_PATH:
  A candidate can satisfy Issue #43 and ADR observable behavior with an equivalent
  safe Compose path but be marked FAIL by Project Rule / Completion Checks. The
  benchmark then measures conformance to an unapproved draft implementation rather
  than independent implementation quality. Conversely, candidate design choices are
  partially decided before the declared open-decision lock.
IMPACT:
  False candidate rejection, reduced implementation diversity, authority inversion,
  and potential implementation of a Koo-unapproved design detail.
EVIDENCE:
  Issue #43 Scope explicitly says one-shot migrator service "or equivalent canonical
  Compose path". Design contract §3 fixes three services. RULE-COMPOSE-002 and
  RULE-PLACE-* hard-code mechanisms/locations. implementation C-02/C-10 make those
  candidate completion conditions.
RECOMMENDED_CHANGE:
  Keep MUST only for upper-source observable contracts. Reclassify exact topology,
  service names, placement, restart mechanism and hardening preferences as SHOULD /
  locked-pre-run decisions. If Koo intentionally wants an exact common shape, add an
  explicit open decision for that shape and lock it before candidate execution.
CROSS_FILE_UPDATES:
  design contract -> rule catalog -> implementation Completion Checks -> Light review
  conformance text -> Selection / Final Synthesis wording.
FIXED_POLICY_AFFECTED: NO
```

### F-02

```text
ID: F-02
SEVERITY: Major
CATEGORY: TEST ORACLE / MUTATION
AFFECTED_FILES:
  - reference/mandatory-mutations.md
  - prompts/implementation.md
  - prompts/implementation-evaluation.md
  - prompts/final-synthesis.md
  - prompts/heavy-review-opus.md
ROOT_CAUSE:
  M-08 mutates the oracle/assertion rather than the production/runtime defect class.
PROBLEM:
  M-08 says to remove the __EFMigrationsHistory check or force it to success, then
  expects the clean-start test to turn RED because history is missing. Once the
  assertion itself is removed/forced-success, that same test cannot reliably detect
  the missing history.
FAILURE_OR_CONFUSION_PATH:
  A correct validator receives the mutation that disables its own check and may stay
  GREEN; or the suite requires a second validator that checks the first validator,
  changing the intended defect class. Either result invalidates the claimed
  final_mutation_kill_rate=100% meaning.
IMPACT:
  False assurance or unavoidable false failure at the merge-blocking mutation gate.
EVIDENCE:
  mandatory-mutations.md M-08 Defect vs Expected detection are self-referential.
RECOMMENDED_CHANGE:
  Leave the oracle unchanged. Mutate the runtime path so Migrator appears successful
  (exit 0) without recording the expected InitialFoundation migration, then require
  the unchanged migration-history assertion to turn RED. Exact injection mechanism
  remains under D-06.
CROSS_FILE_UPDATES:
  M-08 text, implementation C-09 interpretation, evaluator probe list, Final Synthesis
  mutation report expectation, Opus M-08 review description.
FIXED_POLICY_AFFECTED: NO
```

### F-03

```text
ID: F-03
SEVERITY: Major
CATEGORY: LIGHT / HEAVY SEPARATION / BLIND SPOT
AFFECTED_FILES:
  - reference/review-perspective-matrix.md
  - prompts/light-findings-fix.md
  - prompts/heavy-review-sol.md
  - prompts/heavy-review-opus.md
ROOT_CAUSE:
  "Do not repeat / re-score Light findings" is not limited to resolved Light findings,
  while the Author is allowed to REJECT a Light Blocker/Major candidate.
PROBLEM:
  The matrix tells Heavy reviewers to read the Light list and not repeat/re-score the
  same findings. The fixer can reject a Light B/M candidate with a rationale. There is
  no explicit rule requiring Heavy to independently verify rejected/unresolved B/M
  candidates that fall within its own scope.
FAILURE_OR_CONFUSION_PATH:
  L1 flags a serious secret/lifecycle issue -> Author rejects it incorrectly -> Heavy
  sees it in Light list -> "do not repeat/re-score" causes skip -> fixed Head reaches
  B0/M0 despite unresolved root cause.
IMPACT:
  A deliberate role-separation optimization can become a merge-blocking blind spot.
EVIDENCE:
  review-perspective-matrix §8 vs light-findings-fix §3 and Heavy non-goal wording.
RECOMMENDED_CHANGE:
  Apply the no-repeat rule only to RESOLVED / ACCEPTED+FIXED Minor/Nit and confirmed
  duplicate findings. REJECTED, UNRESOLVED, or ESCALATED B/M candidates are not
  excluded; a Heavy reviewer must independently verify them when they intersect its
  primary scope.
CROSS_FILE_UPDATES:
  review matrix, Light fix handoff schema, Sol/Opus entry/must-review text.
FIXED_POLICY_AFFECTED: NO
```

### F-04

```text
ID: F-04
SEVERITY: Major
CATEGORY: PIPELINE EXECUTABILITY / IDENTITY INTEGRITY
AFFECTED_FILES:
  - run.json
  - prompts/implementation-evaluation.md
  - prompts/selection-adjudication.md
  - prompts/final-synthesis.md
  - prompts/light-review-project-quality.md
  - prompts/light-review-contract-conformance.md
  - prompts/light-findings-fix.md
  - prompts/heavy-review-sol.md
  - prompts/heavy-review-opus.md
  - prompts/conditional-judge.md
  - prompts/targeted-fix.md
  - prompts/targeted-re-review.md
  - prompts/issue-ready-review.md
ROOT_CAUSE:
  Stage outputs are called "locked" but there is no canonical immutable artifact
  reference contract or registry binding each output to its input Head/revision.
PROBLEM:
  Several prompts accept `<LOCKED>`, `<LOCKED_ARTIFACT>`, result names, or revisions,
  but Final Synthesis in particular has no unambiguous Evaluation/Selection artifact
  location containing the element decisions. S0 Static Gate also has an output schema
  in the matrix but no canonical recorded artifact identity.
FAILURE_OR_CONFUSION_PATH:
  A fresh harness can consume a stale Evaluation/Selection or a Light review from a
  previous Head while all human-readable revision labels look valid. The next prompt
  then acts on the wrong decisions/findings without an exact identity failure.
IMPACT:
  Wrong-target fixes, stale selection, stale review disposition, and loss of the exact
  Head integrity that the process is explicitly designed to preserve.
EVIDENCE:
  run.json has revisions/gates but no stage-artifact registry. Selection expects a
  locked Evaluation; Final Synthesis only carries `<LOCKED>` revisions; Light/Heavy
  prompts use free-form artifact placeholders.
RECOMMENDED_CHANGE:
  Add a machine-readable `stage_artifacts` registry to run.json. Every stage output
  records artifact ref/location, revision, source/target Head SHA(s), and producing
  commit/comment ID. Downstream prompts require those exact refs and verify target
  binding before reading content.
CROSS_FILE_UPDATES:
  run.json and every post-candidate prompt input/output schema.
FIXED_POLICY_AFFECTED: NO
```

### F-05

```text
ID: F-05
SEVERITY: Major
CATEGORY: AUTHORITY ORDER / PROMPT EXECUTABILITY
AFFECTED_FILES:
  - prompts/implementation.md
  - prompts/implementation-evaluation.md
  - prompts/light-review-project-quality.md
  - prompts/light-review-contract-conformance.md
  - prompts/issue-ready-review.md
ROOT_CAUSE:
  Some copy-paste prompts present gate/control Issues before specification/ADR in a
  section named Authority, and some omit Approved specification entirely.
PROBLEM:
  Parent #3 / WP #33 are necessary current-state evidence, but AGENTS.md explicitly
  says they are not product/design authority. A fresh model can infer numeric list
  order as precedence and allow stale control state or benchmark references to
  override ADR / Issue semantics.
FAILURE_OR_CONFUSION_PATH:
  Parent/WP state or prose disagrees with Accepted ADR / Issue -> reviewer follows the
  numbered `Authority` list -> wrong stop/fix/conformance judgement.
IMPACT:
  Incorrect review/implementation decision at a safety-critical gate.
EVIDENCE:
  implementation §1 and L1/L2 §3 list Parent/WP/Issue/AGENTS before ADRs; Approved
  specification is omitted. Heavy prompts correctly use Approved spec -> ADR -> Issue.
RECOMMENDED_CHANGE:
  Use the exact authority order everywhere and place Parent/WP under a separate
  `Gate / current-state evidence` heading.
CROSS_FILE_UPDATES:
  implementation, evaluator, L1, L2, issue-ready; reuse the Heavy authority wording.
FIXED_POLICY_AFFECTED: NO
```

### F-06

```text
ID: F-06
SEVERITY: Minor
CATEGORY: OPEN DECISIONS / CONSISTENCY
AFFECTED_FILES:
  - run.json
  - pre-run-checklist.md
  - reference/assumption-ledger.md
  - reference/implementation-and-test-design-contract.md
  - prompts/final-synthesis.md
  - prompts/issue-ready-review.md
ROOT_CAUSE:
  Open-decision records mix evidence requirements with draft preferred answers, and
  D-05 is scoped differently across files.
PROBLEM:
  D-03 contains a preferred host-env -> Compose-secret -> file-reader design before
  lock; D-08 names Luna/Codex as the default author before identity lock; D-05 in the
  ledger is only API start timestamp whereas checklist/user contract requires the
  complete external state capture method. D-02 also splits PostgreSQL digest into P-07.
FAILURE_OR_CONFUSION_PATH:
  Pre-run reviewers can clear TO_LOCK while one external observation remains
  unspecified, or Koo/candidates can be anchored toward a draft D-03/D-08 answer.
IMPACT:
  Local ambiguity and candidate-design leakage before lock; process remains stoppable.
EVIDENCE:
  assumption-ledger D-03/D-05/D-08 vs checklist D-02/D-05/D-08 and run.json open IDs.
RECOMMENDED_CHANGE:
  Keep each D entry question/evidence-only until lock. D-05 must enumerate Migrator
  exit/finish time, API state/start time, migration history and the local/CI command.
  Remove D-08 default identity. Keep PostgreSQL + .NET image identities under D-02.
CROSS_FILE_UPDATES:
  ledger, checklist, run registry, design contract, final-synthesis, issue-ready.
FIXED_POLICY_AFFECTED: NO
```

### F-07

```text
ID: F-07
SEVERITY: Minor
CATEGORY: ROLE DUPLICATION / PROCESS EFFICIENCY
AFFECTED_FILES:
  - reference/project-rule-catalog.md
  - reference/review-perspective-matrix.md
  - prompts/light-review-project-quality.md
  - prompts/light-review-contract-conformance.md
ROOT_CAUSE:
  Rule `Primary owner` metadata is contradicted by L1's instruction to evaluate the
  entire catalog.
PROBLEM:
  Static-, Luna-, Sol-, and Opus-owned rules are still re-evaluated by Composer L1.
  This duplicates the exact work the FND-05 funnel is intended to separate.
FAILURE_OR_CONFUSION_PATH:
  S0 already proves a static digest rule -> L1 rechecks it; Luna later rechecks
  contract; Heavy may see the same rule again. Primary owner becomes descriptive only.
IMPACT:
  Increased prompt length/review cost and lower signal-to-noise, without additional
  safety if escalation rules are retained.
EVIDENCE:
  project-rule-catalog has per-rule Primary owner but §13 says Light reviewer checks
  catalog comprehensively; L1 §5.1 requires all rules.
RECOMMENDED_CHANGE:
  L1 must fully adjudicate Composer-owned rules and obvious cross-cutting violations.
  It consumes S0/L2/Heavy-owned rule status instead of recreating them. Cross-owner
  B/M suspicion is escalated, not silently ignored.
CROSS_FILE_UPDATES:
  catalog reviewer behavior, matrix L1 scope, L1/L2 prompts.
FIXED_POLICY_AFFECTED: NO
```

### F-08

```text
ID: F-08
SEVERITY: Minor
CATEGORY: BENCHMARK VALIDITY / MUTATION DISCLOSURE
AFFECTED_FILES:
  - reference/mandatory-mutations.md
  - prompts/implementation.md
  - prompts/implementation-evaluation.md
ROOT_CAUSE:
  The mutation document says not to give candidates the mutation "answer", while the
  implementation prompt requires candidates to read the same document and explicitly
  prepare for M-01..M-10.
PROBLEM:
  It is unclear whether FND-05 measures general defect-class protection or explicit
  conformance to known mutation recipes.
FAILURE_OR_CONFUSION_PATH:
  Candidate overfits a validator to the listed mutation mechanism and receives a high
  mutation score without broader oracle quality; evaluator later interprets the result
  as independent mutation sensitivity.
IMPACT:
  Benchmark interpretation ambiguity; product gate still has other reviews.
EVIDENCE:
  mandatory-mutations §1 vs implementation Authority/C-09.
RECOMMENDED_CHANGE:
  Choose and state one model. Recommended: candidate sees mutation ID, protected
  contract and required observable oracle property; evaluator injection mechanics are
  pre-locked but not part of the candidate task. If full disclosure is intentional,
  remove the "do not give answer" statement and label the metric accordingly.
CROSS_FILE_UPDATES:
  mandatory mutation rules, implementation C-09, evaluator probe policy.
FIXED_POLICY_AFFECTED: NO
```

### F-09

```text
ID: F-09
SEVERITY: Minor
CATEGORY: SINGLE SOURCE / MAINTAINABILITY
AFFECTED_FILES:
  - README.md
  - pre-run-checklist.md
  - run.json
  - reference/*
  - prompts/*
ROOT_CAUSE:
  Mutable identities, gate states, revisions and open-decision state are duplicated in
  Markdown and JSON without a declared update direction.
PROBLEM:
  run.json is described as machine-readable state, but prompts/checklists also carry
  independently editable values. After D locks and branch creation, drift is likely.
FAILURE_OR_CONFUSION_PATH:
  run.json says one Head/revision/model effort while a copied prompt/checklist contains
  another; the agent follows whichever artifact it receives first.
IMPACT:
  Maintenance drift and wrong-target risk after pre-run lock.
EVIDENCE:
  same model/revision/gate/open-decision values are repeated across most files.
RECOMMENDED_CHANGE:
  Declare run.json authoritative for mutable run identity/state. Markdown may repeat
  fixed policy for readability but must reference run keys for mutable values. Add a
  lock/update checklist that changes run.json first, then generated/copied prompts.
CROSS_FILE_UPDATES:
  README + checklist conventions, run.json schema, prompt variable blocks.
FIXED_POLICY_AFFECTED: NO
```

---

## 8. Role Separation Assessment

### Static vs Composer

Direction is correct but current implementation duplicates work. S0 should own deterministic rules such as Compose validation, changed-file allowlist, digest syntax, prohibited exact keys, source scan and identity checks. Composer should consume S0 output and inspect only semantic/structural quality not machine-proven facts.

### Composer vs Luna

The conceptual split is good:

- Composer: placement, code/config quality, obvious misuse, maintainability, simple scope drift.
- Luna: ADR/Issue/AC -> implementation -> test -> runtime evidence traceability.

The catalog must stop forcing Composer to re-adjudicate Luna-owned rules.

### Light vs Heavy

The funnel is valid. Light should remove broad/obvious issues; Heavy should independently hunt merge-blocking root causes. The required fix is not to broaden Heavy, but to ensure unresolved/rejected Light B/M candidates do not become a `do not review` list.

### Sol vs Opus

Separation is strong and should be kept.

- Sol: architecture, responsibility, authority, essential contract, design-level security.
- Opus: failure, lifecycle, race, hidden dependency, ownership, false assurance, mutation evidence.

Overlap on ordering/security is justified because each approaches a different failure model.

### Heavy non-goals

The explicit non-goals are appropriate. The root-cause exception is sufficient for ordinary Light escapes. Only the unresolved/rejected Light finding rule requires correction.

### Heavy budget / re-review

One full invocation each is practical. The blast-radius matrix is superior to severity-only blanket re-review and should remain.

### Conditional Judge

Trigger conditions are narrow enough. Phase A before reading Sol/Opus is a good anti-anchoring control.

---

## 9. Self-Review Replacement Assessment

### Verdict

**成立する。Separate Formal Self-Reviewを復活させる必要はない。**

Implementation C-01〜C-11 include:

- authority/scope
- topology/order/failure
- explicit migration boundary
- secret/image/volume
- lifecycle
- runtime state evidence
- negative-test positive markers
- mutation readiness
- project rules
- exact Head CI / diff check

This preserves the `AGENTS.md` requirement that Agent A verify tests and inspect its diff without creating a separate independent Self-Review phase.

### Checklist-theater resistance

Good controls already exist:

- PR self-report is lowest evidence.
- actual container state / exit / timestamp / history are required.
- negative tests need intended-path and failure-reason markers.
- Final Synthesis mutation must RED and recover GREEN.
- UNVERIFIED cannot be reported as success.

The main risk is not missing Completion Checks; it is that some checks currently encode draft implementation preferences as hard rules (F-01).

### Prompt load

The implementation prompt is long but still executable because the checks are ordered and concrete. Do not reintroduce another Self-Review execution. Reduce load by centralizing mutable identities and by making the rule catalog owner-specific rather than deleting safety-critical checks.

---

## 10. Project Rule Catalog Assessment

### Strengths

- RULE-ID / PASS-FAIL-N/A format is audit-friendly.
- MUST / MUST NOT / Evidence / Primary owner are generally testable.
- Good prohibitions include API auto-migration, exit masking, committed secrets, secret argv, anonymous DB data volume, production test hooks, false `exit != 0` oracles, and scope drift.
- Static vs human review can be separated cleanly.

### Required corrections

1. Only upper-authority or explicitly pre-run-locked decisions may be `MUST`.
2. Exact service count/name, exact file placement, exact Compose condition mechanism, restart mechanics and additional hardening constraints require either a lock decision or downgrade to `SHOULD / preferred convention`.
3. `Primary owner` must control the main adjudication; L1 should not automatically re-run all rules.
4. Catalog should reference design contract rule IDs rather than restating long behavior where possible.

### False-positive risk

Current false-positive risk is material because an Issue-compliant equivalent implementation can fail catalog conformance. This is the primary reason F-01 is Major.

### False-negative risk

Current catalog is broad; false-negative risk is mainly the Light->Heavy rejected-finding gap, not missing rules.

---

## 11. Mutation and Test-Oracle Assessment

| Mutation | Defect class valid | Expected RED reliable | False-positive risk | Execution cost | Required change |
| --- | --- | --- | --- | --- | --- |
| M-01 API waits only for Migrator start | YES | HIGH | LOW if Compose remains valid | Medium | KEEP |
| M-02 failure becomes exit 0 | YES | HIGH | LOW with real failure marker | Medium | KEEP |
| M-03 API auto-migration | YES | HIGH | LOW | Medium | KEEP |
| M-04 secret in argv | YES | HIGH | LOW with sentinel | Medium | KEEP |
| M-05 digest removed | YES | HIGH | LOW | Low | KEEP |
| M-06 named volume replaced | YES | HIGH | Low-Medium | Medium | KEEP |
| M-07 test fails before intended path | YES | HIGH if marker asserted | LOW | Medium | KEEP |
| M-08 migration history ignored | YES defect class / NO current injection | LOW | HIGH | Medium | **REDESIGN INJECTION** |
| M-09 API starts then exits | YES | HIGH | LOW | Medium | KEEP |
| M-10 reset leaves resource | YES | HIGH | Medium if project-scoping is weak | Medium | KEEP + assert exact project resource identity |

Required mutation sequence is correct:

```text
baseline GREEN
-> inject exactly one controlled defect
-> target RED for expected reason
-> revert
-> GREEN
-> residue 0
```

A syntax error, build error, missing executable, registry outage or unrelated pre-path failure is not a valid kill unless that is the explicit defect class being tested.

M-08 must be corrected before lock.

---

## 12. D-01〜D-08 Open Decision Assessment

| Decision | Correctly open | Evidence sufficient | Candidate leakage | Missing dependency | Required change |
| --- | --- | --- | --- | --- | --- |
| D-01 minimum Compose version | YES | YES | NO | exact local/CI support proof | lock from actual versions + required features |
| D-02 exact image digests | YES | PARTIAL | NO | PostgreSQL is split as P-07 | unify PostgreSQL + .NET identities under D-02 |
| D-03 secret source / reader | PARTIAL | YES | **YES** | cross-platform behavior | remove preferred concrete answer until lock |
| D-04 lifecycle commands | PARTIAL | YES | YES, restart semantics partly fixed | command semantics vs implementation mechanism | lock commands/semantics first or downgrade hard rules |
| D-05 external state capture | **NO — too narrow in ledger** | PARTIAL | NO | Migrator exit/finish, API state/start, history | rename/expand D-05 to full observation method |
| D-06 failure injection override | YES | YES | LOW | exact test-only mechanism | keep exact mechanism open; examples are fine |
| D-07 cross-platform contract | YES | YES | NO | supported shell/helper and path policy | lock from local + CI evidence |
| D-08 Final Synthesis identity | PARTIAL | YES | **YES** | exact visible label/effort | remove default Luna/Codex before Koo lock |

### Missing decision check

There is one conditional missing decision:

```text
D-09 candidate-common implementation-shape contract
```

Do **not** add D-09 automatically.

- If exact three-service topology, service names, Dockerfile/doc/test placement, exact dependency mechanism, restart mechanism, network/privilege hardening are intended as common candidate MUSTs, those choices need an explicit Koo/pre-run lock. In that case add D-09 (or split it into appropriately scoped decisions).
- If they are not intended as product/benchmark-fixed choices, remove the hard MUSTs and allow candidate designs that satisfy the observable contract. Then D-09 is unnecessary.

This review does not choose the value.

---

## 13. Simplification Opportunities

### S-1 — run.json as mutable-state SSOT

Keep fixed policy prose in Markdown, but centralize mutable values in `run.json`:

- common base
- exact model/harness/effort
- candidate branches/PRs/Heads
- D-01..D-08 state/value/evidence
- prompt revisions
- stage artifact refs
- gate state

This removes the highest-risk duplication without making copy-paste prompts non-self-contained.

### S-2 — Design contract owns behavior; catalog owns validation

Do not repeat a full behavioral design twice.

- design contract: what observable behavior is required
- rule catalog: how a rule is evaluated, evidence, owner

Catalog rules can cite a design-contract section/ID.

### S-3 — Owner-scoped Light review

L1 should not enumerate Static/Luna/Heavy-owned rules. This reduces review length while preserving escalation.

### S-4 — Common identity block

All post-candidate prompts need the same immutable identity fields:

```text
REPOSITORY
TARGET_ISSUE
BASE_SHA
TARGET_HEAD_SHA
SOURCE_ARTIFACT_REF(S)
SOURCE_ARTIFACT_REVISION(S)
PROMPT_REVISION
```

Keep those fields repeated in each copy-paste prompt for safety; generate values from `run.json` rather than hand-maintaining them.

### S-5 — Do not shorten Heavy non-goals

The Heavy exclusion lists are long but useful. Removing them would undo a fixed FND-05 experiment. Only normalize the unresolved/rejected Light exception.

---

## 14. Consolidated Change Plan — P0 / P1 / P2

### P0 — Blocker / Major

#### P0-1 Remove or explicitly lock implementation-shape overconstraints

Affected:

- design contract
- project rule catalog
- implementation
- L1/L2
- Selection / Final Synthesis

Dependency: Koo only if exact shape is intentionally fixed. Otherwise mechanical downgrade to observable-contract language.

#### P0-2 Redesign M-08 injection

Affected:

- mandatory mutations
- implementation/evaluator/final synthesis mutation references
- Opus mutation assessment

Dependency: D-06 determines exact injection mechanism, not the defect contract.

#### P0-3 Close rejected/unresolved Light B/M blind spot

Affected:

- review matrix
- Light fix output
- Sol/Opus entry rules

No policy redesign required.

#### P0-4 Add immutable stage-artifact registry

Affected:

- run.json
- evaluation / selection / final / light / heavy / judge / targeted fix / re-review / issue-ready schemas

Required before first cross-harness handoff.

#### P0-5 Normalize authority order

Affected:

- implementation
- evaluator
- L1/L2
- issue-ready

Approved specification -> Accepted ADR -> Issue #43 -> AGENTS governance -> benchmark contract; Parent/WP become gate evidence.

### P1 — Minor before lock

#### P1-1 Normalize D-01..D-08

- D-02 includes all exact image identities.
- D-03 removes preferred answer until lock.
- D-05 covers all external state capture.
- D-08 removes default author.

#### P1-2 Make rule ownership enforceable

L1 only owns Composer rules plus escalation; S0/L2/Heavy outputs are consumed.

#### P1-3 Clarify mutation disclosure model

State whether candidate sees only defect classes/oracle properties or full injection recipe.

#### P1-4 Declare update direction

`run.json` first for mutable state; Markdown/prompt values derived or copied from locked run state.

### P2 — Optional polish

- Add short cross-reference IDs from catalog rules to design-contract sections.
- Collapse repeated explanatory paragraphs that do not carry independent stop conditions.
- Keep safety-critical identity, prohibition and operation-permission blocks self-contained in each executable prompt.

---

## 15. Exact Rewrite Proposals

### P0-A — Authority block replacement

Apply to implementation / evaluator / L1 / L2 / issue-ready as appropriate:

```text
## Authority order

1. Koo-approved product policy and approved product specification
2. Accepted ADR-0001 / ADR-0008 / ADR-0009
3. Target Issue #43
4. AGENTS.md governance rules
5. Locked FND-05 pre-run benchmark contracts
6. PR descriptions / model self-report

Parent Issue #3 and WP-1 Issue #33 are current phase/gate/progress evidence.
They do not override product specification, Accepted ADR, or Issue #43.
If a lower source conflicts with a higher source, stop and report the conflict.
```

### P0-B — Observable topology contract

Replace exact-three-service acceptance wording with:

```text
Required observable runtime roles are:

- PostgreSQL 18 runtime with named data volume
- FND-04 explicit Migrator execution before API permission
- normal API runtime

A dedicated one-shot `migrator` Compose service is allowed and is the current
reference design, but Issue #43 also permits an equivalent canonical Compose path.
Until an explicit pre-run decision fixes the exact implementation shape, service
count/name and orchestration mechanism are not independent acceptance criteria.

Any accepted design MUST prove:
- PostgreSQL is actually usable before migration begins;
- Migrator failure is non-zero and prevents API start;
- only Migrator success permits API start;
- API startup does not apply migrations;
- the behavior is externally observable and reproducible.
```

For exact paths:

```text
Repository-root `compose.yaml`, project-local Dockerfiles and
`docs/operations/docker-compose.md` are preferred conventions.
Treat them as MUST only when an upper-source or explicit pre-run lock fixes them.
Otherwise an equivalent placement is acceptable when ownership is unambiguous and
all candidates are evaluated by the same observable contract.
```

### P0-C — M-08 replacement

```text
## M-08 — Migrator reports success without applying expected migration

### Defect

Keep the production test / validator unchanged. Temporarily alter only the runtime
or Migrator execution path so that the Migrator completes with exit code 0 while the
expected `InitialFoundation` row is not recorded in `public.__EFMigrationsHistory`.
The exact injection mechanism is locked under D-06.

### Protected contract

Migrator exit 0 alone is insufficient; successful migration application must be
confirmed from external database state.

### Expected detection

- clean-start / migration-history oracle is RED;
- observed Migrator exit remains 0;
- API/order observations alone cannot turn the test GREEN;
- expected migration-history row is absent or otherwise does not match the contract.

### Invalid detection

- changing/removing the history assertion itself;
- forcing the validator result to failure;
- YAML/build/CLI failure before the runtime path is reached.
```

### P0-D — Heavy handoff exception

Replace the matrix common rule with:

```text
Heavy reviewers do not repeat findings that are RESOLVED and verified, or accepted
and fixed Minor/Nit findings already owned by Light.

REJECTED, UNRESOLVED, ESCALATED, or evidence-incomplete Blocker/Major candidates are
NOT excluded from Heavy scope. When such a finding intersects the Heavy reviewer's
primary responsibility, the reviewer independently verifies the root cause from the
fixed Final Head and primary evidence rather than accepting either the Light finding
or the Author rejection.
```

Add to Light fix output:

```text
HEAVY_HANDOFF:
- resolved_findings:
- rejected_or_unresolved_blocker_major_candidates:
- evidence_incomplete_findings:
```

### P0-E — run.json stage artifact registry

Add a structure equivalent to:

```json
"stage_artifacts": {
  "implementation_evaluation": {
    "ref": null,
    "revision": null,
    "source_head_shas": [],
    "producer_commit_sha": null
  },
  "selection_adjudication": {
    "ref": null,
    "revision": null,
    "source_artifact_ref": null,
    "producer_commit_sha": null
  },
  "light_l1": {
    "ref": null,
    "target_head_sha": null,
    "producer_commit_sha": null
  },
  "light_l2": {
    "ref": null,
    "target_head_sha": null,
    "producer_commit_sha": null
  },
  "light_fix": {
    "ref": null,
    "old_head_sha": null,
    "final_head_sha": null,
    "producer_commit_sha": null
  },
  "heavy_sol": {
    "ref": null,
    "target_head_sha": null,
    "producer_commit_sha": null
  },
  "heavy_opus": {
    "ref": null,
    "target_head_sha": null,
    "producer_commit_sha": null
  }
}
```

Downstream prompts must carry exact `*_ARTIFACT_REF` and verify the stored Head/revision before using content.

### P1-A — D-05 replacement

```text
### D-05 — External state capture method

status: TO_LOCK

Lock one reproducible local/CI method for each required observation:

- Migrator container/process exit code
- Migrator finished timestamp
- API container state, including never-started vs started-then-exited
- API started timestamp
- PostgreSQL migration-history query/result
- Compose/project identity required to avoid stale-resource observation

The lock records the exact command/tool source and expected machine-readable fields.
No candidate chooses a different evidence method after execution starts.
```

### P1-B — D-03 / D-08 neutral wording

```text
D-03 defines required security properties and evidence only until Koo locks the
source/reader design. Do not include a preferred concrete implementation in the
candidate contract before that lock.

D-08 remains MODEL=null / HARNESS=null / EFFORT=null until Koo fixes the exact
Final Synthesis identity. Do not name a default author in the executable prompt.
```

### P1-C — Rule ownership

```text
A rule's Primary owner performs the full PASS/FAIL/N/A adjudication.
Other stages consume that result and do not repeat the full check.
If another stage observes a potential Blocker/Major root cause in its own evidence,
it records an ESCALATION and may independently verify only that root cause.
```

---

## 16. KEEP / MODIFY / DROP / ADD

### KEEP

- 3 independent candidates
- no OpenCode
- no separate Formal Self-Review
- predefined Completion Checks
- common evaluation + element-level Selection
- candidate merge/cherry-pick prohibition
- curated Final Synthesis from current main
- Static -> Composer -> Luna -> Light fix -> fixed Head
- Sol / Opus Heavy roles
- Heavy explicit non-goals
- Heavy root-cause exception
- Heavy budget 1 each
- conditional Judge
- finding-owner / blast-radius re-review
- external runtime evidence
- baseline GREEN -> mutation RED -> restore GREEN -> residue 0
- M-01..M-07 / M-09 / M-10 defect classes
- Issue Ready + Koo start authorization

### MODIFY

- exact implementation-shape MUST rules
- M-08 injection
- Light rejected/unresolved finding handoff
- authority order in several prompts
- D-02 / D-03 / D-05 / D-08 open-decision wording
- rule ownership semantics
- stage artifact identity
- run.json SSOT responsibility
- mutation disclosure wording

### DROP

- D-08 default Final Synthesis author before lock
- candidate-facing wording that implies an unapproved preferred D-03 answer is already chosen
- unconditional Composer re-review of every catalog rule
- blanket `do not repeat Light findings` wording
- M-08 mutation that removes/forces-success in the oracle itself

### ADD

- immutable stage-artifact registry
- explicit Heavy handling for rejected/unresolved Light B/M
- complete D-05 observation contract
- conditional D-09 only if Koo wants exact implementation shape fixed before candidates

---

## 17. Final Lock Recommendation

```text
CURRENT: FIX_REQUIRED
NEXT: apply P0 + P1 prompt-suite fixes
THEN: finding-owned re-review of F-01..F-09
AFTER B0/M0: proceed to D-01..D-08 primary-evidence lock
AFTER D LOCK: synchronize run.json / checklist / Issue #43 current contract
THEN: fresh Issue Ready Gate
THEN: wait for Koo explicit candidate-start authorization
```

Do not start candidate implementation from PR #145 as currently written.

A complete redesign of the FND-05 funnel is not recommended. The architecture of the process is sound; the necessary work is to tighten authority, identity, mutation validity and role handoff before lock.

---

## 18. Operation Confirmation

```text
TARGET_FILES_CHANGED: NO
TARGET_PR_CHANGED: NO
ISSUE_CHANGED: NO
FND05_IMPLEMENTATION_STARTED: NO
OUTPUT_FILE_ONLY: YES
```

This review changes only the prepared review output file on `agent/fnd05-prompt-suite-independent-review`.

---

## 19. Final one-line assessment

**FND-05のreview funnel自体は成立しているが、上位正本を超えるMUST化、M-08、Light→Heavy handoff、stage artifact identityを修正するまでD-01〜D-08 lockへ進めるべきではない。**
