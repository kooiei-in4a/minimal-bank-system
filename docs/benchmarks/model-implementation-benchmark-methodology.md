# AIモデル実装ベンチマーク方法論

- Status: Active
- Initial version: 2026-08-08
- Updated: 2026-08-08（Issue #40 FND-02の比較で得た再利用可能な検証知見を反映）
- Origin: Issue #39 FND-01 model comparison experiment
- Applies to: 同一Issueを複数のModel + Agent/Harnessで独立実装し、実コードを比較評価する実験

## 1. 目的

この文書は、同一の実装Issueを複数のAIモデル／コーディングエージェントへ実装させ、成果物を同じ基準で比較するための共通手順を定める。

目的は公開ベンチマーク順位を再現することではなく、対象repositoryと対象Issueに対して、どの `Model + Agent/Harness + Effort` が正確・簡潔・安全な変更を作れるかを一次証拠から評価することである。

この文書は製品仕様、ADR、Issue scopeを変更しない。対象Issueの正本が常に優先する。

## 2. 基本原則

1. **実コードを最優先する**
   - 評判、モデル価格、公開ベンチマークだけで採点しない。
2. **必要十分な変更を高く評価する**
   - コード量、抽象化、機能追加の多さを性能とみなさない。
3. **Scope先取りを減点する**
   - 後続Issueの責任を実装したことを加点理由にしない。
4. **品質と速度を分離する**
   - Coding Scoreへ処理時間を混ぜない。
5. **ModelとHarnessを混同しない**
   - 結果は `Model + Agent/Harness + Effort` の組合せとして扱う。
6. **1回の結果で一般性能を断定しない**
   - 各候補1試行なら、その試行の成果物評価として扱う。
7. **自己申告を一次証拠にしない**
   - PR説明やAgent自身の感想より、Issue、diff、test、CIを優先する。

## 3. 実験開始前に固定するもの

候補の実装開始前に、次を記録する。

- Target Issue / title
- Authorityとなる仕様・ADR・計画・`AGENTS.md`
- common base branch / full SHA
- 候補Model
- Agent / Harness
- Effort設定
- branch naming
- 処理時間の記録方法
- 共通実装指示
- Coding Scoreの配点
- Issue固有の採点観点
- Scope / Out of scope
- 必須verification

**実装結果を見た後で、特定候補に有利になるよう採点基準やcommon baseを変更しない。**

評価基準の誤りを修正する必要が生じた場合は、変更理由を記録し、全候補へ同じ基準を遡及適用する。

## 4. 候補実装

各候補は原則として同一common baseから独立branchを作る。

各候補は対象Issueの通常のAgent A責務に従い、少なくとも次を行う。

1. Parent / Control Issue確認
2. Target Issueと正本確認
3. Scope / Out of scope確定
4. 実装
5. local verification
6. diff self-review
7. Draft PR作成
8. CI確認
9. Post-Implementation Notes記録
10. benchmark snapshot Head固定

他候補の差分・評価・採点結果を見てから実装を修正しない。候補間の情報漏れが避けられない場合は実験記録に明記する。

## 5. Post-Implementation Notes

各候補は、実装・self-review・CI確認後、snapshot Headを固定する前にPRへ**1回だけ**Post-Implementation Notesを残す。

目的は自己採点ではなく、コードだけでは観測しにくい判断過程、迷い、Harness上の制約を実験メタデータとして保存することである。

推奨形式:

```markdown
## Agent A Post-Implementation Notes

### Difficult / uncertain decisions
- なし
  または
- 判断に迷った点

### Scope decisions
- 意図的に実装しなかった項目:
  - ...
- Scope境界で迷った項目:
  - ...

### Validation
- 実行した検証:
  - ...
- failure / retry / workaround:
  - ...

### Known concerns
- なし
  または
- ...

### Unverified
- なし
  または
- ...

### Harness / tool observations
- なし
  または
- ...
```

ルール:

- 採点結果を見た後に追記・修正しない。
- 自己申告が立派であることをCoding Scoreの加点理由にしない。
- 実コードと自己認識の一致／不一致は、必要に応じて別の分析材料にする。

## 6. 一次証拠の評価順序

原則として次の順で確認する。

