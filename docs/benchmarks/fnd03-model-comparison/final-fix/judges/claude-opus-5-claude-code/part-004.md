
```
PR:               #110
Head:             2f8d6afe47b5e48dc0b4a316571c0cdf1c920521
Duration:         6.0 min
CI:               SUCCESS (run 31291017508)
Major Fixed:      PARTIAL
Merge Candidate:  NO
Score:            69
A: 21  B: 15  C: 8  D: 11  E: 7  F: 7

```

#### Implementation

`testcontainersDisposeAttempted` フラグ、`ExistsAsync` → `RemoveAsync` → `ExistsAsync` の二重検証、検証成功後のみowner解放という骨格は正しい。

しかし致命的な分岐がある。`ReclaimContainerAsync` は `testcontainersDisposeException` を捕捉して保持するが、**reclaimerが成功した場合その例外を一切使わずに正常復帰する**。

```
if (await reclaimer.ExistsAsync(...)) { try { await reclaimer.RemoveAsync(...); } catch { ...throw... } }
if (await reclaimer.ExistsAsync(...)) { ...throw... }
ownedContainerId = null;
container = null;      // ← testcontainersDisposeException は捨てられる

```

つまり最も一般的なシナリオ（TC dispose失敗 → reclaim成功）でcleanup失敗が無言で成功に変換される。これはHF-03そのものであり、Issue #41 AC「cleanup失敗を黙って無視しない」に直接違反する。しかも `PoisonedTestcontainersDisposeIsNotRetriedAndDoesNotFalseSuccess` testがこの無例外復帰を期待値として固定している。

さらに `ReclaimContainerAsync` 冒頭の `if (ownedContainerId is null) return;` により、**IDが捕捉される前（StartAsync失敗）はcontainerに対して何のcleanupも実行されない**。base実装ですら `candidate.DisposeAsync()` は呼んでいたため、この経路はbaseより後退している。

#### Test proof

実container・実docker CLIによるdaemon検証、両seam（dispose invoker / reclaimer）への決定的注入、attempt countingによるpoisoned非再利用の証明、T-05まで揃っており、機構としては良い。ただし上記のとおりtestの1本が誤った契約を固定している。

#### Findings

```
Blocker: なし
Major:   1) reclaim成功時にTestcontainers dispose失敗を無言で握り潰し、
            cleanup失敗をtest成功へ変換する（HF-03 / R-01 / AC違反）。
            testがこの挙動を期待値として固定している。
         2) ownedContainerId 捕捉前にstartupが失敗した場合、
            ReclaimContainerAsync が即returnし、containerに対する
            dispose も removal も一切行われない（baseより後退）。
Minor:   3) reclaimGate が Dispose されず、CA1001 を pragma で抑止している。
Nit:     RemoveAsync の docker 引数を文字列連結で構築。

```

#### Judgment

6分という最短実行で骨格を作った点は評価できるが、確定Majorの中核要件であるfailure visibilityを設計として放棄しており、さらにstartup partial pathで新たなcleanup欠落を作った。C=8、D=11でgate不成立。Merge Candidateは NO。

---

### `DeepSeek V4 Pro / Open Code`

```
PR:               #111
Head:             700569f30dda9d53a35d802ac048f45dc72255f3
Duration:         53.0 min
CI:               SUCCESS (run 31291241829)
Major Fixed:      PARTIAL
Merge Candidate:  NO
Score:            63
A: 18  B: 9  C: 10  D: 13  E: 7  F: 6

```

#### Implementation

container IDを保持し、`DisposeAsync` で

```
try { await container.DisposeAsync(); } catch { }   // ← 空catch
container = null;
await ForceRemoveContainerAsync(containerId);
containerId = null;

```

とする。poisoned instanceの再利用は起こらず（`container` を即null化）、`docker rm -f` の失敗は例外化されるためownerは保持される。この2点は正しい。

一方で**空のcatch blockによりTestcontainers cleanup失敗が完全に消滅する**。ログも集約もなく、rm が成功すれば `DisposeAsync` は正常終了する。HF-03の最も直接的な形である。`ForceRemoveContainerAsync` は終了コードのみを見て不在の再確認を行わず、`dockerEndpoint` も無視する。

startup catchでは `TryCaptureContainerId` によりidentityを回収しており、R-05の識別子面は満たしている。

#### Test proof

