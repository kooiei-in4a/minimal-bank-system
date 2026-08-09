Minor:   4) ForceCleanupAsync が終了コード判定後に StandardError を読むため、
            出力量によってはdeadlockし得る。
Nit:     file末尾にnewlineがない。README未更新。

```

#### Judgment

Majorが未修正であり、追加コードはlifecycleに接続されていない。A=3はforce cleanup helperの存在に対する最小限の加点である。D=11は既存契約を破壊していないことのみによる。green CIは「壊していない」ことしか示していない典型例で、Merge Candidateは NO。

---

### `MiMo-V2.5 / Open Code`

```
PR:               #120
Head:             8a37daa3d85016348910904dff7ac29c2811200e
Duration:         110.0 min
CI:               SUCCESS (run 31294256088)
Major Fixed:      PARTIAL
Merge Candidate:  NO
Score:            42
A: 12  B: 5  C: 6  D: 9  E: 5  F: 5

```

#### Implementation

`ownedContainerId` を保持し、TC dispose失敗時に `ForceRemoveContainerAsync`（docker rm -f）へfallbackする。単発失敗は救済される。

しかしQwen3.7 Plusと同型の欠陥を持つ。dispose試行フラグがなく、二重失敗時は `container` と `ownedContainerId` を保持したまま throw するため、次の `DisposeAsync` が **poisoned instanceを再度disposeし、その no-op成功で** **`container = null; ownedContainerId = null;`** **として false success を返す**（HF-01 / HF-02）。fallback成功時にはTC失敗を握り潰して正常復帰する（HF-03）。

さらにID捕捉が `StartAsync` 成功後のみで、startup catch内の回収がない。`ForceRemoveContainerAsync` は不在再確認をせず、"No such container" も許容しないため、除去済みcontainerへのretryは必ず失敗する。`dockerEndpoint` も無視する。

#### Test proof

failure injectionは存在しない。`DeterministicContainerCleanupFailureViaForceRemove` は名称に反し、containerを正常にforce removeして不在を確認するだけで、HF-05が無効と明示する注入形態である。可視性・retry・T-05のいずれも未検証。実daemon確認（`docker inspect`）は行われているが、対象が成功pathのみである。

#### Findings

```
Blocker: 1) dispose試行フラグがなく、二重失敗後のretryでpoisoned instanceを
            再利用しfalse successを返す（HF-01 / HF-02）。
Major:   2) fallback成功時にTC dispose失敗を無言で握り潰す（HF-03）。
         3) 有効なfailure injectionが存在せず、T-01〜T-03、T-05が未実証。
         4) tests/.editorconfig を root = true で新規追加した。repository root
            の .editorconfig（charset / end_of_line / insert_final_newline /
            indent / C# style rules）の継承が tests ツリー全体で遮断される。
            目的は自身のtest名（アンダースコア）が誘発したCA1707の抑止であり、
            test名変更で足りる。副作用が対価に見合っていない。
Minor:   5) startup catchでcontainer identityを回収しない。
         6) ForceRemoveContainerAsync が "No such container" を許容せず、
            除去済みcontainerへのretryが必ず失敗する。dockerEndpointも無視。
Nit:     README未更新。

```

#### Judgment

110分という最長の実行時間に対し、root causeの retry 経路は未閉鎖、証拠は実質ゼロ、加えて repository 全体の formatting 継承を断つ config 変更という副作用を残した。Merge Candidateは NO。

---

### `MiniMax M3 / Open Code`

```
PR:               #119
Head:             352b6489d8d4723551eb2634fd9dd612433d2fa6
Duration:         65.0 min
CI:               SUCCESS (run 31293843630)
Major Fixed:      NO
Merge Candidate:  NO
Score:            36
A: 6  B: 7  C: 4  D: 11  E: 4  F: 4

```

#### Implementation

`containerDisposalFailed` / `containerDisposalFailure` / `containerFinalized` の状態を lock 付きで導入し、container IDを保持する。しかし `DisposeAsync` の実質的な変更は次の一点に集約される。

```
if (wasAlreadyFailed) { return; }    // ← 2回目以降は無言で即return

