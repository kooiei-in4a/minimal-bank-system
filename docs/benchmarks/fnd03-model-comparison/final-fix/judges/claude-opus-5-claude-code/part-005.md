```

#### Judgment

実装の正しさは上位群と同等で、endpoint整合という他案が見落とした点を唯一押さえている。証拠設計も独創的で、Docker不要な決定的failure injectionという再利用価値の高い資産を残した。E=6は420行のコストによる。全gate閾値を満たし Merge Candidate。実行75分はコストとして高い。

---

### `Qwen3.7 Plus / Open Code`

```
PR:               #112
Head:             9ab18236b9169b21b36689b0787a761267bfbdd8
Duration:         54.0 min
CI:               SUCCESS (run 31291287279)
Major Fixed:      PARTIAL
Merge Candidate:  NO
Score:            47
A: 12  B: 5  C: 6  D: 12  E: 6  F: 6

```

#### Implementation

`ContainerCleanupHandle`（container ID + docker CLI）を導入し、TC dispose失敗時にfallbackする。単発の失敗は救済されるが、**dispose試行済みを記録するフラグが存在しない**。

TC dispose失敗かつfallbackも失敗した場合、`throw` するだけで `container` も `cleanupHandle` も保持したままになる。次の `DisposeAsync` は先頭の `if (candidate is not null)` に入り、**poisoned instanceに対して再度** **`DisposeAsync`** **を呼ぶ**。それは無言のno-opで成功するため、

```
container = null;
cleanupHandle = null;
return;          // ← false success。containerはdaemon上に残存し得る

```

となり、HF-01およびHF-02をそのまま踏む。修正対象のMajorが、二重失敗経路でそのまま再現する。

加えてfallback成功時には元のTC失敗を握り潰して正常復帰する（HF-03）。`ForceRemoveAsync` は除去後の不在確認を行わない。

#### Test proof

failure injectionが**一件も存在しない**。

- `ContainerCleanupFailureIsVisibleAndFallbackSucceeds`: containerを先に正常除去してから `DisposeAsync` を呼ぶ。HF-05が明示的に無効とする注入形態であり、可視性のassertも皆無。
- `TestcontainersDisposedStateIsLatchedAfterFirstDispose`: 1回目のdisposeが成功しているため、2回目のno-opは何も証明しない（除去済みcontainerが除去済みのまま、というだけ）。
- `StartupFailureWithPartialCleanupRetainsBothFailures`: unreachable endpointでcontainer未生成。T-05未達。
- 存在確認primitiveが `docker ps -q --filter id=` で、**停止済みcontainerを「不在」と誤判定する**。

#### Findings

```
Blocker: 1) dispose試行フラグがないため、TC dispose失敗＋fallback失敗の
            retry経路でpoisoned instanceを再利用し、その no-op を成功と
            みなしてownerを解放する（HF-01 / HF-02）。修正対象Majorが
            当該経路で未修正のまま残っている。
Major:   2) fallback成功時にTC dispose失敗を無言で握り潰す（HF-03）。
         3) 有効なfailure injectionが存在せず、T-01/T-02/T-03/T-05のいずれも
            実証されていない。
Minor:   4) docker ps -q は実行中containerのみを列挙し、存在確認として不正確。
         5) ForceRemoveAsync が不在再確認を行わない。
Nit:     test名が検証内容を過大に表現している。README未更新。

```

#### Judgment

単発failureのケースは救済されるためA=12（表面的修正の上限付近）とし、MAJOR\_FIXEDはPARTIAL。ただしretry経路にoriginal Majorがそのまま残っており、B=5と合わせてMerge Candidateは明確に NO。

---

### `GPT-5.6 Luna / Open Code`

```
PR:               #115
Head:             bbc2ede9921cafb74b71b84667aa80bd472b37ae
Duration:         17.0 min
CI:               SUCCESS (run 31291994899)
Major Fixed:      YES
Merge Candidate:  YES
Score:            83
A: 26  B: 16  C: 13  D: 13  E: 8  F: 7

