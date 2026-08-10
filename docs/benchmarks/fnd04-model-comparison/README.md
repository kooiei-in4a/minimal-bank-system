# FND-04 Model Comparison

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Status: **FINAL REVIEW + JUDGE COMPLETE / CONFIRMED MAJOR / TARGETED FIX REQUIRED**

このdirectoryはFND-04 benchmarkの実行条件、candidate snapshots、Implementation Evaluation、Selection / Adjudication、Final Synthesis、role-diverse review、Judge adjudicationを管理するbenchmark control正本である。

## Current state

```text
H0 implementation snapshot       8/8 LOCKED
Formal Self-Review               8/8 LOCKED
H1 self-review fix snapshot      8/8 LOCKED
H1 exact-head CI                 8/8 SUCCESS
Implementation Evaluation        COMPLETE / LOCKED
Selection / Adjudication         COMPLETE / LOCKED
Final Synthesis                  COMPLETE / SNAPSHOT LOCKED
Role-diverse independent review  COMPLETE / 5 OF 5
Finding normalization            COMPLETE
Judge A / B                      COMPLETE / QUORUM MATCH
Judge C                          NOT REQUIRED
Gold / Reference                 LOCKED / CHANGES_REQUIRED
Targeted Major fix               READY / NOT STARTED
Formal Agent B                   BLOCKED UNTIL FIX + RE-REVIEW
```

## Final Synthesis target under adjudication

```text
PR:            #140
Branch:        agent/issue-42-fnd-04-final-code
Base SHA:      38c07e210fe4e8689f1d8aeabbb07b92610d1826
Head SHA:      99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
Duration:      29 minutes
```

CI identities:

```text
PR merge-ref:
  Run 31350916189
  checkout d12de2ae07003a10d19d576808cf88ec7796da23
  SUCCESS

Direct Head:
  Run 31350870902
  checkout 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
  SUCCESS
```

Direct-head CI: build 0 warnings / 0 errors、pending-model PASS、non-PG 42 PASS、real PG 23 PASS。

## Reviewer pool — completed

Reviewer pool revision: `fnd04-reviewer-pool-v2`

| Slot | Model + Harness | Role | Verdict | Merge-ready |
|---|---|---|---|---|
| R1 | GPT-5.6 Sol / Codex | runtime / failure-path | APPROVE_WITH_FINDINGS | YES |
| R2 | Claude Opus 5 / Claude Code | deep technical / test assurance | CHANGES_REQUIRED | NO |
| R3 | GPT-5.6 Luna / Codex | specification / scope | APPROVE_WITH_FINDINGS | YES |
| R4 | GPT-5.6 Sol / Browser | framework official-source | APPROVE | YES |
| R5 | Cursor Auto / Cursor | practical broad scan | APPROVE_WITH_FINDINGS | YES |

Raw artifacts 5/5 captured.

## Judge quorum — complete

Judge AとJudge Bは互いの結果を見ず、まずraw reviewsを読まないPhase AからReferenceを構築した。

```text
Judge A — GPT-5.6 Sol / Codex
  CHANGES_REQUIRED
  Blocking root cause: NR-01
  Merge-ready: NO

Judge B — Claude Opus 5 / Claude Code
  CHANGES_REQUIRED
  Blocking root cause: NR-01
  Merge-ready: NO
```

Reference verdict / blocking root cause / merge-readyが完全一致したためJudge Cは不要。

## Adjudicated Gold / Reference

Canonical:

- `review-benchmark/gold-review.md`
- `review-benchmark/gold-review.json`
- Revision: `fnd04-final-gold-v1`

```text
Gold verdict:       CHANGES_REQUIRED
Merge-ready:        NO
Blocker:            0
Major:              1
Confirmed blocker:  G-01 / NR-01
```

### G-01 / NR-01 — design-time regression false assurance

Current production `BankDbContextFactory`のfail-closed behavior自体は正しい。

しかし`DesignTimeConnectionSafetyTests`は、主に`exit != 0`と固定destination文字列の不在だけを検証しており、Judge A/Bは独立mutationで次を確認した。

- off-blocklist fabricated destinationを再導入してもtestがgreen
- factoryへ到達できないtool/build failureでもtestがgreen

したがってFinal SynthesisでmandatoryとしたC8-M01 regression guardがguard対象のdefect classを防げておらず、merge-required verificationが実質未達としてMajor / blocking。

production code変更は不要。test-only fixで解消する。

## Nonblocking Gold

- G-02 / NR-02 — 60s timeout mechanisms / exit taxonomy coupling: Minor
- G-03 / NR-04 — PR CI wording: Nit
- G-04 / NR-05 — ordinary failure exit taxonomy coverage: Minor
- G-05 / NR-06 — low-information assertions: Nit
- NR-03 — product findingとして棄却。model-drift negativeは独立再現済み

今回のtargeted fixへ非blocking source改善を混ぜない。

## Next — targeted Major fix

Canonical prompt:

- `prompts/final-synthesis-major-fix.md`
- Revision: `fnd04-final-major-fix-v1`

原則変更対象:

```text
tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs
```

目標:

- connection-required production design-time / Npgsql pathへ到達したことをpositiveにassert
- destination未構成がfailure原因であることをpositiveにassert
- arbitrary non-zero tool/build failureをPASSにしない
- off-blocklist fabricated destination mutationでtestがFAILする
- factory未到達failureでtestがFAILする
- mutationをdiscard後、production baselineでPASS
- new direct-head CI / PR merge-ref CIの両方SUCCESS

その後、G-01だけを対象にMajor-fix independent re-reviewを行う。

## Experiment flow

```text
8 candidates
  -> H0 / SR / H1                         COMPLETE
    -> Implementation Evaluation          COMPLETE
      -> Selection / Adjudication         COMPLETE
        -> Final Synthesis                COMPLETE
          -> role-diverse review 5/5      COMPLETE
            -> Judge A/B                  COMPLETE
              -> Gold / Reference         CHANGES_REQUIRED
                -> G-01 targeted fix      NEXT
                  -> Major-fix re-review
                    -> Formal Agent B
```

PR #140はDraft / unmergedのまま。Ready化、merge、Issue #42 closeは禁止。