# FND-04 Final Synthesis Independent Review Benchmark

Status: **JUDGE QUORUM COMPLETE / CONFIRMED MAJOR / TARGETED FIX REQUIRED**

```yaml
BENCHMARK_ID: fnd04-final-synthesis-independent-review
RUN_ID: fnd04-final-review-20260810
POOL_REVISION: fnd04-reviewer-pool-v2
TARGET_PR: 140
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
HEAD_SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
RAW_CAPTURE: 5 / 5
GOLD_REVISION: fnd04-final-gold-v1
```

このdirectoryはFND-04 Final Synthesisのrole-diverse independent review、finding normalization、Judge adjudication、Gold / Referenceを管理する。

## Target identity

- PR #140 / Head `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- Base `38c07e210fe4e8689f1d8aeabbb07b92610d1826`
- PR merge-ref CI `31350916189`: SUCCESS / checkout `d12de2ae07003a10d19d576808cf88ec7796da23`
- direct-head CI `31350870902`: SUCCESS / checkout exact Head `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`

Direct-head runはbuild 0 warnings / 0 errors、pending-model PASS、non-PostgreSQL 42 PASS、real PostgreSQL 23 PASS。

## Role-diverse raw review — COMPLETE

| Slot | Model + Harness | Verdict | Merge-ready |
|---|---|---|---|
| R1 | GPT-5.6 Sol / Codex | APPROVE_WITH_FINDINGS | YES |
| R2 | Claude Opus 5 / Claude Code | CHANGES_REQUIRED | NO |
| R3 | GPT-5.6 Luna / Codex | APPROVE_WITH_FINDINGS | YES |
| R4 | GPT-5.6 Sol / Browser | APPROVE | YES |
| R5 | Cursor Auto / Cursor | APPROVE_WITH_FINDINGS | YES |

Raw Markdown + JSON pairは`reviews/`へ5/5保存済み。raw semanticsは後から書き換えない。

## Pre-Judge normalization

- `finding-normalization-prejudge.md`
- `finding-normalization-prejudge.json`

最重要争点は`NR-01` — C8-M01 design-time regression testのfalse assurance。

production Head自体はfail-closedで正しいが、R2はoff-blocklist fabricated destinationとfactory未到達failureでもcommitted testがgreenになるmutationを提示した。R5も同root causeをMinorとして検出したためJudgeへ送った。

## Judge quorum — COMPLETE

Judge prompt: `fnd04-final-judge-v1`

```text
Judge A — GPT-5.6 Sol / Codex
  Reference: CHANGES_REQUIRED
  Blocking:  NR-01
  Merge-ready: NO

Judge B — Claude Opus 5 / Claude Code
  Reference: CHANGES_REQUIRED
  Blocking:  NR-01
  Merge-ready: NO
```

A/Bは互いの結果を見ず、raw reviewer結果を読む前のPhase Aで独立にNR-01 mutationを再現した。

Quorum keyが完全一致したため**Conditional Judge Cは使用しない**。

## Adjudicated Gold / Reference — LOCKED

Canonical:

- `gold-review.md`
- `gold-review.json`
- Revision: `fnd04-final-gold-v1`

Final verdict:

```text
CHANGES_REQUIRED
Merge-ready: NO
Blocker: 0
Major: 1
Confirmed merge blocker: G-01 / NR-01
```

### G-01 / NR-01

`DesignTimeConnectionSafetyTests`が任意のnon-zero終了と固定blocklist不在だけを合格条件にするため、次でもgreenになり得る。

- production factoryにoff-blocklist fabricated destinationを再導入
- `--no-build` operationがfactoryへ到達できないtool/build failure

current production behaviorが正しいこととは別に、merge-required regression verificationがguard対象のdefect classを検出できないためMajor / blocking。

Required fixは**test-only**。production code変更は不要。

## Confirmed nonblocking Gold findings

- G-02 / NR-02 — coincident 60s timeout budgets / exit taxonomy coupling: Minor
- G-03 / NR-04 — CI evidence wording: Nit
- G-04 / NR-05 — ordinary failure exit taxonomy coverage: Minor
- G-05 / NR-06 — low-information assertions: Nit
- NR-03 — product findingではない。independent model-drift negative reproductionでevidence limitation解消

Targeted Major fixではG-01以外をsource変更へ混ぜない。

## Targeted fix — NEXT

Canonical prompt:

- `../prompts/final-synthesis-major-fix.md`
- Revision: `fnd04-final-major-fix-v1`

Expected scope:

```text
tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs
```

Required sensitivity after fix:

1. normal production Head -> targeted test PASS
2. off-blocklist fabricated destination mutation -> targeted test FAIL
3. factory-unreachable/tooling failure -> targeted test FAIL
4. mutation discard -> targeted test PASS / no residue
5. direct-head + PR merge-ref CI -> both SUCCESS

PR #140はDraftのまま維持する。

## Product merge gate

```text
5/5 raw review                  COMPLETE
finding normalization          COMPLETE
Judge A/B                      COMPLETE
Judge C                        NOT REQUIRED
Gold / Reference               LOCKED
G-01 targeted fix              NEXT
Major-fix re-review            NOT STARTED
Formal Agent B                 BLOCKED
```

PR Ready化、merge、Issue #42 closeはG-01 targeted fixと独立re-review完了まで禁止。