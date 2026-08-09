理由: `DockerEndpointFaultProxy` によって「containerは実daemon上で生きたまま、Docker control planeだけが落ちる」という、root causeが発生する条件そのものを実transport levelで再現した唯一の案。そのうえでproduction の Testcontainers instance に対し直接 `DisposeAsync()` を呼び、**例外なく返るのに実containerは生存し続ける**ことをupstream daemonで確認している。T-01〜T-05のすべてが実Dockerで検証されており、`AggregateException` のinner数まで厳密にassertしている。次点はGPT-5.6 Terra（実 `DockerContainer` subclassによるlibrary挙動の再現）とDeepSeek V4 Flash（実HTTP protocol越しのdaemon-side counter検証）。

```
BEST MINIMAL DESIGN: GPT-5.6 Luna / Open Code (#115)

```

理由: 新規fileゼロ、reflectionなし、CLIなし、proxyなし、fake daemonなし。既に推移依存として存在するDocker.DotNetのみで、poisoned instanceの即時破棄・ID保持・失敗合成・retryを実現している。106行のtestで実containerと実daemonに対しfailure pathを両方向（残存→不在）検証し、T-05まで到達している。「高度な実装だから加点しない／最小だから自動加点しない」という基準の下で、**必要十分**に最も近い。

```
BEST QUALITY / SPEED: Grok 4.5 / Cursor (#107)

```

理由: 9.1分・82点（9.01 点/分）で、A≥24 / B≥14 / C≥11 / D≥12 のgate閾値をすべて満たした唯一の高速candidate。reclaim-first順序でroot causeを構造的に回避し、reflectionによる実 `Resource.Disposed` のlatch検証（false→true）という低コストで高い証拠価値を持つ手法を採用した。Composer 2.5は比率では11.50と上回るが、cleanup失敗の無言握り潰し（HF-03）とstartup partial pathのcleanup欠落によりmerge-readyではないため、「品質優先」の原則に従い採用しない。

---

## 9. Final Synthesis Recommendation

```
Recommendation: B
（GPT-5.6 Sol / Codex #108 をbaseとし、Claude Opus 5 #109 と GPT-5.6 Terra #113
  のtest資産、DeepSeek V4 Flash #114 のendpoint整合を統合する）

```

Aを採らない理由は明確である。Sol単体は実装として最良だが既存test 1本を削除しており、Opus5単体は証拠として最良だが不要な自前HTTP client（397行）を抱えている。両者の欠点は互いの長所で正確に埋まる。Dは論外で、6件が既にmerge-readyである。Cは有力だが、Solの実装をほぼそのまま使える以上、新規作成はコストに見合わない。

### Adopt

| 採用元 採用内容                          |                                                                                                                                                                                                                                                               |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **GPT-5.6 Sol / Codex #108**      | ownership labelによるresource identity（`.WithLabel` + label filter列挙）。`containerDisposeAttempted` によるsingle-dispose latch。`list → force remove → re-list` の除去後検証と、残存時の明示的例外。検証成功時のみのowner解放。TC失敗×independent失敗の4象限すべてに対応した例外合成。`HasPendingContainerCleanup` の公開。 |
| **DeepSeek V4 Flash #114**        | independent Docker clientのendpointを `TestcontainersSettings.OS.DockerEndpointAuthConfig` から解決し、Testcontainers自身の解決結果と必ず一致させる。Solの残存Minor（client endpoint整合の非明示）とOpus5のMinor（DOCKER\_HOST/platform default依存）を同時に解消する。                                         |
| **Claude Opus 5 #109**            | `DockerEndpointFaultProxy`（loopback TCP中継 + `BreakDockerAccess()`）。これをT-01の**高忠実度証拠**として1本だけ採用し、実transport失敗下でproduction instanceの無言no-opと実container生存を実daemonで確認する。`AggregateException` のinner数厳密検証というassertion方針も採る。                                        |
| **GPT-5.6 Terra #113**            | `FailingDeleteContainer : DockerContainer`（`UnsafeDeleteAsync` のみoverride）。proxyより遥かに軽量に実libraryの失敗→latch→no-opを再現できるため、**日常的に回すT-01/T-03の主力test**として採用し、proxy testは高忠実度の補強に回す。`ValidateContainerId` 相当の識別子バリデーション方針も採る。                                      |
| **Grok 4.5 #107**                 | reflectionで `Resource.Disposed` を2回読み false→true を確認する**library semantics regression guard**。Testcontainers版数更新時にlatch挙動の変化を即座に検知する安価な番人として1本だけ残す。                                                                                                            |
| **Base 91e3fca（維持）**              | `UnreachableDockerEndpointIsAnExplicitStartupFailure` と `internal PostgreSqlContainerFixture(string dockerEndpoint)` を**復活させる**。Solが削除したが、Issue #41 ACのDocker到達不能経路のcoverageである。                                                                              |
| **GPT-5.6 Luna / Open Code #115** | 実装judgmentとして、抽象は最小限に留める方針。container wrapper interface（Terra `IPostgreSqlTestContainer`）は採らない。                                                                                                                                                                |

