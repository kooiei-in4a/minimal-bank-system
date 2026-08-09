
実装だけ見ればA=27で上位群に匹敵し、name/IDの二重identityは他案にない利点である。しかしB軸がgate閾値14を下回る（12）。「PR本文が言うほどには実証されていない」典型例であり、green CIとstate machine testだけでは本Majorの修正をPASSにできない。Merge Candidateは NO とするが、**実daemonでのfailure path testを1本追加すれば即座に上位merge候補になる**。

---

### `Claude Opus 5 / Claude Code`

```
PR:               #109
Head:             4859b736e69cdecdc3a5797ae7c69f849b13f2a7
Duration:         28.0 min
CI:               SUCCESS (run 31290330550)
Major Fixed:      YES
Merge Candidate:  YES
Score:            91
A: 28  B: 19  C: 14  D: 15  E: 6  F: 9

```

#### Implementation

ownership tokenをcontainer IDと明示し、`ReclaimContainerAsync` が `containerDisposeAttempted` で単一disposeを保証、以降は `DockerEngineEndpoint.RemoveContainerAsync`（自前のDocker Engine API client）でID指定removal。`StartAsync` 直後にIDを取得し、startup catchでは `containerId ??= TryReadContainerId(candidate)` で部分生成containerのidentityを回収する。204/404のみを解放条件とし、404解放の前提（解決endpointが生成時endpointと同一であること）をコード内に明記している。

`DockerEngineEndpoint` はnpipe / unix socket / tcp を自前で接続し、HTTP/1.1リクエストを手書きしてstatus lineだけを解析する。Docker.DotNetが推移依存として既に存在することを考えると、この再実装は必要性が低い。

#### Test proof

14案で最も高忠実度のfailure injectionである。`DockerEndpointFaultProxy` がloopback TCPで実Docker endpointへ中継し、Testcontainersはこのproxy経由でcontainerを生成する。`BreakDockerAccess()` は転送を止めpooled connectionも切断するため、**containerは実daemon上で生きたまま、Docker control planeだけが落ちる**。これはroot causeが起きる条件そのものである。

- T-01: 実transport失敗による実 `container.DisposeAsync()` の失敗。
- T-03: **実productionのTestcontainers instanceに対して直接** **`fixture.Container.DisposeAsync()`** **を呼び、例外なく返ること、かつupstream daemon上でcontainerが生存し続けることを確認**。libraryの無言no-opの直接的実証。
- T-04: upstream endpoint（proxyを迂回）への `/containers/{id}/json` で毎段階の実状態を確認。
- T-05: post-start fault injectorでproxyを壊しつつprimary失敗を投げ、`AggregateException` のinner数が正確に2であること、containerが実在すること、ID保持、復旧後の回収まで検証。

#### Findings

```
Blocker: なし
Major:   なし
Minor:   1) DockerEngineEndpoint が Docker Engine API clientの自前実装
            （HTTP/1.1手書き、status lineのみ解析）であり、推移依存の
            Docker.DotNet で置換可能。将来の保守コストが不必要に高い。
         2) endpoint解決が DOCKER_HOST とplatform defaultのみで、
            Testcontainersのdocker context / testcontainers.properties
            解決と乖離し得る（コメントで開示済み）。
         3) removal後に ContainerExistsAsync による再確認を行わず204/404で解放する。
Nit:     cleanup系testが3本とも実container起動を伴い、CI時間を増やす。

```

#### Judgment

A/B/C/Dのいずれも上位で、特にD=15（既存契約を一切壊さず、ctor後方互換、README更新も正確）とB=19は全案中最高。総合首位。減点はE軸のみで、証拠のためのproxyは正当だが、Docker Engine clientの自前実装は「必要十分」を超えている。Merge Candidateだが、採用時はこのclientをDocker.DotNetへ置換すべきである。

---

### `Claude Sonnet 5 / Claude Code`

```
PR:               #118
Head:             51b9f1e54957576180244fa71cf28e468f2a33d3
Duration:         55.0 min
CI:               SUCCESS (run 31292745071)
Major Fixed:      PARTIAL
Merge Candidate:  NO
Score:            72
A: 22  B: 11  C: 9  D: 14  E: 7  F: 9

```

#### Implementation

`containerDisposeIsPoisoned` フラグ＋container IDを鍵にした `DockerContainerCleanup`（docker CLI）へのfallback。`TryForceRemoveAsync` は `docker rm` の終了コードではなく**除去後の** **`docker inspect`** **の結果**で成否を返すため、検証つきである点は良い。owner解放は検証成功後のみ、元のTC失敗は除去成功後も例外として再送出される（R-01充足）。

ただしID捕捉が `await candidate.StartAsync(); containerId = candidate.Id;` の成功後のみで、**startup catch内にidentity回収がない**。`StartAsync` がcontainer生成後（wait strategy timeout等）に失敗した場合、`containerId` はnullのまま `CleanupContainerAsync` に入り、TC disposeが失敗すると `id is null` branchで `container = null` を設定して「independent cleanupを試行できなかった」と明言して例外を投げる。ownerが完全に失われる。

