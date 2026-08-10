# FND-04 Retrospective — Claude Opus 5

```yaml
MODEL: "Claude Opus 5"
HARNESS: "Claude Code"
EFFORT: "xHigh"
MODEL_SLUG: "claude-opus5-xhigh-claudecode"
TARGET_ISSUE: 42
FINAL_PR: 140
ANALYSIS_DATE: "2026-08-10"
SCOPE: "process retrospective (not a code review)"
INDEPENDENCE: "docs/retrospectives/ に他モデルのFND-04 retrospectiveは存在しなかった（作成時点）。他モデルの振り返り結果は一切参照していない。"
```

対象:

```yaml
REPOSITORY: kooiei-in4a/minimal-bank-system
TARGET_ISSUE: 42
FINAL_PR: 140
FINAL_REVIEWED_HEAD: 3511688401533f60bb77c7dcc647c4c2c4aa84c6
FINAL_MERGE_COMMIT: 9a352a3a61945647273ccc7dfbc8e1816c3ca07c
BENCHMARK_CONTROL_BRANCH: agent/fnd04-benchmark-control
COMMON_BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
```

---

## 1. Executive Summary

FND-04は、**プロセスとしてFND-03より明確に改善した**。ただし改善の主因は「候補数を14から8へ減らしたこと」ではない。一次証拠が示す主因は次の3点である。

1. **review roundとMajor fix roundの縮小**。FND-03は独立reviewer 17名、Major fix roundを14候補+3 Judgeで実施した（`docs/benchmarks/fnd03-model-comparison/review-benchmark/run.json`、`final-fix/run.json`）。FND-04は独立reviewer 5名、Major fixは**単一実装者による1回のtargeted fix + 2名のtargeted re-review**で完結した（`review-benchmark/run.json`）。実行回数でおよそ17→3である。
2. **品質が落ちるどころか上がった**。FND-03では17 reviewer全員がmerge-blocking root causeを見逃し、Goldは`post_hoc_adjudication`として事後確定された（`independent-review-benchmark-protocol.md` §16、`fnd03-.../review-benchmark/README.md`）。FND-04では**run中に** blocking Major（G-01 / NR-01）が発見され、Judge A/Bがblind Phase Aで独立再現し、merge前に解消された。
3. **決定的だったのは reviewer数でも role 分散でもなく、「mutationを実際に実行したこと」**。G-01をMajorとして提出したのはR2（Claude Opus 5 / Claude Code / `deep_technical_test_assurance`）のみ。R5がMinor、R4は本文で言及のみ、R1/R3は指摘なし（`finding-normalization-prejudge.md`）。R2は`Host=db;Database=ambient_fallback`への退行注入と`--no-build`到達不能化という2つのmutationを実行して初めてfalse assuranceを立証した。

最も重要な反証的事実がある。**R1（GPT-5.6 Sol / Codex）はG-01を見逃したが、同一Model+HarnessであるJudge A（GPT-5.6 Sol / Codex）はblind Phase Aで同じmutationを独立再現しG-01へ到達した**（`gold-review.md`）。同じモデル・同じ対象で結果が割れた原因はモデル能力ではなく、**prompt構造とrole framing**である。Reviewer promptはP10で「固定禁止文字列の非出現だけで destination safety を証明したことになっていないか」と*名指しで警告していた*にもかかわらず、5名中2名が完全に見逃した。

したがってFND-04の最大の知見は次である。

> **prompt に「何を疑え」と書いても検出されない。検出されるのは「実際に壊してみた」reviewerだけである。**

一方、明確に過剰なものも特定できた。`agent/fnd04-benchmark-control` はFND-04期間中に**docs-only commit 72件、CI run 73件（うち51件がcancelled）** を発生させた。これはFND-04のproduct/candidate branch全体のCI run合計38件の約2倍である。さらに現在の`main`が保持するFND-04 run.jsonは`schema 1.1 / prepared_not_started`のままで、実際の完了状態（control branch上の`schema 1.9`）と矛盾している。**正本がmainに無い**ことは証拠管理上の実害である。

総合判定: 現行方式は**製品デリバリのパイプラインとしては過剰、開発手法検証benchmarkとしてはほぼ適正**。リポジトリの目的が「開発手法検証用の内部デモ」である以上、研究計装そのものが成果物なので**全体としては「適正」**と判断する。ただしartifact管理とcontrol-branch CIは無条件に「過剰」である。

---

## 2. FND-04 Timeline

すべてUTC。GitHub timestampsは*coordinator wall clock*であり、**agent処理時間ではない**（run.jsonが明示的に禁じている）。

| 時刻 | 工程 | 一次証拠 |
|---|---|---|
| 08-09 10:27 | Issue #128 起票（FND-03振り返りをFND-04方法論へ反映） | Issue #128 created_at |
| 08-09 10:34 | PR #129 作成 — 方法論docs更新 | PR #129 |
| 08-09 10:41 | **Issue #42 Issue Ready = PASS / Implementation = PERMITTED** | Issue #42 comment 5231080595 |
| 08-09 10:46 | PR #130 — pre-run lock | PR #130 |
| 08-09 10:54 | **Benchmark Pre-Run Lock**（common base `38c07e2`、8 branch identical、`candidate_execution: not_started`） | Issue #42 comment 5231132126 |
| 08-09 11:06 | candidate effort lock（run.jsonにeffort未記録の不整合を実行前に補正） | Issue #128 comment 5231183433 |
| 08-09 11:31–12:42 | **H0実行 — 8 candidate Draft PR #131–#138 作成** | PR created_at |
| 08-09 13:05 | **H0 lock 8/8 / exact-head CI success 8/8**。同時にduration計測の全run中止を決定 | Issue #128 comment 5231669763 |
| 08-09 13:05–15:13 | **Formal Self-Review 8/8 → H1 8/8** | run.json `formal_self_review_lock`, `h1_lock` (2026-08-10T00:13+09:00) |
| 08-10 01:51 | **Implementation Evaluation LOCKED**（H1 winner C5、H0 winner C1、merge-ready 7/8） | `results/implementation-evaluation.md` |
| 08-10 02:07 | **Selection / Adjudication LOCKED**（primary C5、partial C1、C8-M01 mandatory guard） | `results/selection-adjudication.md` |
| 08-10 02:25–02:54 | **Final Synthesis 実装（29分・author記録）** | PR #140 body `DURATION_MINUTES: 29` |
| 08-10 02:51 | PR #140 作成 / Head `99cee438` | PR #140 created_at |
| 08-10 03:20 | **Reviewer pool revision 2**（6枠→5枠、実行前に改訂） | `reviewer-pool-revision-2.md` |
| 08-10 03:20–05:16 | **role-diverse independent review 5/5 → normalization → Judge A/B** | `finding-normalization-prejudge.md` |
| 08-10 05:16 | **Gold / Reference LOCKED — CHANGES_REQUIRED / G-01 Major / merge-ready NO / Judge C不要** | `gold-review.md` |
| 08-10 05:28–05:58 | **targeted Major fix（30分・author記録）** | `major-fix-snapshot.md` |
| 08-10 05:54 | fix commit `3511688` — test-only / 1 file / +18 / -0 | PR #140 commits |
| 08-10 05:59 | Major fix snapshot LOCKED | `major-fix-snapshot.md` |
| 08-10 06:55 | **targeted re-review 2/2 → G-01 CLEARANCE PASS** | `major-fix-clearance.md` |
| 08-10 07:22 | **Formal Agent B review 4894487758 提出 — APPROVE / B0 M0 / GitHub event = COMMENTED** | PR #140 reviews |
| 08-10 07:40 | **PR #140 MERGED** — merge commit `9a352a3` | PR #140 merged_at |
| 08-10 07:42 | **Issue #42 CLOSED / COMPLETED** + close evidence comment | Issue #42 comment 5237276272 |

Issue Ready から Issue Close まで **約21時間1分**（うち夜間の中断を含む）。

### 各工程がなぜ存在したか / 前工程のどのリスクを潰したか

| 工程 | 潰したリスク | 実際に価値を生んだか |
|---|---|---|
| Issue Ready / fixed contract | 候補ごとに異なる前提で実装し比較不能になる | **YES**。全8候補がversion pin・connection key・60秒budget・empty baselineで一致。契約解釈の争点はrun中ゼロ |
| Benchmark pre-run lock | 結果を見てから条件を変える汚染 | **YES**。8 branch identical / `not_started` を実行前に固定。effort不整合も*実行前*に補正（comment 5231183433） |
| H0 | self-review後の実装と初回実装が混ざり、self-review能力が測れない | **YES**。Self-Review Gain（0〜+3）が測定可能になった |
| Formal Self-Review | 実装者自身の説明を証拠として扱ってしまう | **部分的**。C6が自分のfalse assuranceを除去（+3）、C8は自分のMajorを見逃した |
| H1 | SR findingを無批判に自動採用する | **YES**。C5 SR-01 / C6 SR-01の2件がover-strictとして正当にreject。disposition記録が残った |
| Implementation Evaluation | 「CI greenだから良い」判断 | **YES**。8/8 CI success下でC8-M01というMajorを摘出 |
| Selection / Adjudication | 上位候補をそのままmerge/cherry-pickして設計が混濁する | **YES**。C5 primary + C1 partial + C6明示非採用 + C8-M01 mandatory guardという構造化された選択 |
| Final Synthesis | candidate branchのmergeによる出所不明の実装 | **YES**。29分・25 files・+1167/-1でBlocker 0。以後の唯一のMajorは*mandatory guardの仕様*に起因 |
| role-diverse review | FND-03の「同質reviewer 17名全員見逃し」の再演 | **YES（ただしn=1）**。G-01発見 |
| Judge A/B | reviewer多数決でMinorへ落とす／1名のMajor主張で過剰反応する | **YES**。1/5しかMajorを出していない指摘を、blind Phase Aで独立再現してblocking確定 |
| Gold | Judge出力が分散したまま修正指示が曖昧になる | **YES**。required fixの方向とmutation sensitivity 3条件を明文化 |
| targeted Major fix | 全candidate再実行という17実行のコスト | **YES**。1実行30分・test-onlyで完了 |
| targeted re-review | fix自体の新規Major混入 | **YES**。2名がM1/M2/baseline/recoveryを独立再現、new Blocker/Major 0 |
| Formal Agent B | benchmark結論をproduct merge判断へ流用する | **YES（後述4.7）**。他工程が誰もやらなかったbase main CIとのtest件数照合を実施 |
| exact Head / merge-ref CI | merge-ref runをdirect-head CIと誤認する | **YES**。実際に誤認が発生し（G-03/NR-04）、reviewerが検出、supplementで解消 |

---

## 3. What Was Carried Forward from FND-03

Issue #128 本文の "Key decisions already approved by Koo" が、FND-03→FND-04の継承・変更の一次証拠である。

### 3.1 継承 — exact Head / merge-ref CI identity の分離

```text
FND-03での問題: PR CI・push CI・merge-ref CIの区別が曖昧なまま「CI成功」と記録され得た
↓
導入された対策: exact Head SHAへ紐づくrunをidentityごとに分けて記録する規約
↓
FND-04での利用: 全8 candidateでexact-head CI runを記録。Final Synthesisも direct-head と merge-ref を別に記録
↓
効果: 【実効あり・しかも必要性が実証された】Final Synthesis PR本文が merge-ref run 31350916189 を "Exact Head CI" と誤記。
      reviewer R1/R2/R3/R5 が別のpush run 31350870902 を特定し、coordinatorが再取得して
      `final-synthesis-ci-supplement.md` で解消。この規約が無ければ誤記のまま通っていた
```

### 3.2 継承 — Final Synthesis は candidate merge / cherry-pick 禁止

```text
FND-03での問題: 上位候補をそのまま採用すると設計の出所と責任が曖昧になる
↓
導入された対策: current mainからcurated re-implementationを行うFinal Synthesis方式
↓
FND-04での利用: selection-adjudication.json に candidate_merge: PROHIBITED / candidate_cherry_pick: PROHIBITED を明記
↓
効果: 【実効あり】PR #140は2 commitのみ。Base→Head 25 files / +1167 / -1 が完全に説明可能。
      Formal Agent Bが独立にdiff件数一致を確認できた
```

### 3.3 継承 — Agent B を product merge gate として benchmark と分離

```text
FND-03での問題: benchmark rankingとmerge判断が混ざるリスク
↓
導入された対策: independent-review-benchmark-protocol.md §3「Separation from formal Agent B review」
↓
FND-04での利用: Formal Agent Bをfresh contextで最後に実行し、
                「benchmark多数決・clearance artifactを判断根拠にしない」と prompt で明示
↓
効果: 【実効あり】Agent B reviewは実際に MIN-01 / MIN-02 という新規findingを出し、
      さらに base main run 31309214350 との test件数差分照合（Unit +1 / non-PG +11 / PG +9）という
      他工程が一度も行っていない検証を実施した
```

### 3.4 継承 — 実PostgreSQL fixture（FND-03の成果物そのもの）

```text
FND-03での問題: SQLite / InMemory代替でprovider固有の挙動を検証したことにしてしまう
↓
導入された対策: PostgreSQL 18.4 / Testcontainers 4.13.0 fixture
↓
FND-04での利用: assumption-ledger A-11 で「SQLite/InMemoryへ置換しない」を固定。
                clean apply / rerun / failure / no-auto-migration / 実60秒timeout をすべて実PostgreSQLで検証
↓
効果: 【実効あり・かつ品質差の主要因】implementation-evaluation の Findings Matrix で、
      「real PostgreSQL lock + real processで60s budget発火」を達成したのはC1/C5のみ。
      残りは pre-cancelled token / fake delegate 中心で "weaker evidence" と分類され、
      これが順位を分けた主要軸になった
```

### 3.5 継承 — green CI ≠ failure-path correctness

```text
FND-03での問題: Major fix round 14候補すべてがCI success、しかしmerge-readyは1/14
↓
導入された対策: evaluator-only probes / assumption ledger を実装前に固定する規約（methodology §21）
↓
FND-04での利用: fnd04-evaluator-probes-v1（P-01〜P-11）を候補実行前にLOCK
↓
効果: 【実効あり】FND-04でも exact-head CI は 8/8 success。それでもC8-M01 Majorが残った。
      implementation-evaluation.md が「FND-04でも green CI != failure-path correctness が再確認された」と明記
```

