# FND-04 Selection / Adjudication

Status: **LOCKED**

```yaml
RESULT_REVISION: fnd04-selection-adjudication-v1
TARGET_ISSUE: 42
COMMON_BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
IMPLEMENTATION_EVALUATION_CONTROL_HEAD: e2a5937de695cf06e3dedb2f79b5cc4f85812b4f
IMPLEMENTATION_EVALUATION_REVISION: fnd04-implementation-evaluation-v1
LOCKED_AT: 2026-08-10T11:07:00+09:00
FINAL_SYNTHESIS_BRANCH: agent/issue-42-fnd-04-final-code
```

この文書は、H0 / Formal Self-Review / H1 / Implementation Evaluationを確定した後に、FND-04 Final Synthesisへ採用する設計・検証要素を選別したcanonical adjudicationである。

candidate branch / PRは変更しない。Final Synthesisはcandidate rankingへ追加しない。

## 1. Selection verdict

**Primary implementation base: C5 — Claude Opus 5 / Claude Code / H1**

- Candidate slug: `claude-opus-5-claude-code`
- PR: #134
- H1 Head: `3a788cc31b3f65177d60dd3995842231dd505187`
- H1 score: 99 / 100
- Findings: Blocker 0 / Major 0 / Minor 0 / Nit 0
- Merge-ready at candidate evaluation: YES

C5をFinal Synthesisの設計上の主軸とする。ただしcandidate branchをmerge / cherry-pickしてFinal Synthesisとしない。現在の`main`から別branchを作成し、選択した設計をcurated implementationとして再構成する。

## 2. C5から採用するもの

### Architecture

- `MinimalBankSystem.Infrastructure`が`BankDbContext`、provider configuration、migrations、snapshot、design-time factoryを所有する。
- shared persistence configurationによりAPI / Migrator / design-timeでNpgsql provider、migrations assembly、history tableを一致させる。
- design-time model-only operationではNpgsql providerを維持し、SQLite / InMemory / fake providerへfallbackしない。
- 接続情報がないmodel-only contextでは、架空の接続先をfabricateしない。
- dedicated `MinimalBankSystem.Migrator`をone-shot executableとする。
- API startupはmigration / `EnsureCreated` / ad-hoc DDLを実行しない。
- `InitialFoundation`はempty baselineとし、business schemaを持ち込まない。

### Runtime / failure handling

- canonical key `ConnectionStrings:Database` / environment form `ConnectionStrings__Database`を維持する。
- Migratorはmissing / unreachable / authentication / migration / timeout failureをnon-zeroへ伝播する。
- migration command timeoutとwhole-operation cancellation budgetを60秒に固定する。
- success時のみexit code 0とする。

### Verification

- FND-03実PostgreSQL fixtureを再利用する。
- clean DBへexplicit Migrator processでbaselineを適用する。
- rerunしてhistoryが変化しないことを確認する。
- missing / unreachable / rejected credential failureを実processで確認する。
- PostgreSQL上の実lockでmigrationをblockingし、production 60秒budgetでtimeoutすることを確認する。
- API startup前後でschema / migration historyが変化しないことを実PostgreSQLで確認する。
- APIから実`BankDbContext`をresolveしてもschema mutationが起きないことを確認する。
- actual EF pending-model mechanismを使用する。
- idempotent migration SQL generationを検証する。
- baselineのUp / Down operationがemptyであることを検証する。

## 3. C1から追加採用するもの

C1 — GPT-5.6 Sol / Codex / H1 (`7025c256b8b1ec1f0f4b9904f71a1047faac4cca`)のarchitecture全体は採用しない。C5と重複するためである。

ただし次の**failure-output secret non-disclosure regression**はFinal Synthesisへ追加採用する。

- fixtureのcredential / sentinel passwordを含む失敗connection stringでMigratorを実行する。
- processがnon-zeroで終了することを確認する。
- captured stdout / stderrへpasswordが出力されないことを確認する。

これはC5のfailure evidenceを補強するtest-only hardeningとして扱い、testを通すためだけの過剰なlogging abstractionは導入しない。現行実装がそのままPASSする場合、production codeを変更しない。

## 4. C6から採用しないもの

C6 — Claude Sonnet 5 / Claude Code / H1 (`af7bdc27f8daaae682a602946b04b122b50dee38`)のFormal Self-Reviewで追加された`TimeProvider` seamは、60秒budgetを高速・決定論的に確認する方法として有効だった。

しかしFinal Synthesisでは**初期採用しない**。

理由:

- C5には実PostgreSQL lockを使い、production entry point / production timeoutを実時間で通すより強い証拠が既にある。
- C5は定数として60秒であることも別testで確認している。
- 現時点でproduction codeへtestability seamを追加するより、単純なC5構造を維持する方がIssue #42に対して必要十分である。

実60秒testがCIでflakyまたは運用上許容できないことが一次証拠で判明した場合のみ、後続fixで再検討する。

## 5. C8 Major adjudication

### C8-M01 — CONFIRMED / REJECTED PATTERN / REGRESSION REQUIRED

C8 — DeepSeek V4 Flash / Open Code / H1 (`8af19e033b79d42ab8a03b32521ec809fd0a8588`)は、`ConnectionStrings__Database`未設定時にdesign-time factoryが次の架空接続先を生成する。

