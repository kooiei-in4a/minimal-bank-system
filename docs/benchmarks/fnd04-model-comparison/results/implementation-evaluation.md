# FND-04 Implementation Evaluation

Status: **LOCKED**

```yaml
RESULT_REVISION: fnd04-implementation-evaluation-v1
TARGET_ISSUE: 42
COMMON_BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
H1_LOCK_COMMIT: 93d46a3822a8fddc342781cf5cd981cbac268cdd
SCORING_REVISION: fnd04-implementation-v1
EVALUATOR_PROBES_REVISION: fnd04-evaluator-probes-v1
LOCKED_AT: 2026-08-10T10:51:00+09:00
```

この文書は、H1 8/8 LOCK後に実施したFND-04 Implementation Evaluationのcanonical resultである。candidate branch / PRはEvaluation中に変更していない。

DurationはH0 / Formal Self-Review / H1の全candidate一貫収集が成立しなかったため、正式評価ではすべてN/Aとし、Speed Score / Quality-Time Index / Practical Score speed componentは計算しない。GitHub timestampからAgent処理時間を推測していない。

## Reference Review

Issue #42、Accepted ADR、Common Base、`fnd04-evaluator-probes-v1`を候補比較前にReferenceとして固定した。

主要contract:

- EF Core / Design = 10.0.10
- Npgsql / provider = 10.0.3
- repository-local dotnet-ef = 10.0.10
- BankDbContext / provider configuration / migrations / snapshot / design-time factoryはInfrastructure責任
- dedicated `MinimalBankSystem.Migrator`がone-shot migration applyを所有
- API startup migration / EnsureCreated / ad-hoc DDLは禁止
- canonical connection keyは`ConnectionStrings:Database`、environment formは`ConnectionStrings__Database`
- InitialFoundationはempty baselineでbusiness DDL禁止
- migration historyは`public.__EFMigrationsHistory`
- connection / migration / timeout / cancellation failureは非0終了
- migration execution budgetは60秒
- actual EF pending-model mechanismを使用
- temporary model-only negative drift probeでfailureを確認可能であること
- real PostgreSQL verificationをSQLite / InMemory / fake providerへ置換しない

P-05の60秒budgetは、毎candidateで実60秒sleepを要求せず、production pathへのwiringと決定論的probeでもPASS可能とした。一方、定数比較だけ、`CanBeCanceled`だけ、pre-cancelled tokenだけ、test-only fake delegateだけではproduction 60秒budget発火を完全証明したとは扱わない。

Merge-ready最低条件はBlocker 0 / Major 0、exact-head CI success、required verification成立、Hard Scope違反なし、real PostgreSQL verification維持である。

## H1 Ranking

| Rank | Candidate | Model / Harness / Effort | H1 Score | B / M / m / N | Merge-ready | Verdict |
| ---: | --- | --- | ---: | --- | --- | --- |
| 1 | `claude-opus-5-claude-code` | Claude Opus 5 / Claude Code / xHigh | **99** | 0 / 0 / 0 / 0 | **YES** | APPROVE |
| 2 | `gpt-5.6-sol-codex` | GPT-5.6 Sol / Codex / xHigh | **98** | 0 / 0 / 0 / 1 | **YES** | APPROVE |
| 3 | `claude-sonnet-5-claude-code` | Claude Sonnet 5 / Claude Code / xHigh | **98** | 0 / 0 / 0 / 0 | **YES** | APPROVE |
| 4 | `grok-4.5-cursor` | Grok 4.5 / Cursor / high | **93** | 0 / 0 / 1 / 0 | **YES** | APPROVE_WITH_MINOR |
| 5 | `gpt-5.6-terra-codex` | GPT-5.6 Terra / Codex / xHigh | **92** | 0 / 0 / 2 / 0 | **YES** | APPROVE_WITH_MINOR |
| 6 | `gpt-5.6-luna-opencode` | GPT-5.6 Luna / Open Code / Max | **91** | 0 / 0 / 2 / 0 | **YES** | APPROVE_WITH_MINOR |
| 7 | `gpt-5.6-luna-codex` | GPT-5.6 Luna / Codex / xHigh | **90** | 0 / 0 / 2 / 1 | **YES** | APPROVE_WITH_MINOR |
| 8 | `deepseek-v4-flash-opencode` | DeepSeek V4 Flash / Open Code / Max | **80** | 0 / **1** / 1 / 0 | **NO** | CHANGES_REQUIRED |

C1とC6は98点同点。tie-breakでは、C1がreal PostgreSQL lock + real Migrator processで実際の60秒budget failureを駆動しているためC1を上位とした。

