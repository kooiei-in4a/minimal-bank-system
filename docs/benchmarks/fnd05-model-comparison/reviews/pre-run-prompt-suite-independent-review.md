# FND-05 Pre-Run Prompt Suite — Independent Review

```yaml
DOCUMENT_STATUS: "COMPLETED INDEPENDENT REVIEW"
REVIEW_TARGET_PR: 145
REVIEW_TARGET_HEAD: "57df6ae1a30ac23151fbcd707f191f5d26dba029"
REVIEW_TARGET_BASE: "a69471578eed12823a1469017dac7fddf32ad41b"
REVIEW_MANIFEST: "pre-run-prompt-suite-review-manifest.md"
OUTPUT_BRANCH: "agent/fnd05-prompt-suite-independent-review"
OUTPUT_PR: 146
OUTPUT_FILE: "docs/benchmarks/fnd05-model-comparison/reviews/pre-run-prompt-suite-independent-review.md"
REVIEWER_MODEL: "Grok 4.5"
REVIEWER_HARNESS: "Cursor"
REVIEWER_EFFORT: "High"
REVIEWER_SLUG: "grok-4.5-cursor-high"
ATTEMPT: 1
```

## 1. Executive Verdict

```text
VERDICT: FIX_REQUIRED
BLOCKER_COUNT: 0
MAJOR_COUNT: 6
MINOR_COUNT: 4
NIT_COUNT: 1
TARGET_HEAD_VERIFIED: YES
```

固定方針（3 candidate / Formal Self-Review廃止 / Light 2 → Head lock / Heavy 2 / Judge conditional / curated Final Synthesis）自体は上位正本と矛盾せず、再設計は不要である。

ただし D-01〜D-08 の一次証拠 lock へ進む前に、prompt suite 側で次を修正する必要がある。

1. Issue #43 が許す「同等 Compose 経路」を、exact 3-service / exact Compose condition / exact placement の MUST へ昇格している。
2. M-08 が production defect ではなく oracle 自身を壊す mutation になっており、期待 RED を信頼できない。
3. Light の REJECTED / UNRESOLVED Blocker・Major 候補を Heavy が再確認しない解釈が可能で、blind spot が残る。
4. 一部 prompt の `Authority` 番号リストが Parent / WP を ADR・Issue より上に見せる。
5. D-03 / D-05 / D-08 が open 宣言と同時に preferred / default / 狭義定義を先取りしている。
6. stage 間の locked artifact を path + content identity で一意参照する control-plane 契約が不足している。

---

## 2. Target Verification

GitHub 一次証拠（`gh pr view 145`）で確認した。

```yaml
REPOSITORY: "kooiei-in4a/minimal-bank-system"
TARGET_PR: 145
TITLE: "docs(fnd05): prepare ADR-first implementation and review funnel"
STATE: OPEN
BASE_BRANCH: "agent/fnd04-final-retrospective-synthesis"
BASE_SHA: "a69471578eed12823a1469017dac7fddf32ad41b"
HEAD_BRANCH: "agent/fnd05-pre-run-preparation"
HEAD_SHA: "57df6ae1a30ac23151fbcd707f191f5d26dba029"
CHANGED_FILES: 22
ADDITIONS: 5006
DELETIONS: 0
```

22 files はすべて `docs/benchmarks/fnd05-model-comparison/**` 配下で、review manifest の exact list と一致する。対象外ファイルの混入はなし。

Output branch の merge-base も `57df6ae1a30ac23151fbcd707f191f5d26dba029` であることを確認した。

`BLOCKED — TARGET MOVED` ではない。

---

## 3. Phase A Reference Review

target suite 評価前に、Issue #43、ADR-0001 / 0008 / 0009、`AGENTS.md`、PR #144 FND-04 final retrospective を固定した。

### 3.1 Issue #43 Close condition

Docker Compose v2 で PostgreSQL、one-shot migrator、API を再現可能に起動・停止でき、migration 成功後だけ API が開始し、migration 失敗時は API を開始しない状態を実現した時点で完了する。

### 3.2 Scope / Out of scope

**Scope:** Compose v2、PostgreSQL 18、API、one-shot migrator **または同等の Compose 正本経路**、named volume、image digest pin、secret 外部注入、migration → API ordering、migration failure 時 API 非起動、deterministic start / stop / clean reset。

**Out of scope:** health endpoint、business endpoint / schema、backup / restore、production deployment、scheduler / orchestrator、API startup auto-migration。

### 3.3 Ordering / failure / no-auto-migration

- PostgreSQL → Migrator → API
- clean DB では migrator 適用後だけ API 開始
- migration failure 時 API 非起動
- migration 未実行を黙って許容しない
- API 通常 startup では schema migration を実行しない

### 3.4 Secret / image / volume

- connection string / password を repository へ固定しない
- secret を command-line 引数へ直接展開しない
- approved major 内の digest で image を固定
- PostgreSQL data は named volume

### 3.5 FND-06 boundary

FND-05 は Compose wiring / lifecycle / external observation / secret・image・volume 統合 / ordering を担う。FND-06 の `/health/*` を先取りせず、container state / exit / timestamp / migration history で ordering を証明する。

### 3.6 Agent / review / merge rules (`AGENTS.md` + Issue)

- Agent A は仕様不足を独自解釈で埋めない
- Agent B は正本 → 差分 → テスト順で独立再検証し、原則対象を変更しない
- Blocker / Major 0、必須証拠、範囲逸脱なし、明示許可が merge gate
- Parent #3 は統制 Issue であり仕様・ADR・AC の正本ではない