### 3.6 継承 — product merge と benchmark archive の分離（methodology §24）

```text
FND-03での問題: archive作業（tag 28本・PR close 27件・branch削除28本）が次Issue開始をブロックする
↓
導入された対策: methodology §24 で product完了とarchive完了を別gateに
↓
FND-04での利用: FND-03 archive作業（agent/fnd03-benchmark-archive* 等）がFND-04実行と同一時間帯で並走
↓
効果: 【実効あり、ただし副作用あり】並走は成立した。
      一方でFND-04終了時点でも candidate branch 8本 / Draft PR #131–#138 / control branch が未整理のまま、
      かつ main の run.json が pre-run scaffold（prepared_not_started）のまま取り残された。
      「critical pathから外す」ことと「正本を放置する」ことが区別されていない
```

---

## 4. What Changed in FND-04

Issue #128 が承認済み変更として列挙した7点＋実行中に発生した2点。

### 4.1 candidate 14 → 8

| 項目 | 内容 |
|---|---|
| 変更前 | FND-03: 計画14 / 採点13 / 1件no-change |
| 変更後 | FND-04: 8（active 6 + challenger 2）、reserve 3、suspended 3をpool lifecycleとして分類 |
| 狙い | 実行コスト削減、低品質candidateの反復投入の停止 |
| 実際の結果 | merge-ready 7/8。スコア分布 80 / 89–93 / 95–99。**上位5候補が92〜99に密集**し識別力は低下。ただしMajorは1件（C8）出た |
| 副作用 | Open Code / challenger枠を残したおかげで**唯一のMajorがC8 challengerから出た**。強い候補だけに絞っていたらC8-M01は存在せず、それに由来するmandatory guardもG-01も発生しなかった |
| 残すべきか | **残す（6へ微調整）**。後述4.1詳細 |

### 4.2 H0 → Formal Self-Review → H1 の三段化（新規）

| 項目 | 内容 |
|---|---|
| 変更前 | FND-03: 実装＝1スナップショット。self-reviewは実装時間に埋没 |
| 変更後 | H0固定 → 同一Model+Harnessのfresh context review-only → accepted findingのみ修正しH1 |
| 狙い | implementation能力とself-review能力の分離測定 |
| 実際の結果 | SR finding計6件 / accepted 4 / rejected 2。Gain合計+7点。**4/8がfinding 0件でH1のcode changeもゼロ** |
| 副作用 | finding 0のcandidateでもH1を明示実行するルールのため、成果ゼロの実行が4件発生 |
| 残すべきか | **残すが変更**（後述4.2詳細） |

### 4.3 review: 同質17名 → role分散5名

| 項目 | 内容 |
|---|---|
| 変更前 | FND-03: 同一prompt×17 reviewer。**全員がmerge-blocking root causeを見逃し**、Goldは事後裁定 |
| 変更後 | R1 runtime/failure、R2 deep technical/test assurance、R3 spec/scope、R4 framework official-source、R5 fast practical の5枠 |
| 狙い | 同質視点の量的増加より観点の異質性 |
| 実際の結果 | **G-01をrun中に発見**。ただし発見者は1/5 |
| 副作用 | 実行前にpool revision 2で6→5へ再削減（`reviewer-pool-revision-2.md`）。方法論§16.1の「約6枠」と実運用が既に乖離 |
| 残すべきか | **残すが変更**（後述4.3詳細） |

### 4.4 Judge 3名常設 → 原則2名 + 条件付き3人目

| 項目 | 内容 |
|---|---|
| 変更前 | FND-03 Major fix roundは3 Judge常設（`final-fix/judges/`） |
| 変更後 | Judge A/B、verdict・blocking root cause・merge-readyのいずれかが不一致のときのみJudge C |
| 狙い | 一致する見込みが高い局面で3人目を払わない |
| 実際の結果 | **quorum完全一致（CHANGES_REQUIRED / NR-01 / NO）でJudge C不使用** |
| 副作用 | 一致の質は高かったが、独立性は完全ではない（Judge B = R2と同一Model+Harness） |
| 残すべきか | **そのまま残す**（後述4.4詳細） |

### 4.5 Gold を blind pre-locked 方向へ（Judge Phase A方式）

| 項目 | 内容 |
|---|---|
| 変更前 | FND-03 Goldは`post_hoc_adjudication`。reviewer結果を見た後に一次source突合でMajorを明確化 |
| 変更後 | Judge promptがPhase A（raw reviewerを読まずに独立Reference作成）→ Phase B（裁定）を強制 |
| 狙い | reviewer出力へのanchoringの排除 |
| 実際の結果 | **決定的に有効**。Judge A/Bともに Phase A で off-blocklist mutation と factory-unreachable mutation を独立再現。多数決なら NR-01 は Minor 相当で終わっていた（Major主張は1/5） |
| 副作用 | Judge 1名あたりのコストがreviewerより高い |
| 残すべきか | **必ず残す。FND-04で最も効いた設計** |

### 4.6 Major fix: 14候補benchmark → 単一targeted fix

| 項目 | 内容 |
|---|---|
| 変更前 | FND-03: Major fixを14候補で再benchmark + 3 Judge。merge-ready 1/14 |
| 変更後（方法論§22） | 上位4候補程度へ絞る |
| 実際の運用 | **さらに絞って1実装者・1回**。30分・test-only・1 file・+18/-0で完了、再修正ゼロ |
| 狙い | Major修正は「探索」ではなく「Goldが指定した方向への最小適用」である、という再定義 |
| 実際の結果 | 一発クリア。targeted re-review 2/2が独立にM1/M2 sensitivityを再現 |
| 副作用 | 方法論doc（≤4候補）と実運用（1候補）が乖離。次回の判断基準が文書上不明確 |
| 残すべきか | **残す。ただし方法論docを実運用に合わせて改訂すべき** |

### 4.7 duration計測の中止（実行中に発生した変更）

| 項目 | 内容 |
|---|---|
| 変更前 | FND-03: 全candidateのduration_minutes（自己申告）を収集し、Speed Score / Quality-Time Indexを算出 |
| 変更後 | H1にのみ`h1-execution-wrapper.md`でepoch計測を試行 |
| 実際の結果 | **失敗**。8 candidate一貫収集が成立せず、run全体でSpeed Score / Quality-Time Index / Practical Score speed componentを**算出中止** |
| 評価すべき点 | **推測で埋めなかったことが正しい判断**。run.jsonが `"do not infer from GitHub timestamps"` を明記 |
| 残すべきか | **計測方式を変更して残す**。FND-03の「PR本文自己申告」方式は14/14収集できていた。凝った方式が退行を招いた |

### 4.8 Controlled Mutant arm（設計されたが未実行）

`independent-review-benchmark-protocol.md` §16.3 と `evaluator-probes.md` は Controlled Mutant（正しいsnapshotへ既知欠陥を注入したreview専用target）を規定したが、**FND-04では実行されなかった**（control branchにmutant artifactが存在しない）。結果として、reviewerのFalse Negative率は測定できていない。G-01を2/5が完全に見逃した事実は判明したが、それはたまたまG-01が実在したからであり、体系的なrecall測定ではない。

### 4.9 GitHub review state と技術approvalの分離（発生事象）

Formal Agent BがAPPROVEを試み、GitHubが `422 Review Can not approve your own pull request` で拒否 → event `COMMENTED`。FND-03でも同一事象（`final-outcome.md`: "Agent B GitHub state: COMMENTED — self-approval restriction"）。**2回連続で同じ制約に当たっており、これは偶発ではなく構造的制約である**。

---

## 5. Evaluation of New Experiments

### 5.1 8 candidateへの削減 — 詳細評価

**モデル多様性は十分だったか: 十分**

投入構成は Model 6種（GPT-5.6 Sol / Terra / Luna、Claude Opus 5 / Sonnet 5、Grok 4.5、DeepSeek V4 Flash）× Harness 4種（Codex / Claude Code / Cursor / Open Code）。GPT-5.6 Luna は Codex と Open Code の両方に配置され、**同一モデル×異Harness比較が1組維持されている**（H1: Codex 90 / Open Code 91）。多様性設計としては8枠で十分機能した。

**情報量は不足しなかったか: 上位帯では不足した**

H1スコア: 99, 98, 98, 93, 92, 91, 90, 80。上位3件が98–99で密集し、C1とC6は98同点。tie-breakは「real PostgreSQL lock + real Migrator processで実60秒budget failureを駆動しているか」という**単一の証拠強度軸**で行われた。これはrubric（100点満点8軸）の分解能が上位帯で足りていないことを意味する。候補を増やしても解決しない（rubricの問題）。

**実行コスト削減効果: 実装phaseでは限定的**

- FND-03実装phase: 13実行
- FND-04実装phase: 8×3（H0/SR/H1）= 24実行

**候補数は減ったが実装phaseの実行回数は約1.8倍に増えている。** 「8候補にしたからコストが下がった」は事実ではない。総コストが下がったのは review 17→5 と Major fix 17→3 の効果である。

**上位モデルの識別能力: 実装スコアでは低い、証拠強度では高い**

スコアでは C1/C5/C6 が識別困難。しかし `Findings Matrix` の軸（60s budget発火の証明方法、DbContext resolve後のschema不変確認、rerun regression、SR精度）では明確に分離した。**識別しているのは総合点ではなくmatrixである。**

**弱いcandidateを大量投入する価値: FND-04では正の価値があった**

challenger枠のDeepSeek V4 Flash（80点・rank 8）が**run唯一のMajor C8-M01**を生んだ。そのC8-M01がSelection/Adjudicationで「mandatory guard必須」となり、そのguardの仕様不足がG-01となり、G-01がFND-04最大の学習になった。**弱い候補は「品質比較」ではなく「失敗パターンの供給源」として価値がある。** ただしFND-03のsuspended 3件（MiniMax M3 / MiMo-V2.5 / MiMo-V2.5-Pro）を外した判断は妥当で、no-result / 反復Majorの候補は供給源にすらならない。

**6 / 8 / 10 のどれが妥当か → 6**

根拠:
- 8のうち C2 / C3 / C7 は Major 0・SR finding 0・Selection採用要素 0 で、Findings Matrix上も "weaker evidence" 群に一括分類された。**この3件が無くても Implementation Evaluation の結論（C5 primary / C1 partial / C6非採用 / C8-M01 Major）は1つも変わらない**。
- 一方 challenger 枠は削れない（C8がMajor供給源）。
- 推奨構成: active 4（Claude系2 + GPT/Codex系2）+ challenger 2 = **6**。実行回数 6×3 = 18（−25%）。

### 5.2 H0 → Formal Self-Review → H1 — 詳細評価

**H0からH1で何が変化したか（一次証拠）**

| Candidate | SR verdict | findings | accepted/rejected | H1 code change | Gain |
|---|---|---|---|---|---:|
| C1 gpt-5.6-sol-codex | NO CHANGE | 0 | – | NONE (head==H0) | 0 |
| C2 gpt-5.6-terra-codex | NO CHANGE | 0 | – | NONE (head==H0) | 0 |
| C3 gpt-5.6-luna-codex | NO CHANGE | 0 | – | NONE (head==H0) | 0 |
| C4 gpt-5.6-luna-opencode | FIX REQUIRED | 1 Minor | 1/0 | tests_only | **+2** |
| C5 claude-opus-5-claude-code | NO CHANGE | 2 (Minor+Nit) | 1/1 | comment_only | **+1** |
| C6 claude-sonnet-5-claude-code | FIX REQUIRED | 2 Minor | 1/1 | production_testability_plus_tests | **+3** |
| C7 grok-4.5-cursor | NO CHANGE | 0 | – | NONE (head==H0) | 0 |
| C8 deepseek-v4-flash-opencode | NO CHANGE | 1 Nit | 1/0 | one_line_cleanup | **+1** |

**Self-Reviewは本当に不具合を減らしたか: 1件だけ、しかし本質的な1件**

C6のSR-02が「60秒bounded timeout testの証拠強度が弱い」を検出し、H1で production `MigrationRunner` の CTS が正確に60秒budgetをscheduleし、その発火でdelegate tokenがcancelされexit non-zeroになることを決定論的に証明した（+3、run中最大Gain）。**これは実質的なfalse assuranceの除去である。**

対して C2 / C3 / C7 のfinding 0は evaluator Minorを見逃した false negative、C8 は Nit（未使用using）を直して **自分のMajorを見逃した**。実装評価は「C8はSRでMinorを発見してもMajorを見逃した = run中最大のSR failure」と記録している。

**単なる追加修正時間になっていないか: 4/8ではなっていた**

C1/C2/C3/C7はH1でコード変更ゼロ。H1 wrapper は「finding 0でもH1 phaseを明示実行」「empty commit禁止」「CI再利用可」と定めているため、**4実行が純粋な記録作業に費やされた**。

**Self-Review能力をモデル能力として測れるか: 測れる。ただしFinding件数では測れない**

一次証拠が示す3類型:
- **正当なゼロ**: C1（finding 0、H0時点で98点、evaluator Minor 0）→ finding 0が正しい
- **見逃しのゼロ**: C2 / C3 / C7（finding 0だが evaluator Minorあり）→ false negative
- **表層のみ**: C8（Nitを直しMajorを見逃す）→ 最悪パターン

つまり **SR品質 = (evaluatorのfinding集合) と (SRのfinding集合) の一致度**であり、件数は指標にならない。scoring.md §"Self-Review metrics" が「Finding件数自体は加点しない」と定めていたのは正しい。

**implementation能力とself-review能力を分離評価すべきか: すべき。既に分離した価値が出ている**

H0 winner = C1（gpt-5.6-sol-codex）、H1 winner = C5（claude-opus-5-claude-code）、SR Gain winner = C6（claude-sonnet-5-claude-code）。**3つとも別のcandidateである。** 1つのTotalでは絶対に見えない。この分離だけでもH0/SR/H1導入は正当化される。

