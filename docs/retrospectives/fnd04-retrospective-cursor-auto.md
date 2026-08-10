# FND-04 Retrospective — Grok 4.5 / Composer 2.5 (Cursor Auto)

```yaml
MODEL: "Grok 4.5,Composer 2.5"
HARNESS: "Cursor"
EFFORT: "Auto"
MODEL_SLUG: "cursor-auto"
ANALYST_ROLE: Independent Development Process Retrospective Analyst
TARGET_ISSUE: 42
FINAL_PR: 140
FINAL_REVIEWED_HEAD: 3511688401533f60bb77c7dcc647c4c2c4aa84c6
FINAL_MERGE_COMMIT: 9a352a3a61945647273ccc7dfbc8e1816c3ca07c
BENCHMARK_CONTROL_BRANCH: agent/fnd04-benchmark-control
ANALYSIS_DATE: 2026-08-10
```

## 1. Executive Summary

FND-04は、FND-03で確立した「固定契約 → multi-candidate benchmark → curated Final Synthesis → independent review → Agent B → merge」の骨格を維持しつつ、**candidate数を14→8へ削減**し、**H0/Formal Self-Review/H1**、**role-diverse review（最終5名）**、**2-Judge + conditional C**、**targeted Major fix + 2本re-review**を実験した。

結論として、プロセスはFND-03より改善した。最大の価値は「弱いcandidate大量投入」ではなく、**(1) fixed implementation contract、(2) challengerが生んだC8-M01をFinal Synthesisのmandatory guardへ転用、(3) JudgeによるmutationでG-01 false assuranceをMajor確定、(4) test-only targeted fixで手戻りを最小化**した点にある。一方、Formal Self-Review全8必須と、多数同質reviewerの代替としてのrole分散は方向正しいが、**多数決ではG-01を潰せず（Majorを付けたのは5中1）**、Judge/mutationが実質の品質ゲートだった。

現在方式の総体評価は**やや過剰だが適正寄り**。FND-05では8 candidate維持か6への微減、Self-Reviewの条件付き化、targeted fix維持、Formal Agent B維持が妥当。

## 2. FND-04 Timeline

一次証拠（Issue #42 comments、PR #140、`agent/fnd04-benchmark-control` artifacts、Actions runs）に基づく再構成。

| 時点 (UTC / 判明値) | 工程 | なぜ存在したか | 潰したリスク | 実際の価値 |
| --- | --- | --- | --- | --- |
| 2026-08-09 10:41 | Issue Ready / fixed contract | 実装前の曖昧さを契約化 | package/ownership/timeout/design-timeの独自解釈 | **高**。以降の評価軸が安定 |
| 2026-08-09 10:54 | Benchmark pre-run lock | Issue #128制御下でcommon base固定 | candidate間のbase drift | **高**。8 branch identical確認 |
| 2026-08-09〜10 | 8× H0 | 実装能力比較 | 単一モデル依存 | **高**。7/8 merge-ready候補を生成 |
| 同期間 | Formal Self-Review → H1 | 実装直後と自己修正を分離測定 | 「一発実装」と自己改善の混同 | **中**。最大+3、Major見逃しも発生 |
| 2026-08-10 10:51 | Implementation Evaluation | H0/H1をlocked採点 | rankingの後付け変更 | **高**。C8-M01 Major確定 |
| 2026-08-10 11:07 | Selection / Adjudication | curated採用要素を明示 | cherry-pick/ranking汚染 | **高**。C5 primary + C1 partial + C8 regression |
| 2026-08-10 11:25–11:54 | Final Synthesis (29分) | production実装を再構成 | candidate mergeによる偶然採用 | **高**。+1149/-1、CI成功 |
| 実行前 | Reviewer pool v2 (6→5) | cost/coverage再調整 | 未開始rawの無駄実行 | **中**。役割は維持、同質増員を回避 |
| ~12:00–14:16 | Role-diverse review + Judges | Final Synthesisの独立検証 | author blind spot | **高**。G-01発見の入口 |
| 14:16 | Gold G-01/NR-01 lock | merge blocker確定 | 「production正しい＝merge可」誤認 | **非常に高** |
| 14:28–14:58 | Targeted Major fix (30分) | test oracleのみ修正 | 全candidate再実装 | **非常に高**。1 file +18 |
| ~15:55 | Targeted re-review 2/2 | fix感度の独立確認 | author自己申告依存 | **高**。M1/M2再現 |
| Formal Agent B | product merge gate | benchmark多数決からの独立 | Gold clearanceの代用merge | **高**。fresh再検証 + Minor発見 |
| 07:40–07:42 UTC | Ready / merge / Issue close | 製品完了記録 | 証拠欠落close | **高**。Close Evidence完備 |
| control finalize | benchmark control最終化 | 実験と製品の分離保管 | artifact散逸 | **高**。`final_complete` |