### Reject

| 却下対象 理由                                                                                                |                                                                                                                                                                                                   |
| ------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 自前Docker Engine API client（Opus5 `DockerEngineEndpoint`）                                               | Docker.DotNet（`Docker.DotNet.Enhanced 4.3.3`）がTestcontainersの推移依存として既に存在する。HTTP/1.1手書きとstatus line解析の保守コストが対価に見合わない。ただし `Directory.Packages.props` へ**直接 PackageReference を宣言**し、推移依存への暗黙依存は解消する。 |
| docker CLI process呼び出し（Terra / Sonnet5 / Grok / Composer / Qwen / DS Pro / MiMo）                       | `docker` binaryへの依存を新規に導入する。加えて "No such container" の文字列一致はCLI版差・locale差に脆い。Docker.DotNetなら同一のsocket/pipeを型付きで扱える。                                                                                |
| in-process fake Docker daemon 420行（DS Flash）                                                           | 設計は優れているが、単一bug修正の付随物としては過大。fault proxyで同等の証拠が得られる。Docker不要testという利点は認めるが、本repositoryのCIは既に実Dockerを持つ。                                                                                            |
| container wrapper interface（Terra `IPostgreSqlTestContainer`）                                          | 本修正に必須ではなく、`Container` propertyの型変更という波及を生む。                                                                                                                                                      |
| reclaim成功時にTC失敗を握り潰す設計（Composer / Qwen / DS Pro / MiMo / Grokのbest-effort dispose）                     | Issue #41 AC「cleanup失敗を黙って無視しない」およびR-01への違反。independent pathが検証つきで成功した場合でも、元の失敗は例外として投げる。                                                                                                         |
| dispose試行フラグを持たない設計（Qwen / MiMo）                                                                       | retry経路でpoisoned instanceを再利用しfalse successを生む。                                                                                                                                                   |
| production fixtureへのtest専用mutator（MiniMax `MarkContainerFinalizedForTest` / `ReleaseContainerForTest`） | 本番lifecycleに検証専用の状態遷移を露出させる。                                                                                                                                                                      |
| reclaim-first順序（Grok）                                                                                  | 正常系でも毎回外部cleanupを走らせ、Testcontainers自身のcleanupを常時迂回する。Testcontainersを主、independent pathを保険とする順序を維持する。                                                                                              |
| `tests/.editorconfig` の `root = true`（MiMo-V2.5）                                                       | repository root `.editorconfig` の継承をtestsツリー全体で遮断する。test名の修正で足りる。                                                                                                                                 |
| `TestcontainersSettings.ResourceReaperEnabled` のprocess-global書き換え（DS Flash）                           | fake daemonを採らないため不要になる。                                                                                                                                                                          |

### Final implementation shape

実装者が次に構築すべき形をarchitecture levelで示す（本評価sessionでは実装しない）。

**1. Identity層**

- `PostgreSqlBuilder` に一意のownership label（`com.in4a.minimal-bank-system.postgresql-fixture = <GUID>`）を付与する。これをcleanup ownershipの唯一の正本とする。
- container IDは補助情報として保持してよいが、識別子の正本にしない。IDが取得できない部分起動でもlabel列挙で捕捉できることが、この選択の目的である。

**2. Independent cleanup owner**

- Docker.DotNet clientを `TestcontainersSettings.OS.DockerEndpointAuthConfig` 由来のendpointで構築し、Testcontainers本体と必ず同一daemonを指すことを保証する。
- 操作は `ListContainersAsync(All = true, filter: label=<value>)` → 各containerを `RemoveContainerAsync(Force, RemoveVolumes)` → **再度列挙**。残存があれば残存IDを含む例外を投げる。
- `Directory.Packages.props` に `Docker.DotNet.Enhanced` を明示宣言する。

**3. Cleanup state machine（fixture）**

```
状態: containerDisposeAttempted (bool), resourceOwner (nullable), container (nullable)

CleanupContainerAsync():
  semaphoreで直列化
  resourceOwner が null なら return（既に最終化済み）

  if (!containerDisposeAttempted && container is not null):
      containerDisposeAttempted = true          // latch前に必ず立てる
      try  Testcontainers DisposeAsync()
      catch → tcException として保持（ここでthrowしない）

  try  resourceOwner.RemoveAndVerifyAsync()
  catch → independentException として保持

  if (independentException is null):
      container = null; resourceOwner = null; owner.Dispose()   // ← 解放はここだけ

  4象限で例外を投げ分ける:
    tc失敗 & ind失敗 → AggregateException、owner保持（retry可能）
    tc失敗 & ind成功 → tc失敗を包んで throw（resourceは除去・検証済みと明記）
    tc成功 & ind失敗 → ind失敗を throw、owner保持
    tc成功 & ind成功 → 正常復帰

```