**次回も全candidateで必須にする価値があるか: 条件付きでYES**

必須にする価値はあるが、**finding 0のH1を独立実行にする必要はない**。SR verdictが `NO CHANGE` の場合、coordinatorが「H1 Head = H0 Head / code_change NONE / CI再利用」を記録して閉じれば、失われる証拠は無い（C1/C2/C3/C7で実際にそうなっている）。これで4実行削減。

### 5.3 Role-diverse Independent Review — 詳細評価

**reviewer数を減らしてもcoverageを維持できたか: できた。ただし薄氷**

| Slot | Reviewer | Role | Verdict | B/M/m/N | NR-01検出 |
|---|---|---|---|---|---|
| R1 | GPT-5.6 Sol / Codex | runtime_failure_path | APPROVE_WITH_FINDINGS | 0/0/1/1 | ✗ |
| R2 | Claude Opus 5 / Claude Code | deep_technical_test_assurance | **CHANGES_REQUIRED** | 0/**1**/1/2 | **✓ Major** |
| R3 | GPT-5.6 Luna / Codex | specification_scope | APPROVE_WITH_FINDINGS | 0/0/1/0 | ✗ |
| R4 | ChatGPT Opus 5.6 Sol / Browser | framework_official_source | APPROVE | 0/0/0/0 | △ 本文言及のみ |
| R5 | Cursor Auto / Cursor | fast_practical | APPROVE_WITH_FINDINGS | 0/0/2/0 | △ Minor |

FND-03の17名は全員見逃した。FND-04の5名は1名が捕まえた。**coverageは維持されたが、検出はn=1である。**

**同質reviewerを増やすより価値があったか: 反証がある**

R1 = GPT-5.6 Sol / Codex は見逃した。しかし Judge A = **同一の GPT-5.6 Sol / Codex** はblind Phase Aで同じmutationを独立再現しG-01へ到達した（`gold-review.md`「Judge A/Bは互いの結果を読む前のPhase Aで、少なくとも次を独立再現した」）。

**同じモデル・同じHarness・同じ対象で、prompt構造だけが違うと結果が割れた。** したがって「role分散がG-01を発見させた」とは一次証拠から断定できない。断定できるのは次である。

- reviewer promptのP10は「testが単にerror messageに禁止文字列がないことだけを見て、実destination safetyを誤って証明したことになっていないか」と**G-01の根本原因をほぼ名指しで書いていた**。それでも2/5が完全に見逃した。
- 発見したR2とJudge A/Bに共通するのは **role** ではなく **mutationを実際に実行したこと**。
- R1のrole（runtime_failure_path）は、むしろ注意をtest oracleの感度から逸らした可能性がある。

**実際にG-01発見へ寄与した構造は何か: 「壊して確かめる」実行**

R2の証拠は「`UseBankPostgreSqlModelOnly()` を `Host=db;Database=ambient_fallback` へ改変 → 実際に `server 'tcp://db:5432'` へ接続しに行くのにcommitted testは合格」「build outputを退避 → factory到達不能でもcommitted testは合格」という**実行結果**である。静的読解では出せない。

**reviewer roleを固定すべきか: role固定より「実行要件」の固定が優先**

FND-05の推奨は role の細分化ではなく、**出力スキーマに mutation 実行結果を必須フィールドとして持たせること**。少なくとも test assurance 枠は「mutationを実行し、baseline / mutant / recovery / residue を報告する」を PASS 条件にする。

**モデル能力とreview role適性を分離すべきか: 現時点では分離できない**

FND-04のデータでは、同一モデルがrole/promptによって結果を変えている。よって「このモデルはこのroleに向く」を主張する材料はない。分離評価を始めるには **Controlled Mutant arm（§16.3、FND-04では未実行）が必要**。

### 5.4 Judgeを原則2名にしたこと — 詳細評価

**2 Judgeで十分だったか: 今回は十分**

Judge A/Bが `REFERENCE_VERDICT` / `BLOCKING_ROOT_CAUSES` / `MERGE_READY` の3キー全一致。Judge C不要。しかも一致は「同じ結論に賛成した」ではなく、**両者がPhase Aで独立にmutationを再現した**上での一致であり、質の高い一致である。

**3 Judge常設より効率的か: YES**

FND-03のMajor fix roundは3 Judge + 14候補 = 17実行。FND-04は2 Judge。Judge Cの発動条件が明確（verdict / blocking root cause / merge-ready のいずれか不一致）なので、必要時にのみ払う設計が機能した。

**Judge間一致をどこまで信頼できるか: 今回は高いが、独立性は完全ではない**

- Judge B = Claude Opus 5 / Claude Code = **R2と同一Model+Harness**。R2がMajorを出した根本原因にJudge Bが到達するのは、独立性の観点で最も弱いリンク。
- 一方 Judge A = GPT-5.6 Sol / Codex は、**同一Model+HarnessのR1がその指摘を出していない**。したがってJudge Aの到達は明確に独立な確認である。
- 結論: **quorumの信頼はJudge Aが担保している。Judge Bは補強**。

**独立Judgeが同じ誤りをするリスク: 構造的に残る**

Phase A blind を課しても、両Judgeが同じ一次証拠（Issue #42、同じdiff、同じCI log）を読む以上、契約解釈の系統誤差は共有される。FND-04ではG-02（60秒二重deadline / exit taxonomy coupling）について Judge A が Nit、Judge B が Minor と割れており、**Goldは「READMEで0/1/2をdeployment-facing contractとして公開している点を重く見てMinor」と理由付きで裁定した**。この不一致処理が記録されていることは健全である。

**Judge C発動条件を改善すべきか: 1点だけ追加すべき**

現行3キーに加えて、**「blocking root causeの required fix 方向が実質的に矛盾する場合」** を発動条件へ追加すべき。verdictとroot causeが一致しても修正方向が割れると、targeted fixが空回りする。FND-04では発生しなかったが、targeted fixを1候補・1回に絞った以上、方向が誤ると手戻りコストが直撃する。

### 5.5 Targeted Re-Review — 詳細評価

**full re-reviewより効率的だったか: 圧倒的に**

- full re-review相当: 5 reviewer + 2 Judge + Gold再固定 ≈ 8実行
- 実施: 2 reviewer + clearance 1 ≈ 3実行

しかも対象diffは1 file / +18 / -0。full re-reviewは対象に対して明らかに過剰だった。

**regression検出能力は十分だったか: 十分。ただし「fixがtest-onlyであること」が前提**

T1（GPT-5.6 Sol / Codex / xHigh）とT2（Cursor / Auto）が、いずれも独立に
`baseline PASS → M1 FAIL → M2 FAIL → recovery PASS → residue NONE` を再現し、
old→new delta が 1 commit / 1 file / +18 / -0 / production変更なし であることを再確認した。
その上で Formal Agent B が3度目の独立再現を行った。**同一mutationを3回実行しており、ここは明確に冗長。**

**何件のreviewerが適切か: 2件が適切。ただし1件でも成立し得た**

production code変更ゼロ・test-only・18行という条件下では、2件は保守的すぎる可能性がある。ただしT2がCursor Autoという「実務routing」枠で、T1と異なる経路の確認になっている点は価値がある。**2件維持を推奨するが、Formal Agent BによるG-01再検証は省略してよい。**

**Major severityによってreview範囲を変えるべきか: 変えるべき。基準は severity ではなく diff 種別**

FND-04の証拠が示す妥当な分岐:

```text
production code 変更なし / test-only / 1 file
  -> targeted re-review 2名 + clearance
production code 変更あり / 単一module
  -> targeted re-review 2名 + 該当pathのfull probe再実行
production code 変更あり / 複数module or architecture変更
  -> full review round 再実行（reviewer 5名 + Judge 2名）
```

severityではなく **blast radius** で決めるべきである。G-01はMajorだったが blast radius はテスト1本だった。

### 5.6 Formal Agent B — 詳細評価

**benchmark結果に引っ張られない独立性: 確保された**

prompt が「benchmarkの多数決・candidate ranking・model score・clearance artifactは merge判断の根拠にしない」と明示し、review本文も `benchmark artifact（major-fix-clearance.md 等）は supplemental としてのみ参照し、判断の根拠にしていません` と記録している。

**Gold / Judgeとの役割重複: 一部重複、しかし固有の価値あり**

重複した部分:
- G-01のM1/M2 mutation再現（Judge A/B、T1/T2に続き**3回目**）

重複しなかった固有の価値（**他のどの工程も実施していない**）:
1. **base main run 31309214350 との test件数差分照合**: Unit +1、non-PG +11（MigrationModelTests 10 + DesignTimeConnectionSafetyTests 1）、PG +9（MigrationBaselineTests 9）が正確に一致し、**FND-03の既存testが1件も失われていない**ことを証明。
2. **real PostgreSQL step所要の整合確認**: base 17s → head 1m22s。実時間60秒timeout testが実際に走ったことと整合。
3. **isolated copy（`git archive`）での完全再実行**: build / non-PG / real PG / model drift probe / idempotent SQL CLI。
4. **新規finding MIN-01 / MIN-02**: 「Migrator failure testが失敗理由をpinしていない」「idempotent SQL CLIがCIで実行されていない」。いずれもGold / Judge / reviewerが出していない。

**product merge gateとして必要だったか: 必要だった**

MIN-01は特に示唆的である。G-01（design-time側のfalse assurance）を修正した直後に、**同種の弱さがMigrator failure test側に残っていること**をAgent Bが指摘した。benchmark chainはG-01の周辺しか見ておらず、この一般化を誰も行っていない。

**今後省略可能なケースはあるか: ある**

- product code 変更なしで docs / benchmark artifact のみのPR
- Gold / Judge / targeted re-review が同一Model+Harnessで既に完了しており、かつ diff が単一test file

上記以外では省略すべきでない。少なくとも**「base CIとの test件数照合」だけは常に必要**で、これはAgent Bの役割として明文化する価値がある。

### 5.7 GitHub approvalと技術approvalの分離 — 提案

**発生事実**

```text
Formal Agent B 技術判定       : APPROVE / Blocker 0 / Major 0 / Merge-ready YES
GitHub API 応答               : 422 Review Can not approve your own pull request
GitHub review state           : COMMENTED (review id 4894487758)
Repository ruleset            : 別actor APPROVED state は required ではない
実際のmerge                   : 通常のPR merge経路 / bypassなし / merge commit 9a352a3
```

FND-03（`final-outcome.md`: Agent B review 4890768131 / verdict APPROVE / GitHub state COMMENTED）でも**同一事象**。2回連続であり構造的制約と断定できる。

**今後の標準運用として記録すべきもの（提案）**

3層を必ず別フィールドとして記録する。

```yaml
technical_approval:
  reviewer: "<Model / Harness / Effort>"
  verdict: APPROVE | APPROVE_WITH_FINDINGS | CHANGES_REQUIRED
  blocker: 0
  major: 0
  reviewed_head_sha: "<exact SHA>"
  review_id: <GitHub review id>          # 技術判定の所在を必ずGitHub上に固定する

github_review_state:
  event: COMMENTED | APPROVED | CHANGES_REQUESTED
  event_downgrade_reason: "422 self-approval prohibited (author == authenticated actor)"
  # NOTE: state は technical_approval を意味しない。両者を等値にしない

repository_enforcement:
  separate_actor_approval_required: false
  evidence: "<ruleset / branch protection の確認方法と時刻>"
  required_status_checks: ["build-test"]
  merge_path: "normal PR merge / no bypass"
  bypass_used: false
```

追加の運用ルール（提案）:

1. **technical approval は必ず GitHub review body に記録する**（review idを持たせる）。ローカルレポートだけにしない。FND-04はこれを満たしている。
2. **`repository_enforcement` の確認は merge の直前に毎回取り直す**。FND-04は「直前FND-03で確認済みのrepository ruleset」を根拠にしている（`merge_actions.repository_rule_evidence`: `recent_fnd03_repository_rule_check_plus_normal_pr_merge_enforcement`）。前Issueの確認を流用しているのは、rulesetが変更され得る以上、厳密には弱い。
3. **event downgrade を「制約」として記録し、「合格」とも「未達」とも書かない**。FND-04の記述（`This is treated as a technical product merge gate PASS ... It does not assert that a repository ruleset requiring a non-author GitHub approval has been satisfied.`）はこの分離ができており、そのまま定型文にすべき。
4. 別actor approvalが将来requiredになる場合に備え、**bot account または second account を事前に用意するかどうかをFND-05開始時に一度だけ決定する**。毎回の判断事項にしない。

---

## 6. G-01 / NR-01 Case Study

### 6.1 因果連鎖（一次証拠から再構成）

```text
C8 (DeepSeek V4 Flash) が design-time factory の未設定時に
  Host=127.0.0.1;Port=5432;Database=design_time を fabricate
        ↓  Implementation Evaluation
C8-M01 = Major / MERGE_READY: NO
        ↓  Selection / Adjudication
「Final Synthesisには C8-M01 再発防止testを mandatory guard として必須追加」
  最低条件5項目を明記（child processでenv除去 / production factory使用 /
  non-zero / fallbackしないことを証明 / global env変更禁止）
        ↓  Final Synthesis
DesignTimeConnectionSafetyTests を実装
  = exit != 0 + 6要素の固定blocklist非出現
        ↓  role-diverse review
R2 が mutation 2種で false assurance を実証 → Major
        ↓  Judge A / B（blind Phase A）
両者が独立に同じmutationを再現 → G-01 / NR-01 blocking 確定
        ↓  targeted fix（test-only, +18/-0）
positive failure marker 4種を追加
        ↓  T1 / T2 / Formal Agent B
M1 / M2 で red、baseline / recovery で green を独立再現 → CLEARED
```

**最重要の観察**: Selection/Adjudicationが指定した「最低条件5項目」は、**"何を証明せよ" は書いていたが "どう壊れたら red になるべきか" を書いていなかった**。Final Synthesisは5項目を文言通り満たしている。にもかかわらずtestは無感度だった。

### 6.2 なぜ実装candidate比較では見逃せたか

