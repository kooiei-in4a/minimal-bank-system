# FND-03 Final Code Major Fix — Independent Evaluation

## 1. Evaluator Identity

実際の実行モデルを記録します。

```
EVALUATOR_MODEL: "GPT-5.6 Pro"
EVALUATOR_HARNESS: "Browser"
EVALUATOR_EFFORT: "Pro"
EVALUATOR_SLUG: "gpt-5.6-pro-browser"
ATTEMPT: 1

```

---

## 2. Reference Review

### Confirmed root cause

Testcontainers .NET 4.13.0では、`Resource.Disposed`の参照自体が`Interlocked.CompareExchange(ref _disposed, 1, 0)`によってdisposed状態をラッチします。

`DockerContainer.DisposeAsyncCore()`は、この`Disposed`を確認した後でDocker resourceの削除を実行します。そのため、

1. 最初の`DisposeAsync()`でdisposed状態がラッチされる
2. Docker container削除が失敗する
3. 実containerは残る
4. 同じinstanceへの2回目の`DisposeAsync()`は冒頭でreturnする
5. 呼出し側が「例外なし」をcleanup成功と誤認する

という状態が成立します。

Common Baseのfixtureは、最初の削除失敗後も同じ`PostgreSqlContainer`を保持し、次回`DisposeAsync()`が正常returnした場合に`container = null`としていました。そのため、poisoned instanceのno-opによって実containerが残ったままownerを失う可能性がありました。

### Reference Requirements

| IDPASS条件 |                                                                                                |
| -------- | ---------------------------------------------------------------------------------------------- |
| R-01     | Testcontainers disposal、independent cleanup、startup cleanupの失敗を正常終了へ変換しない                      |
| R-02     | 一度失敗したTestcontainers instanceをresource removal retryに使用しない                                     |
| R-03     | Testcontainers instanceとは独立したcontainer ID、name、label等のresource identityを保持する                   |
| R-04     | Docker daemonがresource不在を確認した場合に限りownerを解放する。失敗時は独立pathで再試行可能である                               |
| R-05     | startup primary failureとcleanup failureを両方保持し、partial resourceのownerを失わない                      |
| R-06     | `template0`、`Pooling=false`、database isolation、DROP retry等の既存database lifecycleを退行させない         |
| R-07     | PostgreSQL 18.4 digest、Testcontainers 4.13.0、Npgsql 10.0.3、real PostgreSQL CI、no fallbackを維持する |

Issue #41はreal PostgreSQL fixture、database isolation、cleanup failure visibility、CI実行を要求し、application DbContext、migration、business schemaを明示的に対象外としています。

### Required Test Evidence

| ID必須証拠 |                                                                   |
| ------ | ----------------------------------------------------------------- |
| T-01   | disposed latch後のcontainer removal failureを決定論的に発生させる              |
| T-02   | 最初のcleanup failureが呼出し側へ見える                                       |
| T-03   | poisoned same-instanceのno-opをcleanup成功と扱わない                       |
| T-04   | wrapper fieldだけでなくdaemon-sideのcontainer残存・消滅を確認する                 |
| T-05   | startup primary failure＋cleanup failure＋owner retention＋最終回収を確認する |
| T-06   | exact Headのbuild、既存test、real PostgreSQL CIが成功する                   |

### Reference Lock

```
REFERENCE LOCKED

```

この基準は14 candidateのdiffを読む前に固定しました。

### Post-lock discoveries

以下はcandidate固有bugとして評価へ反映しています。Reference自体は変更していません。

- Terra: `docker inspect`の任意の非0終了をcontainer不在と誤認し得る
- Sonnet: CLI障害をcontainer不在と誤認し、startup時にもownerを失い得る
- Composer / DeepSeek V4 Pro / MiMo: fallback成功時に元のcleanup failureを隠す
- Qwen / MiMo: double failure後に同一poisoned instanceを再使用する
- MiniMax: independent final cleanupがfixture lifecycleへ統合されていない

---

## 3. Collection Integrity

```
Candidate count: 14 / 14
Exact common base: PASS
  91e3fca181558cd1523390347f4f2f80d6014d26

Candidate Head fixed: PASS 14 / 14
Draft PR: PASS 14 / 14
Exact Head CI: SUCCESS 14 / 14

Identity mismatch: 0
Execution-metadata discrepancy:
  1
  Qwen3.7 Plus:
    registry effort: MAX
    PR reported effort: default

```

`run.json`に固定された14件のPR、Head、duration、CI runを照合し、各Headに紐づく`Build and Test`がすべて`completed / success`であることを再確認しました。Qwenの差異はeffort表示だけで、candidate identity、Head、PR、CIには不一致がありません。

