# FND-04 Retrospective — GPT5.6 SOL

```yaml
MODEL: "GPT5.6 SOL"
HARNESS: "Browser"
EFFORT: "Pro"
MODEL_SLUG: "gpt5.6-sol-pro-browser"
TARGET_ISSUE: 42
FINAL_PR: 140
FINAL_REVIEWED_HEAD: 3511688401533f60bb77c7dcc647c4c2c4aa84c6
FINAL_MERGE_COMMIT: 9a352a3a61945647273ccc7dfbc8e1816c3ca07c
ANALYSIS_MODE: independent_primary_evidence_only
```

本振り返りは、Issue #42、PR #140、exact commit、GitHub Actions、candidate評価、review / Judge / Gold / clearance、FND-03 archiveを一次証拠として再構成した。既存のFND-04 retrospective成果物は参照していない。

## 1. Executive Summary

### 結論

FND-04は、**FND-03より開発プロセスとして明確に改善した**。改善の中心は、モデル数を増やすことではなく、工程を目的別に分離したことである。

- 初期candidateを14から8へ削減した。
- H0、Formal Self-Review、H1を分離し、実装能力と自己修正能力を観測可能にした。
- 独立reviewを17本の同質投入から、実績上5本のrole-diverse reviewへ縮小した。
- Judgeを3名常設から2名＋条件付き第3 Judgeへ変更した。
- Major発見後は14候補の再実装競争を行わず、1件のtargeted fixと2本のtargeted re-reviewで閉じた。
- benchmark判定とproduct merge gateを分け、最後にfresh contextのFormal Agent Bを実施した。

この変更により、FND-03比で初期candidateは42.9%減、独立reviewerは70.6%減、常設Judgeは33.3%減、Major-fix実装attemptは14本から1本へ92.9%減となった。一方で、Final Synthesisのtest oracleに存在したG-01 / NR-01をmerge前に発見し、test-onlyの最小修正で解消している。したがって、単なるコスト削減ではなく、**少数の異質な観点へ投資を寄せて品質ゲートを維持した改善**と評価する。[^issue128] [^fnd03] [^reviewrun]

ただし、現在の方式を通常の全Issueへそのまま適用するなら**過剰**である。FND-04はbenchmark方法論の実験としては妥当だったが、8候補すべてへのFormal Self-Review、no-change候補を含むH1管理、5 independent reviews、2 Judges、Gold、2 re-reviews、Formal Agent Bという多層構造は、定常開発には重い。FND-05では6候補を基本とし、Formal Self-Reviewを上位・高リスク候補へ限定し、reviewerを4役＋条件付き1役へ縮小するのが妥当である。

### 最も価値があった変更

**Role-diverse Independent Reviewと、mutation evidenceを用いたJudge / Gold adjudicationの組合せ**である。

Final Synthesisのproduction behavior自体は正しかったが、`DesignTimeConnectionSafetyTests`は次の無関係な失敗でもgreenになり得た。

- production design-time factoryへ到達しないtool / build failure
- blocklist外のfabricated destinationへの接続失敗

Deep Technical / Test Assurance担当のR2がこの構造をmutation probeで実証し、Judge A / Bもfresh Phase Aで独立再現した。これは「CIがgreen」「processがnon-zero」「禁止文字列が見つからない」だけでは、意図したfailure pathを通った証拠にならないことを具体的に示した。[^r2] [^gold]

### 最も費用対効果が低かった工程

**全8candidate一律のFormal Self-Review**である。

Self-Reviewにより4候補が改善し、合計7点、最大+3点の効果はあった。しかし、4候補は無変更で、C2 / C3 / C7はevaluatorが認定したMinorを見逃し、C8はMajor C8-M01を見逃した。Finding件数や「自己レビューを実施した事実」だけでは能力を測れない。H0/H1実験自体は価値があったが、次回以降も全candidateへ必須化する根拠は不足している。[^impl]

### FND-05推奨形

```text
6 candidates
  -> H0 lock / exact-head CI
  -> top 4 または risk-triggered Formal Self-Review
  -> changed candidateだけH1 / CI
  -> Selection / Adjudication
  -> curated Final Synthesis
  -> 4 role-diverse reviews (+1 conditional)
  -> Judge A / B (+ Judge C conditional)
  -> targeted fix
  -> targeted re-review 2本
  -> high-risk IssueのみFormal Agent B
  -> direct-head + merge-ref identity
  -> Ready / merge / main identity / close evidence
```

## 2. FND-04 Timeline

時刻は日本標準時。`wall-clock elapsed`は工程間の経過時間であり、モデル処理時間ではない。candidateとreviewの厳密な処理時間は一貫収集されておらず、GitHub timestampから推定しない。[^impl]

| 時点 | 工程 | 一次証拠上の状態 | 前工程のリスクに対する役割 | 評価 |
|---|---|---|---|---|
| 2026-08-09 | Issue Ready / fixed contract | package、ownership、connection key、empty baseline、60秒budget、no-auto-migration、pending-model、idempotent SQLをIssue #42へ固定 | candidateごとの仕様解釈差を抑える | 非常に有効 |
| 2026-08-09 19:48 | Benchmark pre-run lock | common base `38c07e2...`、8 branch同一、candidate未開始 | branch差・先行実装・rubric後付けを防ぐ | 非常に有効 |
| 2026-08-09夜 | 8 candidate H0 | H0 8/8、exact-head CI 8/8 success | 独立初回実装能力を固定 | 有効 |
| 同夜 | Formal Self-Review | 8/8 locked、fresh context、review-only | 自己説明へのアンカリングを抑え、自己検出能力を測る | 効果あり、全件必須は過剰 |
| 2026-08-10 00:13 | H1 lock | H1 8/8、CI 8/8。4候補はH0=H1、4候補は変更 | 自己レビュー後の改善をH0と分離 | 有効 |
| 2026-08-10 10:51 | Implementation Evaluation | H1 winner C5=99、7/8 merge-ready、C8-M01 Major | candidate自己申告ではなく共通probeで比較 | 有効 |
| 2026-08-10 11:07 | Selection / Adjudication | C5 primary、C1一部採用、C8-M01 regression必須、C6 seam不採用 | 単純なwinner mergeではなく設計要素を選別 | 有効 |
| 2026-08-10 11:25–11:54 | Final Synthesis | current mainからcurated実装、29分、initial Head `99cee43...` | candidate履歴・不要差分を持ち込まない | 有効 |
| 2026-08-10 12:02 | Review input lock | Base / Head / merge-ref / CI identityをsnapshot化 | reviewerのwrong targetを防ぐ | 非常に有効 |
| 12:02–14:16 | Role-diverse review 5本、Judge A/B | 5/5 complete。Judge A/BがCHANGES_REQUIRED / NR-01 / NOで一致 | 同質多数決では見えないtest assurance defectを検出 | 最重要 |
| 2026-08-10 14:16 | Gold G-01 / NR-01 lock | productionは正しいがregression testがfalse assurance、Major 1 | findingをroot causeへ正規化し、修正範囲を限定 | 非常に有効 |
| Gold後30分 | Targeted Major fix | 1 commit、1 test file、+18/-0、production変更なし | architecture再設計や全候補再実装を回避 | 非常に有効 |
| 2026-08-10 15:55 | Targeted re-review / clearance | 2/2 G01_FIXED、M1/M2 mutationでred、B0/M0 | fixの有効性と新規重大回帰を確認 | 非常に有効 |
| 2026-08-10 16:22 | Formal Agent B | exact new Headをfresh review、APPROVE、B0/M0、Minor 2 / Nit 3 | benchmark結果とproduct merge gateを分離 | 有効 |
| 2026-08-10 16:40 | Ready / merge | PR #140 merge、merge commit `9a352a3...` | 技術判定・GitHub state・repository ruleを分離して適用 | 有効 |
| 2026-08-10 16:42 | Issue close | mainとmerge commit一致、close evidence、Issue #42 completed | merge前closeや証拠欠落を防ぐ | 非常に有効 |
| merge後 | Benchmark control finalization | review sub-runは`final_complete`。一方、parent `run.json`とREADMEはpre-review / pre-merge状態を残す | product進行をarchiveから分離 | 方針は有効、状態同期は改善必要 |