## H0 Ranking

| Rank | Candidate | H0 Score |
| ---: | --- | ---: |
| 1 | `gpt-5.6-sol-codex` | **98** |
| 2 | `claude-opus-5-claude-code` | **98** |
| 3 | `claude-sonnet-5-claude-code` | **95** |
| 4 | `grok-4.5-cursor` | **93** |
| 5 | `gpt-5.6-terra-codex` | **92** |
| 6 | `gpt-5.6-luna-codex` | **90** |
| 7 | `gpt-5.6-luna-opencode` | **89** |
| 8 | `deepseek-v4-flash-opencode` | **79** |

H0 1位と2位は98点同点。H0最優秀は、初回実装時点でtest evidenceの過大表現を残さなかったC1 `gpt-5.6-sol-codex` とする。

## Self-Review Gain

| Candidate | H0 | H1 | Gain | SR Findings | Accepted | Rejected | Evaluation |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `gpt-5.6-sol-codex` | 98 | 98 | 0 | 0 | 0 | 0 | Finding 0は妥当 |
| `gpt-5.6-terra-codex` | 92 | 92 | 0 | 0 | 0 | 0 | evaluator Minorを見逃し |
| `gpt-5.6-luna-codex` | 90 | 90 | 0 | 0 | 0 | 0 | evaluator Minorを見逃し |
| `gpt-5.6-luna-opencode` | 89 | 91 | **+2** | 1 | 1 | 0 | true positive、実質改善 |
| `claude-opus-5-claude-code` | 98 | 99 | **+1** | 2 | 1 | 1 | true positive + over-strict findingの正しいreject |
| `claude-sonnet-5-claude-code` | 95 | 98 | **+3** | 2 | 1 | 1 | **Self-Review最優秀** |
| `grok-4.5-cursor` | 93 | 93 | 0 | 0 | 0 | 0 | timeout evidence弱点を見逃し |
| `deepseek-v4-flash-opencode` | 79 | 80 | **+1** | 1 | 1 | 0 | Nitは修正したがMajorを見逃し |

Self-Review Gain最大はC6 `claude-sonnet-5-claude-code` の+3。H0のtimeout testが`TimeoutSeconds == 60` / cancel可能tokenを主に確認していたfalse assuranceを発見し、H1でTimeProvider seamを使ってproduction `MigrationRunner`のCancellationTokenSourceが正確に60秒budgetをscheduleし、そのbudget発火でdelegate tokenがcancelされexit non-zeroになることを決定論的に証明した。

C5 SR-01 / C6 SR-01のconnection resolution path差に関するFindingはover-strict / false positiveと評価し、H1でrejectしたdispositionを妥当とした。

## Candidate Findings

### C1 — gpt-5.6-sol-codex — 98 / APPROVE

- real Migrator processでclean apply / rerun / connection failureを検証
- secret非出力を検証
- real PostgreSQL migration-history lockで約60秒budget failureを駆動
- API DbContext resolution後もschema無変更を確認
- temporary model-only negative drift: exit 1 -> discard -> exit 0を確認
- Formal SR Finding 0は妥当
- blocking findingなし

### C2 — gpt-5.6-terra-codex — 92 / APPROVE_WITH_MINOR

- production implementationは契約を満たす
- temporary negative model drift evidenceあり
- Minor: P-03 explicit rerun runtime regression evidence不足
- Minor: P-05はreal PostgreSQL lockを使うが250ms external cancellation中心でproduction 60秒CTS発火を直接駆動していない
- Formal SR Finding 0はこれらevidence gapを見逃した

### C3 — gpt-5.6-luna-codex — 90 / APPROVE_WITH_MINOR

- clean apply / pending model / API no-auto-migration成立
- Minor: timeoutは50ms test-only delegate injection中心
- Minor: connection failureもin-process entry point中心でprocess-level failure assuranceが弱い
- Nit: rerun / operational evidenceが上位候補より薄い
- Formal SR Finding 0はevidence weaknessを見逃した

### C4 — gpt-5.6-luna-opencode — 91 / APPROVE_WITH_MINOR

- SR-01はtrue positive
- H1でmalformed `__EFMigrationsHistory`によるactual migration failure testを追加
- pre-cancelled cancellation testを追加
- Minor: pre-cancelled tokenはproduction内部60秒budget発火の直接証明ではない
- Minor: process/rerun boundaryのevidenceは上位候補より弱い
- Self-Review Gain +2