工程の流れ:

```text
Issue Ready / fixed contract
↓ Benchmark pre-run (common base 38c07e2..., 8 branches)
↓ 8× H0 → Formal Self-Review → H1 (CI 8/8 SUCCESS)
↓ Implementation Evaluation (H1 winner C5; C8-M01 Major)
↓ Selection / Adjudication (C5 + C1 secret + C8 guard; C6 TimeProvider非採用)
↓ Final Synthesis PR #140 Head 99cee438...
↓ role-diverse review 5/5 → Judge A/B一致 → Gold G-01
↓ targeted fix Head 3511688... → re-review 2/2 → clearance
↓ Formal Agent B APPROVE (GitHub COMMENTED)
↓ repository rule確認 → merge 9a352a3... → Issue #42 close
↓ benchmark control finalization
```

## 3. What Was Carried Forward from FND-03

### 3.1 Fixed contract / Issue Ready

```text
FND-03での問題
  → package/fixture解釈差がcandidate間で評価ノイズになった
導入された対策
  → 実装前に契約を固定しIssue Readyを明示
FND-04でどう利用したか
  → 2026-08-09にEF/Npgsql/dotnet-ef exact versions、Migrator ownership、
    ConnectionStrings:Database、60s budget、no auto-migrate等をIssue本文へ固定
実際に効果があったか
  → あり。H0/H1採点とSelectionが同一軸で比較可能だった一次証拠がある
```

### 3.2 Common base + exact-head CI + control branch

```text
FND-03での問題
  → base drift / CI identity曖昧さ
導入された対策
  → common base SHA固定、exact Head CI、benchmark control branch
FND-04でどう利用したか
  → base `38c07e2...`、8/8 identical、direct-headとPR merge-refを分離記録
実際に効果があったか
  → あり。Gold時点でPR本文の「Exact Head CI」表記ズレをNitとして検知しつつ、
    実evidence gapは無いと裁定できた
```

### 3.3 Curated Final Synthesis（candidate merge禁止）

```text
FND-03での問題
  → ranking winnerをそのままmergeすると局所最適を製品化する
導入された対策
  → mainから再構築するcurated synthesis
FND-04でどう利用したか
  → SelectionでC5主軸 + C1 partial + C8 mandatory regressionを明示し、
    cherry-pick禁止でPR #140を構築
実際に効果があったか
  → あり。C8の欠陥パターンを「採用しない」だけでなく「再発防止test必須」へ転用できた
```

### 3.4 green CI ≠ failure-path correctness

```text
FND-03での問題
  → Major-fix 14候補すべてCI SUCCESSでもmerge-readyは1/14
導入された対策
  → failure-path / mutation志向の評価
FND-04でどう利用したか
  → C8はCI SUCCESSでもMajor；Final SynthesisもCI SUCCESSだがG-01でCHANGES_REQUIRED
実際に効果があったか
  → あり。プロセス文化として再確認され、G-01発見の前提になった
```

### 3.5 Formal Agent B と Technical APPROVE / GitHub COMMENTED 分離

```text
FND-03での問題
  → 自己承認禁止でGitHub stateがCOMMENTEDになる
導入された対策
  → 技術verdictとplatform eventを分けて記録
FND-04でどう利用したか
  → Agent B review `4894487758` がAPPROVE本文 + COMMENTED event、
    rulesetは別actor APPROVED必須でないと確認してmerge
実際に効果があったか
  → あり。FND-03と同型の運用でmerge可能と判断できた
```

### 3.6 Benchmark / product artifact分離

```text
FND-03での問題
  → 実験artifactと製品PRの混線リスク
導入された対策
  → docs/benchmarks archive、candidate PR unmerged close等
FND-04でどう利用したか
  → PR #140はdocs/benchmarksを変更せず、control branchで結果を管理
実際に効果があったか
  → あり。製品diff (+1167/-1, 25 files) がbenchmark汚染なしと確認できる
```

## 4. What Changed in FND-04

### 4.1 8 candidateへ削減

