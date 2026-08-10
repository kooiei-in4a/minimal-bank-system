# FND-05 Review Perspective Matrix

Revision: `fnd05-review-matrix-v1`

このmatrixは、reviewerの重複を減らし、Heavy ReviewをBlocker / Major探索へ集中させる。

## 1. Review stages

| Stage | Owner | Purpose | Final merge verdict |
| --- | --- | --- | --- |
| S0 Static | CI / scripts | 機械判定可能なruleを落とす | No |
| L1 Project Quality | Composer 2.5 / Cursor | code quality / project rule / placementを広く洗う | No |
| L2 Contract Conformance | GPT-5.6 Luna / Codex | ADR / Issue / AC / evidenceを網羅照合 | No |
| H1 Architecture | GPT-5.6 Sol / Codex | architecture / contractのBlocker / Major | Yes |
| H2 Adversarial | Claude Opus 5 / Claude Code | failure / lifecycle / false assuranceのBlocker / Major | Yes |
| J Conditional | trigger時に固定 | disagreement adjudication | Yes |

## 2. S0 — Static Project Rule Check

### Must check

- restore / build / test
- `docker compose config --quiet`
- Compose service names
- resolved images
- digest-qualified references
- named volume
- prohibited `version:` / `container_name` / privileged / host network / Docker socket
- secret pattern / sentinel
- API startup migration source scan
- changed-file allowlist
- `git diff --check`
- candidate Head / CI identity

### Does not check

- architecture妥当性
- runtime race
- test oracleの意味
- reviewer judgement

### Output

```text
STATIC_GATE: PASS / FAIL
FAILED_RULES:
COMMANDS:
EVIDENCE:
```

S0 FAILのHeadをLight Reviewへ渡さない。

## 3. L1 — Composer Project Quality / Rule Conformance

### Primary objective

Heavy reviewerへ、明白なcode quality・project rule・Compose authoring問題を持ち込まない。

### Must check

- `project-rule-catalog.md`の網羅判定
- file placement
- responsibility placement
- YAML / Dockerfile / entrypointの読みやすさ
- duplicated / conflicting configuration
- magic values
- unnecessary wrappers / abstractions
- exception / exit swallowing
- command / entrypoint semantics
- secret grant / environment / argv
- volume / port / network / privilege
- test name / comment / assertion consistency
- operations documentation
- scope drift

### Must not spend time on

- 新しいarchitecture案の比較
- Accepted ADRの再設計
- rare concurrency root causeの深掘り
- mutationを使ったtest oracle adjudication
- merge可否の最終判断

### Finding target

- Staticでは検出できないProject Rule違反
- Minor / Nitを含む一般quality finding
- 明白なMajor候補はエスカレーションする

### Output

```text
VERDICT: PASS / FIX_REQUIRED
RULE_RESULTS:
FINDINGS:
ESCALATED_MAJOR_CANDIDATES:
FILES_REVIEWED:
UNVERIFIED:
```

## 4. L2 — Luna ADR / Issue / AC Contract Conformance

### Primary objective

ADR → Issue → implementation → test → evidenceの欠落を機械的に見つける。

### Must check

- Parent #3 / WP-1 #33 / Issue #43 state
- ADR-0001 / 0008 / 0009
- Scope / Out of scope
- Acceptance Criteria全件
- service topology
- startup ordering
- Migrator success / failure contract
- API no-auto-migration
- image / volume / secret requirements
- lifecycle commands
- required verification
- PR evidence / CI identity
- unverified items

### Traceability format

| Requirement | Implementation | Test | Runtime evidence | Result |
| --- | --- | --- | --- | --- |

### Must not spend time on

- stylistic preference
- naming polish
- refactor proposal
- lifecycle raceの自由探索
- hidden failure modeの深掘り
- Heavy reviewerの代替となるadversarial analysis

### Finding target

- missing AC implementation
- implementationはあるがtestがない
- testはあるがruntime evidenceがない
- Issue境界逸脱
- wrong target / CI identity

### Output

```text
VERDICT: PASS / FIX_REQUIRED
TRACEABILITY_MATRIX:
MISSING_IMPLEMENTATION:
MISSING_TEST:
MISSING_EVIDENCE:
SCOPE_DRIFT:
UNVERIFIED:
```

## 5. Light Fix Gate

L1 / L2後にAuthorはfindingを整理する。

- accepted findingだけを必要最小限で修正
- rejected findingは理由を記録
- new scopeを追加しない
- static gateを再実行
- required runtime testsを再実行
- direct-head CIを確認
- new Final Headを固定

Heavy ReviewはこのFinal Headだけを対象にする。

## 6. H1 — Sol Architecture / Contract Final Gate