- **Testcontainers instanceへの2回目の** **`DisposeAsync`** **は、いかなる経路でも呼ばない。**
- ownerの解放条件は「independent pathがdaemon側の不在を確認した」の一点のみ。プロセス終了やResource Reaperをcleanupの根拠にしない。

**4. Startup path**

- `InitializeAsync` の catch では、`CleanupContainerAsync()` を同一経路で呼ぶ。labelを識別子にしているため、containerが生成済みかどうかにかかわらずownerは有効である。
- primary失敗とcleanup失敗を `AggregateException` で保持し、cleanup成功時のみownerを解放する。

**5. Test構成（6本）**

| # 目的 手段  |                          |                                                                                                                                       |
| -------- | ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| 1        | T-01 / T-03 主力           | `FailingDeleteContainer : DockerContainer`（Terra）で実containerを起動し、1回目dispose失敗 → 実daemonに存在 → 2回目dispose無例外 → **依然存在** を検証             |
| 2        | T-01 高忠実度補強              | `DockerEndpointFaultProxy`（Opus5）でtransportを切断し、production fixtureの実failure pathを1本だけ通す                                               |
| 3        | T-02 / T-03 / T-04       | 4象限の例外内容、`disposeCallCount == 1`、失敗後の残存とretry後の不在を実daemonで検証                                                                          |
| 4        | T-05                     | 実containerに対しpost-start faultを注入。primary + cleanupの両失敗保持、owner保持、retryでの実除去を検証                                                        |
| 5        | library regression guard | reflectionで `Resource.Disposed` を2回読み false → true を確認（Grok）。Testcontainers版数更新時の破壊を検知                                                |
| 6        | 既存契約                     | `UnreachableDockerEndpointIsAnExplicitStartupFailure` を復活。digest / `template0` / `Pooling=false` / isolation / parallel の既存testはすべて維持 |

**6. ドキュメント**

- `tests/MinimalBankSystem.IntegrationTests/README.md` の "Cleanup ownership" 節を書き換え、(a) Testcontainersがremovalより前にdisposed状態をlatchすること、(b) ownership tokenはlabelでありinstanceではないこと、(c) instanceへのdisposeは生涯1回であること、(d) 解放条件はdaemon側の不在確認のみであること、(e) Resource Reaperやprocess終了をcleanupとみなさないこと、を明記する。Sol #108 の README 追記が最も近い出発点になる。

---

## 10. Key Findings

