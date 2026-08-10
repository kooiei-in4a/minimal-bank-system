# FND-05 Pre-Run Prompt Suite — Independent Review

```yaml
DOCUMENT_STATUS: "COMPLETED INDEPENDENT REVIEW"
REVIEW_TARGET_PR: 145
REVIEW_TARGET_HEAD: "57df6ae1a30ac23151fbcd707f191f5d26dba029"
REVIEW_TARGET_BASE: "a69471578eed12823a1469017dac7fddf32ad41b"
REVIEW_MANIFEST: "pre-run-prompt-suite-review-manifest.md"
OUTPUT_BRANCH: "agent/fnd05-prompt-suite-review-codex"
OUTPUT_PR: 147
OUTPUT_FILE: "docs/benchmarks/fnd05-model-comparison/reviews/pre-run-prompt-suite-independent-review-gpt-5.6-sol-codex-xhigh.md"
REVIEWER_MODEL: "GPT 5.6 Sol"
REVIEWER_HARNESS: "Codex"
REVIEWER_EFFORT: "xHigh"
REVIEWER_SLUG: "<gpt-5.6-sol-codex-xhigh>"
ATTEMPT: 1
REVIEW_DATE: "2026-08-11"
```

## 1. Executive Verdict

```text
VERDICT: FIX_REQUIRED
BLOCKER_COUNT: 1
MAJOR_COUNT: 4
MINOR_COUNT: 2
NIT_COUNT: 0
TARGET_HEAD_VERIFIED: YES
```

固定された3 candidate、Light 2、Heavy 2、no OpenCode、no separate Formal Self-Review、curated Final Synthesis、conditional Judge、finding-owner / blast-radius re-reviewというprocess shapeは成立する。これらを再投票する必要はない。

ただし、現在のsuiteには次の実行前欠陥がある。

1. Issue Ready `PASS`とKooの開始許可を同じ`IMPLEMENTATION_PERMITTED`へ混同し、早期candidate実行を許し得る。
2. 複数promptの`Authority`がParent / Issue / `AGENTS.md`をAccepted ADRより上に置き、正本順序を逆転している。
3. `run.json`がD-01〜D-08の解決値・一次証拠とstage間artifactのimmutable identityを保持できず、後続promptの`<LOCKED_ARTIFACT>`を一意に解決できない。
4. Light findingのfix / rejectをAuthor自身が完了扱いした直後にHeavyへ渡し、Heavyは同じfindingの再確認を除外するため、未修正のMajorがblind spotへ落ちる。
5. M-01、M-03、M-08、M-10は現状のprecondition / injectionではtarget REDを決定的に保証できず、100% kill rateがfalse assuranceまたはflaky resultになり得る。

よってD-01〜D-08の値lockやIssue Ready再評価へはまだ進めない。P0修正後、finding-targeted re-reviewでBlocker / Major 0を確認してからlock workへ進む。

## 2. Target Verification