### 3.7 PR #144 fixed policy

3 candidate、OpenCode 禁止、Formal Self-Review / H1 廃止、Completion Checks 内蔵、Light 2 → Head lock、Heavy 2（原則各1回）、Heavy 非確認項目明示、Judge conditional、re-review は blast radius、candidate merge / cherry-pick 禁止、Selection 後に current main から curated Final Synthesis。

---

## 4. Fixed Policy Assessment

| Policy element | Assessment |
| --- | --- |
| C1 Luna / Codex, C2 Sonnet / Claude Code, C3 Grok / Cursor high | PASS — README / run.json / issue-ready と一致 |
| OpenCode 禁止 | PASS |
| Formal Self-Review / H1 廃止 | PASS — 方針は成立。Completion Checks 品質は F-07 |
| Completion Checks in implementation | PASS WITH RISK — 代替は可能だが overload / theater |
| curated Final Synthesis from main | PASS |
| Light Composer + Luna → fix → Head lock | PASS WITH RISK — handoff に rejected B/M 必須化が不足（F-03） |
| Heavy Sol + Opus once each | PASS |
| Heavy non-goals | PASS WITH RISK — 「解消済み」解釈が blind spot（F-03） |
| Judge conditional | PASS — trigger 記録主体の明示は Minor |
| re-review by blast radius | PASS — multi-role 集約は Minor |
| Issue Ready + Koo start まで実装禁止 | PASS — run.json `implementation_permitted: false` |

固定方針そのものを再投票する必要はない。上位正本と根本矛盾する固定方針はない。問題は draft contract / prompt の実装粒度である。

---

## 5. Cross-File Consistency Matrix

| Topic | Canonical source | Consistency | Required change |
| --- | --- | --- | --- |
| candidate / Light / Heavy counts | README + run.json | PASS | keep |
| no OpenCode / no Formal SR | README + run.json + prompts | PASS | keep |
| process stage order | README | PASS | add S0 handoff clarity |
| Authority order | AGENTS.md | FAIL | F-04 |
| 3-service exact shape | design contract / RULE / implementation | FAIL vs Issue #43 | F-01 |
| Compose conditions as means vs MUST | design §4.4 vs RULE-COMPOSE-002 | FAIL | F-01 |
| D-01..D-08 registry | run.json + ledger | MODIFY | F-05 |
| D-05 scope | ledger vs checklist | FAIL | F-05 |
| D-03 preferred leakage | ledger / contract / checklist | FAIL | F-05 |
| M-01..M-10 IDs | mandatory-mutations | PASS list / FAIL M-08 design | F-02 |
| Light / Heavy responsibilities | matrix | PASS intent / FAIL reject escape | F-03 |
| locked artifact identity | scattered `<LOCKED>` labels | FAIL | F-06 |
| revision IDs | per-file headers | PASS as labels only | strengthen with hashes |
| severity / output schemas | prompts | PASS local / MODIFY handoff fields | F-03 / F-06 |
| scoring metrics | scoring.md / run.json | PASS | keep |
| implementation prohibition | README / checklist / run.json | PASS | keep |

---

## 6. File-by-File Assessment — 22 files

| File | Role | Clarity | Consistency | Executability | Required change |
| --- | --- | --- | --- | --- | --- |
| `README.md` | process overview / policy | PASS | PASS | PASS | keep; optional S0 / artifact SSOT note |
| `run.json` | machine registry | PASS | MODIFY | MODIFY | add decision evidence slots; keep values null until lock |
| `pre-run-checklist.md` | human gate | PASS | MODIFY | PASS | align D-02/D-03/D-05 wording; remove answer-shaped checklist items |
| `scoring.md` | evaluation rubric | PASS | PASS | PASS | keep |
| `reference/assumption-ledger.md` | assumptions + open Ds | PASS | MODIFY | MODIFY | remove preferred/default; broaden D-05; unify D-02/P-07 |
| `reference/implementation-and-test-design-contract.md` | runtime/test contract | PASS | MODIFY | MODIFY | demote exact shape to preferred; keep observable contracts MUST |
| `reference/project-rule-catalog.md` | MUST / MUST NOT | PASS | MODIFY | MODIFY | align with Issue freedom; assign SCOPE owners; demote overconstraints |
| `reference/mandatory-mutations.md` | test oracle mutations | PASS | MODIFY | MODIFY | redesign M-08 injection |
| `reference/review-perspective-matrix.md` | role separation | PASS | MODIFY | MODIFY | rejected/unresolved Light B/M → Heavy must-review |
| `prompts/implementation.md` | candidate Agent A | MODIFY | MODIFY | MODIFY | Authority split; soften exact topology Checks; compress Checks |
| `prompts/implementation-evaluation.md` | 3-candidate eval | PASS | MODIFY | MODIFY | require artifact path/hash |
| `prompts/selection-adjudication.md` | element selection | PASS | MODIFY | MODIFY | require artifact path/hash |
| `prompts/final-synthesis.md` | curated synthesis | PASS | MODIFY | MODIFY | remove default-author seed; require Selection artifact identity |
| `prompts/light-review-project-quality.md` | Composer L1 | MODIFY | MODIFY | MODIFY | Authority split; consume S0; own-primary-rules not full catalog |
| `prompts/light-review-contract-conformance.md` | Luna L2 | MODIFY | MODIFY | MODIFY | Authority split; drop scoring as Authority |
| `prompts/light-findings-fix.md` | Light fix / Head lock | PASS | MODIFY | MODIFY | require rejected/unresolved B/M handoff list |
| `prompts/heavy-review-sol.md` | Sol Heavy | PASS | MODIFY | MODIFY | limit DO-NOT-CHECK to ACCEPTED+FIXED Minor/Nit |
| `prompts/heavy-review-opus.md` | Opus Heavy | PASS | MODIFY | MODIFY | same Light-escape exception |
| `prompts/conditional-judge.md` | conditional judge | PASS | MODIFY | MODIFY | require immutable review artifact refs |
| `prompts/targeted-fix.md` | targeted fix | PASS | PASS | MODIFY | bind finding IDs to artifact identity |
| `prompts/targeted-re-review.md` | finding-owned re-review | PASS | PASS | MODIFY | define multi-role completion aggregation |
| `prompts/issue-ready-review.md` | pre-run gate | PASS | MODIFY | PASS | Authority wording; verify D list consistency |

