# FND-04 Final Synthesis — G-01 Major-Fix Targeted Re-Review

Revision: `fnd04-final-major-fix-rereview-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-04 G-01 Targeted Independent Re-Reviewer** です。

この作業は **Review-only** です。Final Synthesis全体を再レビューしません。Gold `G-01 / NR-01` が新Headで解消したかだけを、old->new diffと一次証拠から独立確認してください。

---

## 0. Reviewer identity

```yaml
REVIEW_SLOT: "<T1 or T2>"
REVIEW_MODEL: "<ACTUAL MODEL>"
REVIEW_HARNESS: "<HARNESS>"
REVIEW_EFFORT: "<ACTUAL EFFORT>"
ATTEMPT: 1
```

Expected:

- T1: GPT-5.6 Sol / Codex / xHigh
- T2: Cursor Auto / Cursor / Auto

Cursor Autoはrouted modelがproduct上で表示される場合だけ記録し、表示されなければ`NOT_EXPOSED`としてください。

---

## 1. Fixed target

```yaml
REPOSITORY: kooiei-in4a/minimal-bank-system
TARGET_ISSUE: 42
TARGET_PR: 140
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
OLD_HEAD_SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
NEW_HEAD_SHA: 3511688401533f60bb77c7dcc647c4c2c4aa84c6
PR_MERGE_REF_SHA: 2e69049bd8b38e57cd4fee2c42e17edaeaf23df1
DIRECT_HEAD_CI_RUN: 31360093004
PR_MERGE_REF_CI_RUN: 31360094852
GOLD_REVISION: fnd04-final-gold-v1
FIX_SNAPSHOT_REVISION: fnd04-final-major-fix-snapshot-v1
PROMPT_REVISION: fnd04-final-major-fix-rereview-v1
```

PR #140のHeadがNEW_HEAD_SHAと違う場合、勝手に追従せず`WRONG_TARGET`で停止してください。

---

## 2. Allowed benchmark inputs

今回はblind benchmarkではなく**confirmed Majorのfix verification**です。以下を読んで構いません。

```text
docs/benchmarks/fnd04-model-comparison/review-benchmark/gold-review.md
docs/benchmarks/fnd04-model-comparison/review-benchmark/major-fix-snapshot.md
```

candidate ranking / scoreは不要であり、裁定根拠に使わないでください。

---

## 3. Scope

old Head `99cee...` -> new Head `351168...` の差分だけを中心に確認してください。

Expected exact delta:

```text
1 commit
1 file
+18 / -0

tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs
```

production sourceが変更されていれば`SCOPE_DRIFT`としてMajor扱いし、報告してください。

---

## 4. Gold G-01 acceptance test

G-01のroot cause:

- old testは`exit != 0`と固定blocklistだけでgreenになれた。
- off-blocklist fabricated destinationでもgreen。
- factoryへ到達できないtool/build failureでもgreen。

新Headが次を満たすか独立確認してください。

### A. Baseline positive-path proof

connectionなしでrepository-local `dotnet-ef database update --no-build`を実行したとき、targeted testがPASSし、少なくとも次の意味をpositiveにpinしていること。

- connection-required EF operationへ到達した
- Npgsql pathへ到達した
- destinationは空 / 未構成
- failure reasonはuninitialized connection

固定line numberや完全stack traceへの過剰couplingがないかも確認してください。

### B. M1 sensitivity — off-blocklist fabricated destination

isolated copy / temporary worktreeでproduction model-only pathへ、例えば次を一時注入してください。

```text
Host=db;Port=5432;Database=ambient_fallback;Username=postgres;Password=postgres
```

期待:

```text
DesignTimeConnectionSafetyTests = FAIL
```

`db` / `ambient_fallback`をblocklistへ足して評価してはいけません。

### C. M2 sensitivity — factory unreachable / unrelated nonzero

`--no-build`で必要なbuild outputを一時退避する等、production factoryへ到達できない代表的なtool/build failureをisolated copyで作ってください。

期待:

```text
DesignTimeConnectionSafetyTests = FAIL
```

### D. Recovery

mutationを完全discardし、new Head baselineでtargeted testが再びPASSすること。

```text
mutation residue = NONE
```

---

## 5. CI verification

独立確認:

### direct-head

```text
Run 31360093004
expected checkout 3511688401533f60bb77c7dcc647c4c2c4aa84c6
```

### merge-ref

```text
Run 31360094852
expected checkout 2e69049bd8b38e57cd4fee2c42e17edaeaf23df1
expected merge 351168... into 38c07e...
```

最低限、build / pending-model / non-PostgreSQL / real PostgreSQLがSUCCESSか確認してください。

production codeは変更されていないため、full real PostgreSQL suiteをreviewer localで再実行することは必須ではありません。CIの23 PASSを確認すれば足ります。

---

## 6. Re-review verdict

次のいずれかを選んでください。

```text
G01_FIXED
G01_NOT_FIXED
WRONG_TARGET
```

`G01_FIXED`条件:

- exact target identity PASS
- fix delta test-only
- baseline targeted test PASS
- M1でFAIL
- M2でFAIL
- recovery PASS
- mutation residueなし
- direct-head CI SUCCESS
- merge-ref CI SUCCESS
- 新たなBlocker/Majorをfix差分に導入していない

G-02/G-03/G-04/G-05などGoldの既知nonblocking findingを再裁定しないでください。

---

## 7. Required output

```text
# FND-04 G-01 Major-Fix Re-Review

Reviewer:
- Slot:
- Model:
- Harness:
- Effort:

Target:
- Result: PASS / WRONG_TARGET
- Old Head:
- New Head:
- Delta:

G-01 verdict:
- G01_FIXED / G01_NOT_FIXED / WRONG_TARGET

Evidence:
- baseline:
- M1:
- M2:
- recovery:
- mutation residue:

CI:
- direct-head:
- merge-ref:

New findings introduced by fix:
- Blocker: NONE / ...
- Major: NONE / ...

Final rationale:
- ...
```

最後にvalid JSONを1つ出力してください。

```json
{
  "schema_version": "1.0",
  "benchmark_id": "fnd04-final-synthesis-independent-review",
  "run_id": "fnd04-final-review-20260810",
  "prompt_revision": "fnd04-final-major-fix-rereview-v1",
  "reviewer": {
    "slot": "T1",
    "model": "...",
    "harness": "...",
    "effort": "...",
    "attempt": 1
  },
  "target": {
    "old_head_sha": "99cee4386ea049ad84e9c087c6fdf1e25cc20f3e",
    "new_head_sha": "3511688401533f60bb77c7dcc647c4c2c4aa84c6"
  },
  "target_verification": "pass",
  "delta": {
    "commits": 1,
    "files": ["tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs"],
    "production_code_changed": false
  },
  "g01_verdict": "G01_FIXED",
  "mutation_sensitivity": {
    "baseline": "pass",
    "M1": "failed_as_expected",
    "M2": "failed_as_expected",
    "recovery": "pass",
    "residue": "none"
  },
  "ci": {
    "direct_head_run": 31360093004,
    "direct_head": "success",
    "merge_ref_run": 31360094852,
    "merge_ref": "success"
  },
  "new_blocker_count": 0,
  "new_major_count": 0,
  "evidence_limits": []
}
```

---

## 8. Stop boundary

- repository変更禁止（mutationはisolated temporary copyのみ）
- commit / push禁止
- PR comment / review投稿禁止
- Ready化 / merge禁止
- Issue変更禁止

結果をチャットへ返して停止してください。
