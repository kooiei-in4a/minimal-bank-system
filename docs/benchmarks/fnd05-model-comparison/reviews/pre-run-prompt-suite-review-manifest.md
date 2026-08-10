# FND-05 Pre-Run Prompt Suite — Independent Review Manifest

Revision: `fnd05-prompt-suite-review-manifest-v1`

Status: **REVIEW PREPARED / TARGET IMMUTABLE**

## 1. Fixed target identity

```yaml
REPOSITORY: "kooiei-in4a/minimal-bank-system"
TARGET_PR: 145
TARGET_TITLE: "docs(fnd05): prepare ADR-first implementation and review funnel"
TARGET_BRANCH: "agent/fnd05-pre-run-preparation"
TARGET_HEAD_SHA: "57df6ae1a30ac23151fbcd707f191f5d26dba029"
TARGET_BASE_BRANCH: "agent/fnd04-final-retrospective-synthesis"
TARGET_BASE_SHA: "a69471578eed12823a1469017dac7fddf32ad41b"
TARGET_CHANGED_FILES: 22
TARGET_ADDITIONS: 5006
TARGET_DELETIONS: 0
TARGET_STATE: "DRAFT / OPEN"
TARGET_SCOPE: "docs/benchmarks/fnd05-model-comparison/** only"
```

Review対象は上記exact Headです。

- branchの最新状態を推測しない。
- target Headが変わっていた場合はreviewを停止する。
- review中にPR #145、target branch、Issue #43を変更しない。

## 2. Review output identity

```yaml
OUTPUT_BRANCH: "agent/fnd05-prompt-suite-independent-review"
OUTPUT_BASE_BRANCH: "agent/fnd05-pre-run-preparation"
OUTPUT_FILE: "docs/benchmarks/fnd05-model-comparison/reviews/pre-run-prompt-suite-independent-review.md"
OUTPUT_SCOPE: "review report only"
```

Reviewerはtarget 22 filesを変更せず、review reportだけを作成する。

## 3. Authority order

Reviewでは次の優先順位を使用する。

1. Kooが確定した方針
2. Approved specification
3. Accepted ADR-0001 / ADR-0008 / ADR-0009
4. Issue #43
5. `AGENTS.md`
6. PR #144のFND-04 final retrospective policy
7. PR #145の22 target files
8. Official Docker documentation when external verification is required
9. PR descriptions / model self-report

下位資料は上位正本を変更しない。

## 4. Fixed policy decisions — not open for preference-based redesign

次はKooが確定した方針であり、単なるモデルの好みや一般論で変更提案しない。

### Implementation candidates — 3

- GPT-5.6 Luna / Codex
- Claude Sonnet 5 / Claude Code
- Grok 4.5 / Cursor — `high`、`high fast`ではない

### Light Review — 2

- Composer 2.5 / Cursor — Project Quality / Rule Conformance
- GPT-5.6 Luna / Codex — ADR / Issue / AC Contract Conformance

### Heavy Final Review — 2

- GPT-5.6 Sol / Codex — Architecture / Contract Final Gate
- Claude Opus 5 / Claude Code — Failure / Lifecycle / False Assurance Final Gate

### Process

- OpenCodeを使用しない
- 独立Formal Self-Review / H1 phaseを置かない
- implementation promptへCompletion Checksを事前定義する
- Light Gate後のfixed HeadだけをHeavy Reviewへ渡す
- Heavy full reviewは原則Sol 1回、Opus 1回
- Heavy promptへ「原則確認しない項目」を明記する
- Judgeはconditional only
- re-reviewはfinding owner / blast radius基準
- candidate branchのmerge / cherry-pickでFinal Synthesisを作らない
- Issue Ready PASSとKooの開始許可までimplementation禁止

ただし、上記方針が上位正本と矛盾する、実行不能、または重大なblind spotを必然的に作る場合は、Blocker / Majorとして根拠付きで指摘できる。

