| Claude Sonnet 5 / Claude Code                              | 22 | 11 | 9  | 14 | 7 | 9 | 72 |
| Composer 2.5 / Cursor                                      | 21 | 15 | 8  | 11 | 7 | 7 | 69 |
| DeepSeek V4 Pro / Open Code                                | 18 | 9  | 10 | 13 | 7 | 6 | 63 |
| Qwen3.7 Plus / Open Code                                   | 12 | 5  | 6  | 12 | 6 | 6 | 47 |
| MiMo-V2.5 / Open Code                                      | 12 | 5  | 6  | 9  | 5 | 5 | 42 |
| MiniMax M3 / Open Code                                     | 6  | 7  | 4  | 11 | 4 | 4 | 36 |
| MiMo-V2.5-Pro / Open Code                                  | 3  | 3  | 3  | 11 | 4 | 3 | 27 |

---

## 6. Candidate-by-Candidate Evaluation

### `GPT-5.6 Sol / Codex`

```
PR:               #108
Head:             d3af857f71a62124842f96de9bced2b748b776be
Duration:         28.68 min
CI:               SUCCESS (run 31290367847)
Major Fixed:      YES
Merge Candidate:  YES
Score:            90
A: 29  B: 18  C: 14  D: 12  E: 8  F: 9

```

#### Implementation

14案中唯一、**ownership labelベース**を採用した。`.WithLabel("com.in4a.minimal-bank-system.postgresql-fixture", <guid>)` をcontainerへ付与し、`DockerContainerResourceOwner` がDocker.DotNetの `ListContainersAsync(All=true, filter=label)` でそのlabelに属するcontainerを列挙し、force removeし、**再度列挙して残存0を確認**してから正常復帰する。残っていれば例外を投げる。

state machineは `containerDisposeAttempted` フラグで、Testcontainers `DisposeAsync` を生涯1回に固定する。TC dispose成功・失敗のいずれでも、その後必ずindependent ownerが実resource状態を確認する。owner解放（`container = null; resourceOwner = null`）はindependent cleanupが検証つきで成功した場合のみ。TC失敗かつindependent成功なら「resourceは除去済みだがcleanup失敗は可視」として例外を投げ、両方失敗なら `AggregateException` を包んでowner保持のまま投げる。

identityがlabelなのでcontainer IDが取得できない部分起動でも列挙で捕捉できる点が、ID保持型より構造的に強い。また成功パスでも毎回daemon側の不在確認が走るのは本案のみである。

#### Test proof

- T-01: independent path側は**実Docker daemonの409 Conflict**（起動中containerに `Force=false` でremove要求）という本物のtransport失敗。TC側の失敗は `IPostgreSqlContainerDisposer` doubleで、1回目throw / 2回目no-opと4.13.0のlatch挙動を明示コメント付きで忠実に模した。
- T-02: 両方の例外messageと `DockerApiException { StatusCode: Conflict }` の存在を検証。
- T-03: `disposer.CallCount == 1` を失敗後・retry後の両方で検証し、poisoned instanceが再利用されないことを証明。
- T-04: `DockerContainerResourceProbe.ExistsAsync`（実Docker inspect）で失敗後true / retry後falseを検証。
- T-05: `serverVersionReader` 差し替えでstartup失敗を注入。実containerが存在する状態で両失敗が保持され、identityが残り、retryで実際に消えることを実daemonで確認。

#### Findings

```
Blocker: なし
Major:   なし
Minor:   1) 既存test UnreachableDockerEndpointIsAnExplicitStartupFailure と
            internal PostgreSqlContainerFixture(string dockerEndpoint) を削除した。
            Issue #41 AC「container起動／接続失敗が明確なtest failureになる」の
            Docker到達不能経路のcoverageが失われている。
         2) Docker.DotNet を Testcontainers の推移依存として暗黙利用しており、
            Directory.Packages.props に直接宣言がない。
Nit:     new DockerClientBuilder().Build() の解決endpointがTestcontainers側の
         解決結果と一致する保証を明示していない（本案はdockerEndpoint注入を
         廃したため実害は小さい）。

```

#### Judgment

root cause closureは14案中最も構造的に完全で、識別子非依存・毎回daemon検証・単一dispose latchの3点が揃う。証拠力もindependent path側が実Docker失敗である点で強い。D軸で既存testを1本削除した分だけ減点した。全gate閾値（A≥24, B≥14, C≥11, D≥12）を満たし、Merge Candidate。

---

### `GPT-5.6 Terra / Codex`

```
PR:               #113
Head:             0c55d66c9ba6e748073cd88314fe40f78d291815
Duration:         21.0 min
CI:               SUCCESS (run 31291508903)
Major Fixed:      YES
Merge Candidate:  YES
Score:            90
A: 28  B: 18  C: 14  D: 14  E: 7  F: 9

```

#### Implementation

