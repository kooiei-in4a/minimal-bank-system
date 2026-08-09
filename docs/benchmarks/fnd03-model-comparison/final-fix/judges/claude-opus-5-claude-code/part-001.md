I'll begin with Phase A — Reference Lock, examining the repository context before reading any candidate diffs.

Reference materials confirmed at primary-source level. Now verifying the exact candidate Heads.

All 14 heads verified against the common base. **REFERENCE LOCKED** — now Phase B.

Phase Bの一次証拠確認が完了しました。以下、要求された形式で報告します。

---

# FND-03 Final Code Major Fix — Independent Evaluation

## 1. Evaluator Identity

```
EVALUATOR_MODEL: "Claude Opus 5"
EVALUATOR_HARNESS: "Claude Code"
EVALUATOR_EFFORT: "xhigh"
EVALUATOR_SLUG: "claude-opus-5-claude-code"
ATTEMPT: 1

```

---

## 2. Reference Review

### Confirmed root cause

Testcontainers .NET 4.13.0 の一次sourceで機構を確定した。

`src/Testcontainers/Resource.cs`:

```
private int _disposed;
protected bool Disposed => 1.Equals(Interlocked.CompareExchange(ref _disposed, 1, 0));

```

`Disposed` は**副作用を持つproperty**である。1回目の読み取りは `_disposed` を 0→1 へ遷移させたうえで `false` を返し、2回目以降は `true` を返す。

`src/Testcontainers/Containers/DockerContainer.cs`:

```
protected override async ValueTask DisposeAsyncCore()
{
  if (Disposed) { return; }                    // ← ここでlatchが立つ
  using (_ = await AcquireLockAsync()) {
    ...
    await UnsafeDeleteAsync();                 // ← 実際のDocker removal。ここでthrowし得る
  }
  await base.DisposeAsyncCore();
}

```

したがって時系列は次になる。

```
1st DisposeAsync
  Disposed を読む → _disposed = 1 に確定（removal より前）
  UnsafeDeleteAsync → _client.RemoveAsync 失敗 → throw
  _container はresetされないので Id / Name は読める
2nd DisposeAsync (same instance)
  Disposed → true → 即return（Dockerに一切接触しない、例外も出ない）

```

Base SHA `91e3fca...` のfixtureは、失敗した `DisposeAsync()` の後もsame instanceを保持し「retryできる」と記述していた（README: *"The fixture retains ownership so cleanup can be retried"*）。実際には2回目は無言のno-opであり、wrapperがそれを成功とみなして `container = null` にすれば、Docker上にcontainerが残ったままownerが消滅する。

要点は「C# referenceを保持していること」と「cleanup retryが可能であること」が同値でない、という点にある。retry可能な唯一の資産は **Docker側のresource identity（container ID / name / ownership label）** であり、Testcontainers instanceではない。

### Reference Requirements

| ID 要求 PASS条件  |                                           |                                                                                      |
| ------------- | ----------------------------------------- | ------------------------------------------------------------------------------------ |
| R-01          | cleanup failure visibility                | container cleanup失敗が呼び出し側へ例外として到達する。無言で成功へ変換しない（Issue #41 AC「cleanup失敗を黙って無視しない」）    |
| R-02          | poisoned same-instance retryを行わない         | 失敗後のTestcontainers instanceに対する2回目以降の `DisposeAsync` をcleanup手段として使わない。その戻りを成功と解釈しない |
| R-03          | deterministic resource ownership          | Testcontainers instanceから独立したresource identityを保持し、daemon側で不在が確認できるまでownerを解放しない     |
| R-04          | final cleanup path                        | 通常のfixture lifecycle（`DisposeAsync`）から到達可能な、instance非依存の最終removal pathが存在する          |
| R-05          | startup primary failure + cleanup failure | 起動途中でcontainerが生成された場合もidentityを回収し、primary失敗とcleanup失敗の両方を保持したままownerを維持する          |
| R-06          | database lifecycle regressionなし           | `PostgreSqlTestDatabase` のdrop契約、`template0`、`Pooling=false`、isolationを壊さない          |
| R-07          | existing FND-03 contract維持                | digest pin、PostgreSQL 18.4検証、parallel policy、no fallback、既存testを維持                   |

### Required Test Evidence

| ID 要求  |                                                                 |
| ------ | --------------------------------------------------------------- |
| T-01   | deterministic container cleanup failure（root causeに到達する失敗であること） |
| T-02   | 最初のcleanup失敗が可視である                                              |
| T-03   | poisoned instanceの再Disposeをfalse successとして扱っていない               |
| T-04   | actual Docker daemon側のresource state（存在／不在）を検証している              |
| T-05   | startup partial-cleanup path                                    |
| T-06   | regression test / exact Head CI                                 |

### Reference Lock

```
REFERENCE LOCKED

```

