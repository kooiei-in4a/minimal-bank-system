# FND-03 Final Code Major Fix — 3 Judge Adjudicated Synthesis

## 1. Executive Summary

3つの独立Judge結果を単純平均せず、Testcontainers 4.13.0一次source、14 candidateのexact Head実コード・test、exact Head CIを使ってFindingを裁定し、6軸100点を再採点した。

| Judge | Artifact identity |
| --- | --- |
| GPT-5.6 Sol / Codex / xHigh | `gpt-5.6-sol-codex/` |
| Claude Opus 5 / Claude Code / xhigh | `claude-opus-5-claude-code/` |
| GPT-5.6 Pro / Browser / Pro | ユーザー上の呼称はBrowser Solだが、self-reported identityを維持。`gpt-5.6-pro-browser/` |

```text
Top candidate:
  GPT-5.6 Sol / Codex — PR #108 — 94 / 100

Merge-ready:
  1 / 14

Final Synthesis:
  Recommendation B
  PR #108をproduction implementation baseとする。
  Terra #113のactual Testcontainers latch/no-op testをtest-onlyで統合する。
  Baseのunreachable-Docker testを復活させる。
  Opus #109のtransport-fault proofは任意のadversarial補強とする。
```

最重要裁定は、Docker create成功後・Testcontainersのinspect結果格納前にstartupが失敗するpartial-create pathである。Testcontainers 4.13.0の`UnsafeCreateAsync`は、DockerからIDを得た後に`ByIdAsync(id)`で`_container`を設定する。この間に失敗すると、actual containerが存在しても`candidate.Id`は取得不能、`Exists()`はfalse、最初の`DisposeAsync()`もno-op成功し得る。

これは新しい要件ではなく、3 Judgeが共通固定したR-03 deterministic ownership、R-04 final cleanup、R-05 startup partial cleanup、HF-02 owner loss禁止の適用である。

create前にunique ownership labelを確立し、native Disposeの結果に関係なくlabel query/remove/re-queryを行うPR #108だけが、このpathを構造的に閉じる。

## 2. Evidence and Method

### Primary evidence

- Candidate registry: [`../run.json`](../run.json)
- Judge artifacts:
  - [`gpt-5.6-sol-codex/`](gpt-5.6-sol-codex/)
  - [`claude-opus-5-claude-code/`](claude-opus-5-claude-code/)
  - [`gpt-5.6-pro-browser/`](gpt-5.6-pro-browser/)