## 5. Explicitly open decisions

D-01〜D-08はcandidate-owned decisionではなく、pre-run lock前の未確定事項である。

- D-01 minimum Docker Compose version
- D-02 exact PostgreSQL / .NET image digests
- D-03 secret source / reader design
- D-04 canonical lifecycle commands
- D-05 external state capture method
- D-06 failure injection override
- D-07 cross-platform contract
- D-08 Final Synthesis exact Model / Harness / Effort

Reviewerは次を確認する。

- open decisionとして正しい位置にあるか
- listに不足がないか
- candidateへ判断を漏らしていないか
- 既に本文で不整合に決め打ちしていないか
- lockに必要な証拠が定義されているか

Review中にD-01〜D-08を推測で確定しない。

## 6. Review target files — exact 22

### A. Control / registry — 4

1. `docs/benchmarks/fnd05-model-comparison/README.md`
2. `docs/benchmarks/fnd05-model-comparison/pre-run-checklist.md`
3. `docs/benchmarks/fnd05-model-comparison/run.json`
4. `docs/benchmarks/fnd05-model-comparison/scoring.md`

### B. Reference contracts — 5

5. `docs/benchmarks/fnd05-model-comparison/reference/assumption-ledger.md`
6. `docs/benchmarks/fnd05-model-comparison/reference/implementation-and-test-design-contract.md`
7. `docs/benchmarks/fnd05-model-comparison/reference/mandatory-mutations.md`
8. `docs/benchmarks/fnd05-model-comparison/reference/project-rule-catalog.md`
9. `docs/benchmarks/fnd05-model-comparison/reference/review-perspective-matrix.md`

### C. Implementation / synthesis prompts — 4

10. `docs/benchmarks/fnd05-model-comparison/prompts/implementation.md`
11. `docs/benchmarks/fnd05-model-comparison/prompts/implementation-evaluation.md`
12. `docs/benchmarks/fnd05-model-comparison/prompts/selection-adjudication.md`
13. `docs/benchmarks/fnd05-model-comparison/prompts/final-synthesis.md`

### D. Light gate prompts — 3

14. `docs/benchmarks/fnd05-model-comparison/prompts/light-review-project-quality.md`
15. `docs/benchmarks/fnd05-model-comparison/prompts/light-review-contract-conformance.md`
16. `docs/benchmarks/fnd05-model-comparison/prompts/light-findings-fix.md`

### E. Heavy / adjudication prompts — 3

17. `docs/benchmarks/fnd05-model-comparison/prompts/heavy-review-sol.md`
18. `docs/benchmarks/fnd05-model-comparison/prompts/heavy-review-opus.md`
19. `docs/benchmarks/fnd05-model-comparison/prompts/conditional-judge.md`

### F. Gate / repair prompts — 3

20. `docs/benchmarks/fnd05-model-comparison/prompts/issue-ready-review.md`
21. `docs/benchmarks/fnd05-model-comparison/prompts/targeted-fix.md`
22. `docs/benchmarks/fnd05-model-comparison/prompts/targeted-re-review.md`

Review preparation files on the output branch are not part of the 22-file target.

## 7. Review dimensions

### R-01 Authority and scope correctness

- Issue #43 / ADR / AGENTS.mdとの整合
- FND-04 / FND-05 / FND-06境界
- docsが正本を上書きしていないか
- implementation prohibitionが維持されているか

### R-02 Cross-file consistency

- model / harness / count
- revision IDs
- filename references
- stage order
- gate names / status
- severity definitions
- output schemas
- branch / PR assumptions
- direct-head / merge-ref terminology
- D-01〜D-08表記

### R-03 Prompt executability

- copy-pasteで実行可能か
- required values / variable fieldsが明確か
- stop conditionsが過不足ないか
- outputsが後工程の入力として使えるか
- contradictory instructionがないか
- tool / evidence requirementsが実行可能か

