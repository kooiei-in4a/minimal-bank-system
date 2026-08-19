# minimal-bank-system WP-2 実装モデル評価レポート

- 対象: `kooiei-in4a/minimal-bank-system`
- Work Package: WP-2 Security and Audit
- 対象Leaf: #164〜#171
- 評価日: 2026-08-18
- 目的: WP-2で実際に使用した実装モデルの成果を横断比較し、今後のAI Agent開発におけるモデル配置・Candidate数・レビュー構成の判断材料とする

---

## 1. Executive Summary

WP-2では8個のleafに対して、合計24個の独立実装Candidateを作成した。

使用されたモデルは以下の5種類。

- Claude Sonnet 5
- GPT-5.6 Luna
- GPT-5.6 Sol
- Grok 4.6
- DeepSeek V4 Flash

結論として、**今回使用したモデルはいずれも「独立実装候補として比較に参加できる基準ライン」には概ね到達した**。

明確に「実装Agentとして使えない」と判断すべきモデルはなかった。

一方で、モデルごとの得意領域には差が見えた。

- **GPT-5.6 Luna**: contractが閉じた後のproduction implementation
- **Claude Sonnet 5**: verification、mutation、race condition、false assurance検出
- **GPT-5.6 Sol**: 複数contractをまたぐ設計・統合・責務境界整理
- **Grok 4.6**: 既存案とは異なる構造的な別解
- **DeepSeek V4 Flash**: 局所的に鋭い設計判断。ただしサンプル不足

また、WP-2開始時点では「3モデルに独立実装させる」方式に大きなR&D価値があったが、WP-2終了時点では、**通常作業まで常に3実装する必要性は下がった**。

今後の標準形としては、

> 1実装 + 独立Verifier

を基本とし、必要に応じて2候補、未知・高リスク領域だけ3候補へ昇格する方式が妥当と考える。

---

## 2. 評価上の注意

この評価は、純粋な「モデル単体比較」ではない。

実際の観測単位は概ね以下。

`Model × Harness × Role × Prompt/Contract × Fresh Context`

主な組み合わせは以下だった。

| Model | 主なHarness |
|---|---|
| Claude Sonnet 5 | Claude Code |
| GPT-5.6 Luna | OpenCode / 一部Codex |
| GPT-5.6 Sol | Codex |
| Grok 4.6 | Cursor |
| DeepSeek V4 Flash | OpenCode |

したがって、以下の評価にはHarness、Prompt、Issue contractの完成度、task typeの影響も含まれる。

また、**Base採用回数だけをモデルランキングとして扱ってはいけない**。

理由:

- Final Synthesisでは他Candidateの良い要素を取り込むことがある
- production codeとverificationの最良Candidateが異なる場合がある
- AUD-01のように全Candidateが同一モデルだったleafもある
- Candidate selectionの価値には「別解を出す」「欠陥を見つける」ことも含まれる

---

## 3. WP-2 対象Leaf

| Issue | Leaf |
|---|---|
| #164 | WP2-DB-01 |
| #165 | WP2-ID-01 |
| #166 | WP2-AUD-01 |
| #167 | WP2-AUTHN-01 |
| #168 | WP2-AUTHZ-01 |
| #169 | WP2-OPR-QRY-01 |
| #170 | WP2-OPR-CREATE-01 |
| #171 | WP2-OPR-MUT-01 |

---

## 4. 実装Candidate全体集計

### 4.1 実装に使われた回数

8 leaf × 3 Candidate = 24実装。

| モデル | 実装回数 | 割合 |
|---|---:|---:|
| Claude Sonnet 5 | 7 | 29.2% |
| GPT-5.6 Luna | 6 | 25.0% |
| GPT-5.6 Sol | 6 | 25.0% |
| Grok 4.6 | 4 | 16.7% |
| DeepSeek V4 Flash | 1 | 4.2% |
| **合計** | **24** | **100%** |

### 4.2 Primary Base採用回数

| モデル | Base採用回数 | 実装回数 | 単純Base採用率 |
|---|---:|---:|---:|
| GPT-5.6 Sol | 3 | 6 | 50.0% |
| Claude Sonnet 5 | 2 | 7 | 28.6% |
| GPT-5.6 Luna | 2 | 6 | 33.3% |
| Grok 4.6 | 1 | 4 | 25.0% |
| DeepSeek V4 Flash | 0 | 1 | 0% |
| **合計** | **8** | **24** | — |

注意:

AUD-01はC1/C2/C3すべてGPT-5.6 Solだったため、Solの1勝は他モデルとの直接比較によるものではない。

---