```

#### Implementation

TC dispose失敗時に**即座に** **`container = null`** **としてpoisoned instanceを捨てる**という単純明快な方針。以降のcleanupは保持した `containerResourceId` を鍵にした `DockerContainerCleanup`（Docker.DotNet、endpoint注入対応）のみが担う。

失敗の合成が丁寧で、TC失敗とfallback失敗を `AggregateException` で束ねて必ず投げる。fallback成功時も元のTC例外を包んで投げるため、R-01を満たす（この点でComposer / Qwen / DeepSeek Pro / MiMo と決定的に異なる）。owner解放は `ClearContainerOwnership()` にまとめられ、removal成功後のみ実行される。新規fileゼロ、reflectionなし、CLIなし、proxyなしで、Docker.DotNetのみを使う最も素直な構成である。

#### Test proof

実container・実Docker daemonでの検証が両方向で揃っている。`DockerContainerCleanup.ExistsAsync`（Docker.DotNet inspect）で失敗後true / retry後falseを確認し、`disposeCalls == 1` でpoisoned非再利用を証明、`FailFirstContainerCleanup` でfallbackの決定的1回失敗を注入する。T-05も実containerに対しstartup primary失敗＋dispose失敗＋fallback失敗の三重を注入し、3種のmessageすべてが到達することを検証する。

106行という短さでT-01〜T-05の実質をカバーしており、証拠効率は最良の部類。TC失敗自体はseam doubleであり、実libraryのlatch挙動そのものの直接実証はない。

#### Findings

```
Blocker: なし
Major:   なし
Minor:   1) tests/MinimalBankSystem.IntegrationTests/README.md が未更新で、
            cleanup ownership契約の記述が旧設計（"The fixture retains
            ownership so cleanup can be retried"）のまま実装と乖離している。
            FND-03ではこのREADMEがownership契約の成果物であるため軽くない。
         2) removal後の不在再確認を行わない（remove call成功で解放）。
         3) 実libraryのpoisoned no-op挙動そのものを実証するtestがない。
Nit:     ctorのoptional delegateが4つあり、test seamがやや多い。

```

#### Judgment

「必要十分」という評価軸に最も近い実装である。新規抽象を最小限に留めながらR-01〜R-05を満たし、実daemonでfailure pathを両方向検証している。全gate閾値を満たし Merge Candidate。README未更新はF/D軸で減点したが、merge時に追記すれば解消する性質の指摘である。17分での達成は品質対時間比でも上位。

---

### `MiMo-V2.5-Pro / Open Code`

```
PR:               #117
Head:             6f4f117ff076a2b828e35e1d832f923596ebc6bb
Duration:         12.0 min
CI:               SUCCESS (run 31292576719)
Major Fixed:      NO
Merge Candidate:  NO
Score:            27
A: 3  B: 3  C: 3  D: 11  E: 4  F: 3

```

#### Implementation

`DisposeAsync` は**一行も変更されていない**。base実装のまま `await candidate.DisposeAsync(); container = null;` を try で包み、失敗時は `container` を保持して例外を投げる。すなわち2回目の `DisposeAsync` はpoisoned instanceに対する呼び出しとなり、no-opが成功と解釈されて `container = null` となる。確定Majorは完全に未修正である。

追加されたのは public `ForceCleanupAsync()` のみで、`docker rm -f` を実行する。しかしこれは**fixtureのlifecycleから一度も呼ばれない**。xUnitのclass fixture teardownが呼ぶのは `DisposeAsync()` であり、`ForceCleanupAsync` は利用者が手動で呼ぶことを前提とした孤立したAPIである。R-04（final cleanup path）が成立していない。

#### Test proof

自己申告で放棄している。

```
// This test is a placeholder because we cannot easily cause Testcontainers DisposeAsync to fail
// without internal knowledge.
...
Assert.True(true, "DisposeAsync ownership test completed");

```

`Assert.True(true, ...)` が3箇所ある。failure injectionは皆無、可視性検証も皆無、retry検証も皆無。実daemon確認は `ForceCleanupSucceedsForExistingContainer` の1本のみで、これは手動force cleanupの動作確認にすぎない。

#### Findings

```
Blocker: 1) DisposeAsync が未変更で、poisoned instanceの再disposeを
            成功とみなす経路がそのまま残存している。確定Majorは未修正。
         2) 追加した ForceCleanupAsync がfixture lifecycleから到達不能で、
            final cleanup pathとして機能しない。
Major:   3) placeholder testおよび Assert.True(true) を成果物として提出し、
            root causeの検証を行っていない。