### R-04 Self-review replacement quality

- separate SRを廃止してもCompletion Checksが十分か
- implementation promptがreview prompt化しすぎていないか
-実装1回で達成可能な負荷か
-自由形式の自己正当化を許していないか

### R-05 Project rule enforceability

- MUST / MUST NOT / correct placementが具体的か
- static / Composer / Luna / Heavy ownerが正しいか
- false positive / overconstraintを生まないか
- project ruleとdesign contractが重複・矛盾していないか

### R-06 Light / Heavy responsibility separation

- ComposerとLunaが重複しすぎないか
- SolとOpusの責務が明確か
- Heavyの`DO NOT CHECK`が重大blind spotを作らないか
- root cause例外が十分か
- Light Gate escapeの扱いが一貫しているか
- Heavy budgetとre-review条件が実用的か

### R-07 Test oracle and mutation quality

- M-01〜M-10がIssue #43のdefect classを表すか
- baseline → RED → restore → GREEN → residue 0が明確か
- mutationがsyntax / build failureへ逃げないか
- candidateとFinal Synthesisの実行負荷が妥当か
- external observationがfalse assuranceを防げるか

### R-08 Docker / secret / lifecycle feasibility

- Compose assumptionsがofficial behaviorと整合するか
- `service_healthy` / `service_completed_successfully`
- container state / exit / timestamp取得
- secret file / argv / rendered config / logs
- image digest / named volume
- stop / start / restart / reset
- FND-06 healthなしで検証可能か

外部仕様の確認にはofficial Docker documentationを優先する。

### R-09 Evidence and identity integrity

- exact Head / common base
- candidate independence
- direct-head vs merge-ref
- duration collection
- runtime evidence order
- PR self-reportの優先度
- mutation residue

### R-10 Process efficiency

- 3 implementation + Light 2 + Heavy 2で成立するか
- redundant stage / artifact / full re-reviewがないか
- promptが長すぎて重要事項を埋没させていないか
-削減できる重複があるか
-品質gateを弱める削減案になっていないか

## 8. Severity

### Blocker

- exact targetを確認できない
-上位正本と根本矛盾
- prompt suiteが安全に実行不能
- implementation prohibitionが破れる
- targetの変更なしではreview不能

### Major

- 誤実装、scope drift、fail-open、secret漏洩、false assuranceを高確率で許す
- stage間の重大矛盾
- outputが次工程のinputとして成立しない
- Heavy exclusionによる重大blind spot
- mutation / runtime verificationが守るcontractを証明できない

### Minor

- processは成立するが、曖昧さ、重複、保守性、局所的な証拠gapがある

### Nit

- behavior / process判断へ影響しない軽微な表現・整理

## 9. Review rules

- root causeが同じ指摘を重複させない。
- 行数や文量だけを問題にしない。
- 「短くした方がよい」だけでなく、削除しても失われないcontractを説明する。
- preferenceとcorrectnessを分ける。
- fixed policyを再投票しない。
- D-01〜D-08を推測でlockしない。
- promptの実行者が誤解する具体的な経路を示す。
- Findingごとにaffected filesとcross-file changeを示す。
- full rewriteを要求する場合、部分修正では解消できない理由を示す。
- review中にtargetを変更しない。
- FND-05 implementationを開始しない。

## 10. Required output summary

```text
VERDICT: READY_FOR_LOCK_WORK / FIX_REQUIRED / REDESIGN_REQUIRED / BLOCKED
BLOCKER_COUNT:
MAJOR_COUNT:
MINOR_COUNT:
NIT_COUNT:
TARGET_HEAD_VERIFIED:
OUTPUT_FILE:
```

`READY_FOR_LOCK_WORK`は、prompt suiteをそのままmerge可能という意味ではない。D-01〜D-08の一次証拠lockとIssue Ready Gateへ進める状態を意味する。
