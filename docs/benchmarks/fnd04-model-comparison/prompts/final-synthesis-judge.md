# FND-04 Final Synthesis — Independent Judge Prompt

Revision: `fnd04-final-judge-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-04 Independent Adjudication Judge** です。

この作業は **Review / Adjudication-only** です。コード、branch、PR、Issueを変更してはいけません。

目的は、5本のrole-diverse independent reviewで提出されたfindingを多数決やreviewerの評判で決めず、Issue #42・Accepted ADR・exact Base→Head diff・test・CI・必要なruntime/framework一次証拠から独立に裁定することです。

---

## 0. Judge Identity

実行ごとに以下だけ変更します。

```yaml
JUDGE_SLOT: "<A or B>"
JUDGE_MODEL: "<ACTUAL MODEL>"
JUDGE_HARNESS: "<HARNESS>"
JUDGE_EFFORT: "<ACTUAL EFFORT>"
ATTEMPT: 1
```

Expected:

- Judge A: GPT-5.6 Sol / Codex
- Judge B: Claude Opus 5 / Claude Code

fresh contextで実行してください。reviewerとして同Model/Harnessを使った過去executionがあっても、その結論を記憶で再利用せず一次証拠から再構築してください。

---

## 1. Fixed Target

```yaml
BENCHMARK_ID: "fnd04-final-synthesis-independent-review"
RUN_ID: "fnd04-final-review-20260810"
JUDGE_PROMPT_REVISION: "fnd04-final-judge-v1"

REPOSITORY: "kooiei-in4a/minimal-bank-system"
TARGET_ISSUE: 42
TARGET_PR: 140

BASE_SHA: "38c07e210fe4e8689f1d8aeabbb07b92610d1826"
HEAD_SHA: "99cee4386ea049ad84e9c087c6fdf1e25cc20f3e"

PR_MERGE_REF_SHA: "d12de2ae07003a10d19d576808cf88ec7796da23"
PR_MERGE_REF_CI_RUN: 31350916189
DIRECT_HEAD_CI_RUN: 31350870902
```

内容裁定前にPR #140のBase / Head / unmerged stateを再取得してください。Headが変わっていれば`WRONG_TARGET`として停止し、新Headへ追従しないでください。

---

## 2. Blind Phase A — Reference Verdictを先に作る

**最初はreviewer raw results / finding normalizationを読まないでください。**

まず次だけから独立Reference Reviewを作成してください。

1. Issue #42
2. `AGENTS.md`
3. implementation plan
4. Accepted ADR-0001 / ADR-0009
5. exact Base→Head diff / production source
6. committed tests
7. GitHub Actions / local probe（必要なら）
8. EF Core / Npgsql semanticsがmaterialなら公式一次source

Phase Aで少なくとも以下を確定してください。

```text
REFERENCE_VERDICT:
  APPROVE / APPROVE_WITH_FINDINGS / CHANGES_REQUIRED

REFERENCE_MERGE_READY:
  YES / NO

REFERENCE_COUNTS:
  Blocker / Major / Minor / Nit

REFERENCE_FINDINGS:
  root cause単位
```

findingを作る場合はSeverity、blocking、affected path、evidence、root cause、impact、required fixを記載してください。

**Phase AのReferenceを固定してからPhase Bへ進んでください。**

---

## 3. Phase B — Raw reviewer findingを裁定

Phase A完了後だけ、次を読んでください。

```text
docs/benchmarks/fnd04-model-comparison/review-benchmark/finding-normalization-prejudge.md

