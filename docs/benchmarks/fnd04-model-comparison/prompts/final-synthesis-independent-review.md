# FND-04 Final Synthesis — Role-Diverse Independent Review Prompt

Revision: `fnd04-final-review-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **Independent Benchmark Reviewer / Agent B相当 Reviewer** です。

この作業は **Review-only** です。

Issue #42 `[FND-04] EF Core・明示的migration実行基盤を確立する` のFinal Synthesisを、実装者の説明・candidate ranking・他reviewerの結果を前提にせず、GitHub上の一次証拠と実コードから独立レビューしてください。

---

# 0. Reviewer Identity

実行ごとに以下だけを設定します。

```yaml
REVIEWER_SLOT: "<R1-R6>"
REVIEWER_MODEL: "<PRODUCT-VISIBLE MODEL NAME>"
REVIEWER_HARNESS: "<HARNESS>"
REVIEWER_EFFORT: "<ACTUAL EFFORT OR NOT EXPOSED>"
REVIEWER_SLUG: "<STABLE MODEL-HARNESS SLUG>"
PRIMARY_ROLE: "<ROLE>"
ATTEMPT: 1
```

product-visible identity / effortは実行時に確認し、利用不能・名称変更がある場合は勝手に代替せず、その事実を結果へ記録してください。

PRIMARY_ROLEは重点観点であり、レビュー範囲を限定しません。全reviewerがIssue #42全体をレビューした上で、PRIMARY_ROLEを深掘りしてください。

---

# 1. Fixed Target Identity

```yaml
BENCHMARK_ID: "fnd04-final-synthesis-independent-review"
RUN_ID: "fnd04-final-review-20260810"
PROMPT_REVISION: "fnd04-final-review-v1"

REPOSITORY: "kooiei-in4a/minimal-bank-system"
TARGET_ISSUE: 42
TARGET_PR: 140
TARGET_PR_TITLE: "[FND-04] EF Core・明示的migration実行基盤 — Final Synthesis"

BASE_BRANCH: "main"
BASE_SHA: "38c07e210fe4e8689f1d8aeabbb07b92610d1826"
HEAD_BRANCH: "agent/issue-42-fnd-04-final-code"
HEAD_SHA: "99cee4386ea049ad84e9c087c6fdf1e25cc20f3e"