```text
Host=127.0.0.1;Port=5432;Database=design_time;Pooling=false;Timeout=5
```

これはproviderをNpgsqlに保つ点だけを満たす一方、connection-required design-time operationが意図しないlocalhost databaseへ接続を試み得る。Issue #42のconnection configuration / design-time contractに反するためMajorを維持する。

### Final Synthesisへのmandatory guard

Final Synthesisには、C8-M01の再発を防ぐ自動testを必須追加する。

最低条件:

1. `ConnectionStrings__Database`をchild process側で明示的に除去する。
2. production `IDesignTimeDbContextFactory<BankDbContext>`を実際に使用するconnection-required EF operationを実行する。
3. operationがnon-zero / fail-closedになることを確認する。
4. SQLite / InMemory / fabricated localhost / ambient default connectionへfallbackしないことを証明する。
5. test process全体のenvironment variableを直接書き換えてparallel raceを作らない。環境変更はchild processへ限定する。

推奨probeはrepository-local `dotnet-ef database update`等のconnection-required commandをsubprocess実行する方法とする。実装上より強く単純な同等証拠があれば置換可能。

model-only operationでconnection stringなしのNpgsql contextを生成すること自体は許容する。禁止対象は**架空の接続先をfabricateしてconnection-required operationのdestinationにしてしまうこと**である。

## 6. Other candidate dispositions

| Candidate | Disposition | Reason |
|---|---|---|
| C1 GPT-5.6 Sol / Codex | PARTIAL ADOPT | secret non-disclosure regressionのみ追加。coreはC5と重複 |
| C2 GPT-5.6 Terra / Codex | NOT SELECTED | C5よりrerun / production timeout証拠が弱く、独自に採る必要なし |
| C3 GPT-5.6 Luna / Codex | NOT SELECTED | timeout / rerun証拠がC5より弱く、独自に採る必要なし |
| C4 GPT-5.6 Luna / Open Code | NOT SELECTED | failure regression強化は有益だがC5がより強いprocess / real-PG evidenceを包含 |
| C5 Claude Opus 5 / Claude Code | PRIMARY | H1 99、finding 0、最も均衡したproduction-path evidence |
| C6 Claude Sonnet 5 / Claude Code | LEARNING ONLY | TimeProvider seamは有益だがC5の実60秒testを優先し初期不採用 |
| C7 Grok 4.5 / Cursor | NOT SELECTED | C5より60秒budget evidenceが弱く、独自に採る必要なし |
| C8 DeepSeek V4 Flash / Open Code | REJECTED | C8-M01 Major。fabricated design-time destinationは禁止 |

## 7. Final Synthesis construction policy

Final Synthesisは次で構築する。

- Branch: `agent/issue-42-fnd-04-final-code`
- Base: current `main`
- Expected base at adjudication: `38c07e210fe4e8689f1d8aeabbb07b92610d1826`
- candidate branch merge: PROHIBITED
- candidate commit cherry-pick: PROHIBITED
- candidate ranking artifact modification: PROHIBITED
- implementation style: C5を主軸に、上記で明示的に採用した要素のみcurateする

現在の`main`がExpected baseから動いていた場合は実装を開始せず、差分を報告して再base判断を求める。

## 8. Required Final Synthesis verification

Final Synthesis authorは少なくとも次を実行・記録する。

- `dotnet tool restore`
- clean restore
- build warnings 0 / errors 0
- non-PostgreSQL tests
- real PostgreSQL category tests
- explicit Migrator clean apply
- explicit Migrator rerun
- missing / unreachable / rejected credential failure
- actual 60-second blocked PostgreSQL timeout
- failed Migrator outputのsecret non-disclosure
- API startup no-auto-migration before / after
- API `BankDbContext` resolve no-schema-mutation
- actual EF pending-model positive check
- evaluator-only temporary model drift negative probe
- temporary change discard後のpending-model clean復帰
- idempotent migration SQL generation
- design-time missing-connection connection-required fail-closed regression for C8-M01
- `git diff --check`
- candidate / benchmark artifact非変更確認
- exact Head CI success

## 9. Duration collection for Final Synthesis

FND-04 candidate durationは一貫収集できなかったため正式benchmarkではN/Aを維持する。

Final Synthesisでは将来改善のため、実装Agentが開始時・終了時を**分単位**で明示的に記録する。

```text
STARTED_AT_LOCAL: YYYY-MM-DD HH:MM
FINISHED_AT_LOCAL: YYYY-MM-DD HH:MM
DURATION_MINUTES: integer
```

GitHub commit / PR / CI timestampからAgent処理時間を推測しない。この値はFinal Synthesis execution metadataであり、既存candidate rankingのSpeed Scoreへ遡及使用しない。

## 10. Next gate

Selection / Adjudication: **COMPLETE / LOCKED**

Final Synthesis: **READY / NOT STARTED**

次工程はローカルAgentによるFinal Synthesis実装である。Draft PR / exact Head CI取得後に、role-diverse independent reviewへ進む。Final Synthesis実装完了だけでmerge-readyとは判定しない。
