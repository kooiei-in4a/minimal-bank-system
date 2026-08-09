# Issue #41 FND-03 — 独立レビュー性能評価の比較サマリー

## 1. 目的

Issue #41 `[FND-03] 実PostgreSQL integration test基盤を確立する` のFinal Synthesisに対して作成された、次の2つのJudge評価を併存保存し、共通結論・相違点・採用判断を短く確認できるようにする。

| 評価文書 | Judge / Harness | Reference Verdict | Blocker / Major / Minor / Nit |
| --- | --- | --- | --- |
| `implementation-evaluation.md` | ChatGPT / Browser・GitHub一次source突合 | **REQUEST CHANGES** | `0 / 1 / 1 / 0` |
| `implementation-evaluation-claude-opus-5.md` | Claude Opus 5 / Claude Code xhigh | **APPROVE** | `0 / 0 / 2 / 3` |

Target identityは両文書で共通である。

```text
Repository: kooiei-in4a/minimal-bank-system
Issue:      #41
PR:         #104
Base SHA:   7946cc55e49c0c6e21ad7b86c20a8435b4976269
Head SHA:   91e3fca181558cd1523390347f4f2f80d6014d26
CI Run:     31277771209
```

---

## 2. 両評価で一致した事項

両Judgeは、次の点では実質的に一致している。

1. PostgreSQL 18.4の実containerがCIで起動し、PostgreSQL category 7件がskipなしで成功している。
2. image referenceはdigest-qualifiedで固定されており、InMemory / SQLite fallbackは存在しない。
3. test単位database、GUID名、`template0`、`Pooling=false`、`DROP DATABASE ... WITH (FORCE)`というdatabase isolation方針は妥当である。
4. application `DbContext`、migration、business schema/table、Docker Compose等のFND-04以降へのscope creepはない。
5. `Fixture.Container.Image.FullName` / `Digest`はDocker daemonのinspect結果ではなく、builderへ渡したreference stringのparse結果である。したがってdigest assertionはdaemon-side runtime evidenceではない。
6. .NET 10の`TextWriter.Synchronized`が返す`SyncTextWriter`は、現runtimeでは返却instance自身のmonitorを使用する。`lock(synchronizedWriter)`はconcurrent writeと相互排他になる。
7. `deepseek-v4-flash-opencode`と`minimax-m3-opencode`の`SyncTextWriter`同期semanticsに関するFindingは一次sourceと矛盾する。
8. `chatgpt-o3-browser`はreviewを完遂できず、`INCOMPLETE`として扱うべきである。

したがって、両評価の差はPostgreSQL test基盤全体の出来ではなく、**container cleanup failure後のTestcontainers内部stateをmerge blockerとして扱うか**に集中している。

---

## 3. 決定的な相違点

### 3.1 Claude Opus 5版

Claude版は次のように判断した。

```text
Reference Verdict: APPROVE
Blocker: 0
Major:   0
Minor:   2
Nit:     3
```

container `DisposeAsync` failureについては、主に次を確認している。

- fixture codeは例外を握り潰さない。
- container fieldは失敗時に保持される。
- xUnitはfixture cleanup exceptionをtest class cleanup failureとして可視化する。
- deterministic failure injection testがないことはcoverage gapだが、機能欠陥とは確定しない。

この前提ではGold Blocker / Majorは0件となり、16件の`APPROVE`は正しい。差はverification depth、framework理解、non-blocking findingの精度で付く。

### 3.2 ChatGPT版

ChatGPT版はTestcontainers .NET 4.13.0のdependency sourceまで追跡し、次のstate transitionをMajorとした。

```text
Resource.Disposed
  = 1.Equals(Interlocked.CompareExchange(ref _disposed, 1, 0))
```

`DockerContainer.DisposeAsyncCore()`は、Docker resource削除より前に`Disposed`を評価する。

```text
1回目のDisposeAsync
  _disposed: 0 -> 1
  Docker RemoveAsync: failure
  container resource: 残存

2回目のDisposeAsync
  Disposed == true
  即return（Docker RemoveAsyncは再実行されない）

repository fixture
  2回目を成功と判断
  container fieldをnullへ変更
```

このため、fixtureがC# referenceを保持していても、**同じTestcontainers instanceでのcleanup retryは実際には成立しない**。2回目のno-opを成功と誤認すると、未回収containerのdeterministic handleを失う可能性がある。

このroot causeを採用すると、Referenceは次になる。

```text
Reference Verdict: REQUEST CHANGES
Blocker: 0
Major:   1
Minor:   1
Nit:     0〜1
```

17 reviewer全件がこのdependency stateを検出していないため、全件が`TP=0 / FN=1`となる。

---

## 4. 一次sourceに基づく採用判断

現時点では、**ChatGPT版のREQUEST CHANGESをcanonicalなmerge判断として扱う方が妥当**である。

理由は次の通り。

- Testcontainersのdisposed flagがresource削除成功前にlatchされることは、source上で決定論的に確認できる。
- Docker remove failure後にdisposed flagをresetするpathはない。
- 同じinstanceへの2回目のdisposeはno-opとなる。
- repository fixtureはno-opを識別できず、成功時と同じくfieldを`null`へ変更する。
- green CIと通常cleanup成功は、Docker remove自体が失敗したpathを通らないため反証にならない。
- Issue #41の重点確認には、cleanup retry、final cleanup、deterministic owner、failed dispose後のhandle保持が明示されている。

ただしAC-06の表現は次のように分離するのが正確である。