`PoisonResourceDisposedFlag` が `Resource._disposed` を直接1に設定する。これはHF-05が明示する「単にdisposed flagだけ変更」に該当し、**実際のDocker removal失敗を一度も発生させていない**。latchが立った後のno-opという後半だけを示すもので、T-01は満たさない。

実daemon検証（`docker inspect`）自体は複数testで行っており、poisoned instanceのdispose後もcontainerが残ることは実証されている。しかし `CleanupFailureIsVisibleAndContainerIdentityIsPreserved` は名称に反して例外を一切assertしておらず（設計上できない）、fixtureのfailure pathを通るtestが存在しない。T-05も未達（unreachable endpointではcontainerが生成されない）。

#### Findings

```
Blocker: なし
Major:   1) catch { } によりcontainer cleanup失敗が無言で握り潰される
            （HF-03 / R-01 / AC違反）。
         2) T-01未達。disposed flag直接書き換えのみで、実removal失敗を
            一度も発生させていない（HF-05）。
Minor:   3) ForceRemoveContainerAsync が除去後の不在確認を行わない。
         4) dockerEndpoint を無視して既定daemonへ rm -f する。
         5) test名（"CleanupFailureIsVisible..."）が実際の検証内容と乖離。
Nit:     tests/README.md（cleanup ownership契約の正本）が未更新。
         docker process呼び出しにtimeoutがない。

```

#### Judgment

独立removal pathは存在し実daemonで機能するが、確定Majorの2本柱のうち「failure visibility」を捨て、証拠面ではroot causeに到達する失敗を一度も作っていない。A=18、B=9でgate不成立。Merge Candidateは NO。

---

### `DeepSeek V4 Flash / Open Code`

```
PR:               #114
Head:             4ab6aaeeeb10188eca16b84e5cdba105f6a28a8f
Duration:         75.0 min
CI:               SUCCESS (run 31291986595)
Major Fixed:      YES
Merge Candidate:  YES
Score:            85
A: 27  B: 16  C: 14  D: 13  E: 6  F: 9

```

#### Implementation

`instanceDisposalAttempted` / `containerRemovalConfirmed` / `containerId` の3状態で構成される明快なstate machine。1回目の `DisposeAsync` はTC disposeを試み、失敗すれば**latch機構を説明する詳細messageつきで例外を投げ**（R-01充足）、以降は `RemoveContainerThroughDockerApiAsync`（Docker.DotNet）に切り替わる。`DockerContainerNotFoundException` を不在確認として扱い、確認できるまで `PendingContainerId` を保持する。

特筆すべきは既定ctorが `TestcontainersSettings.OS.DockerEndpointAuthConfig?.Endpoint` からendpointを解決している点で、**independent clientがTestcontainers自身の解決結果と必ず一致する**。この整合を明示的に担保したのは14案中本案のみである。startup catchでも `containerId ??= TryGetContainerId(candidate)` でidentityを回収する。

#### Test proof

`FakeDockerDaemon`（420行）がin-processでDocker Engine APIを実装し、Testcontainersが**実HTTPでcreate / start / inspect / remove を発行**する。removalのみHTTP 500で失敗させるため、実libraryの `Resource._disposed` latchが本物の経路で立つ。

- T-03: poisoned instanceの再disposeで `ContainerRemoveAttempts` が増えないこと、`ContainerExists` がtrueのままであることを**daemon側のカウンタ**で証明。retry時に新規removal callが1増えることも確認。
- T-05: startup失敗＋cleanup失敗を `Assert.Collection` で厳密に2件検証。

resource stateの確認先が実Dockerではなくstubである点はHF-04の観点で減点対象だが、wrapper fieldではなくreal Docker Engine HTTP protocol越しのdaemon-side状態であり、単なるstate確認とは質が異なる。またこれらのtestはDocker不要のため `Category!=PostgreSqlIntegration` 側で走り、CIコストを増やさない設計判断も妥当である。

#### Findings

```
Blocker: なし
Major:   なし
Minor:   1) failure pathのdaemon検証がstub daemonであり、production
            fallback（Docker.DotNet removal）が実Dockerに対して
            一度も実行されていない。
         2) removal後の InspectContainerAsync による不在再確認がない。
         3) process-global TestcontainersSettings.ResourceReaperEnabled を
            書き換える（DisableParallelization collectionで緩和済み）。
         4) 既定ctorが常に明示endpointを渡すようになり、Testcontainers
            自身のendpoint解決から一段間接になる。
Nit:     420行のstub daemonは単一bug修正としては重い。
