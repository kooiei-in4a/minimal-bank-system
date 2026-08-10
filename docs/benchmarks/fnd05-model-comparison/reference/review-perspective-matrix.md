# FND-05 Review Perspective Matrix

Revision: `fnd05-review-matrix-v2`

このmatrixはreviewerの重複を減らしつつ、Light findingがHeavy blind spotにならないよう責任境界を固定する。

## 1. Review stages

| Stage | Owner | Purpose | Final merge verdict |
| --- | --- | --- | --- |
| S0 Static | CI / scripts | 機械判定可能なruleを落とす | No |
| L1 Project Quality | Composer 2.5 / Cursor | code/config qualityとComposer-owned project rule | No |
| L2 Contract Conformance | GPT-5.6 Luna / Codex | ADR / Issue / AC / evidence traceability | No |
| LF Light Fix | Final Synthesis Author | Light finding disposition / fix / Head lock | No |
| H1 Architecture | GPT-5.6 Sol / Codex | architecture / contractのBlocker / Major | Yes |
| H2 Adversarial | Claude Opus 5 / Claude Code | failure / lifecycle / false assuranceのBlocker / Major | Yes |
| J Conditional | trigger時に固定 | disagreement adjudication | Yes |

## 2. Common artifact lock

S0以降のstage outputは次のidentityを持つ。

```text
ARTIFACT_LOCK:
  stage:
  artifact_path:
  content_sha256:
  prompt_revision:
  target_head_sha:
  source_artifact_refs:
  producer_slot:
  producer_commit_sha:
STATUS: LOCKED / NOT_LOCKED
```

Downstream stageはexact artifact refとtarget Headを照合してから内容を使用する。

## 3. S0 — Static Project Rule Check

### Primary ownership

機械的に判定できるruleだけを完全判定する。

- restore / build / test
- `docker compose config --quiet`相当
- digest syntax / required image identities after D-02 lock
- named volume structure
- exact prohibited key / pattern
- secret pattern / sentinel scan
- API startup migration source scan
- changed-file allowlist
- `git diff --check`
- target Head / CI identity

### Does not check

- architecture妥当性
- runtime race / lifecycle semantics
- test oracleの意味
- reviewer judgement

S0 FAILのHeadをLight Reviewへ渡さない。

## 4. L1 — Composer Project Quality / Rule Conformance

### Primary objective

Heavy reviewerへ明白なcode/config quality・Composer-owned rule違反を持ち込まない。

### Must check

- Project Rule Catalogの**Composer-owned rules**をPASS / FAIL / N/Aで判定
- file / responsibility placementの意味的妥当性
- duplicated / conflicting configuration
- unnecessary wrappers / abstractions
- exception / exit swallowing
- command / entrypoint semantics
- secret / environment / argvの一般quality
- volume / lifecycle documentationの一般quality
- test name / comment / assertion consistency
- obvious scope drift

### Consume, do not re-run

- S0-owned mechanical rule results
- Luna-owned contract traceability
- Heavy-owned deep failure / architecture rules

他ownerのBlocker / Major root cause候補を偶然見つけた場合は`ESCALATION`として記録し、全件再監査へ広げない。

### Must not spend time on

- Accepted ADRの再設計
- architecture案の比較
- rare race root causeの深掘り
- mutation adjudication
- merge可否の最終判断

## 5. L2 — Luna ADR / Issue / AC Contract Conformance

### Primary objective

ADR / Issue requirement → implementation → test / validator → runtime evidenceの欠落を網羅的に確認する。

### Must check

- Product authority / Issue #43 Scope / Out of scope
- Acceptance Criteria全件
- required runtime roles / observable ordering
- Migrator success / failure contract
- API no-auto-migration
- image / volume / secret requirements
- D-04でlockedされたlifecycle contract
- required verification
- PR evidence / CI identity
- unverified items

Parent #3 / WP-1 #33はGate evidenceとして確認するが、Product authorityとして扱わない。

### Traceability format

| Requirement | Implementation | Test | Runtime evidence | Result |
| --- | --- | --- | --- | --- |

### Must not spend time on

- naming / style polish
- broad refactor proposal
- lifecycle raceの自由探索
- hidden failure modeの深掘り
- Heavy reviewerのadversarial analysis代行

## 6. Light Fix Gate

L1 / L2後にAuthorはfindingを整理する。

各finding disposition:

- ACCEPTED + FIXED
- REJECTED
- DUPLICATE
- NOT_APPLICABLE
- UNRESOLVED
- ESCALATED

### Head lock prohibition

次のいずれかがある場合、Heavy handoffには明示的リストが必須である。