1. Target Issueの要求・Acceptance Criteria
2. 承認済み仕様・Accepted ADR・`AGENTS.md`
3. common baseからcandidate Headまでの実diff
4. local build / test / verification結果
5. CI結果
6. 設計品質、Repository適合性、変更最小性
7. Post-Implementation Notes
8. 公開情報・外部ベンチマーク

外部ベンチマークと今回の実コード評価が食い違う場合、今回の実コードを優先する。

## 7. 共通Coding Score

標準配点は100点とする。

| Category | Points | Meaning |
|---|---:|---|
| A. Issue達成度 | 25 | Acceptance Criteriaを正確に満たすか |
| B. 正しさ・実行可能性 | 15 | build/test/runtime等が実際に成立するか |
| C. Scope遵守・指示追従 | 15 | 対象Issueだけを実装しているか |
| D. 設計・Repository適合性 | 10 | ADR、依存方向、責任境界へ適合するか |
| E. テスト・検証品質 | 10 | IssueをCloseできる証拠を作れているか |
| F. コード品質・保守性 | 10 | 可読性、単純性、一貫性があるか |
| G. 変更精度・最小性 | 10 | 不要な変更、boilerplate、先取りが少ないか |
| H. エラー・リスク管理 | 5 | warning、secret、CI、flaky risk等を適切に扱うか |
| **Total** | **100** | |

この配点を使う場合でも、具体的な採点対象はTarget IssueのAC、Scope、Verificationから導出する。

例として、FND-01ではsolution/project/build設定が中心だったが、FND-02ではAPI runtime contract、error、correlation、time、logging等が中心になる。配点フレームワークを共通化してもIssue固有の責任を上書きしない。

## 8. Finding方針

レビューFindingは次で分類する。

- Critical
- Major
- Minor
- Nit

ただし、改善提案ではなく、正しさ、安全性、Issue達成、実質的な保守性に影響する問題を優先する。

原則Findingにしないもの:

- 単なる好み
- stylistic preference
- 将来への一般的な改善提案
- 発生可能性が極めて低く、影響も小さい問題
- Issue Closeを妨げない微細な差

過度なレビューでコードを複雑化させない。

## 9. 速度評価

処理時間はCoding Scoreとは別軸で記録する。

単位が不明な場合は値そのものを相対比較に使用し、勝手に単位を補完しない。

処理時間はModel、branch、PRではなく、**個別のexecution attemptとそのfull Head SHA**へ紐付ける。再実行でHeadが変わった場合、旧attemptの処理時間を新Headへ引き継がない。

- attemptごとに `completed / failed / stopped / no-change` 等のoutcomeを区別する。
- Speed Scoreには、比較可能な処理時間を取得できたcompleted attemptだけを使用する。
- completedでも処理時間を取得できない場合は`N/A`とし、推測値を入れない。
- failed / stopped / no-changeの時間はfailure latencyとして別記してよいが、最速候補の基準には使用しない。

### Quality / Time Index

```text
Quality / Time Index = Coding Score / 処理時間
```

### Practical Score

```text
Practical Score = Coding Score × 0.90 + Speed Score × 0.10
```

Speed Scoreは最速候補を100として、処理時間に反比例する形を標準とする。

速度指標だけで主ランキングを決めない。主ランキングはCoding Scoreとする。

## 10. Model / Agent / Effort分析

比較結果からModel本来の能力を直接断定しない。

少なくとも次を分けて考察する。

### Model側の可能性が高い差

- repository / code理解
- 要件推論
- Scope理解
- 設計判断
- bug回避
- test設計

### Agent / Harness側の可能性が高い差

- repository探索
- tool利用
- build/test実行
- retry
- diff確認
- git操作
- context管理

同一Modelを複数Harnessで実行した候補がある場合は重要な比較対象とする。

Effort設定が異なる場合、その差をモデル能力差と断定しない。未指定Effortを推測しない。

## 11. Candidate snapshot

採点対象となるHeadを固定し、以下を記録する。

- branch
- full Head SHA
- PR
- CI run
- Model
- Agent / Harness
- Effort
- execution attempt / outcome
- 処理時間
- Post-Implementation Notesの有無

snapshot後は原則として候補を修正しない。

重大な検証漏れなどで修正または再実行した候補を再評価する場合は、旧attempt / Headと新attempt / Headを区別し、処理時間を混同せず、他候補との公平性への影響を記録する。