```

初回失敗は可視化されるが、**それ以降のすべての** **`DisposeAsync`** **は何もせず成功を返す**。containerはdaemon上に残り続ける。base実装は少なくとも再throwしていたため、retry挙動はbaseより後退している。

独立removalを行う `ForceContainerRemoveAsync` は `internal` で、`DisposeAsync` からも `InitializeAsync` からも呼ばれない。呼び出しているのはtestのみである。R-04（lifecycleから到達可能なfinal cleanup path）が成立していない。

さらにstartup catchは、cleanupが失敗した場合でも `container = null; containerId = null; containerDisposalFailed = false; containerDisposalFailure = null;` をまとめて実行する。**ownerが完全に消える**（HF-02）。IDの捕捉自体もserver version検証完了後であり、startup失敗時は常にnullである。

`MarkContainerFinalizedForTest` / `ReleaseContainerForTest` というtest専用mutatorがproduction fixtureに public surface として置かれている。

#### Test proof

`FailableContainerActivator` がreflectionで `_disposed = 0`、`_client = null`、`_container = null` を設定する。`_client` をnullにするため **disposeはDockerに到達する前に落ちる**。実際のremoval失敗を模していないHF-05の無効注入である。

`ContainerResourceInspector` による実daemon検証は行われており、失敗後にcontainerが残存することは実証されている（B=7の根拠）。しかし最終的な除去はtestが `ForceContainerRemoveAsync` と `ReleaseContainerForTest` を明示的に呼ぶことでのみ達成されており、production lifecycleでは再現しない。`SameInstanceDisposeIsNoOpAfterFirstFailureAndDoesNotMaskTheOriginalFailure` は名称に反し、2回目・3回目の `DisposeAsync` が無言で返ることを期待値として固定している。

#### Findings

```
Blocker: 1) 初回失敗後の DisposeAsync がすべて無言で即returnし、containerが
            永久に除去されない。independent cleanup（ForceContainerRemoveAsync）
            がlifecycleから呼ばれず、R-04が成立していない。Majorは未修正。
         2) startup catchでcleanup失敗時にも container / containerId /
            failure stateをすべてクリアし、ownerを完全に喪失する（HF-02）。
            baseより後退している。
Major:   3) failure injectionが _client = null によるものでDockerに到達せず、
            container removal failureの証明として無効（HF-05）。
         4) testが「retryは無言で返る」挙動を期待値として固定している。
Minor:   5) MarkContainerFinalizedForTest / ReleaseContainerForTest という
            test専用mutatorがproduction fixtureに露出している。
         6) containerId捕捉がstartup検証完了後で、部分起動時は常にnull。
Nit:     README未更新。