common-base lockからIssue closeまでのwall-clock elapsedは約20時間54分である。内訳のうち、review snapshotからGold lockまでは2時間14分、Goldからclearanceまでは1時間39分、clearanceからFormal Agent B記録までは約28分、Formal Agent Bからmergeまでは約18分だった。これらは待ち時間、操作時間、artifact更新を含み、モデルruntimeとは扱わない。

## 3. What Was Carried Forward from FND-03

FND-03での問題、導入対策、FND-04での使用、実効果を次に示す。FND-03は14 initial candidates、17 independent reviews、14 Major-fix candidates、3 Judges、28 snapshot archiveという構成だった。[^fnd03]

| FND-03での問題 | 導入された対策 | FND-04での利用 | 実際の効果 |
|---|---|---|---|
| provisional 98点でも後からcleanup Majorが見つかった | Final Synthesis後の独立reviewとGold adjudicationを維持 | production実装だけでなくtest oracleをreview対象化 | G-01をmerge前に発見。green CI依存を回避 |
| candidateとreviewerを大量投入し、重複が多かった | 少数精鋭、役割異質性、条件付きJudge | 8 candidates、実績5 role reviews、Judge A/B | blocking coverageを維持しつつ実行本数を大幅削減 |
| Major発見後に14候補でfix競争を再実施した | targeted fix round | selected Final Synthesisへtest-only fix 1本 | 14→1 attempt。root causeに集中 |
| raw score平均だけではJudge間の論点差を処理できない | Reference先行、normalized root cause、quorum key | A/Bが独立Phase A後にNR-01で一致 | blocking判断を明確化。非blocking disagreementも保存 |
| CIの`head_sha`とactual checkoutを混同し得る | exact Head / merge-ref identityを明示 | initial wordingの誤りをR1/R2が指摘し、最終Headではdirect / merge-refを分離 | 最終merge evidenceの再現性向上 |
| archive作業が大量で次Issue開始を圧迫した | product completionとresearch archiveを分離 | merge / closeをcandidate archiveのcritical pathから外した | product進行は改善。ただしparent control stateのstale化が残った |
| benchmark判定がproduct gateを兼ねると自己参照になる | fresh Formal Agent B | clearance後のexact new Headをbenchmark順位に依存せずreview | B0/M0を独立再確認。Minor 2 / Nit 3も追加発見 |

FND-04はFND-03の方法を単純継承したのではない。FND-03の高コスト部分を縮小し、後段の品質ゲートへ重点を移した。この方向は正しい。

## 4. What Changed in FND-04

| 変更前 | 変更後 | 狙い | 実際の結果 | 副作用 | 今後 |
|---|---|---|---|---|---|
| 14 candidate | 8 candidate | 比較可能性を維持しつつコスト削減 | 80–99点の分布、7/8 merge-ready、1 Majorを観測 | 上位3候補は僅差。下位追加の限界効用が低い | 6＋条件付き2へ変更 |
| 初回実装だけ評価 | H0→SR→H1 | implementationとself-reviewを分離 | 4候補改善、最大+3。C8はMajor見逃し | 8本すべてのSR運用が重い | 上位・高リスクに限定 |
| 同じpromptを多数reviewerへ投入 | role-diverse review | coverageを人数ではなく観点で確保 | Deep Technical roleがG-01をmutationで発見 | planned 6とactual 5の差の理由がcontrol上不鮮明 | 4 core roles＋1 conditional、変更理由を記録 |
| Judge 3名常設 | A/B＋不一致時C | adjudicationコスト削減 | blocking key完全一致、C不要 | common-mode errorは残る | C triggerを一致以外にも拡張 |
| Major fixを複数モデルで競争 | selected branchへtargeted fix | 手戻りをroot causeへ限定 | 30分、1 file、production変更なし | fix authorの選択が単一 | isolated fixでは維持 |
| fix後full review | targeted re-review 2本 | 修正と重大回帰だけ確認 | 2/2 clearance、mutation residueなし | adjacent non-Majorの再検証は限定 | severity別scope matrixを導入 |
| benchmark reviewがmerge判断に近い | Formal Agent Bを別gate化 | context contamination回避 | exact new Headをfull review、B0/M0 | 一部重複コスト | 高リスクIssueで必須、低リスクは統合可 |
| approvalを単一概念として扱う | Technical / GitHub State / Enforcementを分離 | platform制約と技術判定の混同防止 | COMMENTEDでも技術APPROVE、rule上approval不要、通常merge成立 | 記録項目が増える | 標準schema化 |
| 処理時間をtimestampから推定 | explicit start / finish / minutes | speed比較の信頼性向上 | Final Synthesis 29分、fix 30分のみ取得 | candidate / reviewはN/A | wrapperで自動収集 |

## 5. Evaluation of New Experiments

### 5.1 8 candidateへの削減