判定凡例: Clarity / Consistency / Executability は PASS / MODIFY / REDESIGN。全体ファイルを REDESIGN と判定したものはない。

---

## 7. Findings

### F-01

```text
ID: F-01
SEVERITY: Major
CATEGORY: Authority / Scope / Overconstraint
AFFECTED_FILES:
  - reference/implementation-and-test-design-contract.md
  - reference/project-rule-catalog.md
  - prompts/implementation.md
  - prompts/light-review-project-quality.md
  - prompts/light-review-contract-conformance.md
ROOT_CAUSE:
  Issue #43 の観測可能契約を満たす「同等経路」自由度が、benchmark draft の exact topology /
  exact Compose condition / exact placement MUST へ昇格されている。
PROBLEM:
  design contract §3 は `postgres` / `migrator` / `api` の正確 3 service を固定し、
  RULE-COMPOSE-002 は `service_healthy` / `service_completed_successfully` 相当を MUST とし、
  implementation C-02 は exact 3 service を Completion Check にしている。
  一方 Issue #43 Scope は "one-shot migrator serviceまたは同等のCompose正本経路" を許可し、
  design contract §4.4 自身も同条件を「実装手段」と書いている。
FAILURE_OR_CONFUSION_PATH:
  candidate または Final Synthesis author が Issue/ADR の観測可能契約を満たす同等実装
  （例: wrapper 起動順、別 service 名、同等 condition 機構）を選ぶ
  → Composer / Luna / Completion Checks が RULE / C-02 FAIL
  → Selection が Issue 適合より catalog 適合を優先
  → benchmark が未承認 draft shape への適合試験になる。
IMPACT:
  false positive FAIL、candidate 多様性の喪失、Issue #43 / ADR 正本の実質上書き。
EVIDENCE:
  Issue #43 Scope wording; design contract §3 / §4.4; RULE-COMPOSE-002;
  implementation.md C-02; Docker docs confirm conditions exist as means, not Issue AC.
RECOMMENDED_CHANGE:
  MUST は観測可能契約（ordering / fail-closed / no-auto-migration / named volume /
  digest / secret non-disclosure / lifecycle reproducibility）に限定する。
  exact service names、exact condition mechanism、exact file placement、追加 hardening
  （port 非公開、non-root、no container_name 等）は SHOULD / preferred convention、
  または Koo が common shape として明示 lock する別 decision にする。
CROSS_FILE_UPDATES:
  design contract, project-rule-catalog, implementation Completion Checks,
  L1/L2 must-check lists, matrix owner notes.
FIXED_POLICY_AFFECTED: NO
```

### F-02

```text
ID: F-02
SEVERITY: Major
CATEGORY: Test Oracle / Mutation
AFFECTED_FILES:
  - reference/mandatory-mutations.md
  - prompts/final-synthesis.md
  - prompts/heavy-review-opus.md
  - prompts/implementation.md
  - run.json
ROOT_CAUSE:
  M-08 の注入対象が production/runtime defect ではなく、migration history assertion /
  oracle 自身になっている。
PROBLEM:
  Defect は "__EFMigrationsHistory 確認を削除または常に success へする"。
  Expected detection は "clean-start test が RED"。
  つまり検出器を壊して検出器の RED を期待しており、baseline GREEN → mutated RED が
  「守るべき欠陥クラス検出」ではなく「oracle 自己破壊」になる。
FAILURE_OR_CONFUSION_PATH:
  Final Synthesis が M-08 を実行
  → history assertion を削ると clean-start が GREEN のまま残る（偽安心）
  または assertion 削除そのものを別メタ検証で捕らえる必要が出る
  → `final_mutation_kill_rate: 1.0` が意味を失う
  → Opus false-assurance gate も同じ壊れた oracle を信じ得る。
IMPACT:
  mandatory mutation set の信頼性が崩れ、Issue #43 の "migration 実適用確認" を証明できない。
EVIDENCE:
  mandatory-mutations.md §9 M-08 Defect / Expected detection;
  Applicability table "migration history assertion";
  run.json metric_targets.final_mutation_kill_rate = 1.0.
RECOMMENDED_CHANGE:
  M-08 を production/runtime 側 defect に再設計する。例:
  - Migrator 成功ログを出して history write を skip / fake する
  - history table を別 DB / 空 schema に向けて exit 0 にする
  - migration apply を no-op にして exit 0 にする
  Expected RED は "history row absence / schema absence を検出する production-facing oracle"。
  oracle 削除そのものは別 meta-check とし、M-08 本体にしない。
CROSS_FILE_UPDATES:
  mandatory-mutations.md, final-synthesis mutation section, Opus must-check wording,
  implementation C-09 interpretation, scoring mutation notes if any.
FIXED_POLICY_AFFECTED: NO
```