`ContainerCleanupOwner` を独立classとして切り出し、`disposeTestcontainersInstanceAsync` delegateと `IContainerResourceCleanup` を受け取る。`testcontainersDisposeAttempted` で単一dispose、失敗時はcontainer IDを鍵に `docker container rm --force` を実行し、`docker container inspect` で不在を確認してからIDをnullにする。independent removal失敗時はIDを保持して `AggregateException`。TC失敗＋independent成功なら「独立pathが除去・検証した」旨を添えて元例外を投げる。

`IPostgreSqlTestContainer` でcontainerを抽象化し、`DockerCliContainerResourceCleanup` はDOCKER\_HOSTを注入endpointへ揃える。`ValidateContainerId` でhex 12〜64文字を強制し、任意文字列でのCLI実行を防いでいる。

#### Test proof

14案で**唯一、実Testcontainers libraryのpoisoned no-opを実containerで再現**している。

`FailingDeleteContainer : DockerContainer` で `UnsafeDeleteAsync` のみをoverrideして失敗させ、実postgres imageで実containerを起動する。

1. 1回目 `DisposeAsync()` → throw
2. 実 `docker inspect` でcontainer存在を確認
3. 2回目 `DisposeAsync()` → **例外なく完了し、containerは依然存在**
4. independent cleanupで除去し不在を確認

これは `Resource._disposed` latchを外部から操作せず、実際のlibrary経路を通した本物の再現であり、T-01/T-03の証拠として最も権威がある。`CleanupOwnerReportsTheOriginalFailureAfterIndependentDockerCleanup` も実container＋実owner＋実CLIで、元失敗の可視性と実daemon不在を同時に検証する。

一方 `StartupFailureRetainsCleanupOwnershipUntilIndependentRetrySucceeds`（T-05）は完全なfake container＋fake cleanupで、state machineのみの検証にとどまる。

#### Findings

```
Blocker: なし
Major:   なし
Minor:   1) TC dispose成功pathではdaemon側の不在確認を行わず成功とみなす。
         2) T-05が全面simulationで、実partial containerを伴う証拠がない。
         3) docker CLI binaryへの依存を追加する（CI ubuntu-latestでは充足）。
Nit:     IPostgreSqlTestContainer 抽象は本修正に必須ではなく、
         Container property の型を変える波及を生んでいる。

```

#### Judgment

実装の正しさはSolとほぼ同等で、既存testを1本も落としていない分D軸で上回る。証拠力は「libraryの実挙動再現」という一点で最良だが、T-05のsimulation依存とTC成功path無検証で相殺し18点。E軸はcontainer抽象の追加分だけSolに劣る。Merge Candidate。

---

### `GPT-5.6 Luna / Codex`

```
PR:               #116
Head:             708213d132e7465eec6c777b5b5f6b4c7ab30d6e
Duration:         17.65 min
CI:               SUCCESS (run 31292206197)
Major Fixed:      YES
Merge Candidate:  NO
Score:            81
A: 27  B: 12  C: 12  D: 14  E: 8  F: 8

```

#### Implementation

新規fileを作らず、fixture file内に `ITestContainerHandle` / `ContainerResourceOwner` / `DockerContainerResourceCleanup` を収めた。identityは**container name（****`mbs_test_container_{guid}`** **を** **`WithName`** **で固定）とcontainer IDの二重化**で、`ResourceReference => resourceId ?? Handle.ResourceIdentity` によりID取得不能時でもnameで除去できる。これはR-05に対して構造的に強い設計である。

`testcontainersDisposeAttempted` で単一dispose、`RemoveAndVerifyAsync` はDocker.DotNetでforce remove後 `InspectContainerAsync` で不在を確認し、残っていれば例外。`IsFinalized` が立つまでownerを保持する。

#### Test proof

ここが本案の弱点である。failure path testは `FakeContainerResourceStore`（`HashSet<string>`）を「daemon」に見立てた**完全なin-memory simulation**で、`PoisonedTestContainerHandle` が1回目throw / 2回目no-opを模す。state machine（dispose 1回のみ、owner保持、retryで解放）は正しく証明されるが、**失敗時のactual daemon stateは一切検証していない**。

実daemon検証は成功pathの `SuccessfulContainerCleanupRemovesTheActualDockerResource`（実Docker inspectで `DockerContainerNotFoundException` を確認）1本のみ。結果として、production実装である `DockerContainerResourceCleanup` は**どのtestでも実Dockerに対して一度も実行されない**。

#### Findings

```
Blocker: なし
Major:   1) independent cleanup実装（DockerContainerResourceCleanup）が
            実Docker daemonに対して一度も実行されておらず、
            production failure pathの実証が空である。
Minor:   2) failure path testのresource stateがHashSetであり、T-04を
            failure側で満たさない。
         3) ContainerResourceOwner に同時実行gateがない。
Nit:     containerOwner?.Handle ?? throw が3箇所に重複。

```

#### Judgment