- Testcontainers 4.13.0:
  - [`Resource.cs`](https://github.com/testcontainers/testcontainers-dotnet/blob/4.13.0/src/Testcontainers/Resource.cs)
  - [`DockerContainer.cs`](https://github.com/testcontainers/testcontainers-dotnet/blob/4.13.0/src/Testcontainers/Containers/DockerContainer.cs)

### Score rule

- raw平均・中央値は不一致の可視化にだけ使用
- Finding裁定後にA〜Fを再採点
- Majorが残る場合はTotalにかかわらず`MERGE_CANDIDATE: NO`
- CIは14 / 14 SUCCESSであり、failure-path品質の識別力は持たない

```text
A. Major Root Cause Closure             /30
B. Failure Injection / Test Proof       /20
C. Ownership / Startup Lifecycle        /15
D. Existing FND-03 Regression Safety    /15
E. Minimality / Maintainability         /10
F. Scope / Evidence / Execution Quality /10
```

## 3. Score Reconciliation

| Rank | Model + Harness | Codex Sol | Claude Opus | Browser Pro | Median | Range | Final | Major Fixed | Merge | Min | Q/min |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | :---: | :---: | ---: | ---: |
| 1 | GPT-5.6 Sol / Codex | 94 | 90 | 95 | 94 | 5 | **94** | YES | YES | 28.68 | 3.28 |
| 2 | Claude Opus 5 / Claude Code | 78 | 91 | 93 | 91 | 15 | **80** | PARTIAL | NO | 28 | 2.86 |
| 3 | GPT-5.6 Luna / Codex | 80 | 81 | 89 | 81 | 9 | **77** | PARTIAL | NO | 17.65 | 4.36 |
| 4 | GPT-5.6 Terra / Codex | 78 | 90 | 84 | 84 | 12 | **77** | PARTIAL | NO | 21 | 3.67 |
| 5 | GPT-5.6 Luna / Open Code | 74 | 83 | 92 | 83 | 18 | **76** | PARTIAL | NO | 17 | 4.47 |
| 6 | DeepSeek V4 Flash / Open Code | 74 | 85 | 85 | 85 | 11 | **74** | PARTIAL | NO | 75 | 0.99 |
| 7 | Grok 4.5 / Cursor | 71 | 82 | 91 | 82 | 20 | **73** | PARTIAL | NO | 9.1 | 8.02 |
| 8 | Claude Sonnet 5 / Claude Code | 70 | 72 | 67 | 70 | 5 | **67** | PARTIAL | NO | 55 | 1.22 |
| 9 | DeepSeek V4 Pro / Open Code | 62 | 63 | 66 | 63 | 4 | **62** | PARTIAL | NO | 53 | 1.17 |
| 10 | Composer 2.5 / Cursor | 57 | 69 | 82 | 69 | 25 | **58** | PARTIAL | NO | 6 | 9.67 |
| 11 | MiniMax M3 / Open Code | 48 | 36 | 59 | 48 | 23 | **48** | NO | NO | 65 | 0.74 |
| 12 | Qwen3.7 Plus / Open Code | 40 | 47 | 49 | 47 | 9 | **42** | NO | NO | 54 | 0.78 |
| 13 | MiMo-V2.5-Pro / Open Code | 42 | 27 | 40 | 40 | 15 | **38** | NO | NO | 12 | 3.17 |
| 14 | MiMo-V2.5 / Open Code | 33 | 42 | 44 | 42 | 11 | **34** | NO | NO | 110 | 0.31 |

## 4. Final Axis Scores

| Model + Harness | A | B | C | D | E | F | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| GPT-5.6 Sol / Codex | 30 | 18 | 15 | 13 | 8 | 10 | **94** |
| Claude Opus 5 / Claude Code | 21 | 20 | 9 | 15 | 6 | 9 | **80** |
| GPT-5.6 Luna / Codex | 21 | 13 | 10 | 14 | 9 | 10 | **77** |
| GPT-5.6 Terra / Codex | 19 | 18 | 9 | 14 | 7 | 10 | **77** |
| GPT-5.6 Luna / Open Code | 19 | 16 | 8 | 14 | 9 | 10 | **76** |
| DeepSeek V4 Flash / Open Code | 20 | 18 | 9 | 14 | 4 | 9 | **74** |
| Grok 4.5 / Cursor | 18 | 15 | 8 | 14 | 8 | 10 | **73** |
| Claude Sonnet 5 / Claude Code | 16 | 13 | 7 | 14 | 8 | 9 | **67** |
| DeepSeek V4 Pro / Open Code | 15 | 9 | 7 | 14 | 8 | 9 | **62** |
| Composer 2.5 / Cursor | 9 | 14 | 5 | 13 | 7 | 10 | **58** |
| MiniMax M3 / Open Code | 9 | 7 | 5 | 14 | 4 | 9 | **48** |
| Qwen3.7 Plus / Open Code | 4 | 4 | 4 | 14 | 7 | 9 | **42** |
| MiMo-V2.5-Pro / Open Code | 3 | 3 | 3 | 14 | 7 | 8 | **38** |
| MiMo-V2.5 / Open Code | 4 | 3 | 4 | 11 | 5 | 7 | **34** |

## 5. Finding Adjudication

| ID | Finding | 裁定 | Severity / effect |
| --- | --- | --- | --- |
| D-01 | Testcontainersはremove前にdisposed stateをlatchし、failed instanceの2回目Disposeはno-op | ACCEPTED | Confirmed Major root cause |
| D-02 | Docker create後・inspect格納前のfailureではactual containerが存在してもID取得不能、native Disposeもno-opになり得る | ACCEPTED | Major。R-03/R-04/R-05の適用 |
| D-03 | `docker inspect != 0`はcontainer不在と同義ではない | ACCEPTED | false absenceでowner解放する場合Major |
| D-04 | fallback成功後もnative cleanup failureを正常終了へ変換しない | ACCEPTED | R-01/HF-03。違反はMajor |
| D-05 | native + fallback double failure後にsame poisoned instanceを再Dispose | ACCEPTED | Original Major未修正 |
| D-06 | manual cleanup helperがfixture lifecycleから到達不能 | ACCEPTED | final cleanup不成立、Major |
| D-07 | pre-remove、pre-cancel、`_disposed`直接書換、`_client=null`だけではT-01の単独証拠にならない | ACCEPTED | Test proof減点 |
| D-08 | Docker API remove成功後の追加inspectがない | 原則Minor | generic errorをabsence化する場合のみMajor |
| D-09 | Solがunreachable-Docker testを削除 | ACCEPTED | Minor。Final Synthesisで復活 |

### D-02がScoreを反転させたcandidate

- Terra: ID取得前partial-create + native-success release
- Luna Codex: stable nameはあるがnative-success時にindependent name cleanupを実行しない
- Opus: `TryReadContainerId`失敗後、native no-op成功を信頼してownerをclear
- Sonnet: ID捕捉がStartAsync成功後のみ
- Grok: IDがなければauthoritative reclaimerを実行できずmanaged disposeをbest-effort化
- Composer: IDがなければReclaimが即return
- DeepSeek Flash: ID不明時にdirect API cleanup不能
- Luna Open Code: ID不明時にnative no-op成功でownership clear

Solはcreate前labelによりID取得へ依存しない。

### CLI/API absence裁定

- Terra: inspect non-zero後、Docker serverが動けば不存在扱い → Major
- Sonnet: rm結果を無視し、inspect non-zeroなら除去済み扱い → Major
- Composer: inspect exit codeだけをbool化 → Major risk
- Qwen test: `docker ps`は停止containerを見落とす → evidence不適切

### Failure swallowing裁定

Majorとして認定:

- Composer
- DeepSeek V4 Pro
- Qwen
- MiMo-V2.5
- Grok（resource safetyは確保するが、固定R-01上はnative failureを捨てる）

## 6. Candidate Final Judgments

| Rank | Candidate | Final judgment |
| ---: | --- | --- |
| 1 | **GPT-5.6 Sol / Codex — 94** | **唯一merge-ready。** create前label、one-shot native dispose、unconditional list/remove/re-list、cleanup gate。残るのはtest proof・既存coverage・dependency明示のMinor |
| 2 | Claude Opus 5 / Claude Code — 80 | Best failure proof。ただしID取得不能partial-createでnative no-op成功を信頼しownerを失い得る。production clientも重い |
| 3 | GPT-5.6 Luna / Codex — 77 | stable name + IDは優秀。ただしnative Dispose正常returnで即finalizeし、partial-create containerをnameで確認しない |
| 4 | GPT-5.6 Terra / Codex — 77 | actual latch/no-op testは最良。ID-only partial-createとCLI false-absenceでmerge不可 |
| 5 | GPT-5.6 Luna / Open Code — 76 | Best minimal attempt。post-start pathは良いがID不明partial-createとnative-success releaseが残る |
| 6 | DeepSeek V4 Flash / Open Code — 74 | strong fake-daemon proof、endpoint整合。ただしID不明partial-createを閉じない |
| 7 | Grok 4.5 / Cursor — 73 | reclaim-firstとactual daemon proofは強い。ID不明startup gapとnative failure swallowingが残る |
| 8 | Claude Sonnet 5 / Claude Code — 67 | post-start state machineは良い。startup ID owner lossとCLI false-absenceがMajor |
| 9 | DeepSeek V4 Pro / Open Code — 62 | independent removeはあるが`catch { }`でnative failureを消し、T-01も弱い |
| 10 | Composer 2.5 / Cursor — 58 | 6分で骨格を作成。ただしfailure swallowing、ID-null no-cleanup、false-absence |
| 11 | MiniMax M3 / Open Code — 48 | failure stateは保持するがfinal cleanupがfixture lifecycleへ接続されず、startupでownerをclear |
| 12 | Qwen3.7 Plus / Open Code — 42 | double failure後にsame poisoned instance retry。fallback successもnative failureを隠す |
| 13 | MiMo-V2.5-Pro / Open Code — 38 | Dispose lifecycleは未変更。manual ForceCleanupとplaceholder testのみ |
| 14 | MiMo-V2.5 / Open Code — 34 | original retry bug、failure swallowing、invalid pre-remove test、`tests/.editorconfig root=true`副作用 |

## 7. Judge Contribution

### GPT-5.6 Sol / Codex Judge

startup partial-createのowner-lossを発見した点が決定的だった。一次sourceで成立する。最終merge gateはこのJudgeが最も正確。

### Claude Opus 5 / Claude Code Judge

failure injection、test fidelity、maintainability、endpoint alignmentの分析が最も深い。Opus/Terra/DeepSeek Flashのtest資産評価をFinal Synthesisへ採用した。一方、partial-createを共通裁定へ反映せずmerge-readyを6件まで広げた。

### GPT-5.6 Pro / Browser Judge

Terra/Sonnetのfalse-absence、Composer/DeepSeek/MiMoのfailure swallowing、Qwen/MiMoのdouble-failure retryを正確に特定した。一方、複数ID-based candidateのpartial-create gapを見逃した。

どれか1 Judgeをそのまま最終結果にはしていない。

## 8. Best-of Categories

```text
BEST IMPLEMENTATION:
  GPT-5.6 Sol / Codex #108

BEST TEST / FAILURE PROOF:
  Claude Opus 5 / Claude Code #109

BEST ROOT-CAUSE LIBRARY PROBE:
  GPT-5.6 Terra / Codex #113

BEST MINIMAL DESIGN ATTEMPT:
  GPT-5.6 Luna / Open Code #115
  ※ partial-create Majorが残るためmerge-readyではない

BEST QUALITY / SPEED:
  GPT-5.6 Sol / Codex #108
  ※ Grokは最強の高速attemptだがR-01/R-05未達
```

## 9. Final Synthesis Recommendation

```text
Recommendation:
  B
```

### Production implementation base

GPT-5.6 Sol / Codex PR #108を採用する。

- create前unique ownership label
- independent label owner
- `containerDisposeAttempted`
- cleanup semaphore
- native結果に関係なくlabel list/remove/re-list
- independent verification成功時だけowner release
- native / independent / startup failure aggregation
- database lifecycle維持

### Required test integration

1. Terra #113の`FailingDeleteContainer : DockerContainer`
   - `UnsafeDeleteAsync`だけを失敗
   - first Dispose throw
   - second same-instance Dispose no-op
   - actual daemon container remains
   - independent ownerでfinal remove
2. Base `91e3fca`のunreachable Docker endpoint testを復活
3. Solのactual Docker 409、owner retention、startup post-start double failure testを維持
4. 必要ならGrokの`Resource.Disposed` false→true regression guardを小さく採用

### Optional adversarial proof

Opus #109のtransport fault proxyは高価値だが、標準suiteへ約400行のraw transport/API実装を持ち込むことは必須ではない。採用する場合もtest-onlyに限定し、productionのhand-written Docker clientは却下する。

### Endpoint / dependency

- independent Docker clientはTestcontainersが解決した同一endpointを使用
- Docker.DotNetを直接使うなら既存versionをCPM/PackageReferenceへ明示
- package version自体は変更しない

### Reject

- StartAsync成功後にだけIDを保存
- native successだけでowner release
- same poisoned instance retry
- generic CLI non-zeroをabsence扱い
- fallback successでnative failureを消す
- manual cleanup helperをlifecycleへ接続しない
- docker CLI文字列/locale依存をproduction正本化
- full fake daemonを標準suite必須化
- raw Docker HTTP clientをproductionへ採用
- pre-remove/pre-cancel/private field破壊だけをT-01とする
- `tests/.editorconfig root=true`

## 10. Final Implementation Shape

```text
Initialize:
  ownershipIdを生成
  independent owner(label key + ownershipId)を先に作成
  同じlabelをcontainer create requestへ付与
  start / PostgreSQL version verification

Cleanup:
  gate取得
  native Disposeを生涯1回だけ試行
  native success/failureに関係なくlabelでAll containersを列挙
  force remove
  同じlabelで再列挙
  0件確認後だけownerを解放

Exception:
  native fail + independent fail -> Aggregate、owner保持
  native fail + independent success -> native failure可視、owner解放
  native success + independent fail -> independent failure、owner保持
  both success -> success
```

startup catchも同じCleanupを呼ぶ。label ownerはcontainer ID取得に依存しない。

## 11. Limitations

- 3 Judgeはいずれも同一candidateを評価した各1 execution
- Browser artifactのself-reported identityはGPT-5.6 Pro / Browser
- Final Scoreはadjudicated benchmark scoreでありモデル一般性能ではない
- partial-createは一次source上成立するが、全candidateへactual daemon injectionを行ったわけではない
- この文書は評価Synthesisであり、Final implementationは未実施

## 12. Final Conclusion

```text
Final Rank 1:
  GPT-5.6 Sol / Codex — 94 / 100

Merge-ready:
  1 / 14

Recommended:
  PR #108 production implementation
  + Terra #113 actual latch/no-op test
  + Base unreachable-Docker regression test
  + optional limited Opus transport-fault test

Next:
  agent/issue-41-fnd-03-final-codeへFinal Synthesis implementationを作成し、
  exact Head CIとAgent B独立レビューでBlocker / Major 0を確認する。
```
