# FND-05 Pre-Run Completion Checklist

Status: **DECISIONS LOCKED / PROMPT GATE B0 M0 / ISSUE READY REVIEW NEXT / IMPLEMENTATION PROHIBITED**

Mutable stateの正本は`run.json`。本checklistは確認手順であり、値を独立に確定しない。

## 1. Fixed process policy

- [x] implementation candidate = 3
- [x] C1 GPT-5.6 Luna / Codex
- [x] C2 Claude Sonnet 5 / Claude Code
- [x] C3 Grok 4.5 / Cursor high
- [x] OpenCode = 0
- [x] separate Formal Self-Review / H1 = 0
- [x] implementation promptへevidence-backed Completion Checksを組み込む
- [x] Light L1 = Composer 2.5 / Cursor
- [x] Light L2 = GPT-5.6 Luna / Codex
- [x] Heavy H1 = GPT-5.6 Sol / Codex
- [x] Heavy H2 = Claude Opus 5 / Claude Code
- [x] Heavy explicit non-goals
- [x] Heavy full review budget = 原則各1回
- [x] Judge = conditional only
- [x] re-review = finding owner / blast radius
- [x] observable contractをexact implementation shapeより優先
- [x] Issue ReadyとKoo start authorizationを分離
- [x] candidate preparationはIssue Ready PASS + Koo authorization後

## 2. Prompt-suite review remediation

### Completed

- [x] exact implementation-shape MUSTの見直し
- [x] M-08をruntime defect mutationへ再設計
- [x] Light rejected/unresolved B/MのHeavy handoff contract追加
- [x] Product authorityとGate evidenceの分離
- [x] open decisionのanswer leakage除去
- [x] `run.json`へstage artifact registry追加
- [x] mutation determinism contract追加
- [x] `FND05-PSR-005` finding-owned targeted re-review
- [x] `FND05-PSR-005` Blocker 0 / Major 0
- [x] `FND05-GATE-001` finding-targeted re-review
- [x] `FND05-GATE-001` Blocker 0 / Major 0
- [x] `run.json.gates.prompt_suite_targeted_re_review_pass = true` 再lock
- [x] `run.json.gates.prompts_locked = true` 再lock

PSR-005 reviewed Head / CI:

```text
6c626451fc7d8059e468d19afbfc3c80b666acb9
31443292973 — SUCCESS
```

GATE-001 reviewed Head / CI:

```text
d37ac13e4b8adc6e14cd140d1b8d5342f04b5a5a
31447021460 — SUCCESS
```

Gate-order fix:

```text
prompts/issue-ready-review.md
fnd05-issue-ready-review-v3
```

Fixed order:

```text
D-01〜D-08 lock
→ Issue Ready PASS
→ Koo explicit start authorization
→ common base / candidate branch / Draft PR / exact identity preparation
→ pre-execution identity verification
→ candidate execution
```

## 3. D-01〜D-08 lock

Canonical lock artifact:

```text
docs/benchmarks/fnd05-model-comparison/reference/pre-run-decision-locks.md
Revision: fnd05-decisions-v1
```

Local evidence artifact:

```text
docs/benchmarks/fnd05-model-comparison/evidence/local-pre-lock-evidence-20260811.md
```

- [x] D-01 minimum Compose version / required features = LOCKED
- [x] D-02 PostgreSQL / .NET exact digest identities = LOCKED
- [x] D-03 secret source / reader design = LOCKED
- [x] D-04 lifecycle commands / semantics = LOCKED
- [x] D-05 external state capture = LOCKED
- [x] D-06 failure injection / mutation determinism = LOCKED
- [x] D-07 cross-platform contract = LOCKED
- [x] D-08 Final Synthesis identity = LOCKED
- [x] `run.json.gates.mutation_determinism_locked = true`

### Key lock summary

```text
D-01: Compose >= 2.38.2
D-02: digest-qualified postgres/.NET images from current registry evidence
D-03: host env -> Compose secret -> mounted file / explicit grant
D-04: down/up restart semantics; clean reset uses --volumes --remove-orphans
D-05: compose ps JSON + docker inspect + migration history + project labels
D-06: deterministic precondition/barrier/signature per mutation
D-07: linux/amd64, Ubuntu 24.04, Bash >=5.2, jq >=1.7, LF shell assets
D-08: GPT-5.6 Terra / Codex / xHigh; fresh context; no silent substitution
```

## 4. Repository / Issue preparation

- [x] PR #144 latest Head is PR #145 current stacked base
- [x] PR #145 prompt-suite / gate-order targeted re-review PASS
- [x] dependency #42 COMPLETE / MERGED reverified
- [x] GATE-001 reviewed Head CI SUCCESS
- [x] Issue #43 body / dependency / gate statusをcurrent locked contractへ同期
- [x] Parent #3でWP-1 Issue Set Ready / Implementation Ready PASSを再確認
- [x] WP-1 #33 current statusをFND-05 current stateへ同期

Issue Ready PASSはfresh Gate Reviewでのみ確定する。

## 5. Common base / candidate preparation

**Issue Ready PASS + Koo explicit start authorization後に実施する。現時点では実施禁止。**

- [ ] current main full SHA取得
- [ ] common base fixed in `run.json`
- [ ] C1 / C2 / C3 branch作成
- [ ] 3 / 3 initial Head = common base
- [ ] 3 Draft PR事前作成
- [ ] exact candidate Model / Harness / Effort固定
- [ ] candidate output 0件確認
- [ ] pre-execution identity verification

## 6. Issue Ready Gate

- [ ] `prompts/issue-ready-review.md` v3 fresh execution
- [ ] Issue Ready verdict = PASS
- [ ] `run.json.gates.issue_ready_pass = true`

Issue Ready PASSだけではcandidate preparationもimplementationも開始しない。

## 7. Koo start authorization

Issue Ready PASS後に別gateとして取得する。

- [ ] Kooがcandidate preparation / execution開始を明示許可
- [ ] `run.json.gates.koo_start_authorized = true`

`implementation_permitted = true`は、その後にcommon base / candidate branch / Draft PR / exact identity / output-zeroのpre-execution gateまで満たしてから更新する。

## 8. Stop rule

次のいずれかが未完了ならcandidate executionを開始しない。

- D-01〜D-08 lock identity保持
- Issue #43 current contract sync
- Parent / WP / dependency gate確認
- Issue Ready PASS
- Koo start authorization
- common base / candidate branch / Draft PR identity
- exact candidate Model / Harness / Effort
- candidate output 0件

## 9. Current next action

1. latest coordinator-state HeadのCI SUCCESSを確認する。
2. fresh Issue Ready Gate v3を実行する。
3. Issue Ready PASS後も停止し、Koo開始許可を待つ。
4. Koo許可後にだけcommon base / candidate preparationへ進む。

現時点ではFND-05 implementationを開始しない。