- C8-M01はcandidate評価時点では**production defect**として摘出された。production codeを読めば見える。
- 一方G-01は**test oracleの感度不足**であり、testを読むだけでは見えない。testが何に対してredになるかは、**壊してみないと分からない**。
- Implementation Evaluationのprobes（P-01〜P-11）は候補のproduction挙動を評価する設計で、**候補のtestが持つmutation感度を測る項目が存在しない**。
- さらにG-01の対象testは candidate 実装には存在せず、**Final Synthesisで新規に作られたもの**である。構造上、candidate比較phaseでは発見しようがない。

### 6.3 なぜFinal Synthesis後reviewで発見できたか

3条件が揃ったため。

1. **対象が存在した**（Final Synthesisで初めて書かれたtest）。
2. **promptが疑うべき対象を名指ししていた**（P10「固定禁止文字列の非出現だけで destination safety を証明したことになっていないか」）。
3. **少なくとも1名が実際にmutationを実行した**（R2）。

3のみが欠けても検出されない。**1と2があっても2/5は見逃した**という事実が、3の決定性を示している。

### 6.4 mutation testing的な発想がどの程度重要だったか

**FND-04で最も重要な単一技法だった。** 根拠:

- G-01の Severity 決定（Minor か Major か）は、`finding-normalization-prejudge.md` で「SEVERITY DISPUTED / JUDGE REQUIRED」だった。決着させたのは Judge A/B が**自ら再現したmutation結果**である。議論ではなく実験が決着させた。
- 修正の完了判定も mutation で行われた。Gold が required fix に「mutation sensitivity 3条件（M1でFAIL / M2でFAIL / discard後PASS）」を明記し、T1 / T2 / Agent B がそれぞれ独立に実行した。
- **findingの提出・severity決定・修正・検収の全4段階が同一のmutation protocolで貫かれている。** これはFND-03には無かった構造である（FND-03のGoldは一次source突合による post_hoc adjudication）。

### 6.5 「testがgreen」だけでは不十分であること

FND-04は同じ命題を2つの独立したレベルで実証した。

| レベル | 事実 |
|---|---|
| candidate | exact-head CI **8/8 success**。しかしC8にMajor（C8-M01）が残存 |
| final synthesis | direct-head CI + merge-ref CI ともに **SUCCESS**、build 0 warnings / 0 errors、real PG 23 pass。しかしG-01（Major）が残存 |

`implementation-evaluation.md` の記述「FND-04でも `green CI != failure-path correctness` が再確認された」は、FND-04ではさらに一段強い形で成立している。

> **green CI ≠ failure-path correctness**（FND-03の教訓）
> **green test ≠ test が failure を検出できる**（FND-04の教訓）

後者はより深刻である。前者は「未テストのpathがある」だが、後者は「テストがあるのに守っていない」であり、**偽の安心を与える分だけ悪い**。

### 6.6 negative testにpositive failure reason pinが必要な条件

一次証拠から導ける必要条件（Goldとfix内容から一般化）:

```text
以下がすべて成立するとき、negative test は positive failure reason pin を必須とする。

(1) test が「失敗すること」を合格条件にしている（non-zero exit / 例外発生 等）
(2) その失敗が、対象pathとは無関係な原因でも起こり得る
    （tool未restore、build失敗、MSBuild評価失敗、process起動失敗、network、timeout）
(3) 守っている契約が failure-safety に属する
    （誤れば意図しないDBへ書く / secretが漏れる / 権限が上がる 等）
```

FND-04の G-01 は(1)(2)(3)すべてに該当した。Formal Agent B の MIN-01（Migrator failure test 2本が非0のみ）は(1)(2)に該当し(3)は別pathで担保されているため非blocking、という判断も同じ枠組みで説明できる。**この3条件は FND-05 の review prompt へそのまま組み込める。**

補助的な指針:
- 固定禁止文字列（blocklist）は**補助assertionにしてよいが主要証拠にしてはならない**。列挙外の値が常に存在する。
- positive markerは「対象pathへ到達した証拠」と「状態が期待どおり（例: destination未構成）である証拠」の**2種類**を要求する。FND-04のfixはまさにこの2種類（`Npgsql` / `Microsoft.EntityFrameworkCore.Migrations` = 到達、`The ConnectionString property has not been initialized.` / `database '' on server ''` = 状態）を追加している。
- 副作用として、Agent Bが NIT-03 で指摘したとおり **provider upgrade時のmaintenance負債**が発生する。これは意図的トレードオフとして記録すべきで、隠すべきではない。

### 6.7 false assurance検出を今後どこへ組み込むべきか

**3箇所に、コスト順で組み込む。**

| 位置 | 内容 | 追加コスト |
|---|---|---|
| **① Selection / Adjudication**（最優先・実質ゼロコスト） | mandatory guard を要求するときは、必ず**同時に mutation sensitivity 受入条件**を書く。「Mを注入したらこのtestがredになること」を仕様の一部にする。FND-04でこれがあればG-01は発生しなかった | 0 |
| **② Formal Self-Review**（低コスト） | SR promptへ「自分が追加したnegative testについて、守るべきdefectを1つ注入してredになることを確認せよ」を追加。C6が自発的にやったことを全candidateへ標準化 | prompt変更のみ |
| **③ Independent Review**（中コスト・出力必須化） | test assurance枠の出力スキーマに `mutation_probes: [{name, injected, expected, observed, residue}]` を**必須フィールド**として追加。空で提出されたreviewは未完了扱い | reviewer 1名あたり数分 |

①が最も費用対効果が高い。FND-04の G-01 は**要求仕様の欠落から生まれた**のであって、実装ミスから生まれたのではない。

---

## 7. Human-in-the-loop Analysis

### 7.1 前提と限界

GitHub上の全event（Issue comment、PR作成、review、merge、close）は単一アカウント `kooiei-in4a` が実行しており、**人間の操作とagentの操作をGitHub側から分離することはできない**。以下は「判断内容の性質」から分類したものであり、操作回数の実測ではない。

### 7.2 人間が必要だった判断（一次証拠あり）

| 判断 | 証拠 | なぜ人間が必要か |
|---|---|---|
| **fixed implementation contract の中身**（EF 10.0.10 / Npgsql 10.0.3 / 60秒 / `ConnectionStrings:Database` / Migrator project名 / empty baseline） | Issue #42 §8、comment 5231080595（2026-08-09） | 技術的に一意でない選択。ADR整合と将来Issue（FND-05以降）への影響を含む製品方針 |
| **プロセス変更の承認**（8候補 / H0-SR-H1 / 約6 reviewer / 2+1 Judge / ≤4 targeted fix / archive分離） | Issue #128 "Key decisions already approved by Koo" | コストと研究価値のトレードオフ。AIには最適化目標が与えられていない |
| **reviewer pool revision 2**（6→5、Sonnet除外、Open Code不使用、Cursor Auto採用） | `reviewer-pool-revision-2.md`（実行前・raw capture 0件時点） | 「高コストでもOpus 5をtest assurance枠に維持する」という費用配分判断 |
| **duration計測を中止し推測で埋めない決定** | Issue #128 comment 5231669763 | データが欠けたとき「埋める／捨てる／やり直す」は研究設計の判断 |
| **Major fixを1候補・1回に絞る決定**（方法論は≤4候補を許容） | `major-fix-snapshot.md` の実運用 | 方法論からの意図的逸脱。逸脱の許容範囲は人間の権限 |
| **merge実行とrepository rule解釈**（COMMENTEDで進めてよいか） | `merge_actions`、Issue #42 close comment | リポジトリ運用ポリシーの解釈と結果責任 |
| **Issue close** | Issue #42 closed_by kooiei-in4a | 同上 |

### 7.3 AIへ移譲できた判断（実際に移譲され機能した）

- 8候補すべてのimplementation / self-review / H1 disposition
- Implementation Evaluationのスコアリングと Findings Matrix
- Selection / Adjudication の採用・非採用理由の構成（C6 TimeProvider非採用の論拠は技術的に妥当）
- Final Synthesis の設計統合と実装
- 5 reviewer / 2 Judge / Gold起草 / mutation再現
- targeted fix の実装
- targeted re-review と clearance判定
- Formal Agent B の product merge gate 判定
- close evidence の編纂

特筆すべきは **Judge が「1/5しかMajorと言っていない指摘」をblockingへ昇格させた**こと。多数決を採らず一次証拠へ戻る判断は、従来「人間が最後に見る」とされてきた種類の判断であり、FND-04ではAIが実行して正解している。

### 7.4 今後AIへ移譲できそうな判断

| 現在人間が担っている作業 | 移譲可能な理由（一次証拠） |
|---|---|
| **finding normalization（pre-judge）** | Judge promptがPhase Aで独立Referenceを作り直す設計なので、normalizationはJudgeへの入力整形にすぎない。Judge出力に統合可能 |
| **clearance artifact の作成** | T1/T2の出力（verdict / new blocker / new major / mutation結果）から機械的に決定できる。`major-fix-clearance.json` の内容は2つのre-review JSONの合成である |
| **CI identity の解決** | Final Synthesis snapshotが `direct_head_push_run: not_independently_resolved` を残し、reviewerが発見、coordinatorがsupplementで解消した。GitHub APIで機械的に解決可能な作業であり、人手の介在自体が誤り |
| **close evidence comment の編纂** | Formal Agent B reviewが必要な材料をすべて含んでいる |
| **control branchのartifact commit** | 72 commit / 73 CI run。定型的なlock更新であり自動化余地が大きい |

### 7.5 人間が残るべき判断

1. **製品契約（Issue §8 fixed contract）の決定**。何を固定し何を後続Issueへ繰り延べるかは、プロダクト戦略そのもの。
2. **severity → blocking の *基準*（policyの水準）**。「assurance defectはmerge blockerたり得る」という基準自体は人間が置く。個別事案への*適用*はAIで足りる（FND-04で実証済み）。
3. **コストをどこに払うかの配分**。reviewer pool revision 2 が典型。AIには「品質を上げよ」としか言えず、「Opus 5を1枠だけ使え」は言えない。
4. **merge / close の実行権限と結果責任**。
5. **方法論からの意図的逸脱の許可**（≤4候補 → 1候補など）。

### 7.6 個別テーマの評価

- **benchmark設計**: 人間必須（何を測るか）。ただし実行制御（lock、identity確認、gate進行）は移譲可能で、FND-04で実際に移譲されている。
- **candidate選択**: pool分類（active / challenger / reserve / suspended）の基準は人間、当てはめは規則化されているのでAI可（methodology §19に suspension signal が明文化済み）。
- **Gold / Judge運用**: 完全移譲可能。FND-04で実証。
- **merge gate**: 技術判定はAI、実行は人間。
- **repository policy**: 人間。
- **Major severity判断**: AI可。ただし基準の設定は人間。
- **product / process trade-off**: 人間必須。
- **「AIを増やせば人間が不要になる」か**: ならない。FND-04で人間が担った7件の判断はいずれも**目的関数の設定**であり、AIを増やしても発生しない。むしろAIを増やすほど「どこに払うか」の判断頻度が上がる。

---

## 8. Cost / Time / Complexity

### 8.1 分類凡例

- **【一次証拠】** GitHub API / artifact に直接記録されている
- **【概算】** GitHub timestamps から算出した *coordinator wall clock*（agent処理時間ではない）
- **【取得不能】** 推測で埋めない

### 8.2 実行回数 —【一次証拠】

| 工程 | FND-04 | 内訳 |
|---|---:|---|
| H0 implementation | 8 | candidate 8 |
| Formal Self-Review | 8 | 8/8 locked |
| H1 | 8 | うち4件はcode change 0 |
| Implementation Evaluation | 1 | locked artifact |
| Selection / Adjudication | 1 | locked artifact |
| Final Synthesis | 1 | PR #140 |
| role-diverse independent review | 5 | pool revision 2 で6→5 |
| finding normalization | 1 | pre-judge artifact |
| Judge | 2 | A / B。C不使用 |
| Gold lock | 1 | |
| targeted Major fix | 1 | |
| targeted re-review | 2 | T1 / T2 |
| clearance | 1 | |
| Formal Agent B | 1 | |
| merge / close evidence | 1 | |
| **合計（概算実行単位）** | **42** | |

FND-03 比較【一次証拠】:

| 工程 | FND-03 |
|---|---:|
| implementation candidate | 13（計画14 / 1件 no-change） |
| Final Synthesis | 1 |
| independent review | **17** |
| Gold（post-hoc） | 1 |
| Major fix candidate | **14** |
| Major fix Judge | **3** |
| final fix synthesis + Agent B | 2 |
| **合計** | **51** |

**総実行回数: 51 → 42（約 −18%）。** ただし内訳は大きく異なる。

| phase | FND-03 | FND-04 | 差 |
|---|---:|---:|---|
| implementation系 | 13 | **24** | **+85%** |
| review / judge系 | 18 | **8** | **−56%** |
| Major fix系 | 17 | **3** | **−82%** |

**FND-04は「実装計測を厚くし、レビューと修正を薄くした」トレードである。**

### 8.3 CI回数 —【一次証拠】GitHub Actions API（`created=2026-08-09..2026-08-10`）

| branch群 | push | pull_request | 合計 | 備考 |
|---|---:|---:|---:|---|
| `agent/issue-42-fnd-04-*`（candidate 8本） | 20（失敗1含む） | 14（失敗1含む） | **34** | |
| `agent/issue-42-fnd-04-final-code` | 2 | 2 | **4** | |
| **FND-04 製品/候補 小計** | | | **38** | |
| `agent/fnd04-benchmark-control` | 73（success 22 / **cancelled 51**） | 0 | **73** | **docs-onlyのbranch** |
| `agent/128-fnd04-benchmark-methodology` | 6 | 3 | 9 | docs-only |
| `agent/128-fnd04-benchmark-lock` | 6 | 1 | 7 | docs-only |
| `agent/benchmark-duration-collection-policy` | 2 | 1 | 3 | docs-only |
| **FND-04 プロセス文書 小計** | | | **92** | |

**docs-onlyのCIが実エンジニアリングCIの約2.4倍。** cancelled 51件はconcurrency groupによる連続pushのキャンセルで、これは純粋な待ち時間とrunner消費である。