8候補は、FND-04の比較目的には十分だった。候補はCodex、Claude Code、Cursor、Open Codeを含み、GPT系、Claude、Grok、DeepSeekをまたいだ。H1 scoreは80–99、上位3候補は98–99、下位1候補だけがMajorを持った。したがって、上位モデル識別、harness差、弱いfailure-proof designの検出という3目的を満たした。[^impl]

特にC8は最終採用されなかったが、fabricated design-time destinationというreject patternを具体化し、Final Synthesisへmandatory regressionを追加する材料になった。この意味で弱いcandidateにも価値はあった。ただし、その価値は「弱い候補を大量投入すること」からではなく、**異なる失敗様式を持つchallengerを少数含めること**から生じた。

今後の妥当数は次のとおりである。

- **6 candidateを標準**: 4 core + 2 challengers。
- **8 candidateへ拡張**: 上位scoreが僅差、harness coverage不足、または新モデルの再入場評価が必要な場合。
- **10 candidateは原則不要**: 比較目的が研究公開または新規harness横断でない限り、後段reviewへ予算を回す方がよい。

### 5.2 H0 → Formal Self-Review → H1

H0 snapshotは非常に価値が高い。H1だけを残すと、初回実装品質と自己修正能力が混ざる。FND-04では次が観測できた。

| 観測 | 結果 |
|---|---|
| H0 winner | GPT-5.6 Sol / Codex、98 |
| H1 winner | Claude Opus 5 / Claude Code、99 |
| 最大Self-Review Gain | Claude Sonnet 5 / Claude Code、+3 |
| H0=H1 | 4 / 8 |
| H1改善 | 4 / 8、合計+7 |
| evaluator finding見逃し | C2 / C3 / C7のMinor、C8のMajor |

この結果から、implementation能力とself-review能力は分離評価すべきである。C1のFinding 0は妥当だったが、C8はNitを直してMajorを見逃した。Finding数を能力指標にすると逆転する。必要な指標は次である。

```text
Implementation Score
Self-Review Recall（evaluator findingとの一致）
Self-Review Precision（false positive率）
Accepted Fix Quality
Regression Introduced
H0→H1 Gain
```

次回もH0/H1方式は残す。ただしFormal Self-Reviewは全candidate必須にしない。

- 上位4候補
- evaluator probe不足がある候補
- CI greenだがfailure pathの証拠が弱い候補
- challenger枠の少なくとも1候補

へ限定する。no findingの候補はH1をH0 aliasとして記録し、新しいagent executionやCIを発生させない。

### 5.3 Role-diverse Independent Review

FND-04では、planned 6 rolesに対して最終runは5/5で完了した。人数削減後もblocking coverageは維持された。少なくとも次の差が一次証拠で確認できる。

- R1 runtime / failure-pathはproduction behaviorを広く再現し、Blocker/Major 0、Minor 1とした。
- R2 deep technical / test assuranceは、同じHeadに対してmutationを行い、専用regression testがguard対象の退行でgreenになるMajorを発見した。

同じコードを見ても、runtime correctnessとtest-oracle sensitivityでは結論が変わった。これは同質reviewerを増やすよりrole diversityが有効だった直接証拠である。[^r1] [^r2]

一方、1回の成功だけで「5人なら常にcoverage十分」とは言えない。また、pre-run scaffoldの6 roleと実run 5/5の差が、parent control artifactからは明確に追跡できない。次回はrole変更をrevision logへ記録する。

推奨固定roleは4つである。

1. Deep Technical / Test Assurance
2. Specification / Scope
3. Adversarial / Failure-Oriented
4. Integration / Release / CI Identity

Framework公式仕様の外部照合やGeneralistは、対象Issueがframework依存または4 role間で証拠不足の場合に5枠目として発動する。モデル能力とrole適性は別軸で記録し、同一モデルを万能reviewerとして固定しない。

### 5.4 Judgeを原則2名にしたこと

Judge A / Bは、raw reviewを読む前にfresh Phase A Referenceを作成し、互いの結果を見ずにmutationを再現した。そのうえで次のquorum keyが一致した。

```text
REFERENCE_VERDICT: CHANGES_REQUIRED
BLOCKING_ROOT_CAUSES: [NR-01]
MERGE_READY: NO
```

Judge Cを使わなかった判断は妥当である。さらに、timeout raceの厳密なreachabilityやordinary failure taxonomyのseverityでは差が残り、その差をGoldが消さずに保存した。つまり2-Judge方式は、blocking gateでは収束し、非blocking論点では過度な一致を強制しなかった。[^gold]

ただし、独立Judgeが同じ誤りをするcommon-mode riskは残る。Judge C発動条件は、単純なverdict不一致に加えて次へ拡張する。

- 同じ結論だが根拠が同一reviewerの引用だけで、独立probeがない。
- safety / data-loss / securityのMajorで、severity境界に不確実性がある。
- 2 Judgeとも同じtoolchain failureに依存している。
- pre-locked mutantまたはexpected sentinelを両Judgeが検出できない。
- root causeは一致するがrequired fix directionが相反する。

### 5.5 G-01 / NR-01の発見

G-01は、candidate比較で見逃したproduction defectではない。Final Synthesisのproduction design-time behaviorは正しくfail-closedしていた。見逃したのは、C8-M01を防ぐため新設したtestが本当にそのdefect classを検出できるかという**test oracleの検証**である。

candidate評価では、各実装のproduction behavior、AC、CI、runtime evidenceを比較する比重が高かった。Final SynthesisではC8のanti-patternを避け、`exit != 0`と禁止文字列不在のregressionを追加したため、表面的にはSelectionのmandatory guardを満たして見えた。review input lockもblocking defectなしとした。[^selection] [^snapshot]

Final Synthesis後reviewで発見できた理由は、R2の役割が「test assurance」であり、testの説明やgreen結果ではなく、**guard対象をmutationしてredになるか**を検証したためである。

今後、negative testにpositive failure-reason pinが必要なのは次の場合である。

- `exit != 0`、exception発生、HTTP 4xx/5xxだけを成功条件にする。
- process launch、build、tool restore、DI、factory生成、network、authenticationなど複数の失敗点がある。
- blocklistの非出現で「安全」を推定している。
- failureが意図したprovider / component / operationへ到達したことが重要である。
- fail-closed contractが誤ると、別databaseへのmigration、secret leakage、data lossなどへつながる。

標準assertion形は次である。

```text
negative outcome
+ expected component/path marker
+ expected failure reason
+ forbidden fallback absence
+ mutation sensitivity
```

false assurance検出は、candidate evaluator probe、Final Synthesis author checklist、Deep Technical reviewの3箇所へ組み込む。特に「negative testを1本以上mutationする」をFinal Synthesis reviewの必須項目とする。