## 12. 比較評価

全候補について可能な限りcommon baseからHeadまでのdiffを取得する。

最低限確認する。

- changed files
- additions / deletions
- Issue固有の成果物
- dependency / reference / contract
- package / dependency version
- test構成
- CI
- local verification
- secret混入
- placeholder
- unrelated change
- Scope外実装
- 過剰設計
- 後続Issueの先取り

単に「動くか」ではなく、**Issueに対して必要十分か**を評価する。

### 検証証拠の忠実度

runtime wiringや外部観測可能なcontractを評価する場合、testの種類を同格に扱わない。

- production entry point / production pipelineを通すrequest-level testは、実際のDI、middleware順序、serializer、logging設定を含む証拠になる。
- test側でproduction componentを再構築したhostはcomponent動作の証拠にはなるが、production wiringの証拠にはならない。
- middleware / handler / serviceの直接呼出しは局所動作の証拠であり、実HTTP contractやproduction wiringの代替にはしない。

security、logging、serialization等の**最終出力**を評価する場合、test doubleが生成した表現だけでproduction出力を証明しない。可能なら実際のformatter / serializer / providerが生成する出力を検証する。

secret non-disclosureでは、request header / query / bodyだけでなく、実装上流入し得る場合はexception message / exception data等にもsentinelを配置する。test loggerがException、scope、structured state等を省略する場合、そのtestだけをactual production outputの非開示証拠にしない。

## 13. Final synthesis

候補のCoding ScoreとFindingを確定した後に限り、必要であれば複数候補の良い部分を統合したcurated / synthesized implementationを作る。

ルール:

- Final synthesisを単独モデルの追加候補として扱わない。
- 「15番目のモデル」等としてModelランキングへ混ぜない。
- 各候補の機能を全部入れるのではなく、Issueに必要な良い設計だけ選ぶ。
- 候補評価で検出した過剰実装やplaceholderを持ち込まない。
- Final synthesisにも同じIssue Scopeを適用する。

Final synthesisは別branch / Headとして記録する。

## 14. Final validation

Final synthesisを作成した場合、比較評価者とは別の実行環境またはlocal Agentで可能な限り再検証する。

Issueに応じて以下を確認する。

- clean restore / dependency install
- build
- test discovery / test result
- warnings
- runtime verification
- secret / credential
- diff scope
- CI

検証で問題がなければ無意味な修正commitを作らない。

## 15. Agent B独立レビュー

Final synthesisは通常の実装と同様、Agent Bによる独立レビューを受ける。

Agent Bは実装者の説明やbenchmark scoreを前提にせず、正本、Issue、diff、test、CIの順で再検証する。

Benchmarkで高得点であることはmerge根拠の代替にならない。

## 16. アーカイブ

実験完了後、候補は次の形を標準とする。

```text
Closed benchmark PR
+
benchmark tag
+
deleted working branch
+
benchmark report / manifest
```

役割:

- PR: 実装差分、CI、会話、Post-Implementation Notesの履歴
- tag: 候補Headの再現可能なsource snapshot
- branch: 作業用。archive後は削除可能
- benchmark report: 比較条件、score、結果の索引

branchを削除する前にtagがremoteで期待Headへ解決することを確認する。

Final synthesis branchの削除・mergeは通常のIssue運用に従い、candidate archiveと混同しない。

## 17. Issue別benchmark report

Issueごとのreportには最低限次を記録する。

- Target Issue / common base
- 候補一覧
- Model / Agent / Effort
- branch / full Head
- PR / CI
- 処理時間
- Coding Score
- Findings
- ranking
- Model vs Harness分析
- Post-Implementation Notesの重要な観察
- Final synthesisがある場合はそのHeadと評価
- archive tag / disposition

Issue固有の評価内容は各reportへ置き、共通方法論をこの文書へ重複させすぎない。

## 18. 方法論変更

この方法論は実験を通じて改善してよいが、変更は新しい知見がある場合に限定する。

- 一回限りの例外を恒久ルール化しない。
- 特定モデルにだけ有利な変更をしない。
- 実験途中の変更は理由と適用範囲を記録する。
- 製品開発の通常フローよりbenchmark運用が優先されないようにする。