| 項目 | 内容 |
| --- | --- |
| 変更前 | FND-03: 計画14 / 採点13（+ Major-fix 14） |
| 変更後 | active 6 + challenger 2 = 8、Reserve/Suspended明示 |
| 狙い | コスト削減しつつ上位識別と失敗パターン収集 |
| 実際の結果 | H1で7/8 merge-ready、上位は98–99点帯で識別可能。challenger C8が唯一のblocking Majorを供給 |
| 副作用 | 弱いモデル大量投入による「失敗カタログ」は薄くなった |
| 残すか | **MODIFYして残す**。8は妥当。6でも成立しうるがC8級challenger枠は維持 |

### 4.2 H0 → Formal Self-Review → H1

| 項目 | 内容 |
| --- | --- |
| 変更前 | 実装1ショット中心 |
| 変更後 | H0固定 → fresh-context review-only → H1 fix |
| 狙い | implementation能力とself-review能力の分離測定 |
| 実際の結果 | Gain: C6 +3、C4 +2、C5 +1、C8 +1(Nitのみ)。4候補はFinding 0。C8はMajor見逃し |
| 副作用 | 全8必須はコスト大。duration一貫収集失敗でSpeed ScoreはN/A |
| 残すか | **MODIFY**。全必須は費用対効果が低い。条件付き必須へ |

### 4.3 Role-diverse independent review（最終5）

| 項目 | 内容 |
| --- | --- |
| 変更前 | FND-03: 同質プロンプト17 reviewer |
| 変更後 | role分散、pool v2で6→5 |
| 狙い | 人数削減でもcoverage維持 |
| 実際の結果 | G-01のMajorはR2（deep technical）のみ。R5はMinor、多数はAPPROVE系。発見はrole設計と強いreviewerの交差 |
| 副作用 | 「roleを付ければ多数決でMajorを拾う」わけではない |
| 残すか | **KEEP/MODIFY**。role分散は有効だが、adversarial/mutation枠を明示必須に |

### 4.4 2-Judge + conditional C

| 項目 | 内容 |
| --- | --- |
| 変更前 | FND-03 Major-fixは3 Judge常設；初期review Goldはpost-hoc寄り |
| 変更後 | Judge A/B必須、不一致時のみC |
| 狙い | コスト削減と独立裁定 |
| 実際の結果 | A/BがNR-01で完全一致、C未使用。双方がPhase Aで独立mutation再現 |
| 副作用 | 一致＝絶対真理ではない（共通盲点リスクは残る） |
| 残すか | **KEEP**。今回の証拠では2で十分 |

### 4.5 Targeted Major fix / targeted re-review

| 項目 | 内容 |
| --- | --- |
| 変更前 | FND-03: Major確定後に14候補再実装比較 |
| 変更後 | Gold指定の最小test fix + re-review 2本 |
| 狙い | 手戻りとコスト削減 |
| 実際の結果 | 1 commit / 1 file / +18、production無変更、30分、2/2 G01_FIXED |
| 副作用 | architecture選択の再競争はできない（今回は不要だった） |
| 残すか | **KEEP**。本番defectでなくassurance defectの典型解 |

## 5. Evaluation of New Experiments

### 5.1 8 candidate削減

- モデル多様性: Codex / Claude Code / Open Code / Cursorを含み、**比較としては十分**。
- 情報量: 上位識別（99/98/98）と失敗パターン（C8-M01）は得られた。弱い候補の大量失敗カタログは減ったが、FND-03で得た学習の再利用で補完可能。
- コスト: implementation系だけで約半分弱（14→8）。Major-fixを14→1にした効果の方が大きい。
- 今後: **8を既定、複雑Issueは10、単純Foundationは6**が妥当。challenger枠（失敗パターン供給）は削らない。

### 5.2 H0 / Formal Self-Review / H1

- H0→H1変化: 実質改善は少数。最大価値はC6のtimeout false assurance検出。
- Self-Reviewは不具合を減らしたか: **条件付きYes**。全体平均では限定的。Major検出には失敗例あり。
- 追加修正時間化: Finding 0の4候補ではほぼoverhead。
- 能力分離: **測れる**（Gain表が一次証拠）。ただしimplementation能力の代替にはならない。
- 次回必須範囲: 全candidate必須は不要。上位・低confidence・timeout/failure-path弱い候補に限定。

### 5.3 Role-diverse review