### 8.4 変更規模 —【一次証拠】

| 対象 | 値 |
|---|---|
| Final Synthesis（Base→初回Head） | 25 files / +1167 / −1 |
| G-01 targeted fix（old→new Head） | 1 commit / 1 file / **+18 / −0** / production変更なし |
| PR #140 総commit数 | **2** |
| `agent/fnd04-benchmark-control` の common base 以降 commit | **72**（すべて docs/benchmarks 配下） |

**製品コード 2 commit に対し、プロセス文書 72 commit。比 1:36。**

### 8.5 Finding件数 —【一次証拠】

| 段階 | Blocker | Major | Minor | Nit |
|---|---:|---:|---:|---:|
| Formal Self-Review 合計（8候補） | 0 | 0 | 4 | 2 |
| Implementation Evaluation（8候補合計） | 0 | **1** | 9 | 2 |
| role-diverse review 5名合計 | 0 | **1** | 5 | 3 |
| Gold（裁定後・root cause単位） | 0 | **1** | 2 | 2 |
| targeted re-review 2名（新規） | 0 | 0 | – | – |
| Formal Agent B | 0 | 0 | 2 | 3 |

**手戻り回数: 1回（G-01のみ）。** Major fixのやり直しは0回。CI失敗による再実行は candidate 1件（grok-4.5-cursor で push 1 / PR 1 の failure）のみ。

### 8.6 所要時間

**【一次証拠】author記録**

| 工程 | 時間 |
|---|---|
| Final Synthesis | **29分** |
| G-01 targeted Major fix | **30分** |

**【概算】coordinator wall clock（GitHub timestamps）**

| 区間 | 概算 |
|---|---|
| Issue #128 起票 → pre-run lock | 約27分 |
| H0実行（初回candidate PR → H0 lock） | 約1時間34分（8候補） |
| SR + H1（H0 lock → H1 lock） | 約2時間8分（8候補×2 phase） |
| Selection / Adjudication | 約16分 |
| review 5名 + normalization + Judge 2名 + Gold lock | 約2時間25分 |
| fix snapshot → clearance | 約56分 |
| clearance → Formal Agent B review提出 | 約27分 |
| Agent B → merge → close | 約20分 |
| **Issue Ready → Issue Close** | **約21時間1分**（夜間中断を含む） |

**【取得不能】**

- 各candidateのH0 / SR / H1 の**agent処理時間**。run.jsonが `"Experimental rough-minute collection was not retained consistently; do not infer from GitHub timestamps."` と明記。8/8一貫収集に失敗し、Speed Score / Quality-Time Index / Practical Score speed componentは**算出中止**。
- 各reviewer / Judge の処理時間（review artifactに記録フィールドなし）。
- token消費・金銭コスト（記録なし）。
- Implementation Evaluation の実作業時間（H1 lock 08-09 15:13Z → lock 08-10 01:51Z の10時間38分は夜間中断を含み、作業時間として使用不可）。
- 人間の操作回数（agentと同一アカウントのため分離不能）。

**注記**: FND-03はPR本文自己申告方式で14/14のduration収集に成功していた（`final-fix/run.json`）。FND-04はより精密なepoch方式を試して**収集そのものに失敗した**。これは方式変更による明確な退行である。

---

## 9. Process Scorecard

```text
5 = 非常に有効 / 4 = 有効 / 3 = 効果はあるが改善余地大 / 2 = 費用対効果が低い / 1 = 廃止候補
```

### Issue Ready / fixed contract — **5**

- **価値**: 全8候補が version / connection key / 60秒budget / empty baseline で一致。run全体で契約解釈の争点ゼロ。reviewerもJudgeもIssue §8を規範として直接引用できた。
- **コスト**: 事前1日未満（Issue #128と並行）。
- **問題点**: なし（実質）。
- **次回方針**: 完全踏襲。§8のような「Fixed implementation contract」節をFND-05でも必ず作る。

### 8 candidate benchmark — **4**

- **価値**: C8-M01 Majorの供給。Findings Matrixによる証拠強度の階層化。同一モデル×異Harness比較の維持。
- **コスト**: 24実行（H0/SR/H1）+ CI 34本。
- **問題点**: 上位5候補が92–99に密集し総合点の分解能が不足。C2/C3/C7は結論に一切影響しなかった。
- **次回方針**: **6へ削減**（active 4 + challenger 2）。challenger枠は削らない。

### H0 snapshot — **5**

- **価値**: SR Gain測定の前提。H0 winner（C1）とH1 winner（C5）が異なるという知見はH0固定なしでは得られない。
- **コスト**: ほぼゼロ（実装完了時点でPR+CIを固定するだけ）。
- **問題点**: なし。
- **次回方針**: 完全踏襲。

### Formal Self-Review — **4**

- **価値**: C6が自分のtimeout testのfalse assuranceを発見・除去（+3）。C8がNitを直しMajorを見逃したという**SR失敗パターンの実証**。SR品質がFinding件数と無相関であることの確認。
- **コスト**: 8実行。
- **問題点**: 品質メカニズムとしては弱い（8候補中1件しか実質的改善なし）。測定器としては強い。
- **次回方針**: **残す**。ただし prompt へ「自分が追加したnegative testに守るべきdefectを注入してredになることを確認せよ」を追加し、品質メカニズムとしても機能させる。

### H1 snapshot — **3**

- **価値**: SR findingの `accepted / rejected` disposition記録。C5 SR-01 / C6 SR-01 のover-strict findingを正当にrejectした記録は、SR精度評価に不可欠。
- **コスト**: 8実行。うち**4実行がコード変更ゼロ**。
- **問題点**: finding 0でも独立実行を必須とするルールが空実行を生む。
- **次回方針**: **MODIFY**。SR verdict = `NO CHANGE` かつ finding 0 の場合は coordinator記録（H1 Head = H0 Head / code_change NONE / CI再利用）で閉じる。finding ≥1 の場合のみ独立実行。**−4実行**。

### candidate Selection / Adjudication — **5**

- **価値**: C5 primary + C1 partial（secret非開示regression）+ C6明示非採用（理由付き）+ C8-M01 mandatory guard。この4つの決定がFinal Synthesisの内容をほぼ決めた。
- **コスト**: 1実行 / 約16分。
- **問題点**: **mandatory guardを要求したが、そのguardのmutation感度受入条件を書かなかった。これがG-01の直接原因。**
- **次回方針**: 残す + 「mandatory guardを要求するときは mutation sensitivity 受入条件を同時に明記する」を必須項目化。

### Final Synthesis — **5**

- **価値**: 29分・25 files・+1167/−1でBlocker 0。両CI green。candidate mergeを禁じたことで出所が完全に説明可能。
- **コスト**: 1実行 / 29分。
- **問題点**: PR本文がmerge-ref runを "Exact Head CI" と誤記（G-03/NR-04）。snapshotが direct-head run を未解決のまま lock。
- **次回方針**: 残す + snapshot lock前に**direct-head runとmerge-ref runの両方を機械的に解決してから**lockする手順を追加。

### role-diverse independent review — **4**

- **価値**: FND-03の17名全員見逃しに対し、5名でrun中にG-01発見。
- **コスト**: 5実行。
- **問題点**: 検出はn=1。同一Model+HarnessがReviewer roleでは見逃しJudge roleでは発見した（R1 vs Judge A）ため、**role分散が効いたという因果は証明されていない**。P10で名指ししても2/5が見逃した。
- **次回方針**: **MODIFY**。5枠は維持しつつ、**test assurance枠を2枠**にし、出力スキーマへ `mutation_probes` を必須フィールド化。roleの細分化ではなく実行要件の強制へ重心を移す。

### 2-Judge方式 — **5**

- **価値**: quorum完全一致でJudge C不要。多数決を採らず、1/5のMajor主張をblockingへ正しく昇格。Phase A blindでJudge A（R1と同一Model+Harness）が独立到達したことが、この方式の有効性の直接証拠。
- **コスト**: 2実行。
- **問題点**: Judge B が R2 と同一Model+Harnessで独立性が弱い。
- **次回方針**: 残す + **「Judgeは当該run内でreviewerを務めたModel+Harnessと重複させない」を優先制約に追加**（不可能な場合はその旨を記録）。Judge C発動条件に「required fix方向の実質的矛盾」を追加。

### Gold Review — **4**

- **価値**: production defectとassurance defectを明確に区別。NR-03を独立再現により棄却。NR-02のsub-claim（reachable race）をJudge間差異ゆえroot causeから除外。required fixとmutation sensitivity 3条件を明文化。
- **コスト**: 1 lock artifact。
- **問題点**: Judge出力との内容重複が大きい。GoldはJudge quorumの整形に近い。
- **次回方針**: **MODIFY**。独立artifactではなく **Judge quorum synthesis の一節**として生成し、`gold-review.json` のみ機械可読正本として残す。

### targeted Major fix — **5**

- **価値**: 30分・test-only・1 file・+18/−0で一発クリア。production変更ゼロ。FND-03の14候補+3 Judge（17実行 / merge-ready 1/14）と比較して圧倒的。
- **コスト**: 1実行 / 30分。
- **問題点**: 方法論§22（≤4候補）と実運用（1候補）の乖離が文書化されていない。
- **次回方針**: 残す + methodology §22 を「Goldがrequired fix方向を一意に指定できた場合は1実装者・1回を標準とする。方向が複数あり得る場合のみ最大3候補」へ改訂。

### targeted re-review — **5**

- **価値**: T1/T2が独立にM1/M2/baseline/recovery/residueを再現。new Blocker/Major 0を確認。full re-review（≈8実行）を3実行で代替。
- **コスト**: 2実行 + clearance 1。
- **問題点**: 同一mutationがJudge A/B、T1/T2、Agent Bで計**5回**実行されている（うち3回はfix後）。
- **次回方針**: 残す + **範囲決定基準を severity から blast radius へ変更**（§5.5の3分岐）。Formal Agent BのG-01再検証は省略。

### Formal Agent B — **4**

- **価値**: base main CI（run 31309214350）との test件数差分照合（Unit +1 / non-PG +11 / PG +9）で**FND-03既存testの無損失を証明**。real PG step所要 17s→1m22s の整合確認。isolated copyでの完全再実行。新規finding MIN-01 / MIN-02。
- **コスト**: 1実行。
- **問題点**: G-01 mutation再現が3回目で冗長。
- **次回方針**: 残す + **scopeを再定義**。「benchmark chainが扱わない項目（base CIとのtest件数照合、既存test無損失、scope boundary、CI identity最終確認、新規finding探索）」を必須項目、「既にclearされたfindingの再検証」を任意項目とする。

### exact Head / merge-ref CI — **5**

- **価値**: 誤認が実際に発生し（PR本文の "Exact Head CI" 誤記）、reviewerが検出、supplementで解消。この規約の必要性がrun内で自己証明された。close evidenceでも両identityを分離記録。
- **コスト**: ほぼゼロ（記録規約）。
- **問題点**: 記録が事後supplementになった。
- **次回方針**: 完全踏襲 + snapshot lock時点で両identityを必須フィールド化（未解決なら lock 不可）。

### benchmark artifact管理 — **2**

- **価値**: すべての工程がrevision付きでlockされ、事後の再構成が可能。この振り返り自体が一次証拠から完全に再構成できたのはこの管理の成果。
- **コスト**: **docs-only commit 72件 / CI run 73件（cancelled 51）/ 製品コード比 1:36**。
- **問題点（重大）**:
  1. **正本がmainに無い**。現在の`main`の `docs/benchmarks/fnd04-model-comparison/run.json` は `schema_version 1.1 / status: prepared_not_started`、README は "PREPARED / NOT STARTED"。実際の完了状態（schema 1.9）は未merge branchにしか存在しない。**mainだけを読むとFND-04 benchmarkは未実行に見える。**
  2. docs-only branchでCIが走る。`paths-ignore` が無い。
  3. candidate branch 8本 / Draft PR #131–#138 が未整理。
- **次回方針**: **MODIFY（優先度最高）**。(a) workflowへ `paths-ignore: docs/**` を追加、(b) lock単位を「工程ごと1 commit」へ粗くする（現在は artifact 1本ごとにcommit）、(c) **Issue close時点でcontrol branchをmainへmergeし、mainの正本を実態と一致させる**。

### merge / close evidence管理 — **5**

- **価値**: close evidence comment（5237276272）がmerge identity chain（reviewed Head / pre-merge Base / merge-ref / merge commit / current main）、CI 2 identity、機能証拠、実行コマンド、Formal Agent B結果、GitHub state制約の説明をすべて含む。第三者が検証可能。
- **コスト**: 1実行。
- **問題点**: repository ruleの根拠が「直前FND-03で確認済み」の流用。
- **次回方針**: 残す + **merge直前にrepository ruleを毎回取り直す**（流用禁止）。

---

## 10. KEEP / MODIFY / DROP

### KEEP（そのまま残す）

| 工程 | 理由（一次証拠） |
|---|---|
| **Issue Ready / fixed implementation contract**（Issue §8方式） | 8候補で契約解釈の争点ゼロ |
| **Benchmark pre-run lock**（common base / branch identical / not_started） | 実行前にeffort不整合を補正できた（comment 5231183433） |
| **H0 snapshot** | H0 winner ≠ H1 winner という知見の前提 |
| **Selection / Adjudication**（candidate merge / cherry-pick 禁止） | PR #140が2 commitで完全に説明可能 |
| **Final Synthesis**（mainからcurated re-implementation） | 29分・Blocker 0 |
| **Judge Phase A blind → Phase B adjudication** | **FND-04で最も効いた設計**。1/5の主張をblockingへ正しく昇格 |
| **2 Judge + 条件付きJudge C** | quorum一致でC不要。3常設より効率的 |
| **targeted Major fix**（全candidate再実行しない） | 17実行 → 1実行 |
| **targeted re-review + mutation sensitivity検収** | 2名が独立再現、new Major 0 |
| **exact Head / merge-ref CI の identity分離** | run内で必要性が自己証明された |
| **evaluator-only probes / assumption ledger の事前LOCK** | green CI 8/8下でMajorを摘出 |
| **product完了とbenchmark archiveの分離**（methodology §24） | FND-03 archiveと並走できた |
| **merge / close evidence bundle** | 第三者検証可能 |

