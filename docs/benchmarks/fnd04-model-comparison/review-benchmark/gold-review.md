# FND-04 Final Synthesis — Adjudicated Gold / Reference Review

Status: **LOCKED / TARGETED FIX REQUIRED**

```yaml
BENCHMARK_ID: fnd04-final-synthesis-independent-review
RUN_ID: fnd04-final-review-20260810
GOLD_REVISION: fnd04-final-gold-v1
TARGET_ISSUE: 42
TARGET_PR: 140
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
HEAD_SHA: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
JUDGE_PROMPT_REVISION: fnd04-final-judge-v1
JUDGE_C_USED: false
LOCKED_AT: 2026-08-10T14:16:00+09:00
```

このGold / Referenceはraw reviewer 5/5を固定した後、Judge A（GPT-5.6 Sol / Codex）とJudge B（Claude Opus 5 / Claude Code）がfresh contextで独立Phase-A Referenceを作成し、その後normalized findingを裁定した結果から固定した。

Judge A/Bは次のquorum keyで完全一致した。

```text
REFERENCE_VERDICT:      CHANGES_REQUIRED
BLOCKING_ROOT_CAUSES:   [NR-01]
MERGE_READY:            NO
```

したがってConditional Judge Cは使用しない。

## Gold verdict

- Verdict: **CHANGES_REQUIRED**
- Merge-ready: **NO**
- Blocker: 0
- Major: 1
- Confirmed merge blocker: `G-01` / normalized `NR-01`

重要な区別として、current production Headのdesign-time behavior自体は複数の独立実行でfail-closedかつ正しいと確認されている。本Goldのblocking事由はproduction defectではなく、Issue #42のfailure-safety contractを守るためのcommitted regression verificationがguard対象の退行を検出できないfalse assuranceである。

## G-01 / NR-01 — C8-M01 regression false assurance

- Severity: **Major**
- Blocking: **YES**
- Affected path: `tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs`

### Root cause

現在のtestは主に次だけを合格条件とする。

1. `dotnet-ef database update --no-build` child processがnon-zeroで終了する。
2. stdout/stderrに固定blocklist (`127.0.0.1`, `localhost`, `design_time`, `Data Source=`, `Sqlite`, `InMemory`) が現れない。

このため、失敗がproduction design-time factory / Npgsql connection-required pathへ実際に到達したことをpositiveに証明しない。また、destinationが未構成であることを直接証明せず、固定文字列の非出現から推定している。

### Independent mutation evidence

Judge A/Bは互いの結果を読む前のPhase Aで、少なくとも次を独立再現した。

- production factoryをoff-blocklistのfabricated destination（例: `Host=db;Database=ambient_fallback`）へ退行させてもcommitted regressionがgreenになる。
- `--no-build` commandがfactoryへ到達できないbuild-output/tooling failureでもcommitted regressionがgreenになる。
- 一方、unmodified Headではproduction factory経路を通るconnection-required operationはNpgsql上でdestination未構成のままfail-closedし、`The ConnectionString property has not been initialized.` 相当のfailureとなる。

つまりcurrent productionは正しいが、その安全性を守る唯一の専用regressionがguard対象のdefect classでredにならない。

### Impact

Issue #42のdesign-time no-fallback / fail-closed contractは、誤れば意図しないdatabaseへmigrationを適用するfailure-safety問題に直結する。Final SynthesisではC8-M01の再発防止testをmandatory guardとして要求していたため、そのguardが守るべき退行でgreenになる状態はmerge-required verificationの実質未達である。

### Required fix

production codeは変更しない。test側を最小修正する。

最低限、connection-required EF operationが意図したproduction design-time / Npgsql pathへ到達し、**destination未構成のために失敗したことをpositiveにassert**する。

例:

- unconfigured connectionを示すpositive failure markerを要求する。
- Npgsql / EF connection-required pathへ到達したmarkerを要求する。
- arbitrary build/tool/MSBuild failureが`exit != 0`だけで合格しないようにする。
- fixed destination blocklistは補助assertionとして残してよいが、主要証拠にしない。
- `--no-build`を維持する場合も、factory未到達の失敗がgreenにならないassertionにする。

修正後、少なくとも次のmutation sensitivityを確認する。

1. off-blocklist fabricated destinationを一時注入するとtestがFAILする。
2. factoryへ到達できない代表的tool/build failureでtestがFAILする。
3. mutationを完全discardしたcurrent productionでtestがPASSする。

mutationは検証用一時変更でありcommitしない。

## G-02 / NR-02 — coincident timeout budgets / exit taxonomy coupling

- Severity: **Minor**
- Blocking: NO

Npgsql CommandTimeoutとwhole-operation CTSがともに60秒であり、例外型によってFailure=1 / Timeout=2を分類する構造には保守性上のcouplingがある。

Judge間では「provider command timeoutがCTSより先に到達するreachable race」の厳密な評価に差があったため、そのsub-claim自体はGold root causeへ含めない。Goldとして確定するのは、同値deadlineの二重機構と公開exit taxonomyが暗黙のordering marginへ依存するという非blockingのassurance / maintainability concernまでとする。

Issue #42の必須contractであるbounded non-zero failureは満たしており、targeted Major fixでは変更不要。

## G-03 / NR-04 — CI evidence wording

- Severity: **Nit**
- Blocking: NO

PR本文でRun `31350916189`を`Exact Head CI`としているが、actual checkoutはPR merge ref `d12de2ae...`。

別のpush run `31350870902`がexact Head `99cee438...`をdirect checkoutしSUCCESSしているためCI evidence gapはない。PR本文を更新する際に両runを分離記載する。

## G-04 / NR-05 — ordinary failure exit taxonomy coverage

- Severity: **Minor**（Judge AはNit、Judge BはMinor。GoldではREADMEで0/1/2をdeployment-facing contractとして公開している点を重く見てMinorとする）
- Blocking: NO

missing / unreachable / rejected credential / malformed history testsはnon-zeroのみをassertし、ordinary Failure=1をpinしない。Issue #42の最低AC違反ではないためtargeted Major fixには含めない。

## G-05 / NR-06 — low-information assertions

- Severity: **Nit**
- Blocking: NO

一部の`Assert.NotNull`、定数pin、pass-through namingは追加behavioral evidenceが小さい。主要runtime evidenceは別testで成立しており非blocking。

## Rejected / evidence-limit findings

### NR-03 — temporary model-drift negative evidence

Product findingとしては採用しない。Judge A/Bを含む独立実行でtemporary model-only driftが検出され、discard後cleanへ復帰することが再現された。synthetic artifactをcommitへ残さないこと自体もIssue contractに整合する。

## CI identity — adjudicated

```text
PR merge-ref run:
  Run 31350916189
  checkout d12de2ae07003a10d19d576808cf88ec7796da23
  SUCCESS

Direct-head run:
  Run 31350870902
  checkout 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
  SUCCESS
```

Direct-head runはbuild 0 warnings / 0 errors、pending-model PASS、non-PostgreSQL 42 PASS、real PostgreSQL 23 PASS。

## Judge quorum decision

Judge A/BがReference verdict、blocking root cause、merge-readyで一致したためJudge Cは不要。

```text
Judge A: CHANGES_REQUIRED / NR-01 / NO
Judge B: CHANGES_REQUIRED / NR-01 / NO
Judge C: NOT USED
```

## Next gate

PR #140をReady化・mergeしてはいけない。

次工程は**NR-01 / G-01だけを対象とするtargeted Major fix**である。production architectureを再設計せず、`DesignTimeConnectionSafetyTests`のfalse assuranceを解消する最小test fixを行う。

新Head + direct-head CI + PR merge-ref CI取得後、Major fixの独立再レビューを行い、G-01が解消したことを確認してからFormal Agent B product merge gateへ進む。