- reviewer削減でもcoverage維持できたか: **部分的Yes**。G-01は発見されたが多数決では埋もれた。
- 同質増員より価値: **Yes**。FND-03の17同質はGold Alignmentが全体的に低く、人数≠検出。
- G-01発見構造: **R2 deep technical + mutation evidence → Judge A/B独立mutation → Gold**。roleラベル単体ではない。
- role固定: Deep Technical / Failure-path / Spec は固定価値あり。Framework browser枠は今回Major非寄与（APPROVE）。
- モデル能力とrole適性: **分離すべき**。同じpromptでもR2だけがMajorにした事実が根拠。

### 5.4 2-Judge方式

- 十分だったか: **今回はYes**（C不要）。
- 3常設より効率的: **Yes**。
- 一致の信頼: quorum key一致は強いが、共通誤りの保険は「mutation再現」と「Formal Agent B」が担った。
- Judge C条件: 現状の「verdict / blocking root cause / merge-ready不一致」で妥当。severityのみの差（Minor vs Nit）でCを起動しなかった運用も妥当（G-04）。

## 6. G-01 / NR-01 Case Study

### 何が起きたか

Final Synthesisのproduction design-timeはfail-closedで正しい一方、C8-M01再発防止として必須化した`DesignTimeConnectionSafetyTests`が、

1. child process `exit != 0`
2. 固定blocklist文字列の非出現

だけでPASSし、off-blocklist fabricated destinationやfactory未到達のtooling failureでもgreenになり得た。Goldはこれを**Major false assurance**と確定（production defectではない）。

### なぜcandidate比較では見逃せたか

- candidate比較の主戦場は「実装がcontractを満たすか」。C5等の正しい実装には同種の弱いoracleが無い、または評価観点がproduction path中心。
- C8のMajorは「fabricateする実装」であり、「正しい実装 + 弱いregression」という合成欠陥はFinal Synthesis段階で初めて現れた。
- 8 candidateのH1評価ではC8-M01をREJECTしguard必須化したが、**guard自体の感度検証はSelection時点でmutationまで固定していなかった**。

### なぜFinal Synthesis後reviewで発見できたか

- R2がcommitted regressionにmutationを当てた。
- Judge A/Bが互いの結果を読む前に同根因を独立再現し、severityをMajor/blockingへ揃えた。
- 「testがgreen」「CI SUCCESS」「production正しい」を分離して考えた。

### mutation testing的発想の重要性

今回の核心。positive failure reason pinが無いnegative testは、**守るべきdefect classでredにならない**可能性がある。FND-04最大の新知見。

### 今後の組み込み先

1. Selectionでmandatory regressionを課す時点で、**最低2 mutation（off-blocklist / factory-unreachable）をAcceptanceに書く**
2. Independent reviewのDeep Technical / Adversarial roleにmutation必須条項
3. Formal Agent BでもG-01再検証（今回実際に実施され価値があった）

## 7. Human-in-the-loop Analysis

| 分類 | 具体例 |
| --- | --- |
| 人間が必要だった判断 | fixed contract確定、8 candidate/pool設計、Selection（C5 vs C6 TimeProvider非採用）、G-01をMajorとして止める最終責任、repository rule解釈、merge Go |
| AIへ移譲できた判断 | H0実装、SR findings作成、H1修正、raw review起草、Judge Phase A mutation、targeted fix実装、Agent B再検証 |
| 今後AIへ移譲できそうな判断 | duration収集の機械化、CI identity（direct-head vs merge-ref）自動突合、pre-judge finding normalization下書き、mutation probe実行テンプレ |
| 人間が残るべき判断 | Issue契約、benchmark設計の費用対効果、product/process trade-off（実60s test採用等）、Major severityの最終裁定、merge/release Go、repository policy |

特記:

- **benchmark設計・candidate選択**: 人間主導が適切。AIは実行者。
- **Gold/Judge運用**: AIが独立裁定できるが、Gold lockとmerge停止は人間（または明示的coordinator）が責任を持つべき。
- **Major severity**: R2 Major vs R5 Minor vs 多数APPROVEの分裂は、人間なしでは多数決で潰される危険を示した。

## 8. Cost / Time / Complexity

証拠区分を明示する。