KNOWN_PR_MERGE_REF_SHA: "d12de2ae07003a10d19d576808cf88ec7796da23"
KNOWN_CI_RUN: 31350916189
```

## Target identity gate

内容レビュー前にGitHubから必ず再取得し、次を確認してください。

- PR #140が存在する
- Base branch = `main`
- Base SHA = `38c07e...1826`
- Head branch = `agent/issue-42-fnd-04-final-code`
- Head SHA = `99cee...0f3e`
- PRが未mergeである
- diffがこのBase→Headに対応する
- CIの対象identityを確認する

不一致がある場合、勝手に新Headへ追従せず、`WRONG_TARGET`として停止してください。

### CI identityについて

既知CI Run `31350916189` はPR Head `99cee...0f3e`に関連付く成功runですが、checkout logではGitHubのpull-request merge ref `d12de2a...`（`99cee...` into `38c07...`）をcheckoutしています。

したがって、レビューでは次を区別してください。

- PR merge-state CIが成功していること
- branch Headそのものをdirect checkoutしたpush CIが確認できるか

確認できないものを「exact direct-head CI」と断定しないでください。ただし、このidentity nuanceだけでコードfindingのSeverityを自動決定せず、実際のriskとrepository gateに照らして判断してください。

---

# 2. Isolation / Blind Review Rules

**読んではいけないもの:**

- candidate ranking / candidate score
- Implementation Evaluation結果
- Selection / Adjudication結果
- 他reviewerのreview
- Gold / Reference Review artifact
- Judge結果
- benchmark summary

`docs/benchmarks/fnd04-model-comparison/`の結果artifactを根拠としてレビューしないでください。

Final SynthesisのPR本文はnavigation / claimed verificationの所在確認には使えますが、正しさの証拠としてそのまま信用しないでください。

一次証拠の優先順位:

1. Issue #42
2. Accepted ADR / `AGENTS.md` / implementation plan
3. Base→Headの実diff / source
4. committed tests
5. GitHub Actionsの実run / job / logs
6. PR本文の自己申告

---

# 3. Authority / Contract

少なくとも次を確認してください。

- Issue #42本文
- `AGENTS.md`
- `docs/plans/phase-4-implementation-issue-decomposition.md`
- Accepted ADR-0001
- Accepted ADR-0009
- FND-03でmerge済みのreal PostgreSQL fixture / CI foundation

Issue #42を製品実装contractの正本として扱ってください。

主要contract:

- EF Core / Design = `10.0.10`
- Npgsql / provider = `10.0.3`
- repository-local `dotnet-ef` = `10.0.10`
- `BankDbContext` / provider config / migrations / snapshot / design-time factory = Infrastructure責任
- migrations assembly = Infrastructure
- explicit Migrator = `src/MinimalBankSystem.Migrator`
- canonical connection = `ConnectionStrings:Database` / `ConnectionStrings__Database`
- `InitialFoundation` = empty baseline
- migration history = `public.__EFMigrationsHistory`
- API startup auto-migration / `EnsureCreated` / ad-hoc DDL禁止
- Migrator successのみexit 0
- connection / migration / timeout failureはnon-zero
- database command timeout / migration cancellation budget = 60秒
- actual EF pending-model mechanismを使う
- evaluator-only model drift negative verificationが成立すること
- idempotent migration SQL generation path
- business schema / Compose / healthはscope外

---

# 4. Required Review Probes

以下を実コード・test・CIから独立に検証してください。

## P01 — Identity / version / ownership

- package/tool version exact pin
- Infrastructure ownership
- dedicated Migrator
- provider / migrations assembly consistency

## P02 — Clean real PostgreSQL apply

- clean DBからexplicit Migrator processで`InitialFoundation`をapply
- exit 0
- migration history 1件
- business schemaなし

## P03 — Rerun

- 2回目もexit 0
- history unchanged

## P04 — Failure propagation

- missing connection
- unreachable PostgreSQL
- rejected credentials
- migration failure

が成功扱いされないこと。

## P05 — Actual 60-second bounded execution

特に厳密に確認してください。

- external test timeoutだけでなくproduction Migrator自身の60秒budgetが発火しているか
- Npgsql database command timeoutも60秒へ設定されているか
- timeout / cancellationがnon-zeroになるか
- testが実際にproduction entry pointを通っているか

## P06 — API no-auto-migration

- normal API startupで`Migrate` / `EnsureCreated` / DDLなし
- real PostgreSQLのbefore/afterでschema/history unchanged
- actual `BankDbContext` resolve後もschema mutationなし

## P07 — Pending model positive

- actual EF mechanismを使ってclean modelを確認しているか
- constant / migration-list tautologyで代替していないか

## P08 — Temporary model drift negative

- model-only changeでpending modelを検出できる設計か
- authorのlocal-only probe claimとcommitted evidenceを区別する
- probe残骸がHeadへ混入していないか

local-only verificationをGitHub一次証拠で再現不能な場合、その限界を明記してください。自己申告だけを自動的にPASS証拠へ昇格させないでください。

## P09 — Forbidden paths

- API startup migrationなし
- `EnsureCreated`なし
- ad-hoc DDLなし

## P10 — Design-time/runtime consistency / fail-closed

最重要観点の一つです。

- design-time factoryがsame Npgsql provider / Infrastructure migrations assemblyか
- connectionなしのmodel-only operationでfake providerへ逃げていないか
- connection-required design-time operationでconnection未設定時にfail-closedか
- fabricated localhost / `design_time` / ambient destinationを生成していないか
- regression testがproduction factory経路を実際に通るか
- testが単にerror messageに禁止文字列がないことだけを見て、実destination safetyを誤って証明したことになっていないか

必要ならEF Core / Npgsqlのofficial source / docsでframework semanticsを確認してください。外部情報を使った場合は出典を明記してください。

## P11 — Idempotent SQL

- repository-local `dotnet-ef` / EF migrator pathで生成可能か
- baselineを正しくguardするか

## P12 — Scope boundary

- business entity/table/constraint先取りなし
- Composeなし
- healthなし
- FND-05/FND-06 scope creepなし
- unnecessary abstraction / boilerplateが重大な保守性問題になっていないか

## P13 — FND-03 regression

- real PostgreSQL fixture / lifecycle / CIを壊していないか

## P14 — Secret non-disclosure

- rejected credential failureでsentinel passwordがstdout / stderrへ漏れないtestがproduction outputを十分忠実に観測しているか
- exception logging等からconnection string / passwordが別pathで露出しないか
- testが弱い場合はfalse assuranceとして評価する

## P15 — CI fidelity

- run / job / step / checkout identity
- build warnings/errors
- non-PostgreSQL / real PostgreSQL execution
- pending-model CI step
- merge-ref CIとdirect-head CIを混同していないか

---

# 5. Role-Specific Deep Dive

PRIMARY_ROLEに応じて以下を追加で深掘りしてください。

### `runtime_failure_path`

- process exit semantics
- cancellation vs command timeout
- lock / connection / auth / malformed migration failures
- exception/logging leakage
- actual external effects

### `deep_technical_test_assurance`

- testsがproduction pathを本当に通るか
- false assurance / tautology / weak assertions
- race / flakiness / process-global state
- negative testsの忠実度
- cleanup / isolation regression

### `specification_scope`

- Issue #42 AC coverage
- ownership / dependency direction
- scope / out-of-scope
- ADR consistency
- FND-05/FND-06先取り

### `framework_official_source_cross_check`

- EF Core design-time factory semantics
- connectionless Npgsql model-only contextの意味
- `HasPendingModelChanges`
- `MigrateAsync` / migration history behavior
- timeout/cancellation semantics
- official docs / sourceと実装主張の一致

### `tool_driven_independent_review`

- GitHub / git / test / runtime toolでclaimsを再検証
- static inspectionだけでなく可能なprobeを重視
- target identity / CI identity

### `fast_independent_review`

- broad defect scan
- high-signal Blocker/Major優先
- scope / failure / hidden coupling / security
- trivial style findingを増やさない

---

# 6. Severity Policy

```text
Blocker:
  このHeadをmerge候補として扱えない致命的問題。