## 5. Leaf別 Candidate / Base結果

| Leaf | C1 | C2 | C3 | Primary Base / Final方向 |
|---|---|---|---|---|
| DB-01 | GPT-5.6 Luna | **Claude Sonnet 5** | Grok 4.6 | **Sonnet 5**をPrimary Base、他Candidate要素も採用 |
| ID-01 | GPT-5.6 Luna | Claude Sonnet 5 | **Grok 4.6** | **Grok 4.6**をPrimary Base、Sonnet要素を一部採用 |
| AUD-01 | GPT-5.6 Sol | **GPT-5.6 Sol** | GPT-5.6 Sol | **Sol C2**をPrimary |
| AUTHN-01 | Claude Sonnet 5 | **GPT-5.6 Luna** | GPT-5.6 Sol | **Luna**をBase、Sonnet/Sol要素を輸入 |
| AUTHZ-01 | Claude Sonnet 5 | DeepSeek V4 Flash | **GPT-5.6 Sol** | **Sol**をBase、他Candidate要素を輸入 |
| OPR-QRY-01 | Claude Sonnet 5 | GPT-5.6 Luna | **GPT-5.6 Sol** | **Sol**をBase |
| OPR-CREATE-01 | **Claude Sonnet 5** | GPT-5.6 Luna | Grok 4.6 | **Sonnet 5**をBase |
| OPR-MUT-01 | Claude Sonnet 5 | **GPT-5.6 Luna** | Grok 4.6 | **Luna production + Sonnet verification** |

### AUTHZ-01の補足

当初C2にはGPT-5.6 Luna / OpenCodeが予約されていたが、実際の実装では人間による明示的な置換により、DeepSeek V4 Flash / OpenCodeが使用された。

したがって本レポートでは「予定モデル」ではなく、実際にCandidate implementationを行ったモデルを集計している。

---

## 6. 全体として基準ラインに到達したか

### 結論

**概ねYES。**

今回使われた各モデルは、少なくとも次のラインには到達していた。

1. 指定されたleafのscopeを理解する
2. 独立実装Candidateを作る
3. build/test/CIへ載せる
4. Candidate comparisonの対象になる
5. Final Synthesisへ全部または一部の成果を提供する

ただし、

> Candidateとして成立する

ことと、

> そのCandidateをそのままmainへ入れられる

ことは別である。

多くのleafではFinal Synthesisで、

- 他Candidateから要素を輸入
- 余計なsurfaceを削除
- framework依存の危険な判断を修正
- verificationを強化
- Heavy Review指摘を修正

している。

したがって、今回確認できたのは、

> **どのモデルも70〜80点程度の実装Candidateを作る能力は十分ある**

ということであり、

> **どのモデルでも単独で常に最終品質へ到達する**

という意味ではない。

---

# 7. モデル別評価

## 7.1 Claude Sonnet 5

### 総評

**実装も強いが、特にverification / adversarial testingで価値が高かった。**

WP-2で最も特徴が明確に出たモデルの一つ。

### 強み

#### 1. Semantic verificationが強い

単にテストを増やすのではなく、

- そのtestが本当にsemantic defectを検出できるか
- mutationが本当に危険状態を成立させるか
- false assuranceになっていないか

を見る傾向が強かった。

特にOPR-MUT-01では、production baseはLunaだったが、

- cross-target concurrency
- OPR-MUT-ADMIN-01
- OPR-MUT-AUTH-01

のverification patternはSonnet Candidateが優れ、Final Synthesisへ移植された。

これは非常に重要な観測。

#### 2. 実DBを使った非自明な証明が得意

AUTHN-01では、

- real PostgreSQL login
- SuccessRehashNeeded
- persisted hash readback
- test-only authentication probe
- handlerReached positive signal

など、実装が正しいだけではなく、実際のruntime semanticsを確認する証拠が強かった。

#### 3. Feature implementationも十分強い

OPR-CREATE-01ではPrimary Baseを獲得。

- atomic Operator + Audit
- concurrent duplicate
- fail-closed
- credential non-disclosure
- exactly-once Audit

まで含む実装を成立させた。

### 弱み・癖

#### 1. 実装surfaceを少し広げる場合がある

OPR-QRYでは、

- optional response fields
- PascalCase enum表現

など、最終的には採用されなかった選択が入った。

#### 2. Production最小性よりverification価値が勝る場合がある

OPR-MUTではproduction baseには選ばれず、verification sourceとして採用された。

これは弱点というより役割適性の差。

### 適性

- Independent Verifier
- Mutation Designer
- Concurrency / Race Review
- False Assurance Review
- Security/Audit semantic review
- 難しいfeature implementation