| 指標 | 値 | 証拠区分 |
| --- | --- | --- |
| implementation candidates | 8 | 一次証拠あり |
| H0 / SR / H1完了 | 8/8 each | 一次証拠あり |
| H1 exact-head CI | 8/8 SUCCESS | 一次証拠あり |
| candidate duration | N/A（一貫収集失敗） | 取得不能（公式N/A） |
| Final Synthesis duration | 29分 | 一次証拠あり（author metadata） |
| Major fix duration | 30分 | 一次証拠あり |
| independent reviewers | 5（pool v2） | 一次証拠あり |
| Judges used | 2（C未使用） | 一次証拠あり |
| Major fix rounds | 1（test-only） | 一次証拠あり |
| targeted re-reviews | 2 | 一次証拠あり |
| Formal Agent B | 1 | 一次証拠あり |
| Final Synthesis初期CI | direct-head + merge-ref（旧Head） | 一次証拠あり |
| Fix後CI | runs `31360093004`, `31360094852` | 一次証拠あり |
| candidate PRs | #131–#138系 | 一次証拠あり（timeline） |
| 人間操作回数 | 多数（prompt投入・lock・Ready・merge） | 概算可能（厳密カウントは取得不能） |
| 総カレンダー | Issue Ready 08-09 → close 08-10 | 一次証拠あり（約1日強の集中実行） |

工程別の重さ（概算・相対）:

```text
最重: 8×(H0+SR+H1) + exact CI
次点: role-diverse review + Judge/Gold
中:   Final Synthesis / Formal Agent B
軽:   targeted fix + re-review（今回は非常に軽い）
固定費: Issue Ready / pre-run lock / artifact管理（重いが品質基盤）
```

FND-03対比（実装+Major-fixのAI実行回数）:

```text
FND-03: ~14 impl + 17 review + 14 fix + 3 judges  ≈ 非常に重い
FND-04: ~8×3段階 + 5 review + 2 judges + 1 fix + 2 rereview + 1 Agent B
        → 特にMajor-fix段階の削減効果が大きい
```

## 9. Process Scorecard

採点: 5=非常に有効 / 4=有効 / 3=効果あり改善余地大 / 2=費用対効果低 / 1=廃止候補

### Issue Ready / fixed contract — Score **5**

- 価値: 評価ノイズ削減、停止条件明確化
- コスト: 低〜中（人間の前処理）
- 問題点: 契約が長いと更新コスト
- 次回方針: KEEP。FND-05着手前に同様lock

### 8 candidate benchmark — Score **4**

- 価値: 上位識別 + challenger失敗パターン
- コスト: 中（14より低いが依然大きい）
- 問題点: duration未取得
- 次回方針: KEEP/MODIFY。6–8、challenger必須

### H0 snapshot — Score **4**

- 価値: 実装能力の基準線
- コスト: 中（実装本体）
- 問題点: なし特筆
- 次回方針: KEEP

### Formal Self-Review — Score **3**

- 価値: 能力分離、稀に重要なfalse assurance検出
- コスト: 高（全8）
- 問題点: Finding 0多数、Major見逃し例
- 次回方針: MODIFY（条件付き）

### H1 snapshot — Score **3**

- 価値: SR dispositionの検証
- コスト: SRに比例
- 問題点: no-op H1が多い
- 次回方針: SR実施候補のみ必須

### Selection / Adjudication — Score **5**

- 価値: C8 guard転用、C6非採用の明示、cherry-pick禁止
- コスト: 中
- 問題点: mandatory regressionのmutation基準が後段依存だった
- 次回方針: KEEP + mutation Acceptance追加

### Final Synthesis — Score **5**

- 価値: 製品実装の単一正本化
- コスト: 29分（今回）
- 問題点: 初期regression oracle弱点
- 次回方針: KEEP

### role-diverse independent review — Score **4**

- 価値: G-01入口、CI identity指摘等
- コスト: 中（5名）
- 問題点: 多数決ではMajorを潰せない
- 次回方針: KEEP。adversarial/mutation必須化

### 2-Judge方式 — Score **5**

- 価値: 一致でGold確定、Cコスト回避
- コスト: 低〜中
- 問題点: 共通盲点理論リスク
- 次回方針: KEEP

### Gold Review — Score **5**

- 価値: merge停止の正本化、G-01明確化
- コスト: 低（Judge成果の固定）
- 問題点: なし
- 次回方針: KEEP

### targeted Major fix — Score **5**

- 価値: 最小差分、30分、architecture非破壊
- コスト: 低
- 問題点: architecture再選定が必要なMajorには不向き
- 次回方針: KEEP（条件分岐ルール化）

### targeted re-review — Score **4**