### F-03

```text
ID: F-03
SEVERITY: Major
CATEGORY: Light / Heavy Separation / Blind Spot
AFFECTED_FILES:
  - prompts/light-findings-fix.md
  - prompts/heavy-review-sol.md
  - prompts/heavy-review-opus.md
  - reference/review-perspective-matrix.md
ROOT_CAUSE:
  Light finding の disposition は記録できるが、REJECTED / UNRESOLVED / ESCALATED
  Blocker・Major 候補を Heavy が独立再確認する必須契約がない。
  同時に Heavy は「Light で解消済み」を原則確認しない。
PROBLEM:
  light-findings-fix は ACCEPTED/REJECTED/... を許すが、Heavy handoff に
  rejected_or_unresolved_blocker_major_candidates が無い。
  Sol/Opus は "Light Reviewで解消済みのMinor / Nit" を探さないと明記し、
  Light Gate escape は Blocker/Major root cause でなければ昇格しない。
FAILURE_OR_CONFUSION_PATH:
  L1/L2 が secret / lifecycle / ordering の Major 候補を出す
  → Author が薄く REJECTED（または UNRESOLVED のまま Head lock）
  → Heavy が「扱済み / 繰り返さない / escape」と解釈して再確認しない
  → B0/M0 の見た目で merge gate へ進む。
IMPACT:
  Light 前処理が fail-open escape hatch になり、Heavy final gate の価値が落ちる。
EVIDENCE:
  light-findings-fix.md §3 Disposition / §5 Heavy handoff list;
  heavy-review-sol.md §7 / §8; matrix §6 Explicitly does not check
  "Light Reviewで解消済みのMinor / Nit".
RECOMMENDED_CHANGE:
  1) Light fix output に rejected/unresolved/escalated B/M 候補の必須リストを追加。
  2) Sol/Opus must-review に当該リストの独立確認を追加。
  3) DO-NOT-CHECK の「解消済み」を ACCEPTED + FIXED + verified に限定。
  4) REJECTED B/M は上位正本一次証拠が無い限り Head lock 禁止、または Heavy 必須再オープン。
CROSS_FILE_UPDATES:
  light-findings-fix, heavy-review-sol, heavy-review-opus, review-perspective-matrix,
  README Light→Heavy handoff note.
FIXED_POLICY_AFFECTED: NO
```

### F-04

```text
ID: F-04
SEVERITY: Major
CATEGORY: Authority / Prompt Executability
AFFECTED_FILES:
  - prompts/implementation.md
  - prompts/light-review-project-quality.md
  - prompts/light-review-contract-conformance.md
  - prompts/issue-ready-review.md
ROOT_CAUSE:
  確認対象（Parent / WP gate 証拠）と製品正本（仕様 / ADR / Issue）が、
  同じ番号付き Authority リストに混在し、Parent/WP が上に見える。
PROBLEM:
  implementation.md §1 は Parent #3 → WP #33 → Issue #43 → AGENTS → ADR の順。
  L1/L2 も同様。AGENTS.md の正本順は 仕様 → ADR → Issue → code/test → PR。
  Parent #3 は統制 Issue であり仕様正本ではない。
FAILURE_OR_CONFUSION_PATH:
  candidate / Light reviewer が番号リストだけを読む
  → Parent コメントや WP 文言を ADR / Issue AC より優先
  → 誤った停止・修正・適合判定。
IMPACT:
  authority inversion。benchmark docs が上位正本を実質変更し得る。
EVIDENCE:
  implementation.md §1; light-review-*.md Authority; issue-ready-review.md §2;
  AGENTS.md §2 / §2.1; design contract §1 と heavy prompts は正しい順を使用。
RECOMMENDED_CHANGE:
  Authority を二分割する。
  A. Product authority: Approved specification → ADR → Issue #43 → AGENTS → code/test
  B. Gate evidence to verify: Parent #3 phase/gate, WP #33 status, Issue Ready
  「矛盾時は A を優先し停止」を明示。
CROSS_FILE_UPDATES:
  implementation, L1, L2, issue-ready; optionally README authority reminder.
FIXED_POLICY_AFFECTED: NO
```

### F-05