### 5.6 Targeted Re-Review

G-01 fix後のre-reviewは、次に限定された。

```text
G-01がfixされたか
+ 新しいBlocker / Majorが増えていないか
```

2 reviewerともbaseline PASS、M1 / M2で対象test FAIL、recovery PASS、residue NONEを確認した。old→new deltaは1 test file、+18/-0、production変更なしだった。full re-reviewより明らかに効率的であり、このseverityと変更範囲では2本が適切だった。[^clearance]

次回はseverityと変更面積でscopeを変える。

| 変更 | 推奨re-review |
|---|---|
| isolated test-only Major | 2 targeted reviewers |
| localized production Major | 2 targeted + 1 adjacent integration role |
| cross-cutting runtime / persistence Major | full 4-role review |
| security、data corruption、auth bypass、Blocker | full review + Judge C原則発動 |

### 5.7 Formal Agent B

Formal Agent Bは、Goldやclearanceを根拠にせずexact new Headを再度full reviewした。G-01 fix後のtargeted re-reviewは意図的にscopeが狭いため、product全体のmerge gateとしてこのfresh reviewには独立した価値があった。結果はAPPROVE、B0/M0、Minor 2 / Nit 3であり、G-01だけでなくIssue #42全AC、direct-head / merge-ref CI、FND-03 regression、secret非出力、pending-model negative probeを再確認した。[^agentb]

重複はあるが、FND-04では省略すべきではなかった。省略可能なのは次をすべて満たす場合に限る。

- 最後のcode change後にfull-scope independent reviewがある。
- reviewerはimplementation / selection / Judgeへ関与していない。
- exact Headとmerge-refを確認している。
- Blocker/Major 0を明示している。
- product riskが低く、repository policy上の別approval要件も満たす。

### 5.8 GitHub approvalと技術approvalの分離

Formal Agent Bの正式技術verdictはAPPROVEだったが、認証actorとPR authorが同じためGitHubは自己APPROVEを422で拒否し、review eventはCOMMENTEDとなった。repository ruleは別actorのAPPROVED stateをrequiredにしておらず、通常PR経路でbypassなしにmergeできた。[^agentb] [^close]

今後は次を標準記録する。

```yaml
technical_review:
  verdict: APPROVE | CHANGES_REQUIRED
  reviewer_identity: <model/harness/actor>
  reviewed_head_sha: <sha>
  blocker: <n>
  major: <n>

github_review:
  event: APPROVED | COMMENTED | CHANGES_REQUESTED
  review_id: <id>
  actor: <github actor>
  platform_constraint: <none | self_approval_422 | ...>

repository_enforcement:
  ruleset_checked_at: <timestamp>
  required_approvals: <n>
  required_checks: [<names>]
  bypass_used: true | false

merge_identity:
  base_sha: <sha>
  reviewed_head_sha: <sha>
  merge_ref_sha: <sha>
  merge_commit_sha: <sha>
```

## 6. G-01 / NR-01 Case Study

### 6.1 何が正しく、何が誤っていたか

| 対象 | 状態 |
|---|---|
| production `BankDbContextFactory` | 正しい。fake / SQLite / InMemory / fabricated destinationへfallbackしない |
| connection-required `dotnet-ef database update` | destination未構成のNpgsql pathでfail-closed |
| committed regression test | 不十分。何が失敗してもnon-zeroなら通り得る |
| CI | greenだが、oracle sensitivityまでは証明しない |

旧testの主要条件は、`exit != 0`と固定blocklistの不在だった。次のmutationでもgreenになった。

1. `Host=db;Database=ambient_fallback`のようなblocklist外destinationをfabricateする。
2. build outputを到達不能にし、factoryへ到達する前にtoolが失敗する。

これは「失敗した」ことと「意図した理由で失敗した」ことを混同した典型的false assuranceである。

### 6.2 なぜcandidate比較で見逃したか

- candidate評価の対象は各candidate H0/H1であり、Final Synthesis固有testはまだ存在しなかった。
- C8-M01をreject patternとして認識できたため、Final Synthesis側のguard追加で安心しやすかった。
- Selection checklistはguardの存在を要求したが、mutation sensitivityまではmandatoryにしていなかった。
- review input lockはsource / CI /scopeを再確認したが、test oracleを破壊するprobeは行わなかった。

### 6.3 なぜ後段reviewで発見できたか

- Deep Technical / Test Assuranceという明確なroleがあった。
- reviewerがPR説明やtest名を証拠として扱わず、mutationで感度を測った。
- Judge A / Bがreviewerの結論を単に採用せず、fresh contextで同種mutationを再実行した。
- Goldがproduction correctnessとassurance defectを分離したため、「productionは正しいからMinor」と矮小化しなかった。

### 6.4 修正の妥当性

新Headは、unconfigured connection、empty destination、Npgsql / EF migrations pathをpositiveにpinした。M1とM2 mutationは対象testをredにし、recovery後はgreen、残渣なしとなった。変更は1 test file、+18/-0で、production architectureを触っていない。これはroot causeに対する最小かつ十分なfixである。[^clearance]

### 6.5 今後の標準

- negative testはfailure reasonをpinする。
- safety regressionは少なくとも1つのcontrolled mutationで感度を確認する。
- blocklistは補助証拠とし、allowlist / positive path markerを主証拠にする。
- test descriptionが主張するcontractと、assertionが実際に証明する範囲をreview項目にする。
- `green CI`をcoverageやoracle correctnessの代替にしない。

## 7. Human-in-the-loop Analysis

### Evidence limit

GitHub上の多数の操作は同じ`kooiei-in4a` identityで実行されており、commit / comment actorだけから「Koo本人の手動操作」か「Kooの認証情報を使うAI agent操作」かを判別できない。したがって、人間介在回数をGitHub actor数から推測しない。明示的に確認できるのは、Issue #128に記録された「Key decisions already approved by Koo」と、最終的なprocess / risk trade-offである。[^issue128]

### 分類

| 分類 | FND-04での判断 | 評価 |
|---|---|---|
| 人間が必要だった判断 | 14→8の実験設計、candidate pool、self-review導入、reviewer role、Judge quorum、Major severityの受容、productとbenchmarkの優先順位 | 目的・予算・risk appetiteを含むため人間が残るべき |
| AIへ移譲できた判断 | exact Head検証、diff / CI取得、candidate score、finding正規化、mutation probe、targeted re-review、merge/close evidence収集 | 一次証拠と停止条件が明確であり自動化向き |
| 今後AIへ移譲できそうな判断 | Judge C trigger、artifact stale検出、time telemetry、ruleset snapshot、review role coverage lint | machine-readable stateとpolicyを整備すれば可能 |
| 人間が残るべき判断 | benchmarkを続ける価値、30%コスト削減の許容、非blocking findingを残してmergeする判断、repository governance、最終的なproduct/process trade-off | 技術的正解だけでは決まらない |

