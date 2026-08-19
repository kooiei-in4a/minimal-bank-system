# minimal-bank-system WP-2 レビューモデル評価レポート

- Repository: `kooiei-in4a/minimal-bank-system`
- Work Package: WP-2 Security and Audit
- 対象Leaf: #164〜#171
- 評価日: 2026-08-19
- 目的: WP-2で実際に使用したレビューモデルの成果を横断比較し、今後のAI Agent開発におけるIssue Ready / Light / Heavy / Targeted Review / Selectionのモデル配置判断材料とする

## 1. Executive Summary

調査したところ、**レビュー側は実装側以上にモデルごとの役割適性がはっきり出ている**。

WP-2初期のIssue Setレビュー時点でも、リポジトリ自身に「Sol＝Issue topology、Opus＝deep evidence、Grok＝軽量なsecond opinion」という観測が記録されていた。実際、その後のleafレビューでも概ねこの傾向が再現された。

なお、DB-01 / ID-01の前半はReviewer model identityの記録がまだ徹底されていないため、**モデル別回数を完全には復元できない**。以下はモデル名までGitHub一次証拠で確認できたレビューを中心に評価する。

## 2. 全体評価

| モデル | 今回強かったレビュー | 暫定評価 |
|---|---|---|
| **Claude Opus 5** | Issue Ready、H2、Candidate比較、契約・authority確認 | **Deep Reviewerとして最有力** |
| **GPT-5.6 Sol** | Heavy H1、adversarial review、Selection | **重大欠陥・false assurance検出に強い** |
| **GPT-5.6 Luna** | Heavy H1/H2、integration/reachability | **実装に近い実戦的Heavy Reviewerとして強い** |
| **Claude Sonnet 5** | Light Review、Targeted Re-review、Issue Ready | **軽量〜中量レビューにかなり良い** |
| **GPT-5.6 Terra** | Light/H2系 | **サンプル不足だが軽量Reviewer候補** |
| **Grok 4.6** | Issue Setのsecond opinion | **別視点・簡素化圧力に価値** |
| **Cursor Auto** | OPR-MUT Issue Ready | **良い結果だが実モデル不明で評価不能** |

## 3. Claude Opus 5

今回のレビューでは、**一番「レビュー専用モデル」らしい成果**を出している。

### 強かったところ

特にIssue Ready Reviewが強い。

AUD-01では実装前に、

- Audit record contract不足
- transaction/fail-closed検証が空振りでもPASSできる問題
- failure injectionがproductionへ漏れる可能性

という**3 Major**を止めた。これはコードバグではなく、「このIssueのまま実装Agentへ渡すと危ない」という種類の問題である。

AUTHN-01でもIssue Ready段階で、

1. disabled Operator login rejectionのownerが不明
2. JWTにsubjectとauthorization-state-versionを載せる契約が明示されていない

という2 Majorを発見している。

OPR-QRYでも3 Majorを出しており、response projection、AUTHZ/Audit ownership、Audit target semanticsがまだ実装可能なほど閉じていないことを検出している。

### 特に評価したい成果

AUTHZ-01 H2。

Opusはこの時点で、

> mandatory 403 Auditに`RequestAborted`を使うとclient disconnectでrequired Auditがcancelされる

という問題を**Minorとして残した**。その時点ではproduction endpointがなく、まだreachableではなかったためである。

そして次のOPR-QRYで実endpointができると、その懸念が実際にreachableになり、**Majorへ昇格して修正対象になった**。

これはかなり良いレビューである。

単に「今バグか」ではなく、

> 今はlatent riskだが、次の構成で危険になる

ところまで見ている。

### 弱み・癖

かなり深く見るため、**通常の軽量レビューには重い**。

また、Minor/Nitまで多く拾う傾向があるので、すべてを修正対象にすると工程が膨らみやすい。Severity policyを明確にして使う必要がある。

### 向いている役割

**Issue Ready / Architecture Contract Review / H2 / authority completeness / cross-leaf risk**。

現時点では、Opusを**最上位のDeep Reviewer**として扱う。

## 4. GPT-5.6 Sol

SolはOpusと少し違う。

**「仕様が足りない」より、「Greenに見える証拠が本当に正しいのか」を壊すのが強い**。

### AUD-01 H1

全テスト・CIがかなり整った後で、

> Audit Downがempty確認後にtable lockを取らないため、確認とDROPの間にruntime INSERTがcommitできる

というraceをMajorとして発見した。

普通のcode reviewというより、

> 「このrollback safety proof、本当に証明になっているか？」

を見るレビューである。

### AUTHN-01 H1

invalid JWT non-disclosure testについて、

> JWTがresponse/logに無いことは確認しているが、その**同じJWTが本当にauthentication failure pathを通った証明がない**

と指摘した。

修正要求は、