```text
ID: F-05
SEVERITY: Major
CATEGORY: Open Decision Integrity / Candidate Leakage
AFFECTED_FILES:
  - reference/assumption-ledger.md
  - reference/implementation-and-test-design-contract.md
  - pre-run-checklist.md
  - prompts/final-synthesis.md
  - run.json
ROOT_CAUSE:
  D-01〜D-08 を TO_LOCK としながら、一部 decision に preferred/default 値や
  狭義定義が本文へ混入し、ファイル間で D-05 の範囲が不一致。
PROBLEM:
  - D-03: preferred host-env → Compose secret → file reader → exec dotnet が先書き
  - D-08: default author = Luna/Codex が先書き
  - D-05: ledger 見出しが "API start timestamp" のみ、checklist は Migrator exit/finish /
    API state/start / history まで含む
  - D-02 / P-07: .NET digest と PostgreSQL digest が二重管理
FAILURE_OR_CONFUSION_PATH:
  pre-run 実行者が ledger の D-05 だけを埋めて TO_LOCK=0 と誤認
  → または D-03/D-08 preferred を「決まっている」と読んで candidate/common shape へ漏洩
  → Issue Ready PASS 後に観測方法欠落、または設計多様性が潰れる。
IMPACT:
  open decision 隔離失敗、false clearance、candidate leakage。
EVIDENCE:
  assumption-ledger D-03/D-05/D-08/P-07; pre-run-checklist §3 D-02/D-03/D-05;
  final-synthesis default author wording; run.json open_decisions list.
RECOMMENDED_CHANGE:
  各 D を question + required evidence のみにする。
  preferred/default は `DRAFT_NOTE` へ隔離し lock 前は非拘束。
  D-05 を full external state capture に改名・拡張。
  D-02 に PostgreSQL + .NET digests を統合するか、P-07 を D-02 配下へ明示統合。
CROSS_FILE_UPDATES:
  assumption-ledger, checklist, design contract secret section, final-synthesis,
  run.json decision schema.
FIXED_POLICY_AFFECTED: NO
```

### F-06

```text
ID: F-06
SEVERITY: Major
CATEGORY: Evidence / Identity Integrity / Prompt Chain
AFFECTED_FILES:
  - prompts/implementation-evaluation.md
  - prompts/selection-adjudication.md
  - prompts/final-synthesis.md
  - prompts/light-findings-fix.md
  - prompts/conditional-judge.md
  - prompts/targeted-fix.md
  - prompts/targeted-re-review.md
  - run.json
  - README.md
ROOT_CAUSE:
  stage 出力の "LOCKED" が revision ラベル / プレースホルダ文字列に止まり、
  immutable artifact path + content identity + target Head の共通契約がない。
PROBLEM:
  Evaluation / Selection / Light / Heavy / Judge が `<LOCKED_ARTIFACT>` や
  `revision:` 文字列だけで次工程へ渡される。S0 Static Gate は matrix にあるが
  dedicated prompt / handoff field が弱い。Judge trigger 記録主体も未定義。
FAILURE_OR_CONFUSION_PATH:
  Selection が古い Evaluation を読む / Final Synthesis が別 Selection を読む /
  Heavy が別 Head の Light disposition を読む / Judge が未固定 review を比較する
  → wrong-target または stale-contract で後工程が成立しない。
IMPACT:
  exact identity 保全が崩れ、benchmark 再現性と merge gate 証拠が壊れる。
EVIDENCE:
  evaluation/selection/judge output schemas; light-findings-fix L1_RESULT/L2_RESULT
  placeholders; README stage diagram vs missing S0 artifact contract.
RECOMMENDED_CHANGE:
  全 stage に共通 lock schema を導入:
  artifact_path, content_sha256, prompt_revision, target_head_sha, produced_at, producer_slot.
  README に S0 → Light の必須入力、Judge trigger 記録者、multi-role re-review 完了条件を追記。
CROSS_FILE_UPDATES:
  all stage prompts listed above, README, optionally run.json stage registry.
FIXED_POLICY_AFFECTED: NO
```

### F-07

```text
ID: F-07
SEVERITY: Minor
CATEGORY: Self-Review Replacement / Prompt Executability
AFFECTED_FILES:
  - prompts/implementation.md
  - prompts/final-synthesis.md
ROOT_CAUSE:
  Completion Checks C-01〜C-11 が Formal Self-Review 代替として広すぎ、
  証拠パス必須でない自己申告 PASS を許しやすい。
PROBLEM:
  実装 1 snapshot に authority、topology、ordering、secret、mutation readiness、
  catalog 確認、CI identity 等を同時要求。C-10 の catalog 全件確認は theater 化しやすい。
FAILURE_OR_CONFUSION_PATH:
  candidate が Checks を全部 PASS と書いて SNAPSHOT LOCKED
  → Evaluation が自己申告に引きずられる
  → 深い欠陥が Light/Heavy へ先送りされ、置換意図（事前固定 DoD）が薄まる。
IMPACT:
  process は成立するが、Formal SR 廃止の品質代替が弱まる。
EVIDENCE:
  implementation.md §8 C-01..C-11 and result schema; separate SR prohibition §7.
RECOMMENDED_CHANGE:
  Checks を identity / runtime mandatory evidence / scope boundary / mutation readiness
  に圧縮。rule catalog 網羅は S0 + L1 primary-owner へ委譲。各 Check に evidence path 必須化。
CROSS_FILE_UPDATES:
  implementation.md, final-synthesis additional checks, optionally matrix.
FIXED_POLICY_AFFECTED: NO
```

### F-08

```text
ID: F-08
SEVERITY: Minor
CATEGORY: Project Rule Catalog / Light Separation
AFFECTED_FILES:
  - reference/project-rule-catalog.md
  - reference/review-perspective-matrix.md
  - prompts/light-review-project-quality.md
ROOT_CAUSE:
  Primary owner 分散と「L1 が catalog 網羅」指示が同時に存在する。
  SCOPE-001〜003 に Primary owner がない。
PROBLEM:
  matrix / catalog は L1 に網羅判定を求めつつ、Static / Luna / Sol / Opus にも owner を割り当てる。
FAILURE_OR_CONFUSION_PATH:
  Composer が全 RULE を再判定し Luna/Static と重複
  → Heavy の「明白な rule 違反 0」指標が解釈不能
  → または SCOPE drift を誰も primary で見ない。
IMPACT:
  Light 分離の効率低下、局所的 gap/overlap。
EVIDENCE:
  project-rule-catalog Primary owner fields; SCOPE section without owner;
  matrix L1 objective wording.
RECOMMENDED_CHANGE:
  L1 は Composer-owned RULE + obvious escape のみ。
  Luna/Static/Heavy owned RULE は参照するが増殖再採点しない。
  SCOPE rules に Primary owner（L1 or L2）を割り当てる。
CROSS_FILE_UPDATES:
  project-rule-catalog, review-perspective-matrix, light-review-project-quality.
FIXED_POLICY_AFFECTED: NO
```