Major:
  Issue #42の重要AC未達、誤動作、failure safety欠陥、false assurance等。
  merge前の修正が必要。

Minor:
  Issue Closeを必ずしも妨げないが、実質的な品質・検証・保守性問題。

Nit:
  小さな正確性・文書・可読性問題。好みだけの指摘は含めない。
```

Blocker / Majorが1件でもあればmerge-readyは`NO`です。

finding countを増やすことを目的にしないでください。同じroot causeを重複findingへ分割しないでください。

---

# 7. Required Output

最終応答は **Markdown Review + Structured JSON** の2部構成にしてください。

## Part A — Markdown

```text
# FND-04 Final Synthesis Independent Review

Reviewer:
- Slot:
- Model:
- Harness:
- Effort:
- Primary role:
- Attempt:

Target verification:
- Repository:
- PR:
- Base SHA:
- Head SHA:
- PR state:
- CI identity:
- Result: PASS / WRONG_TARGET

Verdict:
- APPROVE / APPROVE_WITH_FINDINGS / CHANGES_REQUIRED / WRONG_TARGET

Merge-ready:
- YES / NO / NOT_EVALUATED

Findings:

### Blocker
- ... / NONE

### Major
- ... / NONE

### Minor
- ... / NONE

### Nit
- ... / NONE

Probe matrix:
- P01: PASS / FAIL / PARTIAL / NOT_VERIFIED — evidence
...
- P15: ...

Primary-role deep dive:
- ...

CI assessment:
- ...

Unverified / evidence limits:
- ...

Final rationale:
- ...
```

各findingには必ず、

- ID
- Severity
- blocking yes/no
- affected path/component
- evidence
- root cause
- impact
- required fix direction（blockingの場合）

を含めてください。

## Part B — JSON

Markdownの後に、valid JSON code blockを1つだけ出力してください。

```json
{
  "schema_version": "1.0",
  "benchmark_id": "fnd04-final-synthesis-independent-review",
  "run_id": "fnd04-final-review-20260810",
  "prompt_revision": "fnd04-final-review-v1",
  "target": {
    "repository": "kooiei-in4a/minimal-bank-system",
    "issue": 42,
    "pr": 140,
    "base_sha": "38c07e210fe4e8689f1d8aeabbb07b92610d1826",
    "head_sha": "99cee4386ea049ad84e9c087c6fdf1e25cc20f3e"
  },
  "reviewer": {
    "slot": "...",
    "model": "...",
    "harness": "...",
    "effort": "...",
    "slug": "...",
    "primary_role": "...",
    "attempt": 1
  },
  "outcome": "completed",
  "target_verification": {
    "result": "pass",
    "observed_base_sha": "...",
    "observed_head_sha": "...",
    "pr_state": "..."
  },
  "verdict": "APPROVE",
  "merge_ready": true,
  "severity_counts": {
    "blocker": 0,
    "major": 0,
    "minor": 0,
    "nit": 0
  },
  "findings": [],
  "probe_results": {
    "P01": "pass",
    "P02": "pass",
    "P03": "pass",
    "P04": "pass",
    "P05": "pass",
    "P06": "pass",
    "P07": "pass",
    "P08": "pass",
    "P09": "pass",
    "P10": "pass",
    "P11": "pass",
    "P12": "pass",
    "P13": "pass",
    "P14": "pass",
    "P15": "pass"
  },
  "ci_verification": {
    "verified": true,
    "run_id": 31350916189,
    "conclusion": "success",
    "checkout_identity": "pr_merge_ref_or_direct_head_or_unresolved",
    "observed_checkout_sha": "..."
  },
  "local_verification": {
    "performed": false,
    "summary": "..."
  },
  "evidence_limits": [],
  "completed_at": "<ISO-8601 or NOT_CAPTURED>"
}
```

finding object schema:

```json
{
  "id": "R?-F01",
  "severity": "Major",
  "blocking": true,
  "title": "...",
  "description": "...",
  "affected_path": "...",
  "evidence": ["..."],
  "root_cause_key": null,
  "required_fix": "..."
}
```

`root_cause_key`はGoldを知らないため通常`null`で構いません。勝手にGold IDを推測しないでください。

---

# 8. Prohibited Actions

- code変更
- branch変更
- commit / push
- PR comment / review投稿
- PR Ready化 / merge / close
- Issue #42変更 / close
- candidate branch / PR参照による答え合わせ
- benchmark ranking / evaluation / selection artifact参照
- 他reviewer結果参照
- Gold参照

この実行では**review結果をチャットへ返すだけ**です。Collectorがraw artifactとして別途保存します。

---

# 9. Stop Conditions

- Base / Head / PR identity不一致
- Authority取得不能
- exact target diff取得不能
- targetがreview中に変更された
- product-visible reviewer identityを確定できず、別modelへの代替が必要になった

停止時もStructured JSONを出し、`outcome`を`wrong_target`または`stopped`として理由を記録してください。