### MODIFY（残すが変更する）

| 工程 | 変更内容 |
|---|---|
| **candidate 8 → 6** | active 4 + challenger 2。C2/C3/C7相当は結論に寄与しなかった。challenger枠は維持（Major供給源） |
| **H1 phase** | SR verdict = NO CHANGE / finding 0 のときは coordinator記録で閉じる（独立実行しない）。**−4実行** |
| **Formal Self-Review prompt** | 「自分が追加したnegative testへ守るべきdefectを注入し、redになることを確認せよ」を追加 |
| **Selection / Adjudication の mandatory guard 要求** | guardを要求するときは **mutation sensitivity 受入条件を同時に明記**（G-01の直接の再発防止） |
| **reviewer構成** | 5枠維持。ただし **test assurance枠を2枠**。出力スキーマへ `mutation_probes` 必須フィールド追加。roleは「関心の重点」であって探索範囲の限定ではない旨をpromptで強調 |
| **Judge選定制約** | 「当該run内でreviewerを務めたModel+Harnessと重複させない」を優先制約に。Judge C発動条件へ「required fix方向の実質的矛盾」を追加 |
| **Gold Review** | 独立artifactをやめ、Judge quorum synthesisの一節として生成。`gold-review.json` のみ機械可読正本として維持 |
| **targeted re-review の範囲決定** | severityではなく **blast radius** で3分岐（§5.5） |
| **Formal Agent B の scope** | 「benchmark chainが扱わない項目」を必須、「既にclearされたfindingの再検証」を任意へ |
| **duration計測** | epoch wrapper方式（8/8失敗）を廃し、**FND-03方式（PR本文へ整数分を必須記載、14/14成功）へ戻す**。収集率を優先し精度を諦める |
| **benchmark artifact管理** | `paths-ignore: docs/**`、lock粒度を工程単位へ、**Issue close時にcontrol branchをmainへmergeして正本を一致させる** |
| **methodology §22（targeted fix ≤4候補）** | 実運用（1候補）に合わせて改訂。「required fix方向が一意なら1実装者・1回」を標準に |
| **methodology §16.1（reviewer 約6枠）** | 5枠 + test assurance 2枠 へ改訂 |

### DROP（廃止する）

| 工程 | 理由 |
|---|---|
| **finding normalization（pre-judge）の独立artifact化** | Judge promptがPhase Aで独立Referenceを作り直すため、normalizationはJudge入力の整形にすぎない。`finding-normalization-prejudge.md` の内容はJudge出力へ統合できる。**−1実行 −1 artifact** |
| **clearance の独立artifact化** | T1/T2のJSONから機械的に決定可能。`major-fix-clearance.json` はre-review 2件の合成。**−1実行** |
| **finding 0 candidateのH1独立実行** | C1/C2/C3/C7で code_change NONE / head == H0 / CI再利用が確認済み。**−4実行** |
| **Formal Agent BによるG-01 mutation再検証** | Judge A/B + T1/T2 で4回実行済み。5回目は情報を追加していない |
| **docs-only branchでのCI実行** | 73 run（うち51 cancelled）。実エンジニアリングCIの約2倍。純粋な浪費 |
| **Speed Score / Quality-Time Index / Practical Score speed component**（現行定義のまま） | FND-04で算出不能。計測方式を直すまで指標自体を持たない（持っていると「N/A」を毎回書く作業だけが残る） |

---

## 11. FND-03 vs FND-04

**注記**: 「FND-04の方が新しいから優れている」という評価は行っていない。各行は一次証拠に基づく比較であり、FND-04が劣る行も明示している。

| 観点 | FND-03 | FND-04 | 評価 |
|---|---|---|---|
| **candidate構成** | 計画14 / 採点13 / no-change 1。pool分類なし | 8（active 6 + challenger 2）+ reserve 3 + suspended 3 のlifecycle管理 | **FND-04優位**。suspendedにより no-result 候補を排除。ただし上位帯の分解能はどちらも不足 |
| **implementation評価** | 単一snapshot / 100点 / duration 13件収集 | H0 + H1の二重snapshot / 100点 / **duration収集失敗（0件）** | **両者一長一短**。評価軸はFND-04が豊富、**時間計測はFND-03が明確に優位** |
| **self-review** | 独立phaseなし（実装時間に埋没） | H0→SR→H1の三段化。Gain 0〜+3を測定 | **FND-04優位**。H0/H1/SR winnerが全て別candidateという知見はFND-03では得られない |
| **independent review** | 同一prompt × **17名**。**全員がmerge-blocking root causeを見逃し** | role分散 **5名**。1名がblocking Majorを発見 | **FND-04優位（結果）**。ただし検出n=1で margin は薄い。「17名でも0、5名でも1」は reviewer数が主因でないことの傍証 |
| **Judge** | Major fix roundで **3名常設** | **2名 + 条件付き3人目**。Phase A blind必須。quorum一致でC不要 | **FND-04優位**。Phase A blindが1/5の主張をblockingへ正しく昇格させた |
| **Major発見** | Gold は **post_hoc_adjudication**（reviewer結果を見た後に一次source突合で明確化） | Gold は raw capture後・**Judge Phase A blind**を経て確定。run中に発見 | **FND-04優位**。「事後に判明した」と「工程内で捕まえた」の差は大きい |
| **Major fix** | **14候補 + 3 Judge = 17実行**。merge-ready **1/14** | **1実装者 × 1回**。30分 / test-only / +18-0 / 一発クリア | **FND-04が圧倒的優位**。FND-03の14候補中13件がmerge不可という結果は、この方式の非効率を示している |
| **Formal Agent B** | 1名。APPROVE / **B0 M0 m0 N0** | 1名。APPROVE / **B0 M0 m2 N3**。base CIとのtest件数照合を実施 | **FND-04優位（内容）**。finding件数が多いのは品質低下ではなく**レビューが深い**ため（MIN-01/MIN-02は他工程が出していない） |
| **merge gate** | Agent B技術APPROVE / GitHub state COMMENTED（自己承認制約） | 同一事象。ただし technical / GitHub state / repository enforcement の3層を明示分離して記録 | **FND-04優位（記録）**。事象は同じだが、FND-04は「PASSと主張していない範囲」を明記した |
| **benchmark archive** | tag 28本 / PR close 27件 / branch削除28本 / `archive_status: complete_archived` で**完結** | **未完了**。candidate branch 8本・Draft PR 8件が残存。**mainのrun.jsonは `prepared_not_started` のまま実態と矛盾** | **FND-03が明確に優位**。FND-04は archive を critical path から外した副作用として正本管理を落とした |
| **工数（実行回数）** | 約51実行 | 約42実行（−18%）。内訳は implementation +85% / review −56% / fix −82% | **FND-04優位**。ただし削減は候補数ではなく review / fix round の縮小による |
| **工数（CI）** | （同期間に FND-03 archive関連で多数のdocs-only runが発生） | 製品/候補 38 run に対し **docs-only 92 run** | **両者とも問題あり**。FND-04も docs-only CI を制御できていない |
| **品質** | Agent B時点 B0/M0/m0/N0。merge後 main CI success。**ただしMajorは post-hoc で判明** | Agent B時点 B0/M0/m2/N3。両CI identity success。**Majorは工程内で発見・修正・独立検収** | **FND-04優位**。最終品質は同等だが、**到達経路の信頼性**が異なる |

### FND-04が劣った点（明示）

1. **duration計測**: FND-03は14/14収集、FND-04は0/8。方式変更による退行。
2. **benchmark archive / 正本管理**: FND-03は`complete_archived`まで到達、FND-04はmainの記録が実態と矛盾したまま。
3. **docs-only CI**: FND-04 control branchで73 run（51 cancelled）。FND-03も同種の問題を抱えていたが、FND-04で改善されなかった。

---

## 12. Key Learnings

FND-04の具体的事象からのみ導出。一般論は含めない。

### AI Coding

1. **8/8がexact-head CI successでも、1件にMajorが残った**（C8-M01）。CIは実装品質の下限しか保証しない。
2. **同じ契約を満たす実装でも、証拠の強さは大きく違う**。60秒budgetについて、C1/C5は実PostgreSQL lockでproduction Migratorの実budgetを発火させ、C2/C3/C4/C7/C8はpre-cancelled token / test-only delegate / 250ms external cancellationに留まった。**契約充足とevidence強度は別軸で採点しないと差が出ない。**
3. **fail-openな「気を利かせた既定値」が最も危険な失敗パターン**。C8-M01は `ConnectionStrings__Database` 未設定時に `Host=127.0.0.1;Database=design_time` を生成した。これは「動くようにする」という善意の実装であり、コンパイルも通りテストも通る。**未設定は fail-closed が既定であるべき**という契約は、明文化しないと守られない（Issue §8.4が明文化していたからMajorとして摘出できた）。

### AI Self-Review

4. **Self-Review findingの件数は品質指標にならない**。C1のfinding 0（正当）とC2/C3/C7のfinding 0（見逃し）は同じ数字で全く違う。C8はNit 1件を発見・修正して自分のMajorを見逃した。
5. **Self-Reviewが最も価値を出したのは「自分のテストの証拠強度を疑ったとき」**。C6のSR-02（60秒timeout testの証拠強度が弱い）が唯一の実質的改善（+3）を生んだ。**自分のコードではなく自分のテストを疑うSRが効く。**
6. **over-strict findingを正しくrejectする能力もSR能力の一部**。C5 SR-01 / C6 SR-01（connection resolution path差）は両方rejectされ、Implementation Evaluationはそのdispositionを妥当と評価した。**「見つける」だけでなく「見つけたものを却下する」判断が測れる。**

### AI Code Review

7. **promptに根本原因を名指ししても検出されない**。reviewer promptのP10は「固定禁止文字列の非出現だけで destination safety を証明したことになっていないか」と書いていた。それでも5名中2名（R1/R3）が完全に見逃し、1名（R4）は本文で言及しながらfindingにしなかった。
8. **検出したのは「実際に壊した」reviewerだけ**。R2は `Host=db;Database=ambient_fallback` 注入と build output 退避という2つのmutationを実行した。静的読解のみのreviewerは到達しなかった。
9. **同一Model+Harnessでも prompt構造で結果が変わる**。GPT-5.6 Sol / Codex は R1（role: runtime_failure_path）として見逃し、Judge A（Phase A blind reference作成）として発見した。**「このモデルはこの欠陥を見つけられない」という結論は出せない。出せるのは「このprompt構造では見つからなかった」だけ。**
10. **roleの割当は探索を狭める副作用を持ち得る**。R1のroleがruntime/failure pathだったことが、test oracleの感度から注意を逸らした可能性がある。roleは「重点」であって「範囲」ではないことをpromptで強制する必要がある。

### Judge / Adjudication

11. **多数決を採らなかったことが正解だった**。raw reviewerの分布は Major 1 / Minor 1 / 言及のみ 1 / 無し 2。多数決ならNR-01はMinorに落ちてmergeされていた。
12. **Judgeの独立性は「先にReferenceを作らせる」ことで実装できる**。Phase A（raw reviewerを読まない）→ Phase B（裁定）の順序が、anchoringを構造的に防いだ。Judge A/Bとも Phase A でmutationを再現している。
13. **Judge間の不一致は隠さず裁定理由を残すと機能する**。G-02のsub-claim（provider timeoutがCTSより先に到達するreachable race）はJudge間で評価が割れ、Goldは「そのsub-claim自体はroot causeへ含めない」と範囲を限定した。G-04はJudge A=Nit / Judge B=Minorで、Goldは「READMEがexit taxonomyをdeployment-facing contractとして公開している」を根拠にMinorへ寄せた。**不一致の処理方法が記録されていることが、quorumの信頼性を担保する。**
14. **quorumの信頼度は「同じ結論に達した数」ではなく「独立に到達した経路の数」で測るべき**。Judge B は R2 と同一Model+Harnessで、実質的な独立確認はJudge A の1本だった。

### Test Quality

15. **`green test` は `test が failure を検出できる` を意味しない**。G-01の対象testは、守るべきdefect classを再導入してもgreenのままだった。
16. **「禁止文字列が出ていないこと」は destination safety の証明にならない**。列挙外の値（`Host=db`、Unix socket、ambient default）が常に存在する。**blocklistは補助assertionにしかできない。**
17. **negative testには2種類のpositive markerが要る**。「対象pathへ到達した証拠」（`Npgsql` / `Microsoft.EntityFrameworkCore.Migrations`）と「状態が期待どおりである証拠」（`The ConnectionString property has not been initialized.` / `database '' on server ''`）。FND-04のfixはこの両方を追加している。
18. **positive marker導入は maintenance 負債とのトレードオフ**。Agent B NIT-03が指摘したとおり、EF/Npgsqlのexact messageに依存するためprovider upgrade時に保守が要る。**これを隠さず記録することが正しい運用**。
19. **同種の弱さは1箇所直しても他に残る**。G-01（design-time）を直した直後、Agent BがMIN-01（Migrator failure test 2本が非0のみ）を指摘した。**false assuranceは「クラス」であり「箇所」ではない。**

### Multi-Agent Development

20. **実装・レビュー・裁定・修正・検収を別Model+Harnessへ分けると、単一モデルでは到達しない結論に到達する**。G-01の発見（Claude Opus 5）→ 独立確認（GPT-5.6 Sol）→ 修正（Final Synthesis実装者）→ 検収（GPT-5.6 Sol + Cursor Auto）→ merge gate（Claude Opus 5、fresh context）。**同一モデルの多重実行では代替できない。**
21. **fresh contextの強制が効く**。Formal Self-ReviewもJudgeもFormal Agent Bも「fresh contextで、自分の過去の説明を正しいものとして扱わない」を明示的に要求している。Formal Agent Bは`git archive`でrepository外へ展開して再実行までしている。
22. **上流工程の仕様欠落は下流のモデル能力では埋まらない**。G-01の根本原因はSelection/Adjudicationの「mutation感度受入条件の欠落」であり、Final Synthesisの実装者（最高スコアのC5設計を統合）が正しく仕様を満たしても発生した。**マルチエージェントは仕様の質を増幅するが、補完はしない。**

