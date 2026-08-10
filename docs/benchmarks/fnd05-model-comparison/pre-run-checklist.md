# FND-05 Pre-Run Completion Checklist

Status: **PREPARATION FIX REQUIRED / IMPLEMENTATION PROHIBITED**

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

## 2. Prompt-suite review remediation

3 independent reviewsの共通findingを反映し、targeted re-reviewを通すまでD-01〜D-08 lockへ進まない。

- [x] exact implementation-shape MUSTの見直しを開始
- [x] M-08をruntime defect mutationへ再設計
- [x] Light rejected/unresolved B/MのHeavy handoff contract追加
- [x] Product authorityとGate evidenceの分離
- [x] open decisionのanswer leakage除去
- [x] `run.json`へstage artifact registry追加
- [ ] implementation / evaluation / selection / final / Light / Heavy promptsへv2 contract反映
- [ ] finding-owned targeted re-review
- [ ] Blocker 0 / Major 0
- [ ] `run.json.gates.prompt_suite_targeted_re_review_pass = true`

## 3. Decisions still to lock

`run.json.open_decisions`へ`locked_value`と`evidence_refs`を記録する。

### D-01 Compose version / features

- [ ] local Compose version
- [ ] GitHub Actions version
- [ ] required feature support
- [ ] validation commands

### D-02 PostgreSQL / .NET image identities

- [ ] PostgreSQL 18 exact source + full digest
- [ ] .NET 10 SDK exact source + full digest
- [ ] ASP.NET 10 runtime exact source + full digest
- [ ] platform / architecture確認

### D-03 Secret source / reader design

- [ ] source / grant / reader designをKoo lock
- [ ] repository非保存
- [ ] argv非露出
- [ ] log / rendered-config観測
- [ ] missing-secret fail-closed
- [ ] local / CI再現性

### D-04 Lifecycle commands + semantics

- [ ] validate
- [ ] clean start
- [ ] stop
- [ ] start-after-stop
- [ ] restart
- [ ] down-retain-data
- [ ] clean reset
- [ ] migration gate re-evaluation semantics
- [ ] cleanup absence observation

### D-05 External state capture

- [ ] Migrator exit code
- [ ] Migrator completion ordering evidence
- [ ] API never-started vs started-then-exited state
- [ ] API start ordering evidence
- [ ] migration history query / result
- [ ] Compose project/resource identity
- [ ] local / CI共通machine-readable command

### D-06 Failure injection

- [ ] invalid credential等のfailure path
- [ ] M-01〜M-10 injection plan
- [ ] test-only isolation
- [ ] no production backdoor
- [ ] no residue

### D-07 Cross-platform contract

- [ ] GitHub Actions Linux
- [ ] primary local environment
- [ ] shell / helper requirements
- [ ] path / line ending behavior

### D-08 Final Synthesis identity

- [ ] exact Model
- [ ] exact Harness
- [ ] exact Effort
- [ ] fresh-context availability
- [ ] final branch / Draft PR事前作成

## 4. Repository / Issue preparation

- [ ] PR #144 final retrospective review
- [ ] PR #145 prompt-suite targeted re-review PASS
- [ ] Issue #43 body / dependency / gate statusをcurrent contractへ同期
- [ ] Parent #3 / WP-1 #33 current statusをGate evidenceとして再確認

Issue本文はprompt suiteとD-01〜D-08 lock後に同期する。draft answerを確定事項として先に書かない。

## 5. Common base / branches

- [ ] preparation PR merge
- [ ] current main full SHA取得
- [ ] common base fixed in `run.json`
- [ ] C1 / C2 / C3 branch作成
- [ ] 3 / 3 Head = common base
- [ ] 3 Draft PR事前作成
- [ ] exact model / harness / effort固定
- [ ] candidate output 0件確認

## 6. Gate review

- [ ] `prompts/issue-ready-review.md` fresh execution
- [ ] Issue Ready verdict = PASS
- [ ] `run.json.gates.issue_ready_pass = true`
- [ ] Kooがcandidate execution開始を明示許可
- [ ] `run.json.gates.koo_start_authorized = true`

Issue Ready PASSだけではimplementationを開始しない。

## 7. Stop rule

次のいずれかが未完了ならcandidate executionを開始しない。

- prompt-suite targeted re-review B0 / M0
- D-01〜D-08 lock
- common base / branch / PR identity
- exact model / harness / effort
- Issue Ready PASS
- Koo start authorization

## 8. Current next action

1. PR #145 v2 prompt suite修正を完了する。
2. F-01〜共通findingのtargeted re-reviewを実施する。
3. B0 / M0後にD-01〜D-08を一次証拠でlockする。
4. Issue #43を同期する。
5. fresh Issue Ready Gateを実行する。
6. Koo開始許可後だけcandidateを開始する。

現時点ではDocker Compose実装を開始しない。