### F-09

```text
ID: F-09
SEVERITY: Minor
CATEGORY: Open Decision / Lifecycle Feasibility
AFFECTED_FILES:
  - reference/assumption-ledger.md
  - reference/implementation-and-test-design-contract.md
  - reference/project-rule-catalog.md
  - pre-run-checklist.md
ROOT_CAUSE:
  D-04 は canonical command 文言のみ TO_LOCK だが、restart 意味論
  （Migrator 再作成必須、API-only restart 禁止）は既に MUST 化されている。
PROBLEM:
  open decision の対象範囲と、既に固定された観測意味論の境界が曖昧。
FAILURE_OR_CONFUSION_PATH:
  D-04 で command 文字列だけ lock して完了扱い
  → restart 意味論の異議や同等手順が後から出る
  → candidate / docs が不一致。
IMPACT:
  D-04 lock 作業の完了判定が曖昧。
EVIDENCE:
  ledger D-04; design contract restart section; RULE-LIFE-001; checklist D-04 items.
RECOMMENDED_CHANGE:
  D-04 を (a) command strings と (b) restart semantics に分け、
  semantics を common contract として先に Koo lock するか、SHOULD に戻す。
CROSS_FILE_UPDATES:
  assumption-ledger, design contract, RULE-LIFE-001, checklist.
FIXED_POLICY_AFFECTED: NO
```

### F-10

```text
ID: F-10
SEVERITY: Nit
CATEGORY: Process Efficiency / Wording
AFFECTED_FILES:
  - prompts/implementation-evaluation.md
  - prompts/light-review-contract-conformance.md
ROOT_CAUSE:
  用語の局所的あいまいさ。
PROBLEM:
  Evaluation の MERGE_READY と candidate direct-merge 禁止が並立し紛らわしい。
  L2 Authority に scoring.md が入ると採点項目を AC のように読まれ得る。
FAILURE_OR_CONFUSION_PATH:
  evaluator が candidate を merge 可能と誤解 / Luna が scoring 減点を AC 欠落扱い。
IMPACT:
  process 判断への実害は小さいが誤解コストがある。
EVIDENCE:
  implementation-evaluation output fields; light-review-contract-conformance Authority.
RECOMMENDED_CHANGE:
  MERGE_READY → ELEMENT_SELECTION_ELIGIBLE 等へ改名。
  L2 Authority から scoring.md を外し Reference only へ。
CROSS_FILE_UPDATES:
  implementation-evaluation.md, light-review-contract-conformance.md.
FIXED_POLICY_AFFECTED: NO
```

---

## 8. Role Separation Assessment

| Layer | Intended job | Assessment |
| --- | --- | --- |
| S0 Static | mechanical restore/build/config/secret/digest/allowlist | 方向 PASS。prompt chain 接続は F-06 |
| L1 Composer | project quality / rule conformance | 意図 PASS。full-catalog 指示で overlap（F-08） |
| L2 Luna | ADR/Issue/AC traceability | 意図 PASS。Authority / scoring 混入は修正（F-04/F-10） |
| H1 Sol | architecture / contract final | 意図 PASS。解消済み解釈を限定（F-03） |
| H2 Opus | failure / lifecycle / false assurance | 意図 PASS。同上 |
| Judge | conditional only | 意図 PASS。trigger/artifact identity を強化 |
| Targeted fix/re-review | blast radius | 意図 PASS。multi-role 集約を明示 |

Composer / Luna の前処理として Light は機能し得る。Heavy non-goals は重複削減に有効だが、F-03 を閉じないと blind spot になる。Sol / Opus の意図的重複（ordering / secret）は merge-blocking 領域として許容できる。

---

## 9. Self-Review Replacement Assessment

| Question | Answer |
| --- | --- |
| Completion Checks で Formal SR を代替できるか | YES — 方針は成立 |
| implementation prompt が過重か | YES — F-07 |
| checklist theater を許すか | PARTIAL — 証拠 path 必須化で防ぐ必要 |
| 自己申告だけで PASS できるか | 現状 YES になり得る → 修正必要 |
| evidence 優先順位 | 概ね正しい（runtime > PR self-report）。artifact identity 不足が弱点 |

**結論:** separate Formal Self-Review / H1 を復活させる必要はない。Checks を薄く強くし、深い探索は Light/Heavy に残す。

---

## 10. Project Rule Catalog Assessment

| Aspect | Assessment |
| --- | --- |
| MUST / MUST NOT 検証可能性 | 多くは検証可能 |
| correct placement 明確性 | 明確だが Issue より狭い（F-01） |
| static / Composer / Luna / Heavy owner | 概ね妥当。SCOPE owner 欠落（F-08） |
| false positive risk | 高い — exact shape / condition MUST（F-01） |
| false negative risk | rejected Light B/M escape（F-03） |
| D-01..D-08 との矛盾 | D-03/D-04 意味論の先取りあり（F-05/F-09） |
| 1方式への過剰固定 | YES — Compose condition / topology |