docs/benchmarks/fnd04-model-comparison/review-benchmark/reviews/*.md

docs/benchmarks/fnd04-model-comparison/review-benchmark/reviews/*.json
```

candidate ranking / Implementation Evaluation / Selection-Adjudicationの点数は裁定根拠に使わないでください。

reviewer identity /モデル評判 / token costをFinding真偽の根拠にしないでください。

各normalized candidate `NR-01`〜`NR-06`を、一次証拠に基づき次で裁定してください。

```text
CONFIRMED_BLOCKER
CONFIRMED_MAJOR
CONFIRMED_MINOR
CONFIRMED_NIT
EVIDENCE_LIMIT_ONLY
REJECTED_FALSE_POSITIVE
DUPLICATE
```

---

## 4. Mandatory adjudication questions

### NR-01 — C8-M01 regression false assurance

最重要争点です。

production implementation自体が現在fail-closedで正しいことと、committed regression testがその性質を証明・防御できることを分けて考えてください。

確認:

- `DesignTimeConnectionSafetyTests`はproduction design-time factory / Npgsql connection-required pathへ到達したことをpositiveに証明しているか。
- `exit != 0`だけでtool/build/MSBuild failureを成功扱いしてしまわないか。
- fixed blocklist不在だけでoff-blocklist destination / ambient destinationを防げるか。
- R2 mutation claim（`Host=db;Database=ambient_fallback`でもtest green、build output欠落でもtest green）は再現可能 / code reasoning上妥当か。
- Issue #42 / Final Synthesis verification contract上、このtest assurance不足はmerge-blocking Majorか、それともproduction behaviorが正しいためMinorか。
- 過去にC8-M01 Majorが実際に発生したこと自体をSeverity根拠にせず、今回Headのmerge risk / required evidenceからSeverityを決める。

### NR-02 — timeout classification

- CommandTimeout(60)とCTS(60s)が同一deadlineであることでexit 1 / exit 2のreachable raceが実際にあるか。
- Npgsql 10.0.3 / EF Core 10 semanticsを必要なら一次sourceで確認。
- Issue #42が要求するのはbounded nonzeroか、それともexit 2 taxonomyまでproduct contractか。
- current test / observed runsで十分か。

### NR-03 — model drift negative evidence

R1/R2がindependent temporary drift probeを再現したと報告している。これでR3のevidence limitationが解消したかを裁定してください。

### NR-04 — CI wording

Coordinator一次確認済み:

- Run 31350916189 = PR merge-ref checkout `d12de2ae...`, SUCCESS
- Run 31350870902 = direct Head checkout `99cee438...`, SUCCESS

コードfindingではなくPR evidence metadataのNitかを裁定してください。

### NR-05 — exit taxonomy coverage

Issue contractとREADMEで公開した0/1/2 semanticsを分けて評価してください。

### NR-06 — low-information assertions

behavioral coverageが他で十分ならNit / rejectを判断してください。

---

## 5. Severity policy

```text
Blocker:
  このHeadをmerge候補として扱えない致命的問題。

Major:
  重要AC、failure safety、またはmergeに必要なverificationが実質未達。
  merge前修正必須。

Minor:
  mergeを必ずしも止めないが実質的な品質 / assurance / maintainability問題。

Nit:
  小さな正確性 / 証拠表記 / no-information assertion等。
```

production bugがないという理由だけでfalse assuranceを自動的にMinorへ下げないでください。一方、testの理想論だけでMajorへ上げないでください。Issueのclose / merge gateに必要な証拠かどうかで決めてください。

---

## 6. CI identity

両runを可能な範囲で独立確認してください。

```text
31350916189 — PR merge-ref
d12de2ae07003a10d19d576808cf88ec7796da23

31350870902 — direct Head
99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
```

両方成功なら、direct-head CI evidence gapは解消済みとして扱ってください。

---

## 7. Required Output

```text
# FND-04 Judge Result

Judge:
- Slot:
- Model:
- Harness:
- Effort:
- Attempt:

Target identity:
- PASS / WRONG_TARGET

## Phase A — Independent Reference

Reference verdict:
Reference merge-ready:
Reference counts:
Reference findings:

## Phase B — Normalized Finding Adjudication

NR-01:
- disposition:
- severity:
- blocking:
- reasoning:
- required fix:

NR-02:
...

NR-06:
...

## Final adjudication

Final verdict:
Merge-ready:
Blocking root causes:
Confirmed nonblocking findings:
Rejected findings:

## Judge quorum comparison key

REFERENCE_VERDICT:
BLOCKING_ROOT_CAUSES:
MERGE_READY:

## Evidence limits

- ...
```

最後にvalid JSONを1つ出力してください。

```json
{
  "schema_version": "1.0",
  "benchmark_id": "fnd04-final-synthesis-independent-review",
  "run_id": "fnd04-final-review-20260810",
  "judge_prompt_revision": "fnd04-final-judge-v1",
  "judge": {
    "slot": "A",
    "model": "...",
    "harness": "...",
    "effort": "...",
    "attempt": 1
  },
  "target_verification": "pass",
  "phase_a_reference": {
    "verdict": "...",
    "merge_ready": true,
    "counts": {"blocker":0,"major":0,"minor":0,"nit":0},
    "findings": []
  },
  "normalized_adjudication": {
    "NR-01": {"disposition":"...","severity":"...","blocking":true,"reason":"..."},
    "NR-02": {"disposition":"...","severity":"...","blocking":false,"reason":"..."},
    "NR-03": {"disposition":"...","severity":"...","blocking":false,"reason":"..."},
    "NR-04": {"disposition":"...","severity":"...","blocking":false,"reason":"..."},
    "NR-05": {"disposition":"...","severity":"...","blocking":false,"reason":"..."},
    "NR-06": {"disposition":"...","severity":"...","blocking":false,"reason":"..."}
  },
  "final": {
    "verdict": "...",
    "merge_ready": true,
    "blocking_root_causes": [],
    "confirmed_nonblocking_findings": [],
    "rejected_findings": []
  },
  "quorum_key": {
    "reference_verdict": "...",
    "blocking_root_causes": [],
    "merge_ready": true
  }
}
```

---

## 8. Stop boundary

- code / tests変更禁止
- PR comment / review投稿禁止
- Ready化 / merge禁止
- Issue変更禁止
- Judge同士の結果参照禁止

Judge A / Bは互いの結果を見ずに独立実行してください。Collectorが2結果を回収後、quorum keyを比較する。REFERENCE_VERDICT、blocking root cause、merge-readyのいずれかが不一致ならConditional Judge Cを追加する。