### Human / AI Responsibility Boundary

23. **AIが「1/5の少数意見をblockingへ昇格させる」判断を正しく行えた**。従来「最後は人間が見る」とされた種類の判断が移譲可能であることの実証。
24. **人間が担った判断はすべて「目的関数の設定」だった**。契約の中身、コスト配分（reviewer pool revision 2）、方法論からの逸脱許可、merge権限。**AIを増やしてもこれらは減らない。むしろ選択肢が増える分、判断頻度は上がる。**
25. **データが欠けたときの「埋めない」判断は人間が下した**。duration 8/8収集失敗に対し、GitHub timestampからの推測を明示的に禁じ、Speed Score系の算出を中止した。**AIは「N/Aと書く」ことはできるが「この指標をこのrunでは持たない」と決めることはできない。**

### Benchmark Methodology

26. **pre-run lockは実行前の不整合補正まで含めて価値がある**。effort未記録の不整合を candidate output 0件の時点で補正できた（comment 5231183433）。「locked」は「凍結」ではなく「変更に証拠を要求する状態」。
27. **計測方式を凝ると収集率が落ちる**。FND-03のPR本文自己申告（14/14成功）→ FND-04のepoch wrapper（0/8成功）。**収集率 > 精度。**
28. **設計しただけで実行しなかった実験は、無いのと同じ**。Controlled Mutant arm（protocol §16.3）はFND-04で設計されたが実行されず、結果として reviewer の False Negative率は測定できていない。G-01を2/5が見逃したことは判明したが、それは偶然G-01が実在したからで、体系的なrecall測定ではない。
29. **弱いcandidateは品質比較ではなく失敗パターンの供給源として価値がある**。rank 8のC8がrun唯一のMajorを供給し、それが最大の学習（G-01）へ連鎖した。ただしno-result / no-changeの候補（FND-03のMiniMax M3）は供給源にすらならない。suspended分類は正しい。
30. **方法論docと実運用が同一runの中で3箇所乖離した**。reviewer「約6枠」→5枠、targeted fix「≤4候補」→1候補、Controlled Mutant「使用可」→未使用。**乖離自体は健全な最適化だが、docへ反映しないと次回の判断基準が失われる。**

### Software Engineering Process

31. **手戻りは1回・test-only・+18行だった**。全工程でproduction codeへの手戻りはゼロ。Final Synthesisのproduction実装は一度も差し戻されていない。**上流（contract / selection）を厚くすると下流の手戻りが小さくなる**ことの実例。
32. **プロセス文書のコストが製品コードのコストを大きく上回った**。製品 2 commit に対し control branch 72 commit（1:36）。docs-only CI 92 run に対し製品/候補 CI 38 run。**「検証プロセスの検証」に払うコストは意識的に上限を決めないと際限がない。**
33. **critical pathから外した工程は放置される**。archive分離（methodology §24）は並走を可能にしたが、副作用としてmainの正本が `prepared_not_started` のまま残った。**「後でよい」と「やらなくてよい」を分けるには、期限か完了gateが要る。**
34. **CI identity の混同は実際に起きる**。PR本文の "Exact Head CI" 誤記は、規約があったから検出された。規約が無ければ「CI green」で通っていた。

---

## 13. FND-05 Recommended Process

FND-05開始時にそのまま実行できる形で記述する。

### 13.1 推奨構成一覧

| 項目 | FND-04 | **FND-05推奨** | 根拠 |
|---|---|---|---|
| **candidate数** | 8 | **6**（active 4 + challenger 2） | C2/C3/C7は結論に寄与せず。challengerは唯一のMajor供給源 |
| **H0/H1方式** | H0固定 → SR → H1（全件独立実行） | H0固定 → SR → **H1はfinding≥1のみ独立実行**。finding 0はcoordinator記録で閉じる | C1/C2/C3/C7がcode change 0 |
| **Formal Self-Review** | 全件必須 / review-only / fresh context | 継続 + **「自分が追加したnegative testへdefectを注入しredになることを確認せよ」を追加** | C6の+3が唯一の実質改善。標準化する |
| **reviewer数 / role** | 5（各role 1枠） | **5枠。test assurance を2枠**（別Model+Harness）。他3枠は spec/scope、runtime/failure、framework official-source | G-01検出がn=1。roleではなくmutation実行が効いた |
| **reviewer出力** | Markdown + JSON（probe matrix） | + **`mutation_probes` 必須フィールド**（name / injected / expected / observed / residue）。空提出は未完了 | 静的読解では到達しなかった |
| **Judge構成** | A / B + 条件付きC | 継続。+ **「当該runでreviewerを務めたModel+Harnessと重複させない」を優先制約**。Judge C発動条件へ「required fix方向の実質的矛盾」を追加 | Judge B = R2 と同一で独立性が弱かった |
| **Gold** | 独立artifact（md + json） | **Judge quorum synthesisの一節として生成**。`gold-review.json` のみ機械可読正本 | Judge出力との重複 |
| **finding normalization** | 独立artifact + 1実行 | **廃止**。Judge Phase Bの入力整形としてJudge出力へ統合 | Judge が Phase A で独立Referenceを作り直すため |
| **Major fix方式** | 1実装者 × 1回 | 継続。**required fix方向が一意なら1実装者・1回、複数あり得る場合のみ最大3候補** | 30分・一発クリア |
| **re-review方式** | 2名固定 | **blast radiusで分岐**（下記13.3）。test-onlyなら2名 + clearance | severityとblast radiusは別物 |
| **clearance** | 独立artifact + 1実行 | **廃止**。re-review 2件のJSONから機械生成 | 合成にすぎない |
| **Formal Agent B** | 全項目レビュー | 継続。**必須項目と任意項目を分離**（13.4） | G-01再検証は5回目で冗長 |
| **CI identity** | direct-head / merge-ref を分離記録 | 継続 + **snapshot lock時に両方を必須フィールド化**（未解決ならlock不可） | supplement対応が発生した |
| **処理時間計測** | epoch wrapper（0/8収集） | **FND-03方式へ回帰**: 各PR本文へ `DURATION_MINUTES: <整数>` 必須。取得不能時のみ `N/A` と理由 | 14/14 vs 0/8 |
| **artifact管理** | control branch 72 commit / CI 73 run | **(a) workflowへ `paths-ignore: docs/**`、(b) lock粒度を工程単位、(c) Issue close時にcontrol branchをmainへmerge** | 正本がmainに無い |
| **merge / close** | 3層分離記録 | 継続 + **repository ruleをmerge直前に毎回取り直す**（前Issueからの流用禁止） | FND-04は流用していた |

### 13.2 FND-05 実行回数の見積り

| 工程 | FND-04 | FND-05推奨 | 差 |
|---|---:|---:|---:|
| H0 | 8 | 6 | −2 |
| Formal Self-Review | 8 | 6 | −2 |
| H1 | 8 | 約2（finding≥1のみ） | −6 |
| Implementation Evaluation | 1 | 1 | 0 |
| Selection / Adjudication | 1 | 1 | 0 |
| Final Synthesis | 1 | 1 | 0 |
| independent review | 5 | 5 | 0 |
| normalization | 1 | 0 | −1 |
| Judge | 2 | 2 | 0 |
| Gold lock | 1 | 0（Judge統合） | −1 |
| targeted Major fix | 1 | 1 | 0 |
| targeted re-review | 2 | 2 | 0 |
| clearance | 1 | 0 | −1 |
| Formal Agent B | 1 | 1 | 0 |
| merge / close | 1 | 1 | 0 |
| **合計** | **42** | **29** | **−31%** |

**品質を落とす要素は含まれていない。** 削減はすべて (a) 結論に寄与しなかったcandidate、(b) 成果ゼロだったH1実行、(c) 他artifactの合成にすぎない中間artifact、からの削減である。

### 13.3 re-review 範囲決定ルール（そのまま採用可）

```text
old Head -> new Head の delta が:

  production code 変更なし / test-only / 変更ファイル <= 2
    -> targeted re-review 2名（mutation sensitivity再現必須）
    -> Formal Agent B は当該fixの再検証をスキップ可

  production code 変更あり / 単一 module 内
    -> targeted re-review 2名
    -> 加えて当該module に関係する evaluator probe を全数再実行
    -> Formal Agent B は当該fixを必須検証

  production code 変更あり / 複数 module または architecture 変更
    -> full review round 再実行（reviewer 5名 + Judge 2名）
    -> Gold 再固定
```

### 13.4 Formal Agent B scope（そのまま採用可）

```text
必須（benchmark chainが扱わない項目）:
  1. base main CI run との test件数差分照合（既存testの無損失証明）
  2. Base -> Head 全diffのscope boundary確認
  3. direct-head CI / merge-ref CI の identity 最終確認
  4. isolated copy（repository外）での build + 全test suite 再実行
  5. 新規finding探索（benchmark chainが見ていない観点）
  6. Issue Acceptance Criteria の逐条判定

任意（既にclearされた項目の再検証）:
  7. Gold findingのmutation再現   <- Judge A/B + T1/T2 で済んでいれば省略可
```

### 13.5 Selection / Adjudication へ追加する必須節（G-01再発防止）

```markdown
## Mandatory guard の要求形式

Final Synthesis へ regression guard を必須要求する場合、次の3つを**同時に**記述する。

1. 証明対象（何が守られるべきか）
2. 到達証明（production の当該pathを実際に通ったことをどう示すか）
3. **mutation sensitivity 受入条件**
   - 「M1: <守るべきdefect classを再導入する変更> を注入したとき、当該testがFAILすること」
   - 「M2: <対象pathへ到達できない失敗> が起きたとき、当該testがFAILすること」
   - 「M1/M2をdiscardしたとき、当該testがPASSへ復帰すること」
   - 「mutationはcommitしない」

3を書かない mandatory guard 要求は不完全とみなす。
```

---

## 14. 30% Cost Reduction Scenario

**目標**: 品質をほぼ維持したまま総コスト −30%。

### 削減内容（§13.2の推奨構成そのもの）

| 削減 | 実行 | CI | 根拠 |
|---|---:|---:|---|
| candidate 8 → 6 | −6（H0/SR/H1×2） | 約 −8 run | C2/C3/C7はMajor 0 / SR finding 0 / Selection採用要素 0。Findings Matrix上も "weaker evidence" 群に一括分類され、除いても Implementation Evaluation の結論（C5 primary / C1 partial / C6非採用 / C8-M01 Major）は変わらない |
| finding 0 candidate の H1 独立実行を廃止 | −4 | −4 run | C1/C2/C3/C7が head==H0 / code_change NONE / CI再利用。失われる証拠ゼロ |
| finding normalization artifact 廃止 | −1 | −数 run | Judge Phase A が独立Referenceを作り直す |
| Gold を Judge synthesis へ統合 | −1 | −数 run | 内容重複 |
| clearance を re-review JSON から機械生成 | −1 | −数 run | 合成にすぎない |
| Formal Agent B の G-01再検証を省略 | ±0（時間短縮のみ） | 0 | 5回目の同一mutation |
| **docs-only branchのCIを `paths-ignore` で停止** | 0 | **−92 run** | 実エンジニアリングCIの約2.4倍 |
| **合計** | **−13 / 42 = −31%** | **−100超 run** | |

### 品質への影響評価

| 削った要素 | FND-04で果たした役割 | 削っても成立する理由 |
|---|---|---|
| candidate 2件 | 順位の中位を埋めた | Selection/Adjudicationの4決定に一切寄与していない |
| H1独立実行 4件 | 「変更なし」の記録 | coordinator記録で同じ証拠が残る |
| normalization | reviewer findingのroot cause束ね | Judgeが一次証拠から再構築する設計 |
| Gold独立artifact | 修正指示の明文化 | Judge synthesis内で同じ内容を書ける |
| clearance独立artifact | G-01解消の宣言 | T1/T2 JSONに全事実が含まれる |
| Agent BのG-01再検証 | 5回目の確認 | Judge A/B + T1/T2で4回済み |

**リスク**: candidate 8→6でMajor供給源が減る可能性。C8（challenger、rank 8）がMajorを供給した事実から、**challenger枠は絶対に削らない**（active枠から2件削る）ことでこのリスクを回避する。

### 期待効果

- 実行 42 → 29（−31%）
- CI 130 → 約30（−77%）
- 品質: **維持**（削減対象はいずれも判断へ寄与していない工程）
- 副次効果: control branch commit 72 → 工程単位lockで約15へ

---

## 15. Maximum Quality Scenario

**目標**: コスト +20%（42 → 約50実行）で品質を最大化。

「工程を増やすこと自体を品質向上とみなさない」という前提で、**FND-04で実際に発生したリスクへ直接対応するもの**だけを選ぶ。

### 追加①: mutation sensitivity 受入条件の必須化 — **追加コスト 0**

Selection/Adjudication が mandatory guard を要求する際、mutation sensitivity（M1/M2/recovery）を仕様の一部にする（§13.5）。

**根拠**: G-01は**要求仕様の欠落から生まれた**。実装ミスではない。この1行の追加でG-01は発生しなかった。**コストゼロで最大の効果。最優先。**

### 追加②: test assurance reviewer を 2枠へ — **+1実行**

別Model+Harnessの2枠にし、両方に `mutation_probes` 必須フィールドを課す。

**根拠**: G-01の検出はn=1（R2のみ）。R2が不調だった場合、Goldは APPROVE_WITH_FINDINGS となりfalse assuranceがmergeされていた。**現行の最大の単一障害点への直接の保険。**

### 追加③: Controlled Mutant arm の実行 — **+5実行**

protocol §16.3で設計済みだが未実行。正しいsnapshotへ既知欠陥3件（うち1件は false assurance 型）を注入したreview専用targetを作り、同じreviewer poolへ投入する。