### 個別論点

- **benchmark設計**: AIは候補案と期待情報量を提示できるが、研究目的と予算の上限は人間が決める。
- **candidate選択**: 過去scoreとrole coverageからAIへ大部分移譲可能。ただし新モデルをchallengerへ入れる戦略判断は人間承認を残す。
- **Gold / Judge運用**: evidence normalizationはAI向き。severityとmerge blockingの最終policyは人間のrisk基準へ従う。
- **merge gate**: 技術判定はAIへ移譲可能だが、GitHub identity、ruleset、責任主体の確認は人間が監督する。
- **repository policy**: 読み取り・比較は自動化し、policy変更とbypass承認は人間に限定する。
- **Major severity**: AI複数Judgeで高精度化できるが、data loss / regulatory / business impactを含む場合は人間承認を必須にする。

AIを増やすほど人間が不要になるわけではない。FND-04では、AIの数よりも、人間が**何を比較実験とし、何をproduct gateとするかを分離したこと**が重要だった。

## 8. Cost / Time / Complexity

### 8.1 取得できた数量

| 指標 | 値 | 証拠区分 | 注記 |
|---|---:|---|---|
| initial candidates | 8 | 一次証拠あり | H0 8/8 |
| Formal Self-Review | 8 | 一次証拠あり | 4改善、4無変更 |
| H1 logical snapshots | 8 | 一次証拠あり | exact-head CI success 8/8 |
| candidate distinct CI runs | 最低12 | 概算可能 | H0 8本＋変更H1 4本。no-changeはH0 run再利用 |
| Implementation Evaluation | 1 | 一次証拠あり | 7/8 merge-ready |
| Selection / Adjudication | 1 | 一次証拠あり | C5 primary |
| Final Synthesis | 1 | 一次証拠あり | 29分明示記録 |
| initial Final Synthesis CI | 2 | 一次証拠あり | direct-head + merge-ref |
| role-diverse independent reviews | 5 | 一次証拠あり | planned 6からrun v2で5/5 |
| Judges | 2 | 一次証拠あり | Judge C 0 |
| Gold blocking finding | 1 | 一次証拠あり | G-01 / NR-01 |
| Major fix | 1 | 一次証拠あり | 30分、test-only |
| final fixed Head CI | 2 | 一次証拠あり | direct-head + merge-ref |
| targeted re-review | 2 | 一次証拠あり | 2/2 G01_FIXED |
| Formal Agent B | 1 | 一次証拠あり | APPROVE / B0 M0 |
| FND-04で確認できるdistinct CI | 最低16 | 概算可能 | candidate 12＋old final 2＋new final 2。base/mainや補助runは除外 |
| candidate implementation時間 | 取得不能 | — | run registryがN/Aを明示 |
| candidate self-review時間 | 取得不能 | — | 同上 |
| independent review時間 | 取得不能 | — | stage elapsedとagent runtimeを分離できない |
| 人間の操作回数 | 取得不能 | — | 同一GitHub actorから人間/agentを識別不能 |

### 8.2 工程別の重さ

- **重い**: 8 H0、8 Self-Review、role review 5本。
- **中程度**: 2 Judges、Final Synthesis、Formal Agent B。
- **軽いが高価値**: targeted fix 1本、targeted re-review 2本、exact identity確認。
- **機械時間が長い**: production 60秒timeout testを含むreal PostgreSQL suite。Final Synthesis local PG suiteは約3分32秒、Formal Agent B isolated runは2分18秒だった。
- **運用上重い**: artifact status更新。review sub-runはfinalizedしたがparent stateが追従していない。

### 8.3 FND-03比の削減

| 対象 | FND-03 | FND-04 | 削減 |
|---|---:|---:|---:|
| initial candidate | 14 | 8 | 42.9% |
| independent review | 17 | 5 | 70.6% |
|常設Judge | 3 | 2 | 33.3% |
| Major-fix candidate attempt | 14 | 1 | 92.9% |

FND-04はH0/SR/H1の8 self-review slotsを追加したため、単純に上表を合算して総コスト削減率とはできない。それでも、FND-03の最も高コストだった「多数reviewer」と「Major後の14候補再実装」を削った効果は大きい。

### 8.4 複雑性上の問題

benchmark control branchには、次の状態差がある。

- `review-benchmark/run.json`: `final_complete`、merge / closeまで記録。
- top-level `run.json`: role-diverse review開始前相当のstatusを残す。
- top-level README: Formal Agent B後・merge前のstateを残す。

product codeの正しさには影響しないが、canonical stateが複数あるため、後続analystが誤ったstageを読む可能性がある。artifact管理は**single machine-readable source + generated Markdown**へ変更すべきである。[^toprun] [^index] [^reviewrun]

## 9. Process Scorecard

基準: 5=非常に有効、4=有効、3=効果はあるが改善余地大、2=費用対効果が低い、1=廃止候補。

| Process | Score | 価値 | コスト | 問題点 | 次回方針 |
|---|---:|---|---|---|---|
| Issue Ready / fixed contract | 5 | candidate間の仕様差を抑制 | 事前調査 | contractが長い | KEEP |
| 8 candidate benchmark | 4 | 上位識別とanti-pattern発見 | 実装・CI 8本 | 定常運用には多い | 6＋2 conditional |
| H0 snapshot | 5 | 初回能力を保存 | 低い | なし | KEEP |
| Formal Self-Review | 3 | 4候補改善、C6で有効 | 8 review slots | Major見逃し、半数無変更 | top 4 / risk-triggered |
| H1 snapshot | 4 | self-review gainを測定 | state / CI管理 | no-change管理が冗長 | changed-only CI |
| candidate Selection / Adjudication | 4 | winner mergeを避け、要素別採用 | 1 adjudication | author判断の説明負荷 | KEEP |
| Final Synthesis | 4 | clean baseからproduction用にcurate | 再実装1本 | initial test oracleにMajor | KEEP、mutation checklist追加 |
| role-diverse independent review | 5 | G-01発見の中心 | 5 reviews | planned / actual差の記録不足 | 4＋1 conditional |
| 2-Judge方式 | 4 | blocking root causeを独立収束 | 2 deep reviews | common-mode risk | C trigger拡張 |
| Gold Review | 5 | production defectとassurance defectを分離 | adjudication | reviewer後lockで運用複雑 | KEEP、pre-locked mutant併用 |
| targeted Major fix | 5 | 1 file / 30分でroot cause解消 | 非常に低い | single author | KEEP |
| targeted re-review | 5 | 2/2 mutation clearance | 2 reviews | severity別scope未標準 | KEEP、matrix化 |
| Formal Agent B | 4 | product gate独立性 | full review 1本 | 一部重複 | high-risk必須、低リスク条件付き |
| exact Head / merge-ref CI | 5 | checkout identityを監査可能 | CI 2系統 | 初期PR wordingに誤り | KEEP、workflowで自動表示 |
| benchmark artifact管理 | 3 | traceabilityは高い | 更新負荷大 | parent/sub-run state不一致 | single source化 |
| merge / close evidence管理 | 5 | reviewed Head→merge→main→closeを連結 | 低い | post-merge runは別途なし | KEEP |