### C5 — claude-opus-5-claude-code — 99 / APPROVE

- real Migrator processでclean apply / rerun / missing / unreachable / authentication failureを確認
- real PostgreSQL blockでactual timeoutを確認
- API startupだけでなくBankDbContextをresolveしprovider / migrations assembly / connection整合を確認した上でschema無変更を確認
- temporary negative drift、idempotent SQL、design-time safety evidenceあり
- SR-01はover-strictでreject妥当
- SR-02はtrue positiveでcomment-only fix妥当
- H1最優秀

### C6 — claude-sonnet-5-claude-code — 98 / APPROVE

- H0のP-05 test assurance不足をSRで正しく検出
- H1でproduction runnerの60秒budget wiringを決定論的に証明
- manual temporary negative model drift evidenceあり
- real API DbContext resolution evidenceあり
- SR-01はover-strictでreject妥当
- Self-Review Gain最大 +3

### C7 — grok-4.5-cursor — 93 / APPROVE_WITH_MINOR

- clean apply / rerun / unreachable / API no-auto / positive・negative pending-model / business boundaryのcoverageは広い
- real Migrator executableのclean apply evidenceあり
- Minor: timeout/cancellationはpre-cancelled token中心でproduction 60秒budget発火の証拠が弱い
- Formal SR Finding 0はこの弱点を見逃した

### C8 — deepseek-v4-flash-opencode — 80 / CHANGES_REQUIRED

#### C8-M01 — Major — design-time missing-connection path fabricates a PostgreSQL destination

`src/MinimalBankSystem.Infrastructure/DesignTimeBankDbContextFactory.cs` は `ConnectionStrings__Database` 未設定時に次の接続先を生成する。

```text
Host=127.0.0.1;Port=5432;Database=design_time;Pooling=false;Timeout=5
```

Issue #42のfixed connection contractでは、connection-requiredなdesign-time operationは`ConnectionStrings__Database`を使用し、missing configurationをfabricated destinationへ置き換えてはならない。

この実装では、connection-requiredなEF design-time commandがmissing-configでfail closedせず、意図しないlocalhost `design_time` databaseへ接続を試み得る。P-10 design-time/runtime consistency違反であり、merge前修正必須のMajorとする。

- Major: C8-M01
- Minor: timeout testはpre-cancelled cancellation中心でinternal 60秒budget発火を直接証明していない
- SRはunused usingのNitのみ発見・修正し、C8-M01を見逃した
- MERGE_READY: NO

## Findings Matrix

| Root cause / assurance差 | Candidates | Evaluation |
| --- | --- | --- |
| fabricated design-time DB fallback | **C8 only** | **Major** |
| real PostgreSQL lock + real processで60s budget発火 | C1, C5 | strongest |
| deterministic production timer seamで60s budget発火 | C6 | strong |
| external / pre-cancel / fake delegate中心 | C2, C3, C4, C7, C8 | weaker evidence |
| explicit rerun regression不足 | C2, C3, C4 | Minor evidence gap |
| APIでDbContext resolve後もschema無変更 | C1, C5, C6 | strong |
| startup before/after中心 | C2, C3, C4, C7, C8 | contract pass, weaker evidence |
| SRで重要なfalse assuranceを発見 | C6 | best SR gain |
| SRでMinor evidence gapを見逃し | C2, C3, C7 | false negative |
| SRでMajorを見逃し | C8 | largest SR failure |

## Final Benchmark Conclusion

```yaml
H1_WINNER: claude-opus-5-claude-code
H0_WINNER: gpt-5.6-sol-codex
MAX_SELF_REVIEW_GAIN: claude-sonnet-5-claude-code
MAX_SELF_REVIEW_GAIN_POINTS: 3
MERGE_READY: 7_of_8
NON_MERGE_READY:
  - deepseek-v4-flash-opencode
BLOCKING_FINDINGS:
  - C8-M01
```

FND-04でも `green CI != failure-path correctness` が再確認された。全8候補のexact-head CIはsuccessだったが、C8にはdesign-time connection contractのMajorが残った。

Self-Review能力はFinding件数では評価できない。C1のFinding 0は妥当だった一方、C8はNitを発見してもMajorを見逃した。C6はtest名とassertionの証拠強度を疑い、false assuranceを実際に除去した点で最も価値のあるSelf-Reviewとなった。

## Stop boundary

Implementation Evaluationはここで完了・LOCKする。

このartifact固定ではcandidate修正、Final Synthesis実装、targeted fix、merge、Issue close、Final Synthesis review、Judge adjudicationへ進まない。