1. **`Resource.Disposed`** **は副作用を持つpropertyである。** `1.Equals(Interlocked.CompareExchange(ref _disposed, 1, 0))` は読み取り自体がtest-and-setであり、`DockerContainer.DisposeAsyncCore()` はこれを **Docker removalより前に** 評価する。したがって「removalが失敗した」という事実と「instanceがdisposed扱いになった」という事実は同時に成立する。上位candidateはいずれもこの一次sourceの性質を正しく言語化しており、下位candidateは「2回目がno-opになる」という現象だけを見て、なぜそうなるかを押さえていない。
2. **cleanup ownershipの正本はresource identityであり、C# referenceではない。** 14案の優劣はほぼこの一点で決まった。identityとしてlabelを選んだSolのみが、container IDを取得できない部分起動でも構造的に破綻しない。IDを選んだ案はstartup catchでのID回収が必須になり、それを怠ったSonnet5 / MiMo-V2.5 / MiniMax M3 / Composerがそこで穴を作った。nameとIDを二重化したLuna/Codexは、実装面では最も堅牢な識別子設計だった。
3. **「独立cleanup pathを持つこと」と「失敗を可視化すること」は別要件であり、後者の脱落が最多の失点原因だった。** Composer / Qwen3.7 / DeepSeek V4 Pro / MiMo-V2.5 の4件が、independent removalが成功した場合にTestcontainers側の失敗を無言で捨てて正常復帰する。DeepSeek V4 Proは `catch { }` という最も直截な形である。Issue #41 ACに「cleanup失敗を黙って無視しない」と明記されているにもかかわらず、修正の副作用としてこれを失った点は示唆的で、**failure pathの修正がfailure visibilityを壊す**という典型的な退行パターンである。
4. **除去後の不在再確認を行うかどうかで、修正の質がもう一段分かれた。** Sol / Terra / Grok / Composer / Sonnet5 / Luna(Codex) は `remove → inspect/list` で不在を確認してからownerを解放する。Opus5 / Luna(OC) / DS Flash は remove call の成功をもって解放する。後者はDocker APIのsemanticsとしては妥当だが、前者は「daemonがそう言った」という一段強い根拠を持つ。特にSolは**成功pathでも毎回検証**しており、これは正常系のsilent leakをも検知できる。
5. **有効なfailure injectionは4種類しか観測されなかった。** (a) 実DockerContainer subclassで `UnsafeDeleteAsync` を失敗させる（Terra、最も安価で忠実）、(b) 実transportをproxyで切断する（Opus5、最も忠実）、(c) in-process fake Docker daemonでHTTP 500を返す（DS Flash、Docker不要）、(d) 実daemonへ `Force=false` でremoveして409を得る（Sol）。一方、`_disposed` の直接書き換え（DS Pro）、`_client = null`（MiniMax）、containerを先に正常除去してからdispose（Qwen / MiMo-V2.5）はいずれもDocker removalの失敗に到達せず、T-01の証明にならない。**HF-05の区別は実務上決定的で、これを外した4件はすべて下位に沈んだ。**
6. **緑のCIはこのMajorについて何も語らない。** 14件すべてがexact Headでsuccessだが、実際にはMajor未修正が2件（MiMo-V2.5-Pro、MiniMax M3）、retry経路にoriginal Majorが残存するものが2件（Qwen3.7 Plus、MiMo-V2.5）ある。失敗経路の修正は、失敗経路を意図的に発生させるtestを書かない限りCIで検証されない。
7. **lifecycleへの接続が欠けたcleanup pathは修正ではない。** MiMo-V2.5-Proの `ForceCleanupAsync` と MiniMax M3の `ForceContainerRemoveAsync` は、いずれもコードとしては正しくDockerからcontainerを除去する。しかし xUnit が呼ぶのは `DisposeAsync()` だけであり、両者はtestからしか呼ばれない。R-04を「独立removal codeが存在するか」ではなく「通常のfixture lifecycleから到達可能か」と定義しておいたことが、この2件を正しく分離した。
8. **independent Docker clientのendpointをTestcontainers本体と一致させる問題を、明示的に解いたのは1件だけだった。** DeepSeek V4 Flashの `TestcontainersSettings.OS.DockerEndpointAuthConfig` 参照のみである。Opus5はDOCKER\_HOSTとplatform defaultで近似しリスクをコメントで開示、Grok / DeepSeek V4 Pro / MiMo-V2.5 は注入されたendpointを無視して既定daemonへ話しかける。docker context、rootless socket、Testcontainers Cloudを使う環境では、独立cleanupが別のdaemonを掃除して「404だから消えた」と誤判定し得る。
9. **抽象の量と修正の質に相関はなかった。** 最小のLuna/Open Code（新規fileゼロ、17分）が83点でmerge-ready、最大級のOpus5（397行のtest基盤）が91点、同じく大型のDS Flash（420行のfake daemon）が85点である一方、MiniMax M3は3 fileを追加して36点、110分をかけたMiMo-V2.5は42点だった。加点されたのは抽象そのものではなく、**それが実resource状態の証明に到達しているか**である。
10. **実行時間と品質はこのタスクでは無相関だった。** 9.1分のGrokが82点でmerge-ready、110分のMiMo-V2.5が42点、75分のDS Flashが85点。所要時間はroot causeの理解の深さを反映せず、**Testcontainersの一次sourceを読んだかどうか**が実質的な分水嶺だった。上位6件はいずれもlatch機構をコードコメントかREADMEで正確に説明している。

---

## 11. Final Conclusion

```
Top candidate:
  Claude Opus 5 / Claude Code (PR #109, 91/100)
  — 総合首位。ただし単独採用は推奨しない（自前Docker Engine API clientの保守コスト）。
  実装品質の首位は GPT-5.6 Sol / Codex (PR #108, 90/100, A=29)。

Recommended final approach:
  B — GPT-5.6 Sol / Codex #108 の label-based ownership + verify-after-remove を
  実装baseとし、以下を統合する。
    - DeepSeek V4 Flash #114: TestcontainersSettings 由来の endpoint 整合
    - GPT-5.6 Terra #113:    FailingDeleteContainer による実library失敗再現（T-01主力）
    - Claude Opus 5 #109:    DockerEndpointFaultProxy による実transport失敗（T-01補強）
    - Grok 4.5 #107:         Resource.Disposed latch の regression guard
    - Base 91e3fca:          UnreachableDockerEndpointIsAnExplicitStartupFailure を復活
  却下: 自前Docker Engine API client、docker CLI依存、420行fake daemon、
        container wrapper抽象、cleanup失敗の無言握り潰し。
  Docker.DotNet.Enhanced を Directory.Packages.props へ直接宣言する。

Candidates merge-ready (6):
