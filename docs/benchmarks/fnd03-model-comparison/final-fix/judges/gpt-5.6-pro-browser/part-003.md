
Score: 93
A: 29
B: 20
C: 13
D: 15
E: 6
F: 10

```

#### Implementation

container IDをdurable ownerとして保持し、Testcontainers disposeを一度だけ実行します。失敗後のretryは、Testcontainersとは独立したDocker Engine API requestでIDをforce removeします。

startup失敗後にも可能な限りIDを再取得し、cleanup失敗時はownerを保持します。204または404の場合のみownerを解放します。

#### Test proof

14案中で最も強いfailure proofです。

実containerをfault proxy経由で起動した後、Docker control-plane accessを遮断して、Testcontainersの実削除requestを失敗させています。その後、

- upstream daemonではcontainerが残る
- 同一Testcontainers instanceの再Disposeはno-op
- independent retryもproxy遮断中は失敗しownerを保持
- proxy復旧後はID cleanupが成功
- upstream daemonでcontainer不在

を同じresourceで連続確認しています。

startup post-start primary failure＋cleanup transport failure＋owner retention＋最終回収も実containerで証明されています。

#### Findings

```
Blocker: 0
Major:   0
Minor:   1
  M-01 custom Docker Engine endpoint実装はnpipe / unix / plain TCPを対象とし、
       TLS・SSH・特殊auth等のendpoint構成は対象外。
Nit:     1
  N-01 fixture Disposeのserialization gateがない。

```

#### Judgment

failure proofは最上位です。一方、production用のhand-written Docker HTTP pathとtest用fault proxyの実装量が大きく、Sol案より保守コストが高いため総合2位です。

---

### Claude Sonnet 5 / Claude Code

```
PR: 118
Head: 51b9f1e54957576180244fa71cf28e468f2a33d3
Duration: 55 min
CI: 31292745071 / completed / success

Major Fixed: PARTIAL
Merge Candidate: NO

Score: 67
A: 16
B: 13
C: 7
D: 14
E: 8
F: 9

```

#### Implementation

通常teardownではcontainer IDを保持し、最初のnative failure後にpoison flagを立て、以後はDocker CLI fallbackだけを使用します。fallback成功後も元のnative failureをthrowする点は正しいです。

しかしCLI helperは`docker rm`の終了コードを無視し、その後の`docker inspect`が非0であれば理由を区別せず「不在」とします。daemon到達不能時は`rm`も`inspect`も失敗しますが、`TryForceRemoveAsync`は`true`を返してownerを解放します。

また`StartAsync`がcontainer作成後・`containerId`代入前に失敗し、cleanupも失敗した場合、IDの再取得を試みず`container = null`にします。

#### Test proof

delegate injectionによるstate-machine testと、実containerに対するCLI force-remove testはあります。ただしreal Docker removal failureは発生させておらず、startup partial-cleanup専用testもありません。

#### Findings

```
Blocker: 0
Major:   2
  M-01 rmとinspectがともに失敗した場合をcontainer不在と誤認し、
       実containerを残したままownerを解放する。
  M-02 startup partial failure時、取得可能なcontainer IDを再取得せず
       container referenceをclearできる。
Minor:   1
  M-03 startup primary＋cleanup failureの専用自動testがない。
Nit:     0

```

#### Judgment

通常teardownのstate machineだけを見ると良好ですが、independent verifierとstartup pathにowner-loss経路があります。Major root causeは完全には閉じていません。

---

### Grok 4.5 / Cursor

```
PR: 107
Head: 4a600940ab3d776d60086c74cb040155439b6d37
Duration: 9.1 min
CI: 31289676226 / completed / success

Major Fixed: YES
Merge Candidate: YES

Score: 91
A: 28
B: 16
C: 13
D: 15
E: 9
F: 10

```

#### Implementation

設計を逆転し、container IDによるindependent Docker reclaimを唯一のauthoritative cleanupとしています。

まずCLIで実resourceをforce removeし、再inspectで不在を確認してIDをclearします。その後にTestcontainers wrapperをbest-effortでDisposeするため、poisoned instanceのreturn値はowner解放に影響しません。

independent reclaim failureはthrowされ、IDとcontainer referenceが保持されます。startup post-start failureでも同じpathを使用します。

#### Test proof

reflectionで実Testcontainers instanceのdisposed latchを立て、実containerが残った状態を作ります。fail-once reclaimerによって初回independent cleanupを失敗させ、owner ID保持とdaemon-side残存を確認します。

次回cleanupではactual Docker resourceの消滅を確認しています。startup primary＋cleanup failureについても実container残存と最終回収を確認しています。

#### Findings

```
Blocker: 0
Major:   0
Minor:   1
  M-01 daemon-side removal確認後のTestcontainers wrapper disposal例外は
       best-effortとして抑止される。resource safetyには影響しないが診断情報は失われる。
Nit:     1
  N-01 cleanup serialization gateがない。

```

#### Judgment

実装が小さく、実resourceを常にindependent ownerで回収するためroot causeに強い構造です。9.1分でmerge-ready品質に到達しており、quality/speedの最良候補です。

---