### Primary objective

最終設計がADR・Issueの本質を満たし、mergeを止めるarchitecture / responsibility defectがないか確認する。

### Must check

- ADR intent
- service responsibilities
- PostgreSQL / Migrator / API boundary
- FND-04 / FND-05 / FND-06 boundary
- startup success contract
- failure non-start contract
- API no-auto-migration
- design-level secret boundary
-重大scope drift
- merge readiness

### Explicitly does not check

- formatter / whitespace
- unused using
- naming polish
- minor comments
- README wording / typo
- ordinary DRY
- local magic string
- exhaustive file-placement re-scan
- simple package / digest presence re-check
- mechanical AC checklist repetition
- Light Reviewで解消済みのMinor / Nit
- alternative architecture based only on preference

### Exception

上記除外項目がBlocker / Majorのroot causeへ直結する場合だけ指摘する。

### Output target

- Blocker / Major中心
- Minor / Nit探索を目的にしない
- `APPROVE`または`CHANGES_REQUIRED`

```text
VERDICT:
BLOCKER:
MAJOR:
ARCHITECTURE_ASSESSMENT:
CONTRACT_ASSESSMENT:
MERGE_READY:
LIGHT_GATE_ESCAPE:
UNVERIFIED:
```

## 7. H2 — Opus Adversarial / Failure Final Gate

### Primary objective

happy pathでは見えないfailure、lifecycle、ordering、ownership、false assuranceを探す。

### Must check

- partial failure
- DB ready transition
- Migrator failure / timeout / exit propagation
- API start race
- stop / start / restart / rerun
- volume ownership / cleanup
- process / container state
- secret leak paths
- unexpected fallback
- fail-open
- hidden environment / shell dependency
- test reachability
- failure reason markers
- mandatory mutation sensitivity
- green test false assurance

### Explicitly does not check

- code style
- naming
- formatter
- unused codeの軽微な問題
- ordinary DRY
- simple directory placement
- README typo
- simple package version comparison
- exhaustive AC checklist
- Light Review済みのgeneral quality finding
- approved architectureの好みによる全面変更
- 将来のproduction-grade orchestrator要求

### Exception

上記除外項目がBlocker / Majorのroot causeへ直結する場合だけ指摘する。

### Output target

- Blocker / Major中心
- actual probe / mutation evidence
- `APPROVE`または`CHANGES_REQUIRED`

```text
VERDICT:
BLOCKER:
MAJOR:
FAILURE_PATH_MATRIX:
MUTATION_RESULTS:
FALSE_ASSURANCE:
MERGE_READY:
LIGHT_GATE_ESCAPE:
UNVERIFIED:
```

## 8. Heavy review common rules

- PR説明を正しいものとして扱わない。
- exact Final Headを確認する。
- Light finding一覧を読み、同じ指摘を繰り返さない。
- Light findingsを再採点しない。
- 指摘数の多さを品質とみなさない。
- recommendationとmerge blockerを分離する。
- Blocker / Major root causeを1つのnormal formへまとめる。
- review中にtargetを変更しない。

## 9. Heavy review budget

```yaml
sol_full_review_budget: 1
opus_full_review_budget: 1
default_heavy_re_review_budget: 0
```

予算超過には次のいずれかを必要とする。

- Blocker / Major fix
- architecture / security boundary change
- failure semantics change
- test oracleのmaterial change
- Kooの明示許可

## 10. Re-review ownership

| Finding / Change | Re-review owner |
| --- | --- |
| Light Minor / Nit | original Light reviewer + CI |
| test-only Heavy Major | finding owner + lightweight mutation verifier |
| localized production Heavy Major | finding owner + adjacent Heavy reviewer |
| architecture / security / cross-cutting | Sol + Opus |
| no code change | none |

## 11. Conditional Judge

### Trigger

- H1 / H2のBlocker / Major verdict disagreement
- root cause disagreement
- required fix disagreement
- merge readiness disagreement
- common unverified assumption

### Judge must do

1. Heavy outputsを読む前にTargetからReferenceを作る。
2. Blocker / Major候補を独立probeする。
3. Heavy outputsを読み、一致 / 不一致を裁定する。
4. required fixを必要十分な範囲へ固定する。

### Judge does not do

- Light ruleの再監査
- style review
- candidate rankingのやり直し
- Final Synthesisの再実装

## 12. Process success metrics

- Heavyで見つかった明白なProject Rule違反: 0
- Heavyで見つかったLightが拾うべきAC欠落: 0
- Heavy unique Blocker / Major: 記録
- Heavy full review: 2
- Heavy re-review: 0をdefault
- Conditional Judge: 0をdefault
- Light finding fix後のregression: 0