| 論点 | 判定 |
| --- | --- |
| 最初のcleanup failureを黙って無視しない | **PASS** — 例外として可視化される |
| failure後に同一container handleでretryできる | **FAIL** |
| final cleanupをdeterministically成立させる | **FAIL** |
| failed dispose後も有効なowner/handleを保持する | **FAIL** |

したがって、Majorは単純な「例外を握り潰した」問題ではなく、**container lifecycle全体のretry / final cleanup contractが実dependency semanticsと一致しない問題**である。

---

## 5. ランキング差

Reference Verdictの違いにより、絶対scoreとGradeは大きく異なる。ただし相対順位には一定の共通性がある。

| Model + Harness | ChatGPT版 Rank / Score | Claude版 Rank / Score |
| --- | ---: | ---: |
| Claude Opus 5 / Claude Code | **1 / 65.5** | **1 / 99.0** |
| Claude Sonnet 5 / Claude Code | **2 / 65.0** | 3 / 95.5 |
| GPT-5.6 Sol / Codex | 3 / 59.5 | **2 / 97.0** |
| ChatGPT Opus 5.6 Sol / Browser | 4 / 57.5 | 4 / 90.5 |
| DeepSeek V4 Pro / Open Code | 5 / 57.0 | 7 / 85.0 |
| Grok 4.5 / Cursor | 6 / 56.5 | 5 / 90.0 |
| GPT-5.6 Terra / Codex | 7 / 56.0 | 6 / 89.5 |
| ChatGPT GPT 5.5 / Browser | 8 / 55.0 | 8 / 84.0 |
| MiMo-V2.5-Pro / Open Code | 9 / 51.5 | 11 / 80.5 |
| Composer 2.5 / Cursor | 10 / 50.0 | 9 / 83.5 |
| GPT-5.6 Luna / Codex | 11 / 47.5 | 10 / 82.5 |
| Qwen3.7 Plus / Open Code | 12 / 46.0 | 13 / 76.5 |
| MiMo-V2.5 / Open Code | 13 / 45.5 | 12 / 78.0 |
| GPT-5.6 Luna / Open Code | 14 / 41.5 | 14 / 72.0 |
| DeepSeek V4 Flash / Open Code | 15 / 35.5 | 16 / 65.0 |
| MiniMax M3 / Open Code | 16 / 34.5 | 15 / 70.0 |
| ChatGPT o3 / Browser | 17 / 14.0 | 17 / 28.0 |

共通傾向は次の通り。

- Claude Opus 5 / Claude Codeは両評価で1位。
- 上位3件はClaude Opus 5、Claude Sonnet 5、GPT-5.6 Solの組合せで共通。
- ChatGPT Opus 5.6 Sol / Browserは両評価で4位。
- DeepSeek V4 Flash、MiniMax M3、ChatGPT o3は両評価で下位。
- 相対的なverification depth評価は近いが、**全員がGold Majorを見逃したとするかどうか**で絶対scoreが変わる。

---

## 6. 各文書の利用目的

### `implementation-evaluation.md`

- dependency sourceを含むadversarialなmerge-gate判定
- container lifecycle / cleanup ownershipを重視
- canonical Referenceとして使用
- PR #104を修正前にmergeしない判断の根拠

### `implementation-evaluation-claude-opus-5.md`

- framework/runtime probe、CI incident、digest assertion、reviewer verification depthの詳細分析
- clean implementationに対するprecision benchmarkとしての別解
- Reference disagreementを再検証するための比較資料
- reviewer相対評価の補助資料

両文書を残すことには意味がある。Claude版は誤った文書として廃棄するのではなく、**Testcontainers disposal stateを除く大部分のtechnical analysisが高品質な独立評価**として保持する。

---

## 7. 次の技術確認

source読解だけでもMajorは成立するが、最終的な議論を閉じるためには次のruntime probeが有効である。

1. Testcontainers 4.13.0でcontainerを作成する。
2. 最初のDocker removeだけを決定論的に失敗させる。
3. 同じcontainer instanceへ2回目の`DisposeAsync()`を呼ぶ。
4. Docker remove APIの呼出回数を確認する。
5. containerがdaemon上に残っているか確認する。
6. repository fixtureがfieldを`null`へ落とすか確認する。

期待結果は、source上では次である。

```text
Remove call count: 1
Second DisposeAsync: successful no-op
Daemon container: remains
Fixture field: null after second call
```

この期待結果が再現されればChatGPT版Majorがruntimeでも確定する。反対の結果が出る場合のみ、Testcontainers sourceの別pathまたは外部cleanup mechanismを再調査する。

---

## 8. 結論

1. 両評価はtarget identity、CI成功、database isolation、scope、digest assertionの弱さ、`SyncTextWriter` semanticsでは概ね一致している。
2. 唯一merge判断を反転させる相違は、Testcontainers disposal failure後の内部disposed-state latchである。
3. 一次source上、同じcontainer instanceでのdispose retryは成立しないため、現時点のcanonical判定は**REQUEST CHANGES / Major 1**が妥当である。
4. 17 reviewerは全件このMajorを見逃しており、canonical normalizationでは全件`FN=1`となる。
5. Claude版のreviewer相対評価とruntime probe分析は依然として有用であり、別評価文書として併存保存する。
6. PR #104の修正後にReference Reviewとランキングを再評価する場合は、raw reviewer結果を変更せず、新しいtarget Head / RUN_IDで別benchmarkとして実施する。