- 価値: 独立M1/M2、full再レビュー回避
- コスト: 低（2本）
- 問題点: 2本で十分かはMajor種別に依存
- 次回方針: KEEP。assurance系Majorは2、architecture系は3+

### Formal Agent B — Score **4**

- 価値: fresh再検証、G-01再確認、Minor追加、benchmark非依存
- コスト: 中
- 問題点: Judgeと役割一部重複；GitHub APPROVE不可
- 次回方針: KEEP（省略条件は厳格に）

### exact Head / merge-ref CI — Score **5**

- 価値: identity誤記を吸収しつつ実evidence確認
- コスト: 低（既存CIの読み分け）
- 問題点: PR本文の用語ゆれ
- 次回方針: KEEP。PRテンプレで両欄必須

### benchmark artifact管理 — Score **4**

- 価値: 再現性、段階lock
- コスト: 中（制御branch運用）
- 問題点: main未収載のままcontrolのみだと参照摩擦
- 次回方針: KEEP

### merge / close evidence管理 — Score **5**

- 価値: Close EvidenceがACを網羅
- コスト: 低
- 問題点: Parent #3のチェック更新遅延は別問題
- 次回方針: KEEP

## 10. KEEP / MODIFY / DROP

### KEEP

- Issue Ready / fixed implementation contract
- common base SHA + identical candidate branches
- exact-head CI と PR merge-ref CI の分離記録
- curated Final Synthesis（candidate merge/cherry-pick禁止）
- Selection / Adjudication 文書化
- 2-Judge + conditional Judge C
- Gold lock（CHANGES_REQUIREDでmerge停止）
- targeted Major fix（test/assurance系）
- targeted re-review（mutation感度確認）
- Formal Agent B product merge gate
- Technical Approval と GitHub review state の分離記録
- Close Evidence コメント
- benchmark control branch / artifact lock

### MODIFY

- **candidate数**: 既定8、単純なら6、高リスクなら10。challenger≥1維持
- **Formal Self-Review / H1**: 全必須廃止 → 条件付き（上位N、evidence弱、timeout系、challenger）
- **role-diverse review**: 5維持可。うち1枠を明示的 Adversarial/Mutation必須に
- **mandatory regression**: Selection時点でmutation Acceptanceを併記
- **処理時間計測**: author自己申告を機械収集（wrapper必須）。N/Aを正式スコアに混ぜない方針は維持
- **reviewer pool revision**: 実行前変更は可だが、revision記録を今回同様必須に

### DROP

- **弱いcandidateの大量投入（FND-03型14+）を標準に戻すこと**
- **Major確定後の全candidate再実装benchmarkを標準化すること**（architecture選択が必要な場合のみ例外復活）
- **同質プロンプト17 reviewer**
- **3 Judge常設**（不一致時Cで足りる）
- **Self-Review Finding 0候補への機械的H1再実行**（記録のみで可）

## 11. FND-03 vs FND-04

| 観点 | FND-03 | FND-04 | 評価 |
| --- | --- | --- | --- |
| candidate構成 | 14計画/13採点 | 8（6+2） | FND-04が費用対効果優る。challenger1件でMajorパターン取得 |
| implementation評価 | 単発実装中心 | H0/H1分離採点 | FND-04は測定精度向上。ただしSR全必須は重い |
| self-review | 明示分離なし | Formal SR実験 | 実験価値あり。標準全面適用は過剰 |
| independent review | 17同質 | 5 role-diverse | FND-04が効率的。ただしMajor検出は1名依存 |
| Judge | fix段階3常設、初期Goldはpost-hoc色 | 2+conditional、一致でGold | FND-04が効率的かつ今回有効 |
| Major発見 | review後のdispose latch | test false assurance G-01 | 両方「green≠correct」。FND-04はassurance欠陥型 |
| Major fix | 14候補再実装 | 1 targeted test fix | FND-04が圧倒的に効率的（今回のMajor種別に適合） |
| Formal Agent B | APPROVE / B0 M0 | APPROVE / B0 M0 / Minor2 | 両者とも必要。FND-04はG-01再検証追加価値 |
| merge gate | 技術APPROVE + COMMENTED | 同型 + rules確認明示 | FND-04の方が記録が明確 |
| benchmark archive | COMPLETE/ARCHIVED充実 | control branchでfinal_complete | 両者有効。FND-03の方がmain archive成熟 |
| 工数 | 非常に大 | 大だが明確に削減 | FND-04改善 |
| 品質 | merge後安定 | merge後contract充足 + G-01 cleared | 同等以上。FND-04はoracle品質まで踏み込んだ |