```

#### Judgment

daemon側検証を行うinspectorを用意し、problemをtestで可視化しようとした形跡はあるが、production側の修正が実質的に存在しない。lifecycleから到達できないcleanup pathは修正ではない。Merge Candidateは NO。

---

## 7. Architecture Comparison

| Candidate Ownership Independent cleanup Failure injection Actual daemon verification Startup failure Complexity  |                                |                                               |                                                   |                                       |                            |                               |
| ---------------------------------------------------------------------------------------------------------------- | ------------------------------ | --------------------------------------------- | ------------------------------------------------- | ------------------------------------- | -------------------------- | ----------------------------- |
| GPT-5.6 Sol / Codex                                                                                              | ownership label (GUID)         | Docker.DotNet list→remove→**re-list検証**       | 実Docker 409 Conflict + disposer double            | 実Docker inspect（成功/失敗両方、**全pathで毎回**） | label識別のためID不要で完全카버        | 中（新規1file 127行、interface 4）   |
| GPT-5.6 Terra / Codex                                                                                            | container ID                   | docker CLI rm→**inspect検証**                   | **実DockerContainer subclass**でUnsafeDeleteAsync失敗 | 実docker inspect（失敗path含む）             | catchでID回収、ただしT-05はfake    | 中〜高（新規1file 241行、container抽象） |
| GPT-5.6 Luna / Codex                                                                                             | container **name + ID** 二重     | Docker.DotNet remove→**inspect検証**            | in-memory fake store + handle double              | 成功pathのみ実Docker                       | name fallbackで構造的に強い       | 低〜中（新規fileなし、+282行）           |
| Claude Opus 5 / Claude Code                                                                                      | container ID                   | 自前Docker Engine API（npipe/unix/tcp）           | **実transport fault proxy**                        | 実Docker（upstream直結、全段階）               | catchでID回収、実containerでT-05 | **高**（新規2file 397行、HTTP自前実装）  |
| Claude Sonnet 5 / Claude Code                                                                                    | container ID                   | docker CLI rm→**inspect検証**                   | production fixtureのseam差し替え（架空ID）                 | helper単体のみ実Docker                     | **ID回収なし＝owner喪失**         | 低〜中（新規1file 84行）              |
| Grok 4.5 / Cursor                                                                                                | container ID                   | docker CLI **exists→rm→exists（primary path）** | reflectionで実Disposed読み + reclaimer注入              | 実docker inspect（失敗path含む）             | catchでID回収、実containerでT-05 | 低〜中（新規1file 105行）             |
| Composer 2.5 / Cursor                                                                                            | container ID                   | docker CLI exists→rm→exists検証                 | 両seamへの決定的注入                                      | 実docker inspect（失敗path含む）             | **ID捕捉前はcleanup実行されず**     | 中（新規3file）                    |
| DeepSeek V4 Pro / Open Code                                                                                      | container ID                   | docker CLI rm（**検証なし**）                       | `_disposed` 直接書換のみ（HF-05）                         | 実docker inspect（成功pathのみ）             | catchでID回収                 | 低（fixture内のみ）                 |
| DeepSeek V4 Flash / Open Code                                                                                    | container ID（**endpoint整合**）   | Docker.DotNet remove（検証なし）                    | **in-process fake Docker daemon**（実HTTP 500）      | stub daemon側の実state                   | catchでID回収、T-05厳密検証        | **高**（新規1file 420行）           |
| Qwen3.7 Plus / Open Code                                                                                         | container ID                   | docker CLI rm（検証なし）                           | **なし**（HF-05）                                     | `docker ps -q`（停止containerを誤判定）       | handle未生成                  | 低（fixture内 + handle）          |
| GPT-5.6 Luna / Open Code                                                                                         | container ID（endpoint注入）       | Docker.DotNet remove（検証なし）                    | seam double + FailFirst wrapper                   | 実Docker inspect（**失敗path両方向**）        | catchでID回収、実containerでT-05 | **低**（新規fileなし）               |
| MiMo-V2.5-Pro / Open Code                                                                                        | なし                             | 手動 `ForceCleanupAsync`（**lifecycle未接続**）      | **なし**（placeholder明記）                             | 手動force pathのみ                        | 変更なし                       | 極低（+36行）                      |
| MiMo-V2.5 / Open Code                                                                                            | container ID                   | docker CLI rm（検証なし、endpoint無視）                | **なし**（HF-05）                                     | 実docker inspect（成功pathのみ）             | ID回収なし                     | 低 + .editorconfig副作用          |
| MiniMax M3 / Open Code                                                                                           | container ID（**lifecycle未接続**） | `ForceContainerRemoveAsync`（**test専用**）       | reflectionで `_client = null`（Docker未到達、HF-05）     | 実docker inspect（残存を実証）                | **owner完全喪失**              | 中（新規2file + test専用mutator）    |

---

## 8. Best-of Categories

```
BEST IMPLEMENTATION: GPT-5.6 Sol / Codex (#108)

```

理由: ownership labelという、container IDを取得できない部分起動でも成立する識別子を選び、`list → remove → re-list` によってdaemon側の不在を**全cleanup pathで毎回**確認する唯一の案。`containerDisposeAttempted` によるsingle-dispose latch、検証成功時のみのowner解放、TC失敗とindependent失敗の両方を保存した例外合成が揃っており、R-01〜R-05を構造的な穴なしで満たす。Docker.DotNetのみを使い、reflection・CLI・自前HTTP・fake daemonのいずれにも依存しない点も、保守コストの観点で最良である。

```
BEST TEST / FAILURE PROOF: Claude Opus 5 / Claude Code (#109)

```