## 10. KEEP / MODIFY / DROP

### KEEP

- Issue Ready / fixed implementation contract
- common baseとcandidate branch identityのpre-run lock
- H0 exact snapshot
- candidate評価前のReference / probe lock
- Selection / Adjudicationとcandidate merge禁止
- current mainからのcurated Final Synthesis
- Deep Technical / Test Assuranceを含むrole-diverse review
- Judge A / B＋条件付きJudge C
- mutation evidenceを使うGold / root-cause normalization
- selected branchへのtargeted Major fix
- isolated Majorに対する2本のtargeted re-review
- direct-head / merge-ref CI identity
- Technical Approval / GitHub State / Repository Enforcementの分離
- merge commit / main identity / Issue close evidence

### MODIFY

- 8 candidates → 6 core、必要時のみ+2
- Formal Self-Review 8/8 → 上位4またはrisk-triggered
- H1 no-change候補 → H0 aliasのみ。新run不要
- reviewer 5 → 4 core roles、5人目は条件付き
- Judge C trigger → verdict不一致以外のcommon-mode条件を追加
- Formal Agent B → foundation / security / persistence / releaseでは必須、低リスクではequivalent exact-head full reviewと統合
- 処理時間 → agent wrapperで自動計測し、モデル自己申告を補助扱い
- artifact管理 → parent stateを単一JSONから生成し、stale lintをCI化
- reviewer pool変更 → planned→actual差と理由をrevision recordへ必須記録

### DROP

- telemetry欠損時のSpeed Score推定
- GitHub timestampからagent runtimeを算出する運用
- no-change H1の再実行・再CI
- 「候補数を満たすためだけ」の弱いcandidate追加
- isolated test-only fix後のfull review全件再実行
- 3 Judge常設
- Majorごとの全candidate fix benchmark
- README、parent run、sub-runへ同じmutable statusを手作業で重複記録する方式

## 11. FND-03 vs FND-04

| 観点 | FND-03 | FND-04 | 評価 |
|---|---|---|---|
| candidate構成 | 14 initial、1 no-change | 8、6 core + 2 challenger | FND-04が効率的。FND-05は6へ縮小可能 |
| implementation評価 | initial ranking中心 | H0 / H1を分離、共通probe | FND-04が分析力で優位 |
| self-review | formal分離なし | 8/8 H0→SR→H1 | 新知見あり。ただし全件必須は過剰 |
| independent review | 17同一系統 | 実績5 role-diverse | FND-04が大幅に効率化しG-01も検出 |
| Judge | Major-fixで3名 | A/B、C conditional | FND-04が必要十分 |
| Major発見 | post-hoc Goldでcleanup Major | role review / Judge / Goldでtest-oracle Major | 両方ともFinal Synthesis後に発見。FND-04はassurance分析が深化 |
| Major fix | 14-model fix benchmark | 1 targeted test-only fix | FND-04が圧倒的に効率的 |
| Formal Agent B | final full review、B0/M0/m0/n0 | final full review、B0/M0/m2/n3 | gateは同等。FND-03の方が非blocking残件は少ない |
| merge gate | exact Head、Agent B、post-merge CI | direct-head + merge-ref、rule分離、main identity | FND-04はidentity説明が強い。独立post-merge runはFND-03が明示的 |
| benchmark archive | 28 tags、27 PR close、28 branch deleteまで完了 | product進行とarchive分離。sub-run final、parent stale | FND-04の方針は良いがartifact finalizationは未成熟 |
| 工数 | 14実装＋17review＋14fix＋3Judge | 8実装＋8SR＋5review＋1fix＋2Judge＋2re-review | FND-04が明確に削減。ただし総runtimeは取得不能 |
| 品質 | final Agent B B0/M0、非blocking 0 | final Agent B B0/M0、Minor 2 / Nit 3 | 必須品質は双方達成。FND-04が絶対的に高品質とは断定しない |

FND-04は「新しいから優れている」のではない。FND-03で高コストだった工程を減らしながら、別種のMajorをmerge前に検出し、targetedに閉じたため、**プロセス効率が改善した**。一方、最終非blocking finding数とartifact state整合ではFND-03に劣る面がある。

## 12. Key Learnings

### AI Coding

- 上位candidate間の差はproduction codeよりfailure-path evidenceの強さに現れた。
- C5の実PostgreSQL lock + real processと、C6のdeterministic TimeProvider seamは、異なる正解だった。
- winnerをそのままmergeせず、C5 primary + C1 secret regression + C8 reject patternという要素選択が有効だった。

### AI Self-Review

- self-review能力はimplementation能力と独立である。
- Finding 0は正しい場合も、false negativeの場合もある。
- self-reviewは全員一律より、evidence gapのあるcandidateへ配分すべきである。

### AI Code Review

- reviewer数ではなくroleがcoverageを決めた。
- runtime reviewerがAPPROVEし、test assurance reviewerがMajorを発見したことは、観点分離の直接証拠である。
- review promptには「testを壊して感度を測る」操作を含めるべきである。

### Judge / Adjudication

- 2 Judgeでblocking gateは十分だった。
- agreementの価値は結論一致ではなく、独立probeによるroot cause一致にある。
- severity disagreementを無理に消さず、Goldで範囲を限定したことが適切だった。

### Test Quality

- negative outcomeだけではcontractを証明しない。
- blocklist absenceはdestination absenceではない。
- test oracleにもmutation testing的検証が必要である。
- `green CI`は「実装が正しい」だけでなく「testが正しい」を保証しない。

### Multi-Agent Development

- 多数決より、実装、self-review、independent review、Judge、merge gateの責任分離が有効だった。
- 同じactor identityを使う場合、fresh contextだけでなく参照禁止範囲とexact target lockが必要である。
- agentを増やすより、前工程が潰すべきriskを明示した方がよい。