#### Test proof

failure injectionはproduction fixtureに追加した4引数test-only ctor（container / containerId / disposeOverride / forceRemoveOverride）による**seam差し替えのみ**で、containerIdは `"simulated-container-id"` という架空値である。実Dockerに触れるのは `DockerContainerCleanupForceRemovesAndVerifiesRealDaemonState` 1本で、これはfixtureのfailure pathを通らず helper単体を実containerで検証するにとどまる。libraryの無言no-op自体を実証するtestはない。

`DoubleCleanupFailureRetainsOwnershipAndNeverRetriesThePoisonedNativeDispose` は「T-05-adjacent」と自称するが実体はdouble cleanup失敗であり、startup partial pathは未カバーである。

#### Findings

```
Blocker: なし
Major:   1) startup catchでcontainer identityを回収しないため、
            「containerは生成済み・StartAsync失敗・TC dispose失敗」の
            経路でownerが完全に失われる（R-05 / HF-02）。同経路を
            Sol / Terra / Luna / Opus5 はいずれもID回収でカバーしている。
Minor:   2) failure pathのcontainer IDが架空値で、実daemon stateを伴う
            failure path testが存在しない（HF-04に近い減点）。
         3) test-only 4引数ctorがproduction fixtureのstateを直接seedする
            侵襲的seamになっている。
Nit:     docker CLI binary依存の追加。

```

#### Judgment

主経路（起動成功後のdispose失敗）についてはstate machineもverificationも正しく、A=22は「本質的には正しいが弱いedgeあり」の下限付近。しかしR-05の穴は本Majorと同じ「ownerが消えてcontainerが残る」class of failureであり、Major扱いとした。B=11 < 14、C=9 < 11でgate不成立、Merge Candidateは NO。

なお、genuineなtransport failureの再現を試みて断念した経緯と理由をコード内コメントで明示的に開示している点は、F軸の「known risk disclosure」として正当に加点した。

---

### `Grok 4.5 / Cursor`

```
PR:               #107
Head:             4a600940ab3d776d60086c74cb040155439b6d37
Duration:         9.1 min
CI:               SUCCESS (run 31289676226)
Major Fixed:      YES
Merge Candidate:  YES
Score:            82
A: 24  B: 16  C: 13  D: 12  E: 8  F: 9

```

#### Implementation

唯一**順序を反転**した案である。`ReclaimOwnedContainerAsync` はまずindependent reclaimer（docker CLI）で `ExistsAsync` → `RemoveForceAsync` → `ExistsAsync` 再確認を行い、残存すれば例外。そのうえで最後にTestcontainers `DisposeAsync` を **best-effort** として呼び、例外は握り潰す。

結果としてTestcontainers instanceはcleanup手段として一切使われず、poisoned retryは構造上成立しない。ownerの解放は実daemon不在確認の後のみ。設計としては筋が通っている。

副作用として、正常系でも毎回 `docker inspect` / `docker rm -f` が走るため、docker CLI binaryが常時必須になる。また `CliDockerContainerReclaimer` は `dockerEndpoint` を受け取らず、fixtureがcustom endpointで構築された場合でも既定daemonへ話しかける不整合がある。

#### Test proof

短時間ながら密度が高い。`LatchTestcontainersDisposedState` は**実** **`Resource.Disposed`** **propertyをreflectionで2回読み**、false → true を検証してlatch機構そのものを証明したうえで、実instanceをpoison状態にする。続いて実 `fixture.Container.DisposeAsync()` を呼び、実daemon上にcontainerが残ることを `docker inspect` で確認する。

`ControllableDockerContainerReclaimer` でreclaimer側に決定的失敗を1回注入し、失敗の可視性・ID保持・retry成功・実daemon不在を検証。T-05も実containerに対しpost-start faultを注入し、`AggregateException` の両inner・ID保持・実存在・最終除去まで確認している。

#### Findings

```
Blocker: なし
Major:   なし
Minor:   1) CliDockerContainerReclaimer が dockerEndpoint を受け取らず、
            custom endpoint構成時にdaemonが食い違い得る。
         2) Testcontainers dispose失敗を完全に握り潰すため、TC層の
            cleanup失敗は決して可視化されない（authoritativeな検証が
            先行するため実害は限定的だが、R-01の趣旨からは後退）。
         3) 正常系でも docker CLI が必須になる（Testcontainers単体では
            不要だったplatform依存の追加）。
         4) 「No such container」の文字列一致でmissingを判定しており、
            CLI版差・locale差に脆い。
Nit:     third-party protected memberへのreflectionはversion結合を生む
         （4.13.0 pin下では許容範囲）。

```

#### Judgment

9.1分で全gate閾値を満たした唯一のcandidate。A=24はgate下限ちょうどで、「reclaim-first順序でroot causeを構造的に回避」という正しい解だが、TC失敗の可視性を捨てた点とendpoint不整合で上位群には届かない。証拠力は実latch・実daemn・決定的注入が揃い16点。Merge Candidateは YES。

---

### `Composer 2.5 / Cursor`