上記R-01〜R-07およびT-01〜T-06は、14 candidateのdiffを1件も読む前に、`AGENTS.md` / Issue #3 / Issue #41 / WP #33 / Base SHA `91e3fca` のfixture / Testcontainers 4.13.0一次source のみから固定した。以降、candidateを読んだことによる基準変更は行っていない。

**POST-LOCK DISCOVERY**: なし。全candidate共通のReference変更を要する新規発見はなかった。candidate固有のbugは各評価へ反映した。

---

## 3. Collection Integrity

```
Candidate count:        14 / 14
Exact common base:      14 / 14  (git merge-base = 91e3fca、全件で一致)
Candidate Head fixed:   14 / 14  (registry記載SHAとGitHub PR headRefOidが完全一致)
Draft PR:               14 / 14  (全件 draft=true / state=OPEN / base=agent/issue-41-fnd-03-final-code)
Exact Head CI:          14 / 14  SUCCESS (GitHub Actions APIでhead_sha照会し独立再確認、run IDもrun.jsonと一致)
Identity mismatch:      0

```

補足: `run.json` が記録するとおり、Qwen3.7 Plusは準備時 `effort: MAX` に対しPR本文が `default` と自己申告している。この差はScoreへ反映していない（Coding Score対象外）。

commit数は GPT-5.6 Sol / Codex のみ2、他13件は1。全candidateの変更は `tests/MinimalBankSystem.IntegrationTests/**` に閉じており、`Directory.Packages.props`、`global.json`、`.github/workflows/`、`src/**`、benchmark artifactへの変更は**0件**（HF-06 / HF-07 全件クリア）。例外はMiMo-V2.5の `tests/.editorconfig` 新規追加のみ。

---

## 4. Executive Ranking

| Rank Model + Harness Score /100 Major Fixed Merge Candidate Duration Quality/min  |                               |    |         |     |       |       |
| --------------------------------------------------------------------------------- | ----------------------------- | -- | ------- | --- | ----- | ----- |
| 1                                                                                 | Claude Opus 5 / Claude Code   | 91 | YES     | YES | 28.0  | 3.25  |
| 2                                                                                 | GPT-5.6 Sol / Codex           | 90 | YES     | YES | 28.68 | 3.14  |
| 3                                                                                 | GPT-5.6 Terra / Codex         | 90 | YES     | YES | 21.0  | 4.29  |
| 4                                                                                 | DeepSeek V4 Flash / Open Code | 85 | YES     | YES | 75.0  | 1.13  |
| 5                                                                                 | GPT-5.6 Luna / Open Code      | 83 | YES     | YES | 17.0  | 4.88  |
| 6                                                                                 | Grok 4.5 / Cursor             | 82 | YES     | YES | 9.1   | 9.01  |
| 7                                                                                 | GPT-5.6 Luna / Codex          | 81 | YES     | NO  | 17.65 | 4.59  |
| 8                                                                                 | Claude Sonnet 5 / Claude Code | 72 | PARTIAL | NO  | 55.0  | 1.31  |
| 9                                                                                 | Composer 2.5 / Cursor         | 69 | PARTIAL | NO  | 6.0   | 11.50 |
| 10                                                                                | DeepSeek V4 Pro / Open Code   | 63 | PARTIAL | NO  | 53.0  | 1.19  |
| 11                                                                                | Qwen3.7 Plus / Open Code      | 47 | PARTIAL | NO  | 54.0  | 0.87  |
| 12                                                                                | MiMo-V2.5 / Open Code         | 42 | PARTIAL | NO  | 110.0 | 0.38  |
| 13                                                                                | MiniMax M3 / Open Code        | 36 | NO      | NO  | 65.0  | 0.55  |
| 14                                                                                | MiMo-V2.5-Pro / Open Code     | 27 | NO      | NO  | 12.0  | 2.25  |

同点2件（Sol / Terra）はA軸（root cause closure）を第1 tiebreakとした。

---

## 5. Axis Scores

| Model + Harness A /30 B /20 C /15 D /15 E /10 F /10 Total  |    |    |    |    |   |   |    |
| ---------------------------------------------------------- | -- | -- | -- | -- | - | - | -- |
| Claude Opus 5 / Claude Code                                | 28 | 19 | 14 | 15 | 6 | 9 | 91 |
| GPT-5.6 Sol / Codex                                        | 29 | 18 | 14 | 12 | 8 | 9 | 90 |
| GPT-5.6 Terra / Codex                                      | 28 | 18 | 14 | 14 | 7 | 9 | 90 |
| DeepSeek V4 Flash / Open Code                              | 27 | 16 | 14 | 13 | 6 | 9 | 85 |
| GPT-5.6 Luna / Open Code                                   | 26 | 16 | 13 | 13 | 8 | 7 | 83 |
| Grok 4.5 / Cursor                                          | 24 | 16 | 13 | 12 | 8 | 9 | 82 |
| GPT-5.6 Luna / Codex                                       | 27 | 12 | 12 | 14 | 8 | 8 | 81 |