---

## 4. Executive Ranking

| RankModel + HarnessScore /100Major FixedMerge CandidateDurationQuality/min |                               |        |         |         |           |           |
| -------------------------------------------------------------------------- | ----------------------------- | ------ | ------- | ------- | --------- | --------- |
| 1                                                                          | GPT-5.6 Sol / Codex           | **95** | YES     | **YES** | 28.68 min | 3.31      |
| 2                                                                          | Claude Opus 5 / Claude Code   | **93** | YES     | **YES** | 28 min    | 3.32      |
| 3                                                                          | GPT-5.6 Luna / Open Code      | **92** | YES     | **YES** | 17 min    | 5.41      |
| 4                                                                          | Grok 4.5 / Cursor             | **91** | YES     | **YES** | 9.1 min   | **10.00** |
| 5                                                                          | GPT-5.6 Luna / Codex          | **89** | YES     | **YES** | 17.65 min | 5.04      |
| 6                                                                          | DeepSeek V4 Flash / Open Code | **85** | YES     | **YES** | 75 min    | 1.13      |
| 7                                                                          | GPT-5.6 Terra / Codex         | **84** | PARTIAL | NO      | 21 min    | 4.00      |
| 8                                                                          | Composer 2.5 / Cursor         | **82** | PARTIAL | NO      | 6 min     | **13.67** |
| 9                                                                          | Claude Sonnet 5 / Claude Code | **67** | PARTIAL | NO      | 55 min    | 1.22      |
| 10                                                                         | DeepSeek V4 Pro / Open Code   | **66** | PARTIAL | NO      | 53 min    | 1.25      |
| 11                                                                         | MiniMax M3 / Open Code        | **59** | PARTIAL | NO      | 65 min    | 0.91      |
| 12                                                                         | Qwen3.7 Plus / Open Code      | **49** | NO      | NO      | 54 min    | 0.91      |
| 13                                                                         | MiMo-V2.5 / Open Code         | **44** | NO      | NO      | 110 min   | 0.40      |
| 14                                                                         | MiMo-V2.5-Pro / Open Code     | **40** | NO      | NO      | 12 min    | 3.33      |

Composerはraw `QUALITY_PER_MINUTE`では最高ですが、cleanup failure swallowingのMajorがあるため、品質・速度部門の採用対象にはできません。

---

## 5. Axis Scores

| Model + HarnessA /30B /20C /15D /15E /10F /10Total |    |    |    |    |   |    |        |
| -------------------------------------------------- | -- | -- | -- | -- | - | -- | ------ |
| GPT-5.6 Sol / Codex                                | 30 | 17 | 15 | 14 | 9 | 10 | **95** |
| GPT-5.6 Terra / Codex                              | 21 | 17 | 13 | 15 | 8 | 10 | **84** |
| GPT-5.6 Luna / Codex                               | 28 | 15 | 13 | 15 | 8 | 10 | **89** |
| Claude Opus 5 / Claude Code                        | 29 | 20 | 13 | 15 | 6 | 10 | **93** |
| Claude Sonnet 5 / Claude Code                      | 16 | 13 | 7  | 14 | 8 | 9  | **67** |
| Grok 4.5 / Cursor                                  | 28 | 16 | 13 | 15 | 9 | 10 | **91** |
| Composer 2.5 / Cursor                              | 22 | 15 | 12 | 15 | 8 | 10 | **82** |
| DeepSeek V4 Pro / Open Code                        | 18 | 11 | 8  | 13 | 8 | 8  | **66** |
| DeepSeek V4 Flash / Open Code                      | 28 | 18 | 12 | 14 | 4 | 9  | **85** |
| Qwen3.7 Plus / Open Code                           | 8  | 5  | 6  | 14 | 8 | 8  | **49** |
| GPT-5.6 Luna / Open Code                           | 29 | 16 | 13 | 15 | 9 | 10 | **92** |
| MiMo-V2.5-Pro / Open Code                          | 4  | 3  | 4  | 14 | 7 | 8  | **40** |
| MiMo-V2.5 / Open Code                              | 9  | 4  | 6  | 12 | 6 | 7  | **44** |
| MiniMax M3 / Open Code                             | 15 | 10 | 6  | 14 | 6 | 8  | **59** |

---

## 6. Candidate-by-Candidate Evaluation

### GPT-5.6 Sol / Codex

```
PR: 108
Head: d3af857f71a62124842f96de9bced2b748b776be
Duration: 28.68 min
CI: 31290367847 / completed / success

Major Fixed: YES