**根拠**:
- FND-04ではreviewerのFalse Negative率が**測定できていない**。G-01を2/5が見逃した事実は判明したが、これは偶然G-01が実在したからであり、体系的なrecallではない。
- 「role適性」「モデル別review能力」の主張は、Controlled Mutantなしには一次証拠を持てない。R1 vs Judge A の事例は、**現状のデータではモデル能力とprompt効果を分離できない**ことを示している。
- 目的が「開発手法検証」である以上、**この測定こそが成果物**。

コストを抑える案: Controlled Mutantは全5名でなく **3名（test assurance 2 + spec/scope 1）** に限定して +3実行とする。

### 追加④: Formal Agent B の必須項目へ「false assurance クラス走査」を追加 — **+0実行（時間+）**

Agent BのMIN-01は「G-01と同種の弱さがMigrator failure testに残っている」ことの指摘だった。この一般化をscopeに明記する。

**根拠**: false assuranceは「箇所」でなく「クラス」。FND-04では偶然Agent Bが気付いた。**明示的な必須項目にする。**

### 追加⑤: repository rule の merge直前再取得 — **+0実行**

**根拠**: FND-04は前Issueの確認を流用した。rulesetは変更され得る。

### 見送るもの（コスト対効果が低い）

| 案 | 見送る理由 |
|---|---|
| **Judge C を常設** | FND-04でquorum一致・不要と実証済み。発動条件の改善（required fix方向の矛盾を追加）で十分 |
| **reviewer を 8〜10名へ増員** | FND-03の17名が全員見逃した事実が、reviewer数と検出率の相関の弱さを示す。同じ金を test assurance 枠と Controlled Mutant へ払う方が効く |
| **candidate を 10 以上へ** | 上位帯の分解能不足はrubricの問題。候補を増やしても解決しない |
| **targeted re-review を 3名へ** | test-only +18行に対して既に2名 + Agent B の3重確認。過剰 |
| **全candidateでのMajor fix round復活** | FND-03で merge-ready 1/14。17実行の費用対効果が実証的に悪い |

### 合計

```text
追加①  mutation sensitivity 必須化          +0
追加②  test assurance reviewer 2枠目        +1
追加③  Controlled Mutant arm（3名限定）     +3
追加④  Agent B false assurance クラス走査   +0
追加⑤  repository rule 再取得               +0
------------------------------------------------
合計                                        +4 実行（42 -> 46、+10%）
```

**+20%の予算枠に対し+10%で収まる。** 残り10%は Controlled Mutant を5名フル実行（+2）へ回すか、あるいは**使わずに済ませる**のが正しい。予算があるから使うのは、この振り返りの前提（工程増加＝品質向上ではない）に反する。

### 3シナリオ比較

| | 半分削減 | −30% | +20%（実際は+10%） |
|---|---|---|---|
| 実行回数 | 約20 | 29 | 46 |
| 削る/足すもの | candidate 8→3、reviewer 5→2、Judge 2→1、Gold/normalization/clearance統合、re-review 2→1 | candidate 8→6、H1空実行、中間artifact 3種、docs-only CI | mutation sensitivity必須化、test assurance 2枠、Controlled Mutant 3名 |
| 製品品質 | **おそらく維持**。G-01検出の決定的経路（mutation実行reviewer → Judge blind reference → targeted fix）は約5実行で成立しており、それは残る | **維持** | **向上**（n=1リスク解消 + recall測定） |
| 研究価値 | **崩壊**。model ranking、SR Gain、Findings Matrix、Harness比較がすべて成立しなくなる | **維持** | **大幅向上**（初めてreviewerのFN率が測れる） |
| 判定 | **不採用**。リポジトリ目的が「開発手法検証」である以上、研究価値の崩壊は目的の放棄 | **推奨** | **推奨（①②④⑤は無条件、③は予算次第）** |

**「半分削る」への回答**: 削るとしたら candidate 8→3、reviewer 5→2、Judge 2→1、中間artifact 3種の統合、re-review 2→1（計 −22 / 42 = 52%）。**製品品質はおそらく維持されるが、benchmarkとしては死ぬ。** FND-04の一次証拠が示すのは、**製品を通すのに必要な工程は全体の約1/5（mutation実行reviewer 1〜2名 → Judge blind reference → targeted fix → Agent B ≒ 5〜6実行）にすぎず、残り4/5は測定のための計装である**ということ。どちらを買っているかで答えが変わる。

---

## 16. Final Assessment

### 1. FND-04はFND-03よりプロセスとして改善したか

**改善した。ただし全面的ではない。**

改善（一次証拠）:
- Majorが**事後裁定（FND-03: post_hoc_adjudication）から工程内検出（FND-04: run中に発見・修正・独立検収）へ**変わった。
- 総実行回数 51 → 42（−18%）で、review系 −56% / Major fix系 −82%。
- 手戻り1回・test-only・+18行で完結。production codeへの差し戻しゼロ。
- H0/H1/SRの分離により、implementation winner・self-review winnerが別モデルであるという、FND-03では原理的に得られない知見を獲得。

劣化（一次証拠）:
- duration計測が 14/14収集 → 0/8収集へ**退行**。
- benchmark archive が `complete_archived` → **未完了**、かつmainの正本（run.json）が `prepared_not_started` のまま実態と矛盾。
- docs-only CIが改善されず、93 run（うち51 cancelled）を消費。

### 2. 最も効果があった変更は何か

**Judge の Phase A blind reference 方式。**

理由: G-01は raw reviewer 5名のうち Major主張が1名のみだった。多数決またはreviewer評判ベースの集約なら Minor へ落ちてmergeされていた。Judge A/B に「raw reviewerを読む前に独立Referenceを作れ」と課したため、両者が自らmutationを再現して blocking を確定できた。さらに**Judge A（GPT-5.6 Sol / Codex）は、同一Model+HarnessのR1が見逃した欠陥へ到達している**。これはprompt構造が結果を変えた最も明確な証拠である。

次点は **targeted Major fix**（FND-03の17実行 → 1実行30分）。

### 3. 最も費用対効果が低かった工程は何か

**benchmark artifact管理（control branchのcommit / CI運用）。**

- docs-only commit 72件、CI run 73件（うち51件 cancelled）。製品/候補CI 38 run の約2倍。
- 製品コード 2 commit に対しプロセス文書 72 commit（1:36）。
- **その代償を払って、正本は `main` に無い**。現在の main の run.json は `schema 1.1 / prepared_not_started`、README は "PREPARED / NOT STARTED"。mainだけを読むとFND-04 benchmarkは未実行に見える。

次点は **finding 0 candidate の H1 独立実行**（4実行が code change ゼロ）。

### 4. FND-05でも必ず残すべき工程は何か

優先順:

1. **Issue Ready / fixed implementation contract**（Issue §8方式）— 8候補で契約争点ゼロ
2. **Judge Phase A blind → Phase B adjudication** — FND-04で最も効いた
3. **targeted Major fix + mutation sensitivityによる検収** — 17実行→1実行、一発クリア
4. **Selection / Adjudication**（candidate merge / cherry-pick 禁止）— PR #140が2 commitで完全に説明可能
5. **exact Head / merge-ref CI の identity 分離** — run内で必要性が自己証明された
6. **evaluator-only probes / assumption ledger の事前LOCK** — green CI 8/8下でMajorを摘出
7. **Formal Agent B**（scope再定義のうえで）— 他工程が誰もやらなかったbase CIとのtest件数照合を実施
8. **H0 snapshot** — ほぼ無償でSR Gain測定を可能にする

### 5. FND-05では削るべき工程は何か

1. **finding 0 candidate の H1 独立実行**（−4実行、失われる証拠ゼロ）
2. **finding normalization の独立artifact化**（−1、Judge Phase Aが再構築するため）
3. **clearance の独立artifact化**（−1、re-review 2件のJSONの合成）
4. **Gold の独立artifact化**（−1、Judge synthesisへ統合）
5. **candidate 2件**（8→6。active枠から削る。challengerは維持）
6. **Formal Agent B による G-01相当 mutation の再検証**（Judge A/B + T1/T2 で4回済み）
7. **docs-only branch での CI 実行**（`paths-ignore` を追加）
8. **Speed Score / Quality-Time Index**（計測方式を直すまで指標自体を持たない）

### 6. 今回最も重要だった新しい知見は何か

> **`green test` は `test が failure を検出できる` を意味しない。そして、その差はコードを読んでも分からず、壊してみて初めて分かる。**

FND-03の教訓は「green CI ≠ failure-path correctness」だった。FND-04はその一段深い層を発見した。

具体的には:
- exact-head CI 8/8 success、direct-head + merge-ref CI ともSUCCESS、build 0 warnings / 0 errors、real PostgreSQL 23 pass。**それでもfalse assuranceのMajorが残っていた。**
- reviewer promptは根本原因を**名指しで警告していた**（P10）。それでも5名中2名が完全に見逃した。
- 検出したのは「実際にmutationを注入して壊した」reviewerだけだった。
- 同一Model+Harnessが、Reviewer roleでは見逃し、Judge Phase A（自ら独立Referenceを構築する枠組み）では発見した。

派生する運用上の知見:
- **negative testには2種類のpositive marker（pathへの到達 + 状態の証明）が必要**、blocklistは補助にしかならない。
- **mandatory guardを要求するときは、mutation sensitivity受入条件を同時に書かないと不完全**。G-01は実装ミスではなく**要求仕様の欠落**から生まれた。
- **false assuranceは「箇所」ではなく「クラス」**。G-01を直した直後にAgent BがMIN-01（同種の弱さ）を指摘した。

### 7. 現在の開発方式は「過剰」「適正」「不足」のどれか

**目的を製品デリバリと見るなら「過剰」。開発手法検証と見るなら「適正」。リポジトリの明示された目的（"開発手法検証用の内部デモ"）に照らせば「適正」。ただし2箇所は無条件に過剰。**

内訳:

| 領域 | 判定 | 根拠 |
|---|---|---|
| **製品を通すための工程** | **過剰** | FND-04の一次証拠上、merge可否を決めた決定的経路は「mutation実行reviewer 1〜2名 → Judge blind reference → targeted fix → Formal Agent B」の約5〜6実行。残り36実行は測定のための計装 |
| **benchmark としての計装** | **適正〜やや不足** | H0/SR/H1、Findings Matrix、Judge quorumは正しく機能した。一方 **Controlled Mutant arm が未実行**でreviewerのFalse Negative率が測れておらず、review能力の主張に一次証拠が無い。ここは**不足** |
| **artifact / 正本管理** | **過剰かつ不足**（両方） | commit 72件・CI 93 runは過剰。にもかかわらず**mainの正本が実態と矛盾**しており、成果物としては不足 |
| **人間の関与** | **適正** | 人間が担った7件はすべて目的関数の設定であり、AIへ移譲できない種類。移譲できる部分（normalization、clearance、CI identity解決）はまだ人手に残っているが、量は小さい |

**総合: 適正。** ただし §13〜14 の −31% を実行すれば、研究価値を1つも失わずに「適正の中の効率的な側」へ移動できる。逆に §15 の追加①②④⑤（合計 +1実行）は、コストほぼゼロで現行最大の弱点（G-01検出のn=1依存と、要求仕様側の欠落）を塞ぐ。**この2つは同時に実行すべきである。**

---

## Evidence Index

一次証拠として参照したもの（優先順位順）。

**GitHub（優先1〜5）**
- Issue #42 本文（Fixed implementation contract §8、AC §9、Gate status §14）+ comment 5231080595 / 5231132126 / 5231582163 / 5237276272
- Issue #128 本文（"Key decisions already approved by Koo"）+ comment 5231088471 / 5231135642 / 5231183433 / 5231669763
- PR #140 本文 / commits（`99cee438`, `3511688401`）/ reviews（id 4894487758）/ merge metadata
- PR #129 / #130 / #131–#138 / #139 metadata（created_at, head branch）
- GitHub Actions runs API（`created=2026-08-09..2026-08-10`、全ページ）— run 31350870902 / 31350916189 / 31360093004 / 31360094852 / 31309214350
- merge commit `9a352a3a61945647273ccc7dfbc8e1816c3ca07c`、common base `38c07e210fe4e8689f1d8aeabbb07b92610d1826`

**`agent/fnd04-benchmark-control`（優先6〜7）**
- `run.json`（schema 1.9）、`README.md`、`scoring.md`
- `reference/assumption-ledger.md`、`reference/evaluator-probes.md`
- `prompts/formal-self-review.md`、`prompts/h1-execution-wrapper.md`、`prompts/final-synthesis-independent-review.md`、`prompts/final-synthesis-judge.md`
- `results/implementation-evaluation.md`、`results/selection-adjudication.md`、`results/final-synthesis-ci-supplement.md`
- `review-benchmark/run.json`、`README.md`、`reviewer-pool-revision-2.md`、`finding-normalization-prejudge.md`、`gold-review.md`、`major-fix-snapshot.md`、`major-fix-clearance.md`、`formal-agent-b-result.md`
- `review-benchmark/reviews/claude-opus-5-claude-code.md`、`review-benchmark/re-reviews/t2-cursor-auto.md`

**FND-03 artifacts（優先8）**
- `docs/benchmarks/fnd03-model-comparison/summary.md`、`final-outcome.md`、`archive-manifest.json`
- `fnd03-.../review-benchmark/README.md`、`run.json`
- `fnd03-.../final-fix/README.md`、`run.json`

**共通方法論**
- `docs/benchmarks/model-implementation-benchmark-methodology.md`（§19–24）
- `docs/benchmarks/independent-review-benchmark-protocol.md`（§3, §16）

**優先9（モデル自己申告）として扱い、判断の主根拠にしなかったもの**
- 各candidate PR本文の自己申告、Final Synthesis PR本文の Local verification / duration

---

```yaml
MODEL: Claude Opus 5
HARNESS: Claude Code
EFFORT: xHigh
MODEL_SLUG: claude-opus5-xhigh-claudecode
OUTPUT: docs/retrospectives/fnd04-retrospective-claude-opus5-xhigh-claudecode.md
CHANGES_MADE: このMarkdown 1ファイルの新規作成のみ（product code / test / benchmark raw artifact / Issue / PR / branch は一切変更していない）
```