`deterministically invalid JWT → 実送信 → 401 → handler未到達 → exact JWT非開示`

というsemantic sequenceだった。

かなり典型的な**false assurance検出**である。

### AUTHZ-01 H1

ここでも、

> 404/405/415を守ると書いてあるが、415についてcurrent-Operator DB lookupが起きないことまで証明できていない

というmandatory verification gapをMajorにした。

つまりSol H1は3回続けて、

- race
- test oracleの空振り
- verification coverageの穴

を見つけている。

### Issue Setレビューでも強かった

WP-2最初のレビューでは、Solは**Issue topology / decompositionが最も強かった**と記録されている。

AUTHをAUTHN/AUTHZへ分割し、Operator QueryとCreateを分割する方向を出した。

### 向いている役割

**Heavy H1 / Adversarial Review / Candidate Selection / Final Architecture Review**。

実装側でもSolは統合が強かったが、レビュー側ではさらに、

> 「その証拠、本当にその性質を証明している？」

を担当させるのが非常に良い。

## 5. GPT-5.6 Luna

今回、一番意外に評価が上がったReviewer。

Builderだけでなく、**Heavy Reviewerとしてかなり良い**。

### OPR-QRY H1

Light ReviewはPASSしていた。

ところがLuna H1は、AUTHZ H2で残っていた`RequestAborted`問題が実endpoint導入によりreachableになったことを検出し、

`AUTHZ-H2-MIN-03-NOW-REACHABLE`

として**Majorへ昇格**させた。

これは単なるコードレビューではなく、

> 前leafの残存risk × 今回追加されたendpoint

を組み合わせたcross-leaf reasoningである。

### OPR-CREATE H1

ここでも良い指摘をした。

テスト上は「required rejection Audit failureでfail-closed」がPASSしていたが、Lunaは、

> test writerが`IAuditWriter` method入口でthrowしているだけで、**実際のPostgreSQL Audit persistence failureを通っていない**

ことをMajorにした。

つまり、

> Failure injectionはある
> でも本物のfailure pointを通っていない

というfalse assuranceを発見している。

### OPR-MUT H1

一方で、OPR-MUTでは無理に問題を作らずPASS。

残したのは、

- version/stampを直接assertすると証拠がさらに明確
- concurrency testにexplicit critical-section barrierがない

というMinor程度だった。Material residual riskはなしと判断されている。

この**問題がなければPASSできる校正**も重要である。

### 向いている役割

**production integrationに近いHeavy Review**。

特に、

- transaction
- PostgreSQL
- concurrency
- 実endpoint
- cross-leaf reachability
- 本物のfailure path

を見るレビュー。

Solが「証明論・adversarial」寄りなら、Lunaは**実際のruntimeで壊れるか**寄りに見える。

## 6. Claude Sonnet 5

Sonnetは**Light Review / Targeted Reviewとして非常に使いやすい**。

### OPR-QRY

Issue Readyの3 Major修正後のTargeted Re-reviewを担当してPASS。

Final Synthesis Light ReviewでもBlocker/Major 0でPASSしつつ、

- disabled state tokenのreal endpoint coverage不足
- teller role tokenのreal endpoint coverage不足

を非blocking findingとして残した。

重大でないものをMajorへ膨らませていない。

### OPR-CREATE Issue Ready

ここではかなり重要な仕事をしている。

Sonnetは、

- create endpoint / success contract
- duplicate / invalid credential rejection
- Audit operation / target semantics

の3 Majorを正しく発見した。

ただし**弱点もここで出ている**。

Coordinatorは、

> Majorを見つけたこと自体は正しいが、`POST /operators`や`201 Created`などを既存authorityから自動的に決められるものとして扱った点は修正が必要。これはHumanが選ぶsemantic choice。

と補正している。

つまりSonnetは、

> **問題の存在を見つけるのは正しいが、「AIだけで決めてよいか」のauthority classificationを少し踏み越えた**

というケースがあった。

### 向いている役割

**Light Semantic Review / Targeted Re-review / 中程度のIssue Ready Review**。

毎回Opusを投入するよりかなり現実的である。

## 7. GPT-5.6 Terra

ここはまだ評価保留。

OPR-MUTのFinal Synthesis Light ReviewではTerra / OpenCodeが明示され、PASSしている。

OPR-CREATEでもTerraがLight/H2 Reviewerとして割り当てられた記録があるが、興味深いことに、Light Reviewの正式結果側ではReviewerが**Luna**になっている。

つまりこの時期は、

> planned reviewer model
> と
> actually executed model

の記録が一部ずれている。

これはモデル評価以前に、今後のR&Dでは**actual reviewer identityをstage resultに必須記録するべき**という改善点である。

Terra自体は現状、

> Light Reviewer候補としては問題なさそうだが、評価サンプル不足

である。

## 8. Grok 4.6