### Human / AI Responsibility Boundary

- AIは証拠収集、probe、比較、修正、re-reviewを高い再現性で実行できる。
- 人間は実験目的、予算、risk tolerance、repository governanceを所有する。
- merge判断をAIへ移譲しても、責任主体とplatform identityの設計は人間側に残る。

### Benchmark Methodology

- 8候補で十分なscore dispersionとanti-patternが得られた。
- H0/H1は有効な測定設計だが、全candidate実行は必要ない。
- pre-run lock、raw artifact immutability、candidate / final synthesisの分離は維持すべきである。
- speed telemetryが欠損したらN/Aとする判断は正しかった。

### Software Engineering Process

- Issue Readyでimplementation contractを固定するほど、後段reviewは深いtest assuranceへ集中できる。
- targeted fixは、root causeとchange surfaceが限定できる場合に最も効率的である。
- Technical Approval、GitHub Review State、Repository Enforcementは別のstate machineである。
- artifactのsingle source of truthがないと、productが完了してもprocess記録がstaleになる。

## 13. FND-05 Recommended Process

### 13.1 推奨構成

```yaml
candidate_pool:
  standard: 6
  composition:
    core: 4
    challengers: 2
  expand_to_8_when:
    - top scores are tied and evidence differs materially
    - a required harness or failure profile is missing
    - a new model requires controlled re-entry

h0:
  required_for: all_candidates
  exact_head_ci: required
  immutable_snapshot: required

formal_self_review:
  required_for:
    - top_4_candidates
    - candidates_with_evidence_gaps
    - at_least_1_challenger
  fresh_context: required
  fix_before_finding_lock: prohibited

h1:
  create_new_head_only_when: accepted_change_exists
  no_change: alias_h0
  exact_head_ci: changed_candidates_only

independent_review:
  core_roles: 4
  roles:
    - deep_technical_test_assurance
    - specification_scope
    - adversarial_failure_path
    - integration_release_ci_identity
  fifth_role: conditional

judges:
  default: 2
  judge_c: conditional
  trigger_on:
    - blocking_verdict_disagreement
    - root_cause_disagreement
    - merge_ready_disagreement
    - common_mode_evidence_risk
    - high_impact_severity_uncertainty

major_fix:
  default: selected_final_branch_only
  competing_fix_candidates: exceptional
  require_change_surface_lock: true

re_review:
  isolated_test_only_major: 2_targeted_reviewers
  localized_production_major: 2_targeted_plus_1_adjacent
  cross_cutting_or_security: full_role_review

formal_agent_b:
  required_for:
    - foundation
    - persistence
    - authentication_authorization
    - security
    - release_infrastructure
  conditional_for: low_risk_local_changes

ci_identity:
  direct_head: required
  pr_merge_ref: required
  record_actual_checkout_sha: required
  main_tree_identity_after_merge: required
  post_merge_ci: conditional_when_workflow_context_differs

time_measurement:
  source: harness_wrapper
  fields:
    - started_at_monotonic
    - finished_at_monotonic
    - elapsed_seconds
    - ci_queue_seconds
    - ci_run_seconds
  github_timestamp_inference: prohibited

artifact_management:
  canonical_state: single_json
  markdown_status: generated
  stale_state_lint: required
  planned_actual_role_delta_reason: required

merge_close:
  technical_verdict: required
  github_review_state: required
  repository_enforcement_snapshot: required
  bypass_record: required
  merge_commit_main_identity: required
  close_evidence: required
```

### 13.2 FND-05実行順序

```text
Issue Ready / fixed contract
→ pre-run common base + 6 branches
→ H0 6/6
→ evaluator reference lock
→ risk-based Formal Self-Review
→ changed H1 only
→ implementation evaluation
→ element-level Selection / Adjudication
→ curated Final Synthesis
→ direct-head + merge-ref CI
→ 4 role-diverse reviews
→ Judge A/B
→ Gold / required fix
→ severity-based targeted fix and re-review
→ Formal Agent B when risk profile requires
→ repository enforcement check
→ Ready / merge / main identity / close
→ archive asynchronously, with state lint
```

## 14. 30% Cost Reduction Scenario

### 14.1 工程の半分を削るなら何を削るか

半分削る場合でも、Issue Ready、exact identity、Deep Technical review、Judge、targeted fix、merge evidenceは残す。

削る対象は次である。

1. candidate 8→4。ただしcore 3 + challenger 1を維持する。
2. Formal Self-Review 8→上位2。
3. no-change H1 executionを全廃する。
4. role review 5→3。ただしTest Assurance、Specification、Integrationは残す。
5. Judge C常設を削る。もともとconditionalなので実質維持。
6. Formal Agent Bを低リスクIssueでは統合する。
7. duplicated artifact status更新を削る。

これは品質を一定程度落とす。特にAdversarial roleが3 review枠から外れる場合、G-01相当の検出確率が下がるため、通常推奨にはしない。

### 14.2 品質をほぼ維持して30%削減する推奨案

FND-04の主要agent phase slotsを、H0 8、SR 8、H1 8、role review 5、Judge 2、re-review 2、Formal Agent B 1の計34 logical slotsとして扱う。実invocation数やtoken量ではなく、計画上のslot数である。

推奨案は次である。

- H0: 6
- SR: 4
- H1: 最大4、accepted changeがある場合のみ
- role review: 4
- Judge: 2
- re-review: 2
- Formal Agent B: 1

最大でも23 slotsで、34→23、約32.4%削減となる。Final Synthesisとtargeted fixは双方の方式で必要なため比較外とした。

品質を維持できる理由は、削るのが主に次だからである。

- score下位の追加candidate
- no-change self-review / H1
- role overlapの大きい5人目

一方、G-01を見つけたTest Assurance role、2 Judges、targeted mutation re-review、exact-head / merge-ref CIは削らない。

追加の運用削減として、single JSONからREADMEを生成し、candidate identity、time、CI checkout SHAを自動収集する。人間のコピー操作とstale修正を減らせる。

## 15. Maximum Quality Scenario

コストを20%増やす場合、candidateを10へ増やすより、oracle qualityとindependenceへ投資する。

### 追加するもの

1. **Pre-locked Controlled Mutant**
   - Final Synthesis review前に、negative testの代表的false assuranceを1つcollector-privateに用意する。
   - reviewer能力をreal targetと分けて測る。

2. **Mutation / Oracle specialist 1名**
   - role-diverse reviewの5人目を常設し、process failure、exception reason、test reachabilityを担当する。

3. **Judge C dissent audit**
   - security / persistence / data-loss Majorでは、A/B一致でも1名が独立probeだけを確認する。