### 暫定評価

**Verifier: 非常に強い  
Builder: 強い  
Architect/Synthesizer: 強いが最適配置はVerifier寄り**

---

## 7.2 GPT-5.6 Luna

### 総評

**contractが閉じた後のproduction Builderとして非常に強かった。**

WP-2終了後の「通常実装担当」の第一候補にしやすい。

### 強み

#### 1. 明確なcontractをproduction codeへ落とす能力

AUTHN-01ではPrimary Baseとなり、

- external JWT secret
- startup fail-closed
- JwtAuthnOptions
- token issuance
- non-disclosure
- bearer validation

など主要production shapeが残った。

#### 2. 複雑なDB concurrency実装

OPR-MUT-01では最も重要な結果を出した。

Luna Candidateのproduction codeは、

- READ COMMITTED
- active administrator set locking
- ordered `FOR UPDATE`
- recount after lock
- lock timeout/deadlock handling
- no automatic retry
- Audit atomicity

という複雑なcontractに最も近く、Final Synthesisでもproduction codeはそのまま維持された。

これはBuilderとしてかなり強い証拠。

#### 3. 「仕様が決まれば書ける」タイプ

大きな設計を新しく作るより、

> 既に決められた正解を、正確なproduction codeへ変換する

仕事と相性が良い。

### 弱み・癖

#### 1. Verification designはSonnetに負けることがある

OPR-MUTではproductionはLunaだが、

- cross-target concurrency proof
- critical mutation design

はSonnet案が採用された。

#### 2. Synthetic verification surfaceを作る傾向

AUTHNでは一部のsynthetic store/probe構造がFinal Synthesisで捨てられた。

### 適性

- 通常Builder
- CRUD/feature implementation
- DB transaction
- concurrency implementation
- contract-driven implementation

### 暫定評価

**Builder: 非常に強い  
Verifier: 十分だがSonnetに分がある  
Architect/Synthesizer: 必要十分**

---

## 7.3 GPT-5.6 Sol

### 総評

**複数contract、責務境界、既存architectureを横断する仕事に強い。**

Base採用は3回で最多。

### 強み

#### 1. Boundaryを崩しにくい

AUTHZ-01ではPrimary Base。

- current Operator authority
- JWTは非authoritative
- 401/403 ownership
- Audit ownership
- test-only surface
- routing semantics

など複数の境界を同時に扱った。

#### 2. 必要最小限のsurfaceを選びやすい

OPR-QRYでは、

- `operatorIdentifier`
- `state`
- `role`

のみのrequired-only projectionを選択し、

AUTHZ-owned sourceを余計に変更しない方向がBaseとなった。

#### 3. Final Synthesis / adjudication向き

単独でコードを書くこと以上に、

- Candidate比較
- 何を残すか
- 何を捨てるか
- ownershipをどう維持するか

の判断と相性が良い。

### 弱み・癖

#### 1. Frameworkの具体挙動で外すことがある

AUTHZ C3では、405/415 routing fault判定をDisplayName文字列へ依存させており、Final Synthesisで`RouteEndpoint`ベースへ修正された。

つまり、

> architecture reasoningが強いことと、framework内部挙動を常に正確に扱えることは別

である。

#### 2. AUD-01の勝利は比較として弱い

AUD-01は全CandidateがSolだったため、Base採用1回をモデル間比較として強く評価すべきではない。

### 適性

- Architect
- Issue Ready Review
- Candidate Comparison
- Final Synthesis
- Cross-leaf boundary review
- Security/Auth/Audit統合

### 暫定評価

**Architect/Synthesizer: 非常に強い  
Builder: 強い  
Verifier: 強いが、実装詳細では独立検証が必要**

---

## 7.4 Grok 4.6

### 総評

**既存案とは違う構造的な別解を出すCandidateとして価値が高い。**

ID-01ではPrimary Baseを獲得。

### 強み

#### 1. Frameworkを全部持ち込まない判断

ID-01では、

- Full ASP.NET Core Identity schemaを採用しない
- Domain-owned Operator
- scalar fixed role
- PasswordHasherなど必要primitiveだけ利用

という構造を採用。

最終的にこの方向がPrimary Baseとなった。

これは、

> 標準frameworkだから全部使う

ではなく、

> product invariantに必要なものだけ取る

という良いarchitecture判断。

#### 2. Candidate diversityが高い

他モデルと違う構造を出すことで、

- 既存案の前提を疑う
- Alternative architectureを出す
- 比較対象を増やす

役割を果たす。

### 弱み・癖