- REJECTED Blocker / Major candidate
- UNRESOLVED Blocker / Major candidate
- ESCALATED Blocker / Major candidate
- evidence-incomplete finding

AuthorのREJECT理由だけで当該root causeを解消済み扱いしない。

### Required Heavy handoff

```text
HEAVY_HANDOFF:
  resolved_and_verified_findings:
  rejected_or_unresolved_blocker_major_candidates:
  escalated_blocker_major_candidates:
  evidence_incomplete_findings:
```

Static gate、必要runtime test、direct-head CIを再実行し、新Final Headをlockする。

## 7. H1 — Sol Architecture / Contract Final Gate

### Primary objective

最終設計がADR・Issueの本質を満たし、mergeを止めるarchitecture / responsibility defectがないか確認する。

### Must check

- ADR intent
- required runtime roles / responsibility boundary
- FND-04 / FND-05 / FND-06 boundary
- startup success contract
- failure non-start contract
- API no-auto-migration
- design-level secret boundary
-重大scope drift
- merge readiness
- `HEAVY_HANDOFF`内でSol scopeに入るREJECTED / UNRESOLVED / ESCALATED Blocker・Major候補の独立再確認

### Explicitly does not check

- formatter / whitespace
- unused using
- naming polish
- minor comments / README typo
- ordinary DRY / local duplication
- exhaustive file-placement re-scan
- simple package / digest presence re-check
- mechanical AC checklist repetition
- **ACCEPTED + FIXED + VERIFIEDされたMinor / Nit**
- alternative architecture based only on preference

除外項目がBlocker / Major root causeへ直結する場合は指摘できる。

## 8. H2 — Opus Adversarial / Failure Final Gate

### Primary objective

happy pathでは見えないfailure、lifecycle、ordering、ownership、false assuranceを探す。

### Must check

- partial failure
- DB usable transition
- Migrator failure / timeout / exit propagation
- API start race
- stop / start / restart / rerun
- volume ownership / cleanup
- process / container state
- secret leak paths
- unexpected fallback / fail-open
- hidden environment / shell dependency
- test reachability / failure reason markers
- mandatory mutation sensitivity
- green test false assurance
- `HEAVY_HANDOFF`内でOpus scopeに入るREJECTED / UNRESOLVED / ESCALATED Blocker・Major候補の独立再確認

### Explicitly does not check

- code style / naming / formatter
- unused codeの軽微な問題
- ordinary DRY / simple directory placement
- README typo
- simple package version comparison
- exhaustive AC checklist
- **ACCEPTED + FIXED + VERIFIEDされた一般quality finding**
- approved architectureの好みによる全面変更
- production-grade orchestrator要求

除外項目がBlocker / Major root causeへ直結する場合は指摘できる。

## 9. Heavy common rules

- PR説明を正しいものとして扱わない。
- exact Final Headとartifact lockを確認する。
- resolved Light findingを繰り返さない。
- REJECTED / UNRESOLVED / ESCALATED B/Mを「処理済み」とみなさない。
- 指摘数の多さを品質とみなさない。
- recommendationとmerge blockerを分離する。
- root causeをnormal formへまとめる。
- review中にtargetを変更しない。

## 10. Heavy review budget

```yaml
sol_full_review_budget: 1
opus_full_review_budget: 1
default_heavy_re_review_budget: 0
```

予算超過条件:

- Blocker / Major fix
- architecture / security boundary change
- failure semantics change
- test oracle material change
- Kooの明示許可

## 11. Re-review ownership

| Finding / Change | Re-review owner |
| --- | --- |
| Light Minor / Nit | original Light reviewer + CI |
| test-only Heavy Major | finding owner + lightweight mutation verifier |
| localized production Heavy Major | finding owner + adjacent Heavy reviewer |
| architecture / security / cross-cutting | Sol + Opus |
| no code change | none |

複数reviewerが必要な場合、全required reviewerが`FIXED`を返すまでre-review completeにしない。

## 12. Conditional Judge

### Trigger

- H1 / H2のBlocker / Major verdict disagreement
- root cause disagreement
- required fix disagreement
- merge readiness disagreement
- common unverified assumption

Triggerを記録する主体はcoordinatorであり、`run.json`のstage artifact registryへreasonとsource review refsを固定する。

JudgeはHeavy outputsを読む前にTargetからReferenceを作り、対象root causeだけを独立probeする。

## 13. Process success metrics

- Heavyで見つかった明白なProject Rule違反: 0
- Heavyで見つかったLightが拾うべきAC欠落: 0
- Heavy unique Blocker / Major: 記録
- Heavy full review: 2
- Heavy re-review: 0をdefault
- Conditional Judge: 0をdefault
- Light finding fix後のregression: 0
