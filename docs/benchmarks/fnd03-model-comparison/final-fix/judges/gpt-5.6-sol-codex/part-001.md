# FND-03 Final Code Major Fix — Independent Evaluation

## 1. Evaluator Identity

```
EVALUATOR_MODEL: "GPT-5.6 Sol"
EVALUATOR_HARNESS: "Codex"
EVALUATOR_EFFORT: "xHigh"
EVALUATOR_SLUG: "gpt-5.6-sol-codex"
ATTEMPT: 1
```

## 2. Reference Review

確認対象は[Parent Issue #3](https://github.com/kooiei-in4a/minimal-bank-system/issues/3)、[Issue #41](https://github.com/kooiei-in4a/minimal-bank-system/issues/41)、[Work Package #33](https://github.com/kooiei-in4a/minimal-bank-system/issues/33)、Accepted ADR-0001/0003/0004/0005/0009、common baseの[fixture](https://github.com/kooiei-in4a/minimal-bank-system/blob/91e3fca181558cd1523390347f4f2f80d6014d26/tests/MinimalBankSystem.IntegrationTests/PostgreSql/PostgreSqlContainerFixture.cs)およびtestである。

### Confirmed root cause

Testcontainers 4.13.0の`Resource.Disposed`は状態の単純な読み取りではなく、最初の評価で`_disposed`を`0→1`へ変更するtest-and-setである。[Resource.cs](https://github.com/testcontainers/testcontainers-dotnet/blob/4.13.0/src/Testcontainers/Resource.cs)

`DockerContainer.DisposeAsyncCore()`はこの状態を先にラッチし、その後にDocker resourceの削除を行う。[DockerContainer.cs](https://github.com/testcontainers/testcontainers-dotnet/blob/4.13.0/src/Testcontainers/Containers/DockerContainer.cs)

したがって、初回削除が失敗すると、

```
managed instance: disposed
actual Docker container: exists
```

になり得る。同じinstanceへの2回目の`DisposeAsync()`はDockerへ到達せず正常returnするため、そのreturnを成功としてownerを解放するとcontainerを孤児化する。

### Reference Requirements

- R-01: native/independent cleanup失敗を正常終了へ変換しない。fallback成功後も発生したnative failureを可視化する。
- R-02: failure後の同一Testcontainers instanceをcleanup retryに使用しない。
- R-03: Testcontainers instanceと独立したresource identity/ownerを保持する。
- R-04: startup途中を含む全pathにfinal cleanupがあり、daemonで不存在を確認するまでownerを解放しない。
- R-05: startup primary failureとcleanup failureの両方を保持する。
- R-06: `template0`、database isolation、`Pooling=false`、database cleanup retryを回帰させない。
- R-07: PostgreSQL 18.4、digest、Testcontainers 4.13.0、Npgsql 10.0.3、CI、parallel policy等のFND-03契約を維持する。

Required Test Evidence:

- T-01: container removal failureへ到達する決定的なfailure injection。
- T-02: 最初のcleanup failureがcallerへ伝播する。
- T-03: poisoned instanceのno-opを成功扱いしない。
- T-04: wrapper fieldではなくdaemon-sideの残存と最終削除を確認する。
- T-05: startup primary failure、partial cleanup failure、owner retention、最終cleanup。
- T-06: regression testとexact-head CI。

### Reference Lock

```
REFERENCE LOCKED
```

上記はcandidate diffを読む前に固定した。

### POST-LOCK DISCOVERY

Testcontainersのcreate処理は、DockerからIDを受領した後にcontainer inspect結果を内部fieldへ格納する。この間に失敗すると、実containerは存在するが`candidate.Id`を取得できず、最初の`DisposeAsync()`も内部`Exists()`判定でno-op成功する可能性がある。

したがって、`StartAsync`成功後にだけIDを保存し、native disposeの正常returnだけでownerを解放する実装にはstartup partial-createのowner-loss pathがある。これは新しいReferenceの追加ではなく、固定済みR-03/R-04/R-05およびHF-02の適用である。

---

## 3. Collection Integrity

```
Candidate count:       14 / 14
Exact common base:     14 / 14 = 91e3fca181558cd1523390347f4f2f80d6014d26
Candidate Head fixed:  14 / 14
Draft PR:              14 / 14
Exact Head CI:         14 / 14 completed / success
Real PostgreSQL step:  14 / 14 success
Identity mismatch:     Model/Harness/Headは0件。Qwen3.7 Plusのみeffort registry=MAX / PR=default
Scope violation:       MiMo-V2.5のtests/.editorconfig追加以外、FND-04先取り・package変更なし
```

Registryは[run.json](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd03-major-fix-artifacts/docs/benchmarks/fnd03-model-comparison/final-fix/run.json)を使用した。作業ツリーは評価前後ともcleanであり、変更は行っていない。

---

## 4. Executive Ranking

| RankModel + HarnessScore /100Major FixedMerge CandidateDurationQuality/min |                               |    |         |     |        |      |
| -------------------------------------------------------------------------- | ----------------------------- | -- | ------- | --- | ------ | ---- |
| 1                                                                          | GPT-5.6 Sol / Codex           | 94 | YES     | YES | 28.68m | 3.28 |
| 2                                                                          | GPT-5.6 Luna / Codex          | 80 | PARTIAL | NO  | 17.65m | 4.53 |
| 3                                                                          | GPT-5.6 Terra / Codex         | 78 | PARTIAL | NO  | 21m    | 3.71 |
| 4                                                                          | Claude Opus 5 / Claude Code   | 78 | PARTIAL | NO  | 28m    | 2.79 |
| 5                                                                          | DeepSeek V4 Flash / Open Code | 74 | PARTIAL | NO  | 75m    | 0.99 |
| 6                                                                          | GPT-5.6 Luna / Open Code      | 74 | PARTIAL | NO  | 17m    | 4.35 |
| 7                                                                          | Grok 4.5 / Cursor             | 71 | PARTIAL | NO  | 9.1m   | 7.80 |
| 8                                                                          | Claude Sonnet 5 / Claude Code | 70 | PARTIAL | NO  | 55m    | 1.27 |
| 9                                                                          | DeepSeek V4 Pro / Open Code   | 62 | PARTIAL | NO  | 53m    | 1.17 |
| 10                                                                         | Composer 2.5 / Cursor         | 57 | NO      | NO  | 6m     | 9.50 |
| 11                                                                         | MiniMax M3 / Open Code        | 48 | NO      | NO  | 65m    | 0.74 |
| 12                                                                         | MiMo-V2.5-Pro / Open Code     | 42 | NO      | NO  | 12m    | 3.50 |
| 13                                                                         | Qwen3.7 Plus / Open Code      | 40 | NO      | NO  | 54m    | 0.74 |
| 14                                                                         | MiMo-V2.5 / Open Code         | 33 | NO      | NO  | 110m   | 0.30 |

同点はroot-cause coverage、実証力、複雑性の順で順位を決定した。

---

## 5. Axis Scores

| Model + HarnessA /30B /20C /15D /15E /10F /10Total |    |    |    |    |   |    |    |
| -------------------------------------------------- | -- | -- | -- | -- | - | -- | -- |
| GPT-5.6 Sol / Codex                                | 30 | 18 | 14 | 14 | 8 | 10 | 94 |
| GPT-5.6 Terra / Codex                              | 20 | 18 | 9  | 14 | 7 | 10 | 78 |
| GPT-5.6 Luna / Codex                               | 21 | 16 | 10 | 14 | 9 | 10 | 80 |
| Claude Opus 5 / Claude Code                        | 21 | 20 | 9  | 14 | 5 | 9  | 78 |
| Claude Sonnet 5 / Claude Code                      | 17 | 15 | 7  | 14 | 8 | 9  | 70 |
| Grok 4.5 / Cursor                                  | 18 | 13 | 8  | 14 | 8 | 10 | 71 |
| Composer 2.5 / Cursor                              | 8  | 13 | 5  | 14 | 7 | 10 | 57 |
| DeepSeek V4 Pro / Open Code                        | 15 | 9  | 7  | 14 | 8 | 9  | 62 |
| DeepSeek V4 Flash / Open Code                      | 20 | 18 | 9  | 14 | 4 | 9  | 74 |
| Qwen3.7 Plus / Open Code                           | 3  | 3  | 4  | 14 | 7 | 9  | 40 |
| GPT-5.6 Luna / Open Code                           | 18 | 15 | 8  | 14 | 9 | 10 | 74 |
| MiMo-V2.5-Pro / Open Code                          | 4  | 3  | 4  | 14 | 8 | 9  | 42 |
| MiMo-V2.5 / Open Code                              | 3  | 2  | 3  | 13 | 5 | 7  | 33 |
| MiniMax M3 / Open Code                             | 9  | 7  | 5  | 14 | 4 | 9  | 48 |

---

## 6. Candidate-by-Candidate Evaluation

### GPT-5.6 Sol / Codex

```
PR: #108
Head: d3af857f71a62124842f96de9bced2b748b776be
Duration: 28.68m
CI: 31290367847 / SUCCESS

Major Fixed: YES
Merge Candidate: YES

Score: 94
A: 30
B: 18
C: 14
D: 14
E: 8
F: 10
```

#### Implementation

一意なownership labelをcontainer create前に設定し、Testcontainers instanceとは別のDocker API ownerを保持する。native disposeは最大1回で、成否にかかわらずlabel検索・force remove・再検索を実行する。daemon確認が失敗した場合だけownerを保持するため、partial createやnative no-opにも耐える。

#### Test proof

native disposer failureと独立cleanup failureを分離注入し、実際のrunning containerに`force=false` DELETEを送り409を発生させる。失敗後のdaemon残存、owner保持、native disposer call count不変、retry後のdaemon不存在、startup primary+cleanup failureを確認している。[PR #108](https://github.com/kooiei-in4a/minimal-bank-system/pull/108)

#### Findings

```