#### 1. 少し「賢すぎる」仕組みを持ち込む場合がある

DB-01では、

- SECURITY DEFINER
- persistent event trigger

などが最終的には採用されなかった。

OPR-MUTでもGrok側の広めのproduction abstractionは採用されていない。

#### 2. 最終production baseとしての安定採用はまだ少ない

4回実装して1回Base。

ただしCandidate diversityとしては十分価値がある。

### 適性

- Alternative Builder
- Architecture exploration
- Second opinion implementation
- Framework boundary検討

### 暫定評価

**Builder: 基準以上  
Alternative Architect: 強い  
Minimal production implementation: やや注意**

---

## 7.5 DeepSeek V4 Flash

### 総評

**1サンプルのみのため評価保留。**

AUTHZ-01で使用。

### 強み

Final Synthesisへ採用された要素として、

- RouteEndpoint public-type predicate
- production composition assertions
- explicit role requirement `Fail()`

などがあった。

Primary Baseではなかったが、Candidateとして価値を出している。

### 弱み・癖

一方で、

- singleton `IPolicyEvaluator`
- 独自RoutingFaultAwarePolicyEvaluator

など、問題解決を少し大きな仕組みへ展開する傾向が見られた。

### 適性

現状では断定不可。

少なくとも、

- Candidate implementation
- 局所的architecture idea
- alternative design

には使える。

### 暫定評価

**基準ライン到達  
ただしサンプル不足**

---

# 8. Base採用以外の価値

WP-2で重要だったのは、

> BaseにならなかったCandidate = 失敗

ではなかったこと。

実際にはFinal Synthesisで、

- production base
- verification source
- architecture concept source
- test pattern source
- rejected-alternative evidence

として複数Candidateが使われた。

典型例がOPR-MUT-01。

```text
GPT-5.6 Luna
  → Production implementation

Claude Sonnet 5
  → Verification / mutation patterns

Final Synthesis
  → Luna production + Sonnet verification
```

これは「モデルランキング」では説明できない。

むしろ、

> **役割ごとに最適なモデルを組み合わせる**

方が重要であることを示している。

---

# 9. 3モデル独立実装方式の評価

## 9.1 WP-2で3Candidateにした意味

WP-2では3Candidate方式に十分なR&D価値があった。

得られたもの:

- モデルごとの実装癖
- 同じcontractから出るarchitecture差
- 最良productionと最良verificationが別になること
- 良いtest pattern
- rejectすべき過剰設計
- Candidate比較の方法
- Final Synthesisの方法
- モデル最低品質ライン

したがって、

> WP-2で3Candidateを使ったこと自体は妥当だった

と評価する。

## 9.2 今後も常に3実装する必要があるか

**ない。**

WP-2で十分な学習が得られたため、今後はCandidate数を動的にする方が合理的。

---

# 10. 今後のCandidate数の推奨

## 10.1 1 Candidate — 標準

以下では1実装を標準とする。

- 既存patternの再利用
- CRUD
- 既存transaction pattern
- 既存Audit primitive
- 既存AUTHZ policy利用
- schema変更なし
- semantic choiceなし
- contractが十分閉じている

推奨:

```text
Luna
  ↓
Production Implementation
  ↓
CI
  ↓
Sonnet
Independent Verification
  ↓
必要ならLuna修正
  ↓
Sol
Final semantic / architecture check
```

3モデルを使っても、product implementation自体は1つでよい。

---

## 10.2 2 Candidate — 重要・設計差あり

以下では2Candidateへ増やす。

- 新しいDB transaction
- 新しいsecurity boundary
- schema設計
- concurrency
- 既存patternをそのまま使えない
- 2つ以上の自然なarchitectureがある

候補例:

```text
Candidate A: Luna
  production correctness重視

Candidate B: Sonnet or Grok
  alternative implementation / verification重視

Sol
  selection / synthesis
```

2Candidateが同じ方向へ収束すればconfidenceが上がる。

大きく違えばSolが差分を評価する。

---

## 10.3 3 Candidate — R&D / 未知 / 高リスク限定

以下の場合のみ3Candidateを推奨。

- repository初のarchitecture
- irreversible migration
- 高リスクsecurity境界
- 重大concurrency
- 正解が事前に読めない
- 2Candidateが大きく対立
- 失敗すると後続leaf全体へ影響

つまり、

> **1 → 必要なら2 → 本当に必要なときだけ3**

という昇格方式がよい。

---

# 11. 今後のモデル役割分担案

WP-2の結果から、暫定的に以下の配置が自然。

## GPT-5.6 Luna

**Primary Builder**

用途:

- 通常実装
- CRUD
- transaction
- DB処理
- contract-driven implementation

---

## Claude Sonnet 5

**Independent Verifier / Adversarial Builder**

用途:

- semantic review
- mutation test
- concurrency
- false assurance
- race condition
- fail-closed
- security/audit verification

---

## GPT-5.6 Sol

**Architect / Coordinator / Synthesizer**

用途:

- Issue Ready
- architecture review
- candidate comparison
- cross-leaf boundary
- Final Synthesis
- 最終semantic review

---

## Grok 4.6

**Alternative Builder**

用途:

- 第二案
- architecture exploration
- framework依存を疑う
- Candidate diversity

---

## DeepSeek V4 Flash

**Experimental Alternative**

現時点ではサンプル不足。

追加実験が必要。

---

# 12. 推奨する標準フロー

WP-2後の通常開発では、以下を標準候補とする。

```text
Issue / Contract Ready
        ↓
GPT-5.6 Luna
Production Implementation
        ↓
Build / Test / CI
        ↓
Claude Sonnet 5
Independent Adversarial Review
        ↓
問題あり
   ├─ YES → Lunaへ修正
   └─ NO
        ↓
GPT-5.6 Sol
Final Semantic / Boundary Review
        ↓
Human Decision
※本当に人間が決める必要がある場合のみ
        ↓
Merge
```

これにより、

- 3つのproduction implementationを毎回作らない
- verifier独立性は維持
- model diversityは維持
- Solを高価値な判断へ集中
- Humanをroutine確認から外す

ことができる。

---

# 13. WP-2から得られた主要知見

## 13.1 最低ライン

今回のモデルは概ね全てCandidate Builderとして使える。

## 13.2 「最強モデル1つ」ではなかった

Base採用は4モデルへ分散した。

- Sol: 3
- Sonnet: 2
- Luna: 2
- Grok: 1

## 13.3 実装と検証で最適モデルが違う

OPR-MUTの、

> Luna production + Sonnet verification

が代表例。

## 13.4 高性能モデルを全工程へ使う必要はない

contractが閉じた後はLuna級でも難しいproduction implementationを担当できた。

Solは設計・比較・統合へ集中させた方が合理的。

## 13.5 Candidate diversity自体に価値がある

GrokやDeepSeekのようにBaseにならなくても、

- 別architecture
- 良い局所判断
- rejectパターン
- 比較材料

を提供できる。

---

# 14. 最終評価

WP-2の3Candidate方式はR&Dとして成功した。

最大の成果は、

> 「毎回3モデルに実装させる必要がある」

と分かったことではない。

逆に、

> **どのモデルが何を得意とし、どの程度まで1実装へ縮小できるかが見えてきた**

ことが成果。

WP-2終了時点での推奨方針は以下。

### Default

**1 Production Candidate + Independent Verifier**

### Escalation

- 通常: 1 Candidate
- 重要/設計差あり: 2 Candidate
- 未知/高リスク/R&D: 3 Candidate

### Model assignment

- **Builder**: GPT-5.6 Luna
- **Verifier**: Claude Sonnet 5
- **Architect / Final Synthesis**: GPT-5.6 Sol
- **Alternative Builder**: Grok 4.6
- **Experimental**: DeepSeek V4 Flash

現時点では、この構成がWP-2の実測結果と最も整合している。

---

# Appendix A. 主要Final Synthesis PR

| Leaf | Final PR |
|---|---:|
| WP2-DB-01 | #186 |
| WP2-ID-01 | #190 |
| WP2-AUD-01 | #202 |
| WP2-AUTHN-01 | #196 |
| WP2-AUTHZ-01 | #208 |
| WP2-OPR-QRY-01 | #214 |
| WP2-OPR-CREATE-01 | #220 |
| WP2-OPR-MUT-01 | #224 |

Repository:

`https://github.com/kooiei-in4a/minimal-bank-system`

---

# Appendix B. 今後追加で計測したい指標

今後、モデル評価をより定量化するなら、各leafで以下を記録するとよい。

- implementation completion rate
- required CI first-pass rate
- Final Synthesisで残ったproduction code割合
- 他Candidateから輸入された要素数
- reviewで発見されたBlocker/Major数
- mutation / verification採用数
- scope逸脱数
- over-engineering指摘数
- human decision発生数
- Final Synthesis修正量
- token / compute cost
- elapsed time
- harness別成功率

これを蓄積すると、

> 「モデルが強いか」

ではなく、

> **「この種類の仕事を、どのModel × Harness × Roleへ割り当てると最も効率が良いか」**

を判断できるようになる。