4. **timeout flake observation**
   - 60秒実時間testを3回のscheduled CIで観測し、duration分布とexit taxonomyを保存する。
   - flaky evidenceが出た場合だけC6のTimeProvider seamを採用する。

5. **non-author GitHub approval**
   - 技術reviewとplatform approvalを一致させるため、可能なら別actorのAPPROVED stateを得る。

6. **post-merge main CI**
   - merge-ref treeとmain treeが同一でも、push-only workflowや環境差がある場合はmain runを追加する。

7. **artifact consistency CI**
   - parent / sub-run / READMEのstatus差をfailさせる。

### 追加しないもの

- 弱いcandidateの大量追加
- 3 Judge常設
- isolated Majorに対する全reviewer再実行
- 全candidate Major-fix競争

品質最大化では、実装案の数より「testが正しく失敗するか」「最後のHeadを本当に見たか」「独立性が保たれたか」へ予算を使うべきである。

## 16. Final Assessment

### 1. FND-04はFND-03よりプロセスとして改善したか

**はい。明確に改善した。**

初期candidate、reviewer、Judge、Major-fix attemptを削減しながら、別種のblocking Majorをmerge前に発見し、1 test fileのtargeted fixで閉じた。改善は最終コードの絶対品質ではなく、**同等の必須品質へ到達するための判断効率と手戻り制御**にある。

### 2. 最も効果があった変更は何か

**Role-diverse Independent Review、とりわけDeep Technical / Test Assurance roleを明示し、Judge A/Bがmutationで独立再現したこと。**

### 3. 最も費用対効果が低かった工程は何か

**全8candidate一律のFormal Self-Reviewと、no-change候補を含むH1運用。**

H0/H1の実験価値はあったが、定常化するなら対象を絞るべきである。

### 4. FND-05でも必ず残すべき工程は何か

**Issue Ready、H0 lock、exact-head / merge-ref CI、role-diverse review、mutation-backed Gold、targeted fix / re-review、merge / close identity。**

### 5. FND-05では削るべき工程は何か

**candidateを8から6へ、Formal Self-Reviewを8から4へ削る。no-change H1実行とduplicated status手更新は廃止する。**

### 6. 今回最も重要だった新しい知見は何か

**negative testがgreenでも、安全contractを証明しているとは限らない。failure outcomeに加えて、期待component、failure reason、destination状態、mutation sensitivityを固定しなければfalse assuranceになる。**

### 7. 現在の開発方式は「過剰」「適正」「不足」のどれか

**過剰。**

FND-04というbenchmark方法論実験としては許容できるが、通常の開発標準としては工程が多い。高価値gateを残したまま約30%削減できる一次証拠がある。

---

## Evidence Reviewed

[^issue42]: [Issue #42 — FND-04 fixed implementation contract / close condition](https://github.com/kooiei-in4a/minimal-bank-system/issues/42)
[^issue128]: [Issue #128 — FND-03 learnings applied to FND-04 methodology](https://github.com/kooiei-in4a/minimal-bank-system/issues/128)
[^pr140]: [PR #140 — Final Synthesis](https://github.com/kooiei-in4a/minimal-bank-system/pull/140)
[^close]: [Issue #42 close evidence comment](https://github.com/kooiei-in4a/minimal-bank-system/issues/42#issuecomment-5237276272)
[^impl]: [FND-04 Implementation Evaluation](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd04-benchmark-control/docs/benchmarks/fnd04-model-comparison/results/implementation-evaluation.md)
[^selection]: [FND-04 Selection / Adjudication](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd04-benchmark-control/docs/benchmarks/fnd04-model-comparison/results/selection-adjudication.md)
[^snapshot]: [FND-04 Final Synthesis Snapshot](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd04-benchmark-control/docs/benchmarks/fnd04-model-comparison/results/final-synthesis-snapshot.md)
[^r1]: [R1 Runtime / Failure-Path Review](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd04-benchmark-control/docs/benchmarks/fnd04-model-comparison/review-benchmark/reviews/gpt-5.6-sol-codex.md)
[^r2]: [R2 Deep Technical / Test Assurance Review](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd04-benchmark-control/docs/benchmarks/fnd04-model-comparison/review-benchmark/reviews/claude-opus-5-claude-code.md)
[^gold]: [FND-04 Adjudicated Gold / Reference Review](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd04-benchmark-control/docs/benchmarks/fnd04-model-comparison/review-benchmark/gold-review.md)
[^clearance]: [G-01 Major-Fix Clearance](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd04-benchmark-control/docs/benchmarks/fnd04-model-comparison/review-benchmark/major-fix-clearance.md)
[^agentb]: [Formal Agent B review on PR #140](https://github.com/kooiei-in4a/minimal-bank-system/pull/140#pullrequestreview-4894487758)
[^reviewrun]: [FND-04 review benchmark final run registry](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd04-benchmark-control/docs/benchmarks/fnd04-model-comparison/review-benchmark/run.json)
[^toprun]: [FND-04 parent run registry](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd04-benchmark-control/docs/benchmarks/fnd04-model-comparison/run.json)
[^index]: [FND-04 benchmark control README](https://github.com/kooiei-in4a/minimal-bank-system/blob/agent/fnd04-benchmark-control/docs/benchmarks/fnd04-model-comparison/README.md)
[^fnd03]: [FND-03 complete experiment archive](https://github.com/kooiei-in4a/minimal-bank-system/blob/9a352a3a61945647273ccc7dfbc8e1816c3ca07c/docs/benchmarks/fnd03-model-comparison/README.md)
[^methodcommit]: [Commit applying FND-03 retrospective to FND-04](https://github.com/kooiei-in4a/minimal-bank-system/commit/c0a3422d07f20f0b21bd638d5d1280b5c868f09e)

```text
MODEL: GPT5.6 SOL
HARNESS: Browser
EFFORT: Pro
OUTPUT: docs/retrospectives/fnd04-retrospective-gpt5.6-sol-pro-browser.md
EVIDENCE REVIEWED: Issue #42, Issue #128, PR #140, exact commits, direct-head / merge-ref CI, H0/SR/H1 registry, Implementation Evaluation, Selection / Adjudication, Final Synthesis snapshot, role-diverse reviews, Judge/Gold, Major-fix clearance, Formal Agent B, FND-03 archive
FINAL ONE-LINE ASSESSMENT: FND-04はFND-03より少ないcandidate・review・fix attemptでblocking Majorをmerge前に検出してtargetedに解消した点で改善したが、通常運用としては約30%削減すべき過剰なプロセスである。
```