最初の対象操作として、GitHub上の[PR #145](https://github.com/kooiei-in4a/minimal-bank-system/pull/145)の一次証拠を取得した。target file本文を読む前にHead一致を確認している。

| Item | Expected | Observed | Result |
| --- | --- | --- | --- |
| Repository | `kooiei-in4a/minimal-bank-system` | same | PASS |
| PR | `145` | `145` / Draft / OPEN | PASS |
| Title | `docs(fnd05): prepare ADR-first implementation and review funnel` | same | PASS |
| Base branch | `agent/fnd04-final-retrospective-synthesis` | same | PASS |
| Base SHA | `a69471578eed12823a1469017dac7fddf32ad41b` | same | PASS |
| Head branch | `agent/fnd05-pre-run-preparation` | same | PASS |
| Head SHA | `57df6ae1a30ac23151fbcd707f191f5d26dba029` | same | PASS |
| Changed files | 22 | 22 | PASS |
| Diff stat | +5006 / -0 | +5006 / -0 | PASS |
| Scope | `docs/benchmarks/fnd05-model-comparison/**` | 22 / 22 in scope | PASS |
| Manifest file list | exact 22 | GitHub list and local exact-SHA diff match | PASS |
| Output branch merge-base | target Head | `57df6ae1a30ac23151fbcd707f191f5d26dba029` | PASS |

`TARGET MOVED`ではない。PR #145の22 filesに混入はなく、`run.json`はJSONとしてparseできた。exact Base→Headに対する`git diff --check`も成功した。

## 3. Phase A Reference Review

PR #145の22 filesを読む前に、Parent Issue #3、[Issue #43](https://github.com/kooiei-in4a/minimal-bank-system/issues/43)、Accepted ADR-0001 / 0008 / 0009、`AGENTS.md`、[PR #144](https://github.com/kooiei-in4a/minimal-bank-system/pull/144)のFND-04 final retrospectiveを確認し、次をReferenceとして固定した。

### 3.1 Issue #43 close condition

Docker Compose v2でPostgreSQL、one-shot Migrator、APIを再現可能に起動・停止し、migration成功後だけAPIを開始し、migration失敗時はAPIを開始しないことがClose conditionである。

### 3.2 Scope / out of scope

ScopeはCompose v2、PostgreSQL 18、API、FND-04 explicit Migrator、named volume、digest pin、secret / connection configurationの外部注入、migration→API ordering、fail-closed、deterministic lifecycleである。

Out of scopeはFND-06 health endpoint、business endpoint / schema / data、backup / restore、production deployment、scheduled service / orchestrator、API startup auto-migrationである。承認済み製品仕様のhealth / backup ACは存在するが、WP分割によりFND-05が先取りしないことに矛盾はない。

### 3.3 Required ordering and failure behavior

```text
PostgreSQL connectable / ready
  → FND-04 one-shot Migrator
    → exit 0: API start permitted
    → non-zero: API must never start
```

short syntax `depends_on`の存在だけは証拠にならない。Docker公式文書も、runningとreadyを区別し、`service_healthy`と`service_completed_successfully`を別conditionとして定義している。

### 3.4 API startup no-auto-migration

ADR-0009はmigrationを明示的command / one-shot Compose serviceへ限定し、API通常startupで`Migrate`、`EnsureCreated`、ad-hoc DDLを実行することを禁止する。FND-04のcurrent implementationもAPI startupをschema-read-onlyに保っている。

### 3.5 Secret / image / volume contract

- secret / credential / tokenをrepositoryへ保存しない。
- secretをcommand-line argumentへ直接展開しない。
- PostgreSQL dataはnamed volumeを使う。
- PostgreSQL / .NET imagesはapproved major内のdigestへpinする。exact digestはimplementation PR側で固定する。
- Docker Compose secretsはserviceごとの明示grantとfile mountを提供するが、具体的source / readerはD-03として未確定である。

### 3.6 FND-06 boundary

FND-05では`/health/live` / `/health/ready`を追加しない。API orderingはcontainer state、exit code、Engine timestamp、migration history等を外部観測する。PostgreSQL container readiness用healthcheckはFND-05責任であり、API health contractの先取りではない。

### 3.7 Agent / reviewer / merge gate

- Agent Aは探索・計画・実装・test・diff確認を行い、検証証拠と未検証事項を記録する。
- Independent Reviewerは実装者の説明を前提にせず、正本→diff→test→runtime evidenceの順で確認し、原則targetを変更しない。
- Merge GateはBlocker / Major 0、必須test成功、scope適合、reviewed Head identity、明示的merge許可を要求する。
- Issue #43 comment時点では`ISSUE_READY: NOT YET RE-EVALUATED`、`IMPLEMENTATION: PROHIBITED`、candidate execution未開始である。

### 3.8 External Docker evidence

- [Compose startup order](https://docs.docker.com/compose/how-tos/startup-order/)はrunningをreadyとみなさず、`service_healthy` / `service_completed_successfully`を区別する。
- [`depends_on` reference](https://docs.docker.com/reference/compose-file/services/#depends_on)はshort syntaxがhealthyを待たないことを明記する。
- [Compose secrets](https://docs.docker.com/reference/compose-file/secrets/)はsourceを`file`またはhost `environment`とし、serviceごとのgrantを要求する。
- [`docker compose ps`](https://docs.docker.com/reference/cli/docker/compose/ps/)のJSONはJSON Linesで、`Service`、`State`、`Health`、`ExitCode`等を持つが、Started / Finished timestampは含まない。timestamp取得方法はD-05で別途lockする必要がある。
- [`docker compose config`](https://docs.docker.com/reference/cli/docker/compose/config/)はmerge / interpolation / canonical renderを行い、`--quiet`はvalidationのみである。

## 4. Fixed Policy Assessment

| Fixed policy | Assessment | Result |
| --- | --- | --- |
| C1 Luna / Codex、C2 Sonnet / Claude Code、C3 Grok / Cursor high | README、run、checklist、implementation promptで一致 | PASS |
| Grok `high fast`禁止 | README、run、implementation promptで明示 | PASS |
| OpenCode不使用 | README、run、checklist、implementation promptで一致 | PASS |
| separate Formal Self-Review / H1廃止 | README、run、implementation / final synthesisで一致 | PASS |
| Completion Checksをimplementationへ埋め込む | C-01〜C-11として実装済み。証拠schemaはF-007で要修正 | MODIFY |
| 3 candidate独立実装 | independence、common-base、other-candidate参照禁止が一致 | PASS |
| candidate merge / cherry-pick禁止、current mainからcurated synthesis | scoring、selection、final synthesisで一致 | PASS |
| L1 Composer / L2 Luna | run、README、matrix、promptsで一致 | PASS |
| Light fix後にFinal Head lock | stage順は一致。独立closure欠落はF-004 | MODIFY |
| H1 Sol / H2 Opus、原則各1 full review | run、README、matrix、promptsで一致 | PASS |
| Heavy non-goals明示 | matrixと両Heavy promptで一致し、root-cause例外あり | PASS |
| Judge conditional only | trigger 5種がrun、README、matrix、promptで一致 | PASS |
| re-reviewはowner / blast radius | matrix、targeted fix / re-reviewで一致 | PASS |
| Issue Ready PASS + Koo開始許可まで実装禁止 | README / runは正しいがissue-ready promptとchecklistがpermissionを混同 | FAIL — F-001 |

モデル追加、OpenCode復活、別Formal Self-Review復活は提案しない。必要なのは固定方針を正確に実行するためのcontract修正である。

### 4.1 R-01〜R-10 adjudication summary

| Dimension | Result | Principal evidence / finding |
| --- | --- | --- |
| R-01 Authority and scope correctness | FAIL | Authority逆転 FND05-PSR-002。FND-04 / 05 / 06境界とimplementation禁止自体は明確 |
| R-02 Cross-file consistency | FAIL | permission state、decision / artifact identityにFND05-PSR-001 / 003 |
| R-03 Prompt executability | FAIL | unresolved `<LOCKED_ARTIFACT>`とstage output destination欠落 FND05-PSR-003 |
| R-04 Self-review replacement quality | MODIFY | C-01〜C-11は十分な観点だがevidence binding不足 FND05-PSR-007 |
| R-05 Project rule enforceability | MODIFY | ruleは具体的だがMUST / convention / preference混在 FND05-PSR-006 |
| R-06 Light / Heavy separation | FAIL | role splitは妥当、Light closure blind spotはFND05-PSR-004 |
| R-07 Test oracle / mutation quality | FAIL | M-01 / 03 / 08 / 10のdeterminism不足 FND05-PSR-005 |
| R-08 Docker / secret / lifecycle feasibility | MODIFY | official Compose behaviorと整合。D-03〜D-07 exact lockが未完了 |
| R-09 Evidence / identity integrity | MODIFY | target / common-base / direct-head contractは強いがartifact hash registryなし |
| R-10 Process efficiency | MODIFY | 3 + 2 + 2は成立。SSOT化とtargeted closureで品質を落とさず重複削減可能 |

## 5. Cross-File Consistency Matrix

| Contract / Identity | Source of truth | Referencing files | Result | Gap |
| --- | --- | --- | --- | --- |
| Model / harness / candidate count | Koo policy、PR #144、`run.json` | README、checklist、implementation | PASS | none |
| no OpenCode | Koo policy、`run.json.policy` | README、checklist、implementation | PASS | none |
| no separate Formal SR / H1 | Koo policy、`run.json.policy` | README、implementation、final synthesis | PASS | Completion evidence bindingはF-007 |
| Process stage order | PR #144、README、review matrix | all stage prompts | MODIFY | Light closure verificationがstageとして欠落 |
| Light responsibilities | review matrix | L1 / L2 prompts | PASS | intentional overlapはsecret / lifecycleのrule vs traceabilityに限定 |
| Heavy responsibilities | review matrix | Sol / Opus prompts | PASS | Light closure gapをentry conditionで閉じる必要あり |
| Heavy non-goals / root-cause exception | review matrix | Sol / Opus prompts | PASS | unresolved Light B/Mをexceptionへ追加すべき |
| Judge triggers | `run.json.conditional_judge` | README、matrix、judge prompt | PASS | none |
| Re-review scope | review matrix | targeted fix / re-review | PASS | artifact identityはF-003 |
| Authority order | Koo policy、spec、ADR、Issue、AGENTS | implementation、evaluation、L1、L2、issue-ready | FAIL | Parent / IssueがADRより上に置かれる |
| Revision IDs | `run.json.revisions`、各file header | prompt identity blocks | MODIFY | stage outputのrevision / hash / URIが未固定 |
| D-01〜D-08 | assumption ledger、checklist、`run.json.open_decisions` | design contract、issue-ready、final synthesis | FAIL | value / evidence / approvalを保持するcanonical schemaなし |
| Metrics / gates | `run.json.metric_targets` / `gates` | README、checklist、matrix | MODIFY | readinessとpermissionのderived stateが不明確 |
| Output schema / handoff | each prompt output | next-stage placeholders | FAIL | immutable destination / hash / producer / target Headなし |
| Exact Head / common base | `run.json.target` / candidate identities | implementation、evaluation、Light / Heavy | PASS | null値はpre-run lockで埋める前提 |
| Direct-head vs merge-ref | scoring、catalog RULE-CI-001 | evaluation、final synthesis、reviews | PASS | none |
| Runtime evidence order | scoring | implementation、evaluation、Heavy | PASS | PR self-reportは最下位 |
| Mutation lifecycle | mandatory mutations | final synthesis、Opus、targeted re-review | FAIL | M-01 / 03 / 08 / 10のdeterminism不足 |

## 6. File-by-File Assessment — 22 files

| File | Role | Clarity | Consistency | Executability | Required change |
| --- | --- | --- | --- | --- | --- |
| `README.md` | process overview / start boundary | PASS | PASS | PASS | start boundaryは維持。重複値は将来`run.json`から生成可能 |
| `pre-run-checklist.md` | human gate checklist | PASS | MODIFY | MODIFY | Gate PASSとKoo authorizationからpermissionをderivedする順序へ修正 |
| `run.json` | machine-readable registry | PASS | MODIFY | MODIFY | `decision_locks`、`artifacts`、derived permissionを追加 |
| `scoring.md` | candidate scoring / severity | PASS | PASS | PASS | 現状維持。severityの唯一の共通参照元として活用 |
| `reference/assumption-ledger.md` | external / project assumptions、open decisions | PASS | MODIFY | MODIFY | D-02 / D-05名称を統一し、recommendationとlocked valueを分離 |
| `reference/implementation-and-test-design-contract.md` | runtime / test design contract | PASS | MODIFY | MODIFY | unresolved D値とpreferred proposalを明示分離、topology / placementの同等実装を許容 |
| `reference/project-rule-catalog.md` | enforceable project rules | PASS | MODIFY | MODIFY | authority-backed MUSTとproject convention / preferenceを分類、equivalent resultを追加 |
| `reference/mandatory-mutations.md` | mutation oracle | PASS | MODIFY | MODIFY | M-01 / 03 / 08 / 10へdeterministic precondition / failure signatureを追加 |
| `reference/review-perspective-matrix.md` | role split / budget / re-review | PASS | MODIFY | MODIFY | targeted Light closureをHeavy entry前へ追加 |
| `prompts/implementation.md` | candidate implementation | PASS | MODIFY | MODIFY | Authority順序、permission 3-state、Completion evidence schemaを修正 |
| `prompts/implementation-evaluation.md` | 3-candidate comparison | PASS | MODIFY | MODIFY | Authority、probe isolation、locked output artifact identityを追加 |
| `prompts/selection-adjudication.md` | element-level selection | PASS | MODIFY | MODIFY | input / output artifact URI・hash・revision・Headsを固定 |
| `prompts/final-synthesis.md` | curated implementation | PASS | MODIFY | MODIFY | immutable evaluation / selection input、修正版mutation contractを参照 |
| `prompts/light-review-project-quality.md` | L1 project quality | PASS | MODIFY | MODIFY | Authority修正、targeted closure mode / output identity追加 |
| `prompts/light-review-contract-conformance.md` | L2 traceability | PASS | MODIFY | MODIFY | Authority修正、PARTIAL / FAIL / UNVERIFIEDのindependent closure追加 |
| `prompts/light-findings-fix.md` | Author light fix | PASS | MODIFY | MODIFY | Author disposition後のowner verificationなしでFinal Headをlockしない |
| `prompts/heavy-review-sol.md` | H1 architecture final gate | PASS | MODIFY | MODIFY | entry conditionを`LIGHT_CLOSURE: VERIFIED`へ変更 |
| `prompts/heavy-review-opus.md` | H2 failure / false-assurance gate | PASS | MODIFY | MODIFY | Light closureと修正版mutation preconditionを確認 |
| `prompts/conditional-judge.md` | Heavy disagreement adjudication | PASS | MODIFY | MODIFY | Sol / Opus artifact URI・hash・target Headをinput lockへ追加 |
| `prompts/targeted-fix.md` | locked B/M fix | PASS | MODIFY | MODIFY | finding artifact hashとchange-surface lockのcanonical locationを追加 |
| `prompts/targeted-re-review.md` | finding-owned closure | PASS | MODIFY | MODIFY | old/new Headに加えてfix / finding artifact hashを固定 |
| `prompts/issue-ready-review.md` | pre-run gate | PASS | MODIFY | MODIFY | readinessとKoo authorizationを分離し、早期permissionを禁止 |

`REDESIGN`が必要なfileはない。全Findingは局所的なcross-file修正で閉じられる。

## 7. Findings

### FND05-PSR-001

```text
ID: FND05-PSR-001
SEVERITY: Blocker
CATEGORY: GOVERNANCE / START AUTHORIZATION
AFFECTED_FILES: prompts/issue-ready-review.md; pre-run-checklist.md; prompts/implementation.md; run.json
ROOT_CAUSE: Issue readinessとexecution authorizationを独立stateとして扱わず、同じimplementation permissionへ収束させている。
PROBLEM: issue-ready-review.mdはPASSを「implementationを開始できる」と定義し、出力にIMPLEMENTATION_PERMITTED: YES / NOを要求する一方、末尾ではKooの明示指示まで実行禁止としている。checklistもGate PASS、implementation permitted YES、Koo authorizationを別順で並べる。
FAILURE_OR_CONFUSION_PATH: Gate reviewerがIssue Ready PASS時にIMPLEMENTATION_PERMITTED: YESを出す → candidate promptの「implementationが明示的に許可」を満たしたと実行者が解釈する → run.jsonのkoo_start_authorizedがfalseでもcandidate executionを開始する。
IMPACT: Kooの固定方針とIssue #43のimplementation prohibitionを破る。candidate branch / outputをgate前に汚し、benchmark identityも失う。
EVIDENCE: issue-ready-review.md §4 PASS、§5 output、末尾の互いに矛盾する文言。pre-run-checklist.md §6。README.md start boundaryとrun.jsonのseparate gatesは正しい対照証拠。
RECOMMENDED_CHANGE: ISSUE_READY_RESULT、KOO_START_AUTHORIZED、IMPLEMENTATION_PERMITTEDを分離し、permissionを前2者のANDからのみ導出する。Gate review単体はauthorizationを生成しない。
CROSS_FILE_UPDATES: issue-ready output、checklist順序、implementation gate verification、run.json derived-state ruleを同時更新する。
FIXED_POLICY_AFFECTED: NO
```

### FND05-PSR-002

```text
ID: FND05-PSR-002
SEVERITY: Major
CATEGORY: AUTHORITY / SCOPE
AFFECTED_FILES: prompts/implementation.md; prompts/implementation-evaluation.md; prompts/light-review-project-quality.md; prompts/light-review-contract-conformance.md; prompts/issue-ready-review.md; reference/project-rule-catalog.md
ROOT_CAUSE: governance stateを確認する順序と、矛盾時に適用する正本優先順位を同じAuthority listへ混在させている。
PROBLEM: implementation、L1、L2、Issue Ready promptはParent #3 / WP #33 / Issue #43 / AGENTS.mdをADR-0001 / 0008 / 0009より先に列挙する。これは固定Authority OrderおよびAGENTS.md §2と逆である。
FAILURE_OR_CONFUSION_PATH: IssueまたはParent commentにADRと異なる暫定記述がある → candidate / reviewerが上位に列挙されたIssueを採用 → API auto-migration、secret方式、責任境界等のADR contractを下位資料で上書きする。
IMPACT: 誤実装、scope drift、review verdictの不一致を生む。複数roleが同じ誤った順序を共有するため、後段で相互検出しにくい。
EVIDENCE: implementation.md §1、light-review-project-quality.md §3、light-review-contract-conformance.md §3、issue-ready-review.md §2。対してdesign contract §1とHeavy prompts §4はADR→Issue→AGENTSの順である。
RECOMMENDED_CHANGE: 全promptへ同一のAuthority / Governance blockを導入し、Parent #3 / WP #33は「phase / gate / prohibitionの確認元」であって仕様・設計の優先正本ではないと明記する。
CROSS_FILE_UPDATES: 上記5 promptとRULE-GOV-001を共通文面へ同期する。
FIXED_POLICY_AFFECTED: NO
```

### FND05-PSR-003

```text
ID: FND05-PSR-003
SEVERITY: Major
CATEGORY: STATE / IDENTITY / PROMPT HANDOFF
AFFECTED_FILES: run.json; pre-run-checklist.md; reference/assumption-ledger.md; reference/implementation-and-test-design-contract.md; prompts/implementation-evaluation.md; prompts/selection-adjudication.md; prompts/final-synthesis.md; prompts/light-findings-fix.md; prompts/conditional-judge.md; prompts/targeted-fix.md; prompts/targeted-re-review.md
ROOT_CAUSE: run.jsonがmachine-readable registryを名乗る一方、open decision名とboolean gateしか持たず、解決値・根拠・approval・artifact identityを保存するschemaを持たない。
PROBLEM: D-01〜D-08をlockしても、minimum version、full digests、secret reader、commands、state capture、failure override、platform scope、Final authorを一意に記録するfieldがない。また後続promptは<LOCKED_ARTIFACT>、<LOCKED>、<REVISION>を要求するが、URI / path、hash、producer、target Head、statusが定義されていない。
FAILURE_OR_CONFUSION_PATH: operatorがledger / checklist / promptへ値を手作業で複製する → candidateごとに異なる値またはstale revisionを挿入する。別経路ではevaluation responseを保存しないままSelectionが実行され、Final Synthesisがどのoutputを入力にしたか検証不能になる。
IMPACT: candidate fairness、exact identity、stage reproducibility、wrong-target防止が成立しない。outputが後工程のinputとして安全に機能しない。
EVIDENCE: run.json open_decisions / gates。assumption-ledger D-02はrun registryへfull reference固定を要求するが対応fieldなし。light-findings-fix.mdのL1_RESULT / L2_RESULT、judgeのSOL_REVIEW / OPUS_REVIEW等はplaceholderのみ。
RECOMMENDED_CHANGE: run.jsonへstructured decision_locks、stage_state、artifactsを追加し、各artifactへURI、SHA-256、prompt revision、producer identity、target Base / Head、created_at、statusを記録する。D-09「inter-stage artifact persistence / immutable lock identity」を未決定事項として追加する。
CROSS_FILE_UPDATES: checklist / ledger / all producer-consumer promptsをrun.jsonのfield pathへ参照統一し、手書き複製を禁止する。
FIXED_POLICY_AFFECTED: NO
```

### FND05-PSR-004

```text
ID: FND05-PSR-004
SEVERITY: Major
CATEGORY: REVIEW FUNNEL / BLIND SPOT
AFFECTED_FILES: reference/review-perspective-matrix.md; prompts/light-review-project-quality.md; prompts/light-review-contract-conformance.md; prompts/light-findings-fix.md; prompts/heavy-review-sol.md; prompts/heavy-review-opus.md
ROOT_CAUSE: Light findingのdisposition / fixと、そのclosure検証を同じAuthor actionとして扱い、独立reviewerのtargeted acceptanceを省略している。
PROBLEM: L2はPARTIAL / FAIL / UNVERIFIEDをAuthorがdispositionするとし、light-fixはAuthor自身がaccepted / rejectedを決めてFinal Headをlockする。Heavy entryはdisposition COMPLETE / required fix appliedという自己申告だけを要求し、Heavy non-goalsはmechanical ACまたはLight済みfindingの再確認を除外する。
FAILURE_OR_CONFUSION_PATH: L2がmigration failure evidence欠落をMajor candidateとして報告 → Authorが不十分なtestを追加または誤reject → CIは別testでgreen → Solはmechanical AC repetitionを、OpusはLight済みgeneral findingを除外 → B/M 0としてmerge gateへ進む。
IMPACT: Light→Heavy funnelがfail-openになり、Heavy exclusionが重大blind spotを作る。
EVIDENCE: light-review-contract-conformance.md末尾、light-findings-fix.md §§3–6、review matrix Light Fix Gate / re-review ownership、両Heavy prompt entry / non-goals。
RECOMMENDED_CHANGE: findingがある場合だけoriginal L1 / L2 ownerがFinal Headのchanged surfaceをtargeted closureし、B/M candidate、FAIL、PARTIAL、UNVERIFIEDをFIXED / VALID_REJECTIONへ閉じる。Heavy entryはhashed LIGHT_CLOSURE_ARTIFACT=VERIFIEDを必須にする。full Light re-reviewは不要。
CROSS_FILE_UPDATES: matrixへtargeted Light Closureを追加し、L1 / L2へclosure mode、light-fixへAWAITING_CLOSURE、Heavy entryへverified closure identityを追加する。
FIXED_POLICY_AFFECTED: NO
```

### FND05-PSR-005

```text
ID: FND05-PSR-005
SEVERITY: Major
CATEGORY: TEST ORACLE / MUTATION
AFFECTED_FILES: reference/mandatory-mutations.md; reference/implementation-and-test-design-contract.md; prompts/implementation.md; prompts/implementation-evaluation.md; prompts/final-synthesis.md; prompts/heavy-review-opus.md; prompts/targeted-re-review.md
ROOT_CAUSE: mutationのdefect classは妥当だが、deterministic precondition、injection point、expected failure signatureが不足し、一部はproduction defectでなくoracle自体を無効化している。
PROBLEM: M-01はservice_startedへ弱めてもMigratorが速ければAPI timestampが偶然orderingを満たす。M-03はDBがlatestならauto-migrationを追加してもhistory差が出ない。M-08はhistory assertionを削除 / always successにするため、target testはREDではなくGREENになり得る。M-10は実在するorphan fixtureなしでは--remove-orphans削除を検出できない。
FAILURE_OR_CONFUSION_PATH: Final Synthesisが不安定なmutationを実行 → timingによりGREEN / REDが変動、またはM-08がsurvive → operatorが100% kill rateを記録できない、あるいは無関係failureをtarget REDとして採用 → false assuranceのままHeavyへ進む。
IMPACT: Issue #43の最重要なordering / no-auto-migration / migration-history / cleanup oracleを証明できない。mandatory mutation gateの信頼性を損なう。
EVIDENCE: mandatory-mutations.md M-01 / M-03 / M-08 / M-10と、final-synthesis.mdの一律M-01〜M-10 target RED要件。Docker ps JSONにはtimestampがないためD-05 lockも必要。
RECOMMENDED_CHANGE: mutation共通schemaへPRECONDITION、CONTROLLED_BARRIER、INJECTION_POINT、EXPECTED_FAILURE_SIGNATURE、INVALID_FAILURE_SIGNATURE、CLEANUPを追加し、4 mutationを§15の案へ置換する。Evaluator probeにもisolated worktree / one-at-a-time / residue ruleを追加する。
CROSS_FILE_UPDATES: mutation set、Final Synthesis、Opus、evaluator、targeted re-reviewのreport fieldsを同期する。
FIXED_POLICY_AFFECTED: NO
```

### FND05-PSR-006

```text
ID: FND05-PSR-006
SEVERITY: Minor
CATEGORY: PROJECT RULE / FAIRNESS
AFFECTED_FILES: reference/project-rule-catalog.md; reference/implementation-and-test-design-contract.md; prompts/implementation.md; prompts/light-review-project-quality.md
ROOT_CAUSE: authority-backed safety requirement、locked project convention、単なるpreferred layoutをすべてMUST / FAILとして同一扱いしている。
PROBLEM: catalogはAPI / Migrator Dockerfileのexact two-path placementをMUSTにする一方、design contractは不要なDockerfile重複を避け、implementation promptは別配置を原則許容する。contractはtest-only helperを許可する一方、V-01はservices exactly expected topologyとする。PostgreSQL host port非公開やnon-root runtimeも上位正本では未lockである。
FAILURE_OR_CONFUSION_PATH: candidateがroot multi-target Dockerfile、separate test override、同等以上の配置を採用 → implementation prompt上は説明可能 → L1 catalogでは無条件FAIL → safetyと無関係なcandidate ranking差または不要な再実装が発生する。
IMPACT: false positive、candidate公平性低下、設計の過剰固定。安全gateそのものは維持できるためMinor。
EVIDENCE: project-rule-catalog RULE-PLACE-002 / 004、design contract §§3 / 6 / V-01、implementation §6。
RECOMMENDED_CHANGE: rule classをAUTHORITY_MUST / LOCKED_CONVENTION / PREFERENCEへ分け、resultにEQUIVALENT_BY_LOCKを追加する。default production topologyは「required 3 services + no extra default-enabled permanent service」と定義する。
CROSS_FILE_UPDATES: catalog、design contract、implementation placement、L1 result schemaを同期する。
FIXED_POLICY_AFFECTED: NO
```

### FND05-PSR-007

```text
ID: FND05-PSR-007
SEVERITY: Minor
CATEGORY: SELF-REVIEW REPLACEMENT / EVIDENCE
AFFECTED_FILES: prompts/implementation.md; prompts/final-synthesis.md; prompts/implementation-evaluation.md
ROOT_CAUSE: Completion Checksは詳細だが、final reportがC-01:〜C-11:の自由記述で、各checkを一次証拠へbindするschemaとsnapshot lock invariantがない。
PROBLEM: AgentはCOMPLETION_CHECKSへPASS相当の短文を書き、別のVERIFICATION欄へcommand名だけを書いてSNAPSHOT: LOCKEDを選べる。UNVERIFIEDを成功扱いしない指示はあるが、LOCKEDを禁止する機械判定可能な条件がない。
FAILURE_OR_CONFUSION_PATH: implementation executionが時間不足 → C-09 mutation readinessやC-05 secret process argsを自己申告PASS → snapshot lock → evaluatorが再発見するまでcandidate品質が不明で、separate SR置換の効果測定もできない。
IMPACT: checklist theaterとevidence launderingを許す。後段独立評価があるためmerge safetyの単独Majorではないが、固定policyの測定品質を下げる。
EVIDENCE: implementation.md §§8 / 12、特にfree-form C-01〜C-11とSNAPSHOT field。final-synthesis.mdは同じchecksを参照するだけでschemaを追加しない。
RECOMMENDED_CHANGE: 各C-IDをSTATUS / EVIDENCE / UNVERIFIED_REASONへ構造化し、required checkがPASS以外、またはevidence URI / command resultが欠ける場合はSNAPSHOT=NOT_LOCKEDとする。
CROSS_FILE_UPDATES: implementation / final synthesis output、evaluator input validationを同期する。
FIXED_POLICY_AFFECTED: NO
```

## 8. Role Separation Assessment

### 8.1 Static vs Composer

分離は概ね妥当である。Staticはparse、digest pattern、named volume、prohibited key、source scan、changed-file identity等の決定的checkを担当し、Composerはentrypoint semantics、responsibility placement、duplication、exception swallowing、documentation usabilityを担当する。

ただしcatalogのrule classを分けないと、Staticがpreferred layoutをhard FAILにしてComposerの判断余地を奪う。Static対象はauthority-backedまたはlocked deterministic ruleに限定する。

### 8.2 Composer vs Luna

意図的な重複は許容できる。

- Composer: rule / placement / code qualityとしてsecret grant、volume、lifecycleを見る。
- Luna: Issue ACからimplementation / test / runtime evidenceまでのtraceとして同じ領域を見る。

gapはないが、outputのfinding IDとseverity candidateを共通normal formへ揃えるとLight fixが簡潔になる。

### 8.3 Light vs Heavy

探索責任は良い。Lightが網羅と明白なgap、Heavyがarchitecture / adversarial Blocker / Majorを担当する。問題は探索ではなくclosureである。Author disposition後のindependent Light closureがないため、Heavyの「確認しない項目」がblind spotになる。F-004のtargeted closureを追加すれば、Heavy exclusionを弱めずに解消できる。

### 8.4 Sol vs Opus

重複は必要最小限である。

- Sol: ADR intent、service boundary、Issue本質、design-level security。
- Opus: partial failure、lifecycle、ordering race、ownership、leak path、test oracle。

ordering / secretの重複は「設計保証」と「failure probe」の異なる層であり、削除しない。両promptのroot-cause exceptionも十分である。

### 8.5 Heavy non-goals and exceptions

style、naming、formatter、README typo、simple placement / digest、mechanical AC repetitionを除外する設計は妥当である。ただし次を共通exceptionへ追加する。

> unresolved / rejected / unverified Light Blocker or Major candidate、またはLIGHT_CLOSURE_ARTIFACTがcoverしていないchanged surfaceはHeavy non-goalに含めない。

### 8.6 Budget / re-review / Judge

Sol / Opus各1 full review、default re-review 0は実用的である。fix後のre-reviewをfinding owner / adjacent perspective / blast radiusで決める表も妥当。Conditional Judgeの5 triggersは十分で、常設Judge復活は不要である。

## 9. Self-Review Replacement Assessment

### 9.1 成立性

separate Formal Self-Reviewを廃止し、implementation prompt内C-01〜C-11へ置き換える設計は成立する。自由形式の「セルフレビューせよ」ではなく、authority、scope、ordering、migration、secret、image、volume、oracle、mutation、rule、evidenceを事前固定している点はFND-04 policyと整合する。

### 9.2 残るrisk

- C-01〜C-11がfree-form summaryで、evidenceと一対一にbindされない。
- C-09は「M-01〜M-10を検出可能」と自己申告できるが、candidateでは全mutationを実行しない。
- prompt本文がreference contractを大量複製し、lock後に片方だけ更新されるriskがある。
- exact Head CIは実装完了後にのみ成立するため、CI未完了時のsnapshot stateを明示すべきである。

### 9.3 必要なguard

```text
CHECK_ID:
STATUS: PASS / FAIL / UNVERIFIED / NOT_APPLICABLE
EVIDENCE:
  - command:
  - exit_code:
  - artifact_uri:
  - target_head:
UNVERIFIED_REASON:
```

全required checkが`PASS`で、evidence target Headが一致する場合だけ`SNAPSHOT: LOCKED`とする。Model自己申告は最下位証拠のままとし、Evaluator / Light / Heavyが独立再検証する現在のevidence orderを維持する。

### 9.4 Prompt負荷

負荷は高いが、FND-05 benchmarkとしては実行可能である。品質を落とさず、Authority、stop、final outputはpromptへ残し、詳細rule / mutation本文はlocked revisionとhash参照へ寄せることで短縮できる。

## 10. Project Rule Catalog Assessment

### 10.1 Enforceability

rule ID、MUST / MUST NOT、evidence、ownerがあり、一般的な「きれいに実装する」より大幅に検証可能である。特にAPI no-auto-migration、exit masking、short depends_on、secret argv、digest、named volume、production path、positive marker、mutation residue、CI identityは良い。

### 10.2 Placement

root `compose.yaml`、persistence ownership、Migrator ownership、production / test asset分離は明確である。一方、two Dockerfile exact paths、operations doc exact path、test directory allowlistは安全contractというよりproject conventionである。single multi-target Dockerfile等を無条件FAILにしない。

### 10.3 Owner allocation

Static / Composer / Luna / Heavyの主ownerは概ね妥当。ただしRULE-SEC-004はLightでsentinelの決定的checkを閉じ、Opusはindirect leak / unexpected pathだけを深掘りする二層owner表記がよい。RULE-TEST-004もFinal Synthesis authorが実行し、Opusがevidenceをadjudicateする二層である。

### 10.4 False positive / false negative

| Area | Risk | Required treatment |
| --- | --- | --- |
| exact Dockerfile placement | equivalent designをFAILにする | LOCKED_CONVENTIONまたはEQUIVALENTを許可 |
| exactly 3 services | test-only override / profileまで誤検出 | default-enabled permanent servicesだけを3に限定 |
| PostgreSQL host port ban | upper authority未lock | D-07 / security lockへ結び付けるかPREFERENCEへ降格 |
| non-root runtime | base image / file permission設計を先取り | D-02または追加decisionでlock、未lockならSHOULD |
| centralized contract values | Composeとtestの独立oracleを過度に共有しtautology化し得る | production valueとexpected contractのsourceを分離 |
| secret logs | sentinel一件だけでall leak pathを証明しない | Light deterministic scan + Opus adversarial paths |

### 10.5 Recommended result schema

```text
RULE_ID:
RULE_CLASS: AUTHORITY_MUST / LOCKED_CONVENTION / PREFERENCE
RESULT: PASS / FAIL / NOT_APPLICABLE / EQUIVALENT_BY_LOCK
EVIDENCE:
LOCK_OR_WAIVER_ID:
```

## 11. Mutation and Test-Oracle Assessment

Docker公式仕様上、`service_completed_successfully`、service state / exit code、secret grant、canonical config renderは実装可能である。一方、`docker compose ps --format json`はJSON Linesでtimestampを持たないため、D-05でEngine inspect等のexact methodと時刻比較規則をlockする必要がある。

| Mutation | Defect class valid | Expected RED reliable | False-positive risk | Execution cost | Required change |
| --- | --- | --- | --- | --- | --- |
| M-01 | YES | NO — fast Migratorでraceが顕在化しない | Low false-positive / High false-negative | Medium | controlled barrierでMigrator running中のAPI startを決定的に観測 |
| M-02 | YES | YES if real failure marker fixed | Low | Medium | invalid credential等のexpected error signatureとAPI stateを固定 |
| M-03 | YES | NO — latest DBではauto-migrationがno-op | Medium | Medium–High | clean / pending DBでMigrator dependencyをbypassしたdirect API probeを固定 |
| M-04 | YES | YES | Low if exact sentinel match | Low–Medium | argv / rendered configのactual capture sourceをD-03 / D-05へbind |
| M-05 | YES | YES | Low | Low | registry outageをinvalid signatureとして維持 |
| M-06 | YES | PARTIAL | Medium if static-only result overclaims persistence | Medium | resolved model + retained-volume identityの両方を要求 |
| M-07 | YES | YES | Low | Medium | intended path markerとexpected failure markerを必須維持 |
| M-08 | YES | NO — oracle削除はtestをGREENにし得る | High false assurance | Medium | clean DBでMigratorを呼ばずexit 0にするproduction-path mutationへ置換 |
| M-09 | YES | YES | Low | Low–Medium | `running`安定観測windowとexit stateを固定 |
| M-10 | YES | PARTIAL — known orphanがなければ検出不能 | Low false-positive / High false-negative | Medium–High | known resource fixture / IDsをseedし、cleanup後absenceをassert |

共通reportには`PRECONDITION_CONFIRMED`、`EXPECTED_FAILURE_SIGNATURE`、`OBSERVED_FAILURE_SIGNATURE`、`INVALID_FAILURE_OBSERVED`、`RESIDUE_HEAD`を追加する。

## 12. D-01〜D-08 Open Decision Assessment

値は本reviewで推測lockしていない。

| Decision | Correctly open | Evidence sufficient | Candidate leakage | Missing dependency | Required change |
| --- | --- | --- | --- | --- | --- |
| D-01 minimum Compose version | YES | PARTIAL | NO | local / CIのexact version output、全required feature probe | `service_completed_successfully`、secrets source、JSON Lines parser、config quietを実行証拠化 |
| D-02 exact PostgreSQL / .NET digests | YES | PARTIAL | PARTIAL | platform / manifest-vs-platform digest、official source | ledgerのD-02 `.NET images`とP-07 PostgreSQL分離を統合し、run registryへstructured保存 |
| D-03 secret source / reader | YES | PARTIAL | YES — `preferred` host env→secret→repo entrypointが実質default | runtime user / file permission、missing-secret behavior、CI / Windows reproduction | proposalをNON_BINDINGへ移し、lock後だけcontract MUSTへ昇格 |
| D-04 lifecycle commands | YES | PARTIAL | NO | project name isolation、timeout、exit semantics、interrupt recovery | exact copyable commands + expected state + non-zero ruleをartifact化 |
| D-05 external state capture | YES | PARTIAL | NO | `ps`はtimestampなし、Engine field / time normalization / retry window | command、field path、parser、comparison tolerance、JSON Lines handlingを固定 |
| D-06 failure injection override | YES | PARTIAL | PARTIAL — optionsは列挙されるが方式未lock | isolated override、expected reason、cleanup、no production backdoor | chosen override path / hash / fixture / failure signatureを固定 |
| D-07 cross-platform contract | YES | PARTIAL | PARTIAL — Linux + primary local minimumは先行記述 | primary local exact environment、shell / path / line endings、tool prerequisites | required / best-effort matrixとcanonical runnerを固定 |
| D-08 Final Synthesis identity | YES | PARTIAL | YES — Luna / Codexが`default候補` | artifact access、fresh-context independence、branch / PR identity | default表現をNON_BINDING optionへ変更し、Koo approval evidenceとexact identityをrunへ保存 |

### Missing decision

```text
D-09: inter-stage artifact persistence / immutable lock identity
```

lockに必要な証拠は、artifact store / path、writer、read permission、content hash方式、target Head binding、retention、run.json update responsibilityである。これはcandidateの製品設計判断ではなく、prompt chainを実行可能にするprocess decisionである。

Project Ruleのexact placement、PostgreSQL host exposure、container runtime userをMUSTのまま残す場合は、D-07 / D-02へ明示的に含めるか別decisionとしてlockする。上位正本に不要ならMUSTから降格し、decisionを増やさない方が簡潔である。

## 13. Simplification Opportunities

### 13.1 `run.json`をsingle sourceにする

Model、Harness、effort、branch、PR、Head、revision、D lock、artifact URI / hash、gate stateを`run.json`へ一度だけ記録する。README / checklist / prompt variable blockは生成またはpreflight出力にし、手作業複製を削減する。失われないcontractはidentityとgateであり、むしろ一貫性が上がる。

### 13.2 Common Authority block

5 promptに重複するAuthority / Governance文面を同一blockへ統一する。各promptにはrole固有の追加資料だけを残す。正本順序は削除せず一箇所から挿入する。

### 13.3 Common Target Lock / Operation block

repository、Issue、Base、Head、PR、review-only、prohibited writes、output artifact fieldsを共通template化する。wrong-target防止contractを保ちながら長さを減らせる。

### 13.4 Rule本文の重複を減らす

implementation promptはcritical stop / prohibited behavior / Completion statusを自足させる一方、個別MUSTの全文はlocked `project_rules_revision + sha256`へ参照させる。prompt内ではhigh-risk 10項目だけを再掲する。

### 13.5 Mutation reportを共通化する

Final Synthesis、Opus、targeted re-reviewで同じmutation fieldsを共有する。各promptの独自schemaを削除し、artifact一件をproducer / verifierが読む。

### 13.6 Severityを`scoring.md`へ集約する

Lightは`SEVERITY_CANDIDATE`、Heavy / Judgeはfinal severityという役割差だけを記載し、Blocker / Major本文定義はscoring revisionへ参照統一する。

### 13.7 削除してはいけない記述

- Issue Ready + Koo authorizationの二重gate
- Heavy explicit non-goalsとroot-cause exception
- direct-head / merge-ref distinction
- candidate merge / cherry-pick禁止
- baseline GREEN→mutation RED→restore GREEN→residue 0
- FND-06 / business / backup boundary

## 14. Consolidated Change Plan — P0 / P1 / P2

### P0 — Blocker / Major

1. **Start authorizationを3-state化**
   - Files: `issue-ready-review.md`, `pre-run-checklist.md`, `implementation.md`, `run.json`
   - Dependency: Koo fixed policy only。D lock不要。
2. **Authority / Governance共通blockへ統一**
   - Files: `implementation.md`, `implementation-evaluation.md`, L1, L2, `issue-ready-review.md`, rule catalog
   - Dependency: approved authority order。
3. **Decision / artifact registryとD-09を追加**
   - Files: `run.json`, checklist, ledger、全producer / consumer prompt
   - Dependency: D-09をKooがprocess decisionとしてlock。その後D-01〜D-08 valueを格納。
4. **Targeted Light Closureを追加**
   - Files: review matrix、L1、L2、light fix、Sol、Opus
   - Dependency: artifact registry。
5. **M-01 / M-03 / M-08 / M-10を書換え**
   - Files: mandatory mutations、design contract、implementation / evaluation / final synthesis / Opus / targeted re-review
   - Dependency: D-05 / D-06の値は未lockのままfieldだけ定義可能。exact commandは後で挿入。

### P1 — Minor before lock

1. **Project Rule class / EQUIVALENT導入**
   - Files: project rule catalog、design contract、implementation、L1
   - Dependency: exact conventionsをMUSTにするかKoo判断。不要なら降格。
2. **Completion Checks evidence binding**
   - Files: implementation、final synthesis、implementation evaluation
   - Dependency: artifact registry。

### P2 — Optional polish

1. README / checklist / variable blocksを`run.json`から生成する。
2. Authority / target / operation / finding / mutation schemaをcommon snippet化する。
3. D-02 / D-05のdisplay nameを全fileで完全一致させる。
4. evaluation / selection promptにも`PROMPT_REVISION`とoutput artifact hash echoを追加する。

## 15. Exact Rewrite Proposals

以下は必要箇所だけのcopy-paste可能なreplacement案である。D-01〜D-09の値は埋めていない。

### P0-1 — Issue Readyと開始権限を分離

`prompts/issue-ready-review.md`のVerdict / Outputを置換する。

```text
## 4. Verdict semantics

ISSUE_READY_RESULT: PASS / FAIL / BLOCKED

PASS means only that the Issue Ready requirements are satisfied.
PASS does not authorize candidate execution and does not set
implementation_permitted=true.

KOO_START_AUTHORIZED is independent evidence and can be YES only when a
specific Koo authorization record is supplied in the fixed input.

IMPLEMENTATION_PERMITTED is a derived value:
  YES iff ISSUE_READY_RESULT=PASS
          AND KOO_START_AUTHORIZED=YES
          AND all identity / decision locks remain unchanged.
  NO otherwise.

## Output

ISSUE_READY_RESULT: PASS / FAIL / BLOCKED
ISSUE_READY_EVIDENCE:
KOO_START_AUTHORIZED: YES / NO
KOO_AUTHORIZATION_EVIDENCE: <URL / NOT PRESENT>
IMPLEMENTATION_PERMITTED: YES / NO
```

`pre-run-checklist.md` §6を次へ変更する。

```text
- [ ] Issue Ready Gate result = PASS
- [ ] Koo start authorization evidence recorded
- [ ] run.json.gates.issue_ready_pass = true
- [ ] run.json.gates.koo_start_authorized = true
- [ ] implementation_permitted derived as true only after the four checks above
```

`implementation.md` §3へ追加する。

```text
- run.json.gates.issue_ready_pass = true
- run.json.gates.koo_start_authorized = true
- run.json.implementation_permitted = true

Issue Ready reviewのPASS文言だけをKoo authorizationとして扱わない。
```

### P0-2 — Common Authority / Governance block

implementation、evaluation、L1、L2、Issue Ready promptのAuthorityを次へ統一する。

```text
## Authority and governance

矛盾時の優先順位:
1. Kooが確定した製品方針・承認記録
2. 承認済み製品仕様
3. Accepted ADR-0001 / ADR-0008 / ADR-0009
4. Issue #43のScope / Out of scope / Acceptance Criteria
5. AGENTS.md
6. review済みPR #144 process policy
7. locked FND-05 pre-run contract / rules / mutations
8. code / automated tests / runtime evidence
9. PR本文・model自己申告

Parent Issue #3とWP-1 Issue #33はphase、gate、dependency、Blocker、
implementation prohibitionの確認元であり、仕様・ADR・Issue #43の内容を
上書きする正本ではない。

矛盾を検出した場合は下位資料で補完せず停止する。
```

### P0-3 — `run.json` decision / artifact registry pseudodiff

```json
{
  "implementation_permitted": false,
  "permission_derivation": {
    "requires_issue_ready_pass": true,
    "requires_koo_start_authorized": true,
    "requires_identity_locks_unchanged": true
  },
  "decision_locks": {
    "D-01": {"status":"open","title":"minimum_compose_version","value":null,"evidence":[],"approved_by":null,"locked_at":null},
    "D-02": {"status":"open","title":"exact_postgresql_and_dotnet_image_digests","value":null,"evidence":[],"approved_by":null,"locked_at":null},
    "D-03": {"status":"open","title":"secret_source_and_reader","value":null,"evidence":[],"approved_by":null,"locked_at":null},
    "D-04": {"status":"open","title":"canonical_lifecycle_commands","value":null,"evidence":[],"approved_by":null,"locked_at":null},
    "D-05": {"status":"open","title":"external_state_capture","value":null,"evidence":[],"approved_by":null,"locked_at":null},
    "D-06": {"status":"open","title":"failure_injection_override","value":null,"evidence":[],"approved_by":null,"locked_at":null},
    "D-07": {"status":"open","title":"cross_platform_contract","value":null,"evidence":[],"approved_by":null,"locked_at":null},
    "D-08": {"status":"open","title":"final_synthesis_identity","value":null,"evidence":[],"approved_by":null,"locked_at":null},
    "D-09": {"status":"open","title":"inter_stage_artifact_persistence_and_lock_identity","value":null,"evidence":[],"approved_by":null,"locked_at":null}
  },
  "artifacts": {
    "implementation_evaluation": {"status":"planned","uri":null,"sha256":null,"prompt_revision":"fnd05-implementation-evaluation-v1","base_sha":null,"target_heads":[],"producer":null},
    "selection_adjudication": {"status":"planned","uri":null,"sha256":null,"prompt_revision":"fnd05-selection-adjudication-v1","base_sha":null,"target_heads":[],"producer":null},
    "light_l1": {"status":"planned","uri":null,"sha256":null,"target_head":null,"producer":null},
    "light_l2": {"status":"planned","uri":null,"sha256":null,"target_head":null,"producer":null},
    "light_closure": {"status":"planned","uri":null,"sha256":null,"target_head":null,"verified_by":[]},
    "heavy_sol": {"status":"planned","uri":null,"sha256":null,"target_head":null,"producer":null},
    "heavy_opus": {"status":"planned","uri":null,"sha256":null,"target_head":null,"producer":null}
  }
}
```

各producer promptへ次を追加する。

```text
OUTPUT_ARTIFACT_URI: "<LOCKED_DESTINATION>"
OUTPUT_ARTIFACT_SHA256: "<COMPUTED_AFTER_WRITE>"
OUTPUT_TARGET_HEAD: "<FULL_SHA>"
OUTPUT_PROMPT_REVISION: "<REVISION>"

Output保存後、run.jsonの対応artifact recordと一致しなければ次工程へ進まない。
```

### P0-4 — Targeted Light Closure

review matrixのLight Fix Gate直後へ追加する。

```text
## Targeted Light Closure

Run only when L1 / L2 produced a finding or a PARTIAL / FAIL / UNVERIFIED row.
This is not a full Light re-review.

The original finding owner verifies on the Final Head:
- accepted finding root cause is fixed;
- rejected / N/A disposition is supported by higher authority and primary evidence;
- changed surface is within the finding;
- required static / runtime evidence targets the Final Head;
- no Blocker / Major candidate remains in that finding scope.

LIGHT_CLOSURE_RESULT: VERIFIED / NOT_VERIFIED / NOT_REQUIRED
LIGHT_CLOSURE_TARGET_HEAD:
FINDING_RESULTS:
ARTIFACT_URI:
ARTIFACT_SHA256:

Heavy entry requires VERIFIED or NOT_REQUIRED.
Author self-disposition alone is not sufficient.
```

両Heavy promptのentry conditionを次へ置換する。

```text
- LIGHT_CLOSURE_RESULT = VERIFIED or NOT_REQUIRED
- Light closure artifact SHA-256 matches run.json
- Light closure target Head = FINAL_HEAD_SHA
- no open Light Blocker / Major candidate, FAIL, PARTIAL, or UNVERIFIED
```

### P0-5 — Mutation rewrites

M-01 replacement core:

```text
PRECONDITION:
- baseline ordering test GREEN
- test-only Migrator override emits MIGRATOR_BARRIER_REACHED and remains running
  until an external barrier is released

INJECTED_CHANGE:
- only API depends_on condition changes from service_completed_successfully
  to service_started

EXPECTED_FAILURE_SIGNATURE:
- while Migrator is still running at the barrier, API state becomes started/running;
  ordering test reports API_STARTED_BEFORE_MIGRATOR_EXIT

INVALID_FAILURE_SIGNATURE:
- YAML / build / pull / missing-secret failure
```

M-03 replacement core:

```text
PRECONDITION:
- clean database with no expected migration history row
- direct API probe uses the production API image/entrypoint and bypasses Migrator
  only in a test-only override
- baseline API startup does not alter schema/history

INJECTED_CHANGE:
- add API-startup MigrateAsync (and no other change)

EXPECTED_FAILURE_SIGNATURE:
- API startup changes schema or creates the migration history row;
  no-auto-migration test reports SCHEMA_CHANGED_BY_API_STARTUP
```

M-08 replacement core:

```text
PRECONDITION:
- clean database
- baseline clean-start validator requires the exact expected history row

INJECTED_CHANGE:
- test-only Migrator command exits 0 without invoking the FND-04 Migrator;
  do not modify the validator or its assertion

EXPECTED_FAILURE_SIGNATURE:
- Compose considers Migrator successful, but validator reports
  EXPECTED_MIGRATION_HISTORY_MISSING and the target test is RED
```

M-10 replacement core:

```text
PRECONDITION:
- record exact project container/network/named-volume IDs;
- create the locked cleanup fixture required by D-04/D-06;
- confirm all fixture resources exist before reset

INJECTED_CHANGE:
- remove exactly one required cleanup action at a time
  (volume removal or locked orphan cleanup action)

EXPECTED_FAILURE_SIGNATURE:
- validator reports the exact surviving resource ID and class

INVALID_FAILURE_SIGNATURE:
- resource never existed before reset, project-name mismatch, or unrelated daemon failure
```

### P1-1 — Rule classification and equivalent implementation

`RULE-PLACE-002` replacement example:

```text
RULE-PLACE-002 — Dockerfile ownership
RULE_CLASS: LOCKED_CONVENTION

MUST:
- API and Migrator production image targets are unambiguous and use the common
  repository build context / .dockerignore contract.

PREFERRED PLACEMENT:
- src/MinimalBankSystem.Api/Dockerfile
- src/MinimalBankSystem.Migrator/Dockerfile

EQUIVALENT_BY_LOCK:
- one root multi-target Dockerfile is acceptable only when both targets,
  responsibility boundaries, build commands, and evidence are explicit and the
  locked project-rule decision approves it before candidate execution.

MUST NOT:
- use a test Dockerfile as a production image;
- copy unrelated source/artifacts into the runtime image.
```

V-01の`services exactly contain expected topology`は次へ置換する。

```text
The default production Compose model contains postgres, migrator, and api,
and contains no additional default-enabled permanent service. Test-only helpers
must exist only in a separately identified override/profile and are excluded from
the production-model assertion.
```

### P1-2 — Completion Check evidence schema

`implementation.md` / `final-synthesis.md`のoutputへ適用する。

```text
COMPLETION_CHECKS:
- ID: C-01
  STATUS: PASS / FAIL / UNVERIFIED / NOT_APPLICABLE
  EVIDENCE:
    - command_or_path:
      result:
      artifact_uri:
      target_head:
  UNVERIFIED_REASON:
...
- ID: C-11
  STATUS:
  EVIDENCE:
  UNVERIFIED_REASON:

SNAPSHOT_LOCK_RULE:
- LOCKED only if every required C-ID is PASS;
- every runtime/CI artifact targets HEAD_SHA;
- UNVERIFIED is empty for merge-ready claims;
- git status and mutation residue checks are clean.
```

## 16. KEEP / MODIFY / DROP / ADD

### KEEP

- fixed 3 implementation candidates
- no OpenCode
- no separate Formal Self-Review / H1
- pre-defined Completion Checks
- candidate independence / common base / exact Head
- element-level Selection / Adjudication
- candidate merge / cherry-pick禁止
- current mainからのcurated Final Synthesis
- Static→Composer / Luna→fixed Head→Sol / Opus funnel
- Heavy explicit non-goals + root-cause exception
- Heavy full review each 1 default
- conditional Judge
- finding-owner / blast-radius re-review
- direct-head / merge-ref identity separation
- mutation baseline→RED→restore→residue 0 principle

### MODIFY

- Issue Ready / Koo authorization / permission state
- Authority blocks
- `run.json` machine-readable state
- Light fix closure
- M-01 / M-03 / M-08 / M-10
- rule classification / equivalent placement
- Completion Check evidence schema
- D-02 / D-05 naming and D-03 / D-08 nonbinding proposal wording

### DROP

- `Issue Ready PASS = implementation permitted`という含意
- Author dispositionだけでLight findingをclosedとする経路
- unlocked `<LOCKED_ARTIFACT>` placeholderの手渡し
- M-08でtest assertion自体を削除するmutation
- authority evidenceなしのpreferred layoutを無条件MUST / FAILにする扱い

### ADD

- D-09 inter-stage artifact persistence / immutable lock identity
- structured decision locks / artifact hashes
- targeted Light Closure
- mutation precondition / expected failure signature / invalid signature
- Completion Check evidence-to-Head binding

## 17. Final Lock Recommendation

```text
CURRENT_RECOMMENDATION: PROMPT SUITE FIX REQUIRED
D-01_TO_D-08_LOCK: DO NOT START YET
ISSUE_READY_REVIEW: DO NOT START YET
CANDIDATE_EXECUTION: PROHIBITED
```

次の順序を推奨する。

1. P0-1〜P0-5を同一cross-file change setとして修正する。
2. FND05-PSR-001〜005だけを対象にtargeted independent re-reviewする。
3. Blocker / Major 0になった後、P1-1 / P1-2を反映してprompt revisions / hashesを更新する。
4. D-09をprocess decisionとして先にlockし、artifact store / identityを確定する。
5. D-01〜D-08を一次証拠とKoo approvalでlockし、run.jsonのstructured recordsへ格納する。
6. Issue #43 / Parent #3 / WP #33をcurrent stateへ同期する。
7. fresh Issue Ready Gateを実行する。
8. Issue Ready PASSとKoo start authorizationの両方が揃った時点でのみcandidate executionを開始する。

部分修正で解消可能であり、process shapeの根本再設計や固定モデル構成の変更は不要である。

## 18. Operation Confirmation

```text
TARGET_FILES_CHANGED: NO
TARGET_PR_CHANGED: NO
ISSUE_CHANGED: NO
FND05_IMPLEMENTATION_STARTED: NO
OUTPUT_FILE_ONLY: YES
NEW_PR_CREATED: NO
PR_READY_OR_MERGED: NO
D-01_TO_D-08_LOCKED_BY_REVIEWER: NO
```

## 19. Final one-line assessment

固定されたreview funnelは維持できるが、開始権限・正本順序・artifact lock・Light closure・mutation oracleを直すまでFND-05 lock workへ進めてはならない。