Catalog は有用。lock 前に「観測契約 MUST」と「common shape SHOULD」を分離すれば、過剰拘束と false positive を下げられる。

---

## 11. Mutation and Test-Oracle Assessment

| Mutation | Defect class valid | Expected RED reliable | False-positive risk | Execution cost | Required change |
| --- | --- | --- | --- | --- | --- |
| M-01 ordering weaken | YES | MEDIUM — timing flake possible | MEDIUM | Medium | clarify reliable observation |
| M-02 exit-0 mask | YES | HIGH | LOW | Medium | keep |
| M-03 API auto-migrate | YES | HIGH | LOW | Medium | keep |
| M-04 secret in argv | YES | HIGH | LOW | Low | keep |
| M-05 digest removed | YES | HIGH | LOW | Low | keep |
| M-06 volume replaced | YES | HIGH | LOW | Medium | keep |
| M-07 fail before path | YES meta-oracle | MEDIUM — RED can be trivial | MEDIUM | Medium | keep as oracle-quality; document as meta |
| M-08 history ignored | YES class / NO injection | LOW | HIGH | Medium | **REDESIGN** |
| M-09 API instant exit | YES | HIGH | LOW | Medium | keep |
| M-10 reset residue | YES | HIGH | LOW | Medium | keep |

baseline GREEN → mutation RED → restore GREEN → residue 0 の枠組み自体は明確で良い。candidate 全件強制ではなく Final Synthesis 完走という負荷配分も妥当。

M-08 は lock 前必須修正。

---

## 12. D-01〜D-08 Open Decision Assessment

| Decision | Correctly open | Evidence sufficient | Candidate leakage | Missing dependency | Required change |
| --- | --- | --- | --- | --- | --- |
| D-01 min Compose version | YES | YES — features listed | LOW — features as requirements OK | installed local/GA versions | keep; confirm support matrix |
| D-02 image digests | YES | PARTIAL — split with P-07 | LOW | PostgreSQL digest ownership | unify D-02/P-07 |
| D-03 secret source/reader | PARTIAL | YES | **YES** preferred answer | cross-platform reader behavior | remove preferred until lock |
| D-04 lifecycle commands | PARTIAL | YES for strings | MEDIUM — semantics pre-locked | restart semantics scope | split strings vs semantics |
| D-05 external state capture | **NO — too narrow in ledger** | PARTIAL | NO | Migrator exit/finish, API state, history | expand/rename |
| D-06 failure injection | YES | YES | LOW | no production backdoor proof | keep |
| D-07 cross-platform | YES | YES | LOW | primary local env identity | keep |
| D-08 Final Synthesis identity | YES | YES | **YES** default author | Selection completion | remove default seed |

追加で不足する独立 decision 候補:

- **D-09（任意）:** common service topology / placement / Compose condition mechanism を Issue 自由度より狭く固定するか。F-01 を Koo lock で解消する場合に使用。推測 lock はしない。

Docker 公式文書（startup-order）により、`service_healthy` / `service_completed_successfully` は実在する実装手段である。FND-06 health なしでも、exit / state / timestamp / history による検証方針は可行。

---

## 13. Simplification Opportunities

品質を落とさず削減できるもの:

1. Completion Checks を少数の evidence-backed DoD へ圧縮（F-07）
2. L1 の full-catalog 再監査を primary-owner へ縮小（F-08）
3. Authority / policy / candidate count の repeated prose を README + run.json 参照へ寄せる
4. Heavy DO-NOT-CHECK リストは維持（固定方針）。文言正規化のみ
5. Evaluation / Selection / Light / Heavy の lock footer を共通 block 化（F-06）

削ってはいけないもの:

- mandatory mutations（M-08 再設計後）
- external observation vs Compose-file-only evidence の分離
- candidate independence / curated Final Synthesis
- Light → Head lock → Heavy once
- rejected B/M の Heavy 再確認（追加が必要）

---

## 14. Consolidated Change Plan — P0 / P1 / P2

### P0 — before D lock / Issue Ready

1. **F-01** exact shape MUST を観測契約 MUST + preferred convention に分離
2. **F-02** M-08 injection redesign
3. **F-03** rejected/unresolved Light B/M → Heavy must-review
4. **F-04** Authority 二分割
5. **F-05** D-03/D-05/D-08 open integrity
6. **F-06** locked artifact identity schema

### P1 — before lock polish

1. **F-07** Completion Checks 圧縮 + evidence path
2. **F-08** L1 owner scope / SCOPE primary owners
3. **F-09** D-04 strings vs semantics
4. Judge trigger recorder + multi-role re-review aggregation
5. run.json decision evidence fields

### P2 — optional

1. **F-10** MERGE_READY naming / L2 scoring Authority
2. M-01 timing reliability note
3. prose dedupe across README/checklist/prompts

修正後は finding-owned 再レビュー 1 回で足りる。suite 全体の REDESIGN は不要。

---

## 15. Exact Rewrite Proposals

### P0-1 — Authority block（implementation / L1 / L2 / issue-ready）