## 12. Key Learnings

### AI Coding

- 固定契約下では上位複数がほぼ同点になりうる（H1: 99/98/98）。差は主に**failure-path証拠の強さ**。
- challengerの価値は順位ではなく**禁止パターンの供給**（C8-M01）。

### AI Self-Review

- 自己レビューは測れるが、**Major検出能力はimplementation能力と相関しない**（C8）。
- 最良ケース（C6 +3）は「定数確認だけのtimeout test」というfalse assuranceを突いた。

### AI Code Review

- role分散は有効だが、**多数決はMajorを埋もれさせる**（5中1がMajor）。
- Deep Technical + mutationがG-01の実効ゲート。

### Judge / Adjudication

- 2独立Judgeが同じmutationに到達すればCは不要。
- Judgeの価値は評点平均ではなく、**blocking root causeの固定**。

### Test Quality

- `exit != 0` + block blocklistはfalse assuranceを許す。
- negative testには**positive failure reason / path pin**と**defect-class mutation**が必要。

### Multi-Agent Development

- 段階lock（H0/H1/Eval/Selection/FS/Gold/Clearance）は手戻り制御に有効。
- benchmark結果をmerge根拠にしない分離（Formal Agent B）は維持価値あり。

### Human / AI Responsibility Boundary

- 契約・Selection・Major最終・mergeは人間。
- 実装・mutation実行・再検証はAI。
- 「AIを増やせば人間不要」は、今回のseverity分裂証拠と矛盾する。

### Benchmark Methodology

- 8候補でも学習は成立する。
- duration未収集はSpeed系指標を壊す。計測は実験の一部として強制すべき。
- pool revisionはraw 0件なら許されるが、必ず記録する。

### Software Engineering Process

- production correctness と verification correctness は別物。
- assurance defectはarchitecture再競争なしで直せる場合が多い。
- Technical Approval / GitHub State / Repository Enforcement は三層。

## 13. FND-05 Recommended Process

FND-05（Docker Compose実行基盤）開始時の推奨構成:

```text
candidate数:           6 active + 1–2 challenger（合計7–8）
H0/H1方式:             H0必須。H1は条件付き
Formal Self-Review:    上位3 + challenger + evaluatorがevidence弱と判定した候補のみ
reviewer数 / role:     5
                       R1 runtime/failure-path
                       R2 deep technical + mutation必須
                       R3 specification/scope
                       R4 adversarial/failure-oriented（新設明示）
                       R5 generalist/fast
Judge構成:             A/B必須、Cは不一致時のみ
Major fix方式:         assurance/test系 → targeted最小fix
                       architecture系 → 最大4候補のtargeted再実装（14全再実行しない）
re-review方式:         assurance系 2本（異harness）。architecture系 3本
Formal Agent B:        原則必須。省略は「変更がdocs-onlyかつ契約非影響」等の明示条件のみ
CI identity:           PR本文に Direct-head run / Merge-ref run を分離記載（必須欄）
処理時間計測:          STARTED/FINISHED/DURATION_MINUTES をwrapperで強制。未記入はN/A
artifact管理:          control branch + results lock。製品PRへbenchmark混入禁止
merge / close:         Technical Approval記録、GitHub event、ruleset要件、Close Evidenceを分離記載
false-assurance gate:  mandatory regressionにはM1/M2相当mutationをSelection時点で要求
```

## 14. 30% Cost Reduction Scenario

> 工程の半分を削るとしたら / 品質維持で約30%削減

削る候補（優先順）:

1. **Formal Self-Review/H1の全candidate必須** → 条件付き化（最大効果）
2. **active candidateを8→6**（challengerは残す）
3. **Framework/browser reviewer枠の常設**を状況次第で省略（今回Major非寄与）
4. Judge C常設化は既に避けているので追加削減なし

削ってはいけない:

- Issue Ready / Selection / Gold / targeted fix / Formal Agent B / exact CI identity

品質維持の要点:

- challenger枠とmutation必須reviewを残す
- G-01型欠陥は人数ではなくmutation設計で防ぐ

## 15. Maximum Quality Scenario

> コスト+20%で品質最大化するなら何を追加するか

追加すべきもの（工程増そのものを目的にしない）:

1. **Selection時のmandatory regression mutation gate**（今回の盲点の直接対策）
2. **Adversarial/Mutation専用reviewer 1枠の固定**（role名だけでなく手順必須）
3. **duration/計測の強制**（速度と品質のトレードオフをデータ化）
4. architecture系Major時のみ **最大4候補のtargeted再実装比較**（標準はtargeted単発）

追加すべきでないもの:

- reviewerを17へ戻す
- Major毎に14再実装
- 3 Judge常設

半減シナリオ vs 30%削減 vs +20%品質の比較:

| シナリオ | 主な操作 | 品質リスク | 推奨度 |
| --- | --- | --- | --- |
| 半減 | SR全廃+candidate大幅減+Agent B省略 | 高（G-01再発・独立gate喪失） | 非推奨 |
| 30%削減 | SR条件付き+6–7候補+review微減 | 低〜中（mutation残すなら許容） | **推奨** |
| +20%品質 | mutation gate + adversarial枠 | 低（狙い撃ち追加） | 高リスクIssue向け推奨 |

## 16. Final Assessment

```text
1. FND-04はFND-03よりプロセスとして改善したか
   → YES。特にcandidate削減とMajor-fixのtargeted化で費用対効果が明確に上がった。

2. 最も効果があった変更は何か
   → Major確定後の「14再実装」から「targeted test-only fix + 2 re-review」への転換。
     併せて、C8失敗パターンをFinal Synthesis mandatory guardへ転用したSelection。

3. 最も費用対効果が低かった工程は何か
   → 全8 candidate必須のFormal Self-Review/H1。
     Finding 0が半数近く、Major見逃しもあり、測定価値に対して実行コストが大きい。

4. FND-05でも必ず残すべき工程は何か
   → Issue Ready/fixed contract、common base、Selection、Final Synthesis、
     2-Judge Gold、mutation付き独立レビュー、Formal Agent B、CI identity分離、Close Evidence。

5. FND-05では削るべき工程は何か
   → Self-Review全必須、弱いcandidate大量投入、Major後の全数再実装benchmarkの標準化。

6. 今回最も重要だった新しい知見は何か
   → productionが正しくCIがgreenでも、committed negative testがdefect classに対して
     false assuranceならmerge-blocking Majorになりうる。
     対策はpositive failure pinとmutation sensitivityである。

7. 現在の開発方式は「過剰」「適正」「不足」のどれか
   → やや過剰だが適正寄り（over-specified but effective）。
     FND-03より改善済み。FND-05でSR条件付き化とmutation前倒しを行えば適正域に入る。
```

### Technical Approval / GitHub Review State / Repository Enforcement（標準記録提案）

今後のmerge記録に最低限含める:

```yaml
technical_verdict: APPROVE | CHANGES_REQUIRED
technical_review_id: <id>
github_review_event: APPROVED | COMMENTED | ...
github_self_approval_blocked: true|false
repository_rule_requires_foreign_approval: true|false
rule_evidence: <check note / prior FND reference>
merge_basis: technical_gate_pass_and_rules_satisfied
```

FND-04実績: technical APPROVE / GitHub COMMENTED / foreign approval not required / merge完了。

---

## Evidence Reviewed（一次証拠）

- Issue #42 body / comments（Issue Ready、Pre-Run Lock、H0 notes、Close Evidence）
- PR #140 body / commits / Formal Agent B review `4894487758`
- Merge commit `9a352a3...` / reviewed Head `3511688...`
- `origin/agent/fnd04-benchmark-control` 配下:
  - `results/implementation-evaluation.md`
  - `results/selection-adjudication.md`
  - `results/final-synthesis-snapshot.md`
  - `review-benchmark/README.md`
  - `review-benchmark/finding-normalization-prejudge.md`
  - `review-benchmark/gold-review.md`
  - `review-benchmark/major-fix-snapshot.md`
  - `review-benchmark/major-fix-clearance.md`
  - `review-benchmark/re-reviews/t1-*.md`, `t2-*.md`
  - `review-benchmark/formal-agent-b-result.md`
  - `review-benchmark/run.json`
  - `review-benchmark/reviewer-pool-revision-2.md`
  - `run.json`
- FND-03比較用: `docs/benchmarks/fnd03-model-comparison/README.md`, `summary.md`, `review-benchmark/summary.md`, `final-fix/README.md`, `final-outcome.md`
- Parent Issue #3（フェーズ確認；本分析は完了済みFND-04 retrospectiveであり実装再実行なし）

他モデルretrospectiveは参照していない（directory空、かつ独立分析方針）。