leaf後半の正式レビューにはあまり使われていない。

ただしWP-2 Issue Setレビューでは明確な価値があった。

リポジトリ自身のretrospectiveでは、

- 比較的lightweight
- proportional
- second opinion / simplification pressureに有効
- AUTHNに独立observable authentication verification surfaceが必要だと独自に発見

と記録されている。

つまりGrokは、

> **Heavy Reviewerより、「これ複雑にしすぎてない？」「違う角度から穴はない？」**

に向いている。

## 9. Cursor Auto

これはモデル評価に混ぜない方がよい。

OPR-MUT Issue ReadyではCursor Autoがレビューし、

- API shape
- no-op semantics
- concurrency strategy/conflict
- Audit operation identifiers

という**4つのmaterial semantic choice**を見つけ、`HUMAN_DECISION_REQUIRED`へ正しくエスカレーションした。

しかも人間はReviewerの4つの推奨案をすべて採用している。

レビューとしては良い。

ただし**Autoの裏でどのモデルが使われたか確定できない**ので、「Grokが強かった」等とは評価できない。

## 10. レビューで見つけたMajor数は単純比較できない

参考値としては、

| モデル | 代表的なMajor検出 |
|---|---|
| Opus 5 | AUD Issue Ready 3、AUTHN Issue Ready 2、OPR-QRY Issue Ready 3、ほか |
| Sonnet 5 | OPR-CREATE Issue Ready 3 |
| Sol | AUD H1 1、AUTHN H1 1、AUTHZ H1 1 |
| Luna | OPR-QRY H1 1、OPR-CREATE H1 1 |
| Cursor Auto | OPR-MUT semantic choices 4 |
| Terra | 今回確認範囲ではMajor検出なし |

ただし、これは**勝率表にしてはいけない**。

Issue Readyは契約の穴を複数見つけやすく、Heavy H1は既にかなり完成したコードから「最後の1個の重大欠陥」を探す仕事だからである。

SolのH1 1件は、Issue ReadyのMajor 3件より価値が低いわけではない。むしろ、全CI green後のraceを1件見つける方が難しい場合がある。

## 11. 実装モデル評価と合わせた役割配置

WP-2の結果を統合すると、現時点では以下の配置が自然。

| 役割 | 第一候補 | 第二候補 |
|---|---|---|
| Issue / Architecture設計 | **Sol** | Opus |
| Issue Ready Review | **Opus** | Sonnet |
| 通常Production Builder | **Luna** | Sonnet |
| Light Semantic Review | **Sonnet** | Terra |
| Heavy H1 / false assurance | **Sol** | Luna |
| Runtime/DB/Integration Heavy | **Luna** | Sol |
| H2 / cross-leaf residual risk | **Opus** | Luna |
| Candidate Selection / Adjudication | **Sol / Opus** | — |
| Alternative / simplification review | **Grok** | — |

WP-2 Issue Set自身のretrospectiveでも、

`Issue Architect = Sol / Deep Heavy Reviewer = Opus / Light or Second Opinion = Grok`

という経験則が既に記録されている。

## 12. 一番重要な結論

実装では、

> **3モデル全部にコードを書かせる必要は減ってきた**

という結論だった。

レビュー側では逆に、**モデルを分ける意味がまだかなりある**。

理由は明確で、

> Sonnet LightでPASS
> → Luna H1がMajorを発見

や、

> Sol/実装・CIがgreen
> → Opusがlatent riskを残す
> → 次leafでLunaがreachable Majorとして発見

という実例が出ているからである。

したがって現時点では、

> **「3実装」は減らしてよいが、「Builderとは別モデルのLight/Heavy Review」はむしろ維持する価値が高い**

というのがWP-2のデータから最も強く言えることである。

## 13. Reviewer identity記録に関する改善

今後のR&Dでは、planned reviewer modelとactually executed modelのずれを避けるため、stage result evidenceへ以下を必須記録する。

```yaml
REVIEWER:
  MODEL:
  HARNESS:
  CONTEXT:
  EFFORT:
  ROLE:
  MODE:
```

モデル評価では、予定モデルではなく**実際に実行されたReviewer identity**のみを集計対象とする。

## 14. 最終評価

WP-2のレビュー構成はR&Dとして成功した。

特に重要なのは、

- Light ReviewとHeavy Reviewが異なる価値を持った
- green CIだけではsemantic correctnessを証明できなかった
- false assurance / latent risk / cross-leaf reachabilityを異なるモデルが検出した
- Reviewer diversityが実際にMajor defect検出へ寄与した

ことである。

今後の標準は、

> **1 Production Candidate + Independent Review**

とし、BuilderとReviewerは原則として別モデルにする。

高リスク時のみHeavy H1/H2を追加し、Candidate数は `1 → 必要なら2 → 本当に必要な場合だけ3` と段階的に増やす。
