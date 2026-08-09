
confirmed Majorは未修正。

---

### MiMo-V2.5 / Open Code

```
PR: #120
Head: 8a37daa3d85016348910904dff7ac29c2811200e
Duration: 110m
CI: 31294256088 / SUCCESS

Major Fixed: NO
Merge Candidate: NO

Score: 33
A: 3
B: 2
C: 3
D: 13
E: 5
F: 7
```

#### Implementation

ID保存とDocker CLI fallbackを追加するが、retry stateを持たない。

#### Test proof

containerを先にforce-removeするtestと正常dispose testが中心で、root failureを注入していない。

#### Findings

```
Blocker: なし
Major: primary+fallback failure後の次回Disposeがpoisoned instanceを再利用し、no-opでownerを解放する。startup failure後はcontainer=nullのため保持したIDも再cleanupされない。
Minor: fallback成功時に元のnative cleanup failureを正常終了へ変換する。事前force-removeはHF-05のinvalid injection。
Nit: tests/.editorconfigをrootとして追加し、Issueと無関係にtests配下のCA1707を全無効化している。
```

#### Judgment

HF-01/HF-02/HF-03が複数残り、最下位。

---

### MiniMax M3 / Open Code

```
PR: #119
Head: 352b6489d8d4723551eb2634fd9dd612433d2fa6
Duration: 65m
CI: 31293843630 / SUCCESS

Major Fixed: NO
Merge Candidate: NO

Score: 48
A: 9
B: 7
C: 5
D: 14
E: 4
F: 9
```

#### Implementation

native failure stateとcontainer IDを保存するが、後続`DisposeAsync()`は単にreturnする。独立force-removeはinternal manual helperで、production lifecycleへ統合されていない。

#### Test proof

private fieldをreflectionでnull化してDisposeを失敗させ、その後testがforce-removeと`ReleaseContainerForTest()`を手動実行する。

#### Findings

```
Blocker: なし
Major: failure後のfixture lifecycleにfinal cleanupがなく、通常の後続Disposeは何もせずcontainerを残す。startup catchもcleanup failure後にownerを無条件解放する。
Minor: _client/_containerをnull化する注入はDocker removal failureではなく、手動test-only releaseはproduction final-cleanup proofにならない。
Nit: unusedのtest-only finalized helperがある。
```

#### Judgment

failure visibilityは改善したが、R-04を満たさずMajorは未修正。

---

## 7. Architecture Comparison

| CandidateOwnershipIndependent cleanupFailure injectionActual daemon verificationStartup failureComplexity |                         |                          |                               |                      |                                       |                |
| --------------------------------------------------------------------------------------------------------- | ----------------------- | ------------------------ | ----------------------------- | -------------------- | ------------------------------------- | -------------- |
| GPT-5.6 Sol / Codex                                                                                       | unique label            | Docker API、常時確認          | disposer seam + 実409          | 実daemon list/inspect | 実container post-start、labelでpartial対応 | 4 files / +456 |
| GPT-5.6 Terra / Codex                                                                                     | container ID            | Docker CLI               | `UnsafeDeleteAsync` override  | 実daemon inspect      | fake startup、early-ID gap             | 4 / +528       |
| GPT-5.6 Luna / Codex                                                                                      | stable name + ID        | Docker API               | fake poisoned handle          | 成功pathは実daemon       | fake startup、native-success未検証        | 3 / +412       |
| Claude Opus 5                                                                                             | container ID            | raw Docker API           | 実transport切断proxy             | upstream実daemon      | 実post-start                           | 5 / +671       |
| Claude Sonnet 5                                                                                           | container ID            | Docker CLI               | delegate state machine        | fallbackは実daemon     | 専用testなし                              | 4 / +355       |
| Grok 4.5                                                                                                  | container ID            | Docker CLI authoritative | reflection + scripted failure | 実daemon inspect      | 実post-start、early-ID gap              | 4 / +368       |
| Composer 2.5                                                                                              | container ID            | Docker CLI               | scripted delegates            | 実daemonだがinspect誤判定  | 実post-start                           | 6 / +451       |
| DeepSeek V4 Pro                                                                                           | container ID            | Docker CLI               | reflection poison             | 実daemon inspect      | unreachableのみ                         | 2 / +259       |
| DeepSeek V4 Flash                                                                                         | container ID + endpoint | Docker API               | fake Docker daemon HTTP 500   | failureはstub         | stub上でpartial                         | 5 / +721       |
| Qwen3.7 Plus                                                                                              | container ID            | Docker CLI               | 事前正常削除                        | `docker ps`中心        | unreachableのみ                         | 3 / +402       |
| GPT-5.6 Luna / Open Code                                                                                  | container ID            | Docker API               | delegate fail-once            | 実daemon inspect      | 実post-start                           | 2 / +335       |
| MiMo-V2.5-Pro                                                                                             | managed ref / ID手動取得    | manual Docker CLI        | なし                            | force-remove成功のみ     | unreachableのみ                         | 2 / +151       |
| MiMo-V2.5                                                                                                 | container ID            | Docker CLI               | 事前正常削除                        | 実daemon成功path        | unreachableのみ                         | 3 / +216       |
| MiniMax M3                                                                                                | ID + failure flag       | manual Docker CLI        | private field破壊               | test手動inspect        | unreachableのみ                         | 4 / +372       |

---

## 8. Best-of Categories

```
BEST IMPLEMENTATION:
GPT-5.6 Sol / Codex
理由:
create前のunique ownership label、one-shot native dispose、常時independent daemon verificationを組み合わせ、ID未取得のpartial-createを含めてowner-lossを防いだ唯一の実装。

BEST TEST / FAILURE PROOF:
Claude Opus 5 / Claude Code
理由:
実Docker transportを切断し、native removal failure、same-instance no-op、daemon残存、独立retry、startup primary+cleanup failureを一続きで実証した。

BEST MINIMAL DESIGN:
GPT-5.6 Sol / Codex
理由:
単純なLOC最小ではなく、全Referenceを閉じるために必要十分なlabel ownerとDocker API abstractionに留まる。raw proxyやfake daemonはproduction設計へ持ち込まない。

BEST QUALITY / SPEED:
GPT-5.6 Sol / Codex
理由:
merge-ready候補中の最高品質かつ唯一の候補。raw Quality/min最大はComposer 2.5の9.50だが、Major未修正のため受賞対象外。
```

---

## 9. Final Synthesis Recommendation

```
Recommendation:
A
```

### Adopt

GPT-5.6 Sol / Codex、PR #108、Head `d3af857f71a62124842f96de9bced2b748b776be`をそのまま採用する。

採用pattern:

- container create前に一意ownership labelを確定する。
- Testcontainers instanceとは別にlabel ownerを保持する。
- native Disposeは一度だけ。
- native結果に関係なくindependent Docker query/remove/queryを実行する。
- daemon確認成功後だけownerを解放する。
- native failure、independent failure、startup primary failureを失わない。

### Reject