```text
## Authority

### Product authority (highest first)
1. Koo-approved product policy / approved specification
2. Accepted ADR-0001 / ADR-0008 / ADR-0009
3. Issue #43 (scope, out of scope, AC, verification)
4. AGENTS.md
5. FND-05 design contract / project rules / mutations
   (benchmark docs never override 1-4)

### Gate evidence to verify (not product authority)
- Parent Issue #3: current phase / gates / prohibitions
- WP-1 Issue #33: package status
- Issue Ready / Koo start authorization

If gate evidence conflicts with product authority, stop and report.
Do not resolve by preferring Parent/WP wording over ADR/Issue.
```

### P0-2 — M-08 replacement sketch

```text
## 9. M-08 — Migration apply skipped while Migrator exits 0

### Defect
Make the Migrator production path report success (exit 0) while skipping real
schema application / __EFMigrationsHistory write.
Examples:
- no-op apply path behind temporary override
- point Migrator at an empty alternate database and still exit 0
- fake success after connect without applying migrations

### Protected contract
Clean-start success requires actual migration application evidence, not exit 0 alone.

### Expected detection
- clean-start oracle RED
- missing expected __EFMigrationsHistory row and/or missing expected schema object
- failure reason matches "migration not applied" class

### Invalid detection
- deleting the history assertion from the test
- build/syntax failure
```

### P0-3 — Light fix Heavy handoff addition

```text
REJECTED_OR_UNRESOLVED_BLOCKER_MAJOR_CANDIDATES:
- FINDING_ID:
  SOURCE: L1 / L2
  DISPOSITION: REJECTED / UNRESOLVED / ESCALATED
  SUMMARY:
  AUTHOR_REASON:
  REQUIRED_HEAVY_RECHECK: YES
```

### P0-4 — Heavy DO-NOT-CHECK clarification

```text
- Minor / Nit that are ACCEPTED + FIXED + verified by Light disposition/evidence
- Do NOT treat REJECTED, UNRESOLVED, or ESCALATED Blocker/Major candidates as resolved
- Must independently re-check REJECTED_OR_UNRESOLVED_BLOCKER_MAJOR_CANDIDATES
```

### P0-5 — Open decision entry template

```text
### D-0X — <question only>
status: TO_LOCK
question: <what must be decided>
required_evidence:
  - <commands / docs / SHA>
draft_note_non_binding: <optional; ignored by candidates>
locked_value: null
```

### P0-6 — Common stage lock schema

```text
ARTIFACT_LOCK:
  stage:
  artifact_path:
  content_sha256:
  prompt_revision:
  target_head_sha:
  producer_slot:
  produced_at:
STATUS: LOCKED / NOT_LOCKED
```

### P0-7 — design contract topology demotion（pseudodiff）

```text
- Compose projectは最低限次の3 serviceを持つ。
+ Preferred common shape is three services named postgres / migrator / api.
+ MUST: one PostgreSQL runtime, one explicit one-shot Migrator path, one API service,
+       with observable PostgreSQL→Migrator→API success/failure contracts.
+ Exact names/condition mechanism may vary if Issue #43 equivalent path is preserved
+ and documented; silent redesign across candidates is prohibited once D-09/common shape
+ is locked by Koo.
```

---

## 16. KEEP / MODIFY / DROP / ADD

### KEEP

- 3 candidate + Light 2 + Heavy 2 funnel
- Formal Self-Review / OpenCode 禁止
- curated Final Synthesis / no candidate cherry-pick
- Heavy non-goals（正規化後）
- Judge conditional
- external observation over Compose-file-only evidence
- M-01..M-07, M-09, M-10 framework
- implementation prohibition until Issue Ready + Koo start
- docs-only benchmark scope declaration

### MODIFY

- exact topology / condition / placement MUST → preferred or D-09
- M-08 injection
- Light rejected B/M handoff + Heavy exception
- Authority lists
- D-03/D-05/D-08 open records
- Completion Checks volume
- L1 catalog sweep scope
- artifact lock schema

### DROP

- separate Formal Self-Review revival proposals
- OpenCode revival proposals
- preference-based extra reviewers
- treating scoring.md as product Authority

### ADD

- common ARTIFACT_LOCK schema
- rejected/unresolved B/M Heavy recheck list
- optional D-09 common-shape decision（Koo用、今回は lock しない）
- S0 / Judge-trigger / multi-role re-review completion notes in README

---

## 17. Final Lock Recommendation

```text
VERDICT: FIX_REQUIRED
```

D-01〜D-08 の一次証拠 lock と Issue Ready Gate へ進む前に、P0（F-01〜F-06）を prompt suite へ適用し、finding-owned 再レビューを 1 回行うこと。

P0 修正後の見込み:

- Blocker 0 を維持
- Major 0 へ収束可能
- その時点で `READY_FOR_LOCK_WORK` へ移行可能

これは PR #145 をそのまま merge せよという意味ではない。

---

## 18. Operation Confirmation

```text
TARGET_FILES_CHANGED: NO
TARGET_PR_CHANGED: NO
ISSUE_CHANGED: NO
FND05_IMPLEMENTATION_STARTED: NO
D-01_TO_D-08_SPECULATIVELY_LOCKED: NO
NEW_PR_CREATED: NO
OUTPUT_FILE_ONLY_WRITE: YES
```

本レビューは OUTPUT_FILE 1 件のみを更新する。PR #145 の 22 files、Issue #43、製品コード / テスト / Compose 実装には手を入れない。

---

## 19. Final one-line assessment

固定ファネルは健全だが、Issue 自由度の MUST 昇格・M-08 oracle 自己破壊・Light reject の Heavy 盲点・Authority 混線・open decision 漏洩・artifact identity 不足を直すまで lock 作業へ進むべきではない。
