# FND-04 Final Synthesis — Confirmed Major Fix Prompt

Revision: `fnd04-final-major-fix-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **FND-04 Final Synthesis Major Fix Implementer / Agent A** です。

この作業はFinal Synthesis全体の再実装ではありません。Role-diverse review 5/5とJudge A/B adjudicationは完了し、Gold / Referenceで**G-01 / NR-01 Major**がmerge blockerとして確定しました。

production implementation自体は現在fail-closedで正しいとJudge A/Bが独立確認しています。今回直すのは、その安全性を守る`DesignTimeConnectionSafetyTests`のfalse assuranceです。

---

## 1. Fixed target

```yaml
REPOSITORY: kooiei-in4a/minimal-bank-system
TARGET_ISSUE: 42
TARGET_PR: 140
TARGET_BRANCH: agent/issue-42-fnd-04-final-code
EXPECTED_CURRENT_HEAD: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826

GOLD_REVISION: fnd04-final-gold-v1
BLOCKING_FINDING: G-01
NORMALIZED_FINDING: NR-01
FIX_PROMPT_REVISION: fnd04-final-major-fix-v1
```

最初に`git fetch origin`し、次を確認してください。

- PR #140がOPEN / Draft / unmerged
- PR Base SHAが`38c07e210fe4e8689f1d8aeabbb07b92610d1826`
- PR Head branchが`agent/issue-42-fnd-04-final-code`
- origin Headが`99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- working treeがclean

いずれかが不一致なら、新Headへ勝手に追従せず現状を報告して停止してください。

---

## 2. Authority

Issue #42、Accepted ADR、current Final Synthesis sourceに加え、benchmark control branchの次を確認してください。

```text
docs/benchmarks/fnd04-model-comparison/review-benchmark/gold-review.md
```

Gold revision:

```text
fnd04-final-gold-v1
```

reviewer多数決ではなく、このGoldで確定したG-01だけをblocking fix対象としてください。

---

## 3. Confirmed Major — G-01 / NR-01

対象:

```text
tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs
```

現行testは主に次を確認しています。

```text
process.ExitCode != 0
+
fixed destination blocklistがoutputに出ない
```

これだけでは、

- production design-time factory / Npgsql connection-required pathへ到達する前のtool/build failure
- blocklist外のfabricated destination

でもgreenになり得ます。

Judge A/Bはfresh Phase Aで独立mutationし、少なくとも次を再現しました。

1. `Host=db;Database=ambient_fallback`等のoff-blocklist fabricated destinationを入れても現行testがPASSする。
2. `--no-build` commandがfactoryへ到達できないbuild-output failureでも現行testがPASSする。
3. unmodified production behavior自体はdestination未構成のNpgsql connection-required pathでfail-closedする。

したがって**production codeを変えるのではなく、regression testが意図したfailure originをpositiveに証明するよう強化**してください。

---

## 4. Required fix characteristics

### Must

1. `ConnectionStrings__Database`はchild process側だけでremoveする。
2. repository-local `dotnet-ef database update`等、production `IDesignTimeDbContextFactory<BankDbContext>`を実際に通るconnection-required operationを使用する。
3. non-zeroだけでなく、**connection未構成のNpgsql / EF connection-required pathへ到達して失敗したpositive evidence**をassertする。
4. arbitrary MSBuild / tool / missing build-output failureではtestがPASSしないようにする。
5. off-blocklist fabricated destinationではtestがFAILするようにする。
6. fixed blocklistはsupplementary assertionとして残してよいが、主要な安全性証拠にしない。
7. test process自身のglobal environment variableは変更しない。
8. production codeは変更しない。

### Positive evidenceの例

versionがexact pinされている現在のEF/Npgsql挙動に対して、次のようなsignalを組み合わせてよい。

- `The ConnectionString property has not been initialized.` 相当のunconfigured-connection failure
- Npgsql / EF connection-required operationへ到達したことを示すmarker
- destinationが空 / 未構成であることを示すmarker

全文stack traceや脆いline numberへ過度にcoupleしないでください。

「どんなnon-zeroでも成功」から「意図したfailure originでなければ失敗」へtest semanticsを反転させることが目的です。

より単純で堅牢な同等以上の方法がある場合は採用して構いません。ただしproduction codeへtestability abstractionを追加してはいけません。

---

## 5. Mutation sensitivity verification — 必須

fix後、commit前に一時的mutationを使ってtest感度を確認してください。

mutationは**絶対にcommitしない**でください。可能なら一時worktree / isolated copyを使用してください。

### M1 — off-blocklist fabricated destination

一時的にmodel-only pathを例えば次のようなdestinationへ退行させます。

```text
Host=db;Port=5432;Database=ambient_fallback;Username=postgres;Password=postgres
```

期待結果:

```text
DesignTimeConnectionSafetyTests = FAIL
```

`db` / `ambient_fallback`自体を新しいblocklistへ追加してtestを通してはいけません。**positive failure-origin assertionが壊れるためFAILする**ことを確認してください。

### M2 — factory未到達のunrelated failure

`--no-build`を維持する場合、isolated copyで必要build outputを一時退避するなど、factoryへ到達できない代表的tool/build failureを作ります。

期待結果:

```text
DesignTimeConnectionSafetyTests = FAIL
```

### Recovery

M1 / M2を完全discardし、original production source / build stateへ戻してください。

その後:

```text
DesignTimeConnectionSafetyTests = PASS
working tree = clean except intended fix
mutation residue = NONE
```

を確認してください。

---

## 6. Scope boundary

今回のsource fixは原則として次だけです。

```text
tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs
```

共通test helperへの極小変更が不可避な場合だけ追加可としますが、理由を報告してください。

変更しないもの:

- `BankDbContextFactory` production behavior
- `BankPersistence` production behavior
- Migrator production behavior
- timeout architecture
- migration / snapshot
- business schema
- Compose
- health
- candidate / benchmark artifacts

Goldの非blocking findings（G-02 / G-04 / G-05）をsource変更へ混ぜないでください。

PR本文のCI identity wording（G-03）は、新HeadのCI結果が揃った後にmetadataとして正確に更新して構いません。

---

## 7. Verification

最低限次を実行してください。

```bash
dotnet tool restore
dotnet restore MinimalBankSystem.slnx
dotnet build MinimalBankSystem.slnx --no-restore
```

Targeted:

```text
DesignTimeConnectionSafetyTests PASS
M1 mutation -> targeted test FAIL
M2 unrelated failure -> targeted test FAIL
mutation recovery -> targeted test PASS
```

Regression:

- non-PostgreSQL suite PASS
- real PostgreSQL category PASS
- `dotnet-ef migrations has-pending-model-changes` PASS
- `git diff --check` PASS
- no mutation residue
- no candidate / benchmark artifact change

実PostgreSQL timeout testはproductionを変更していなくても回帰確認として維持してください。

---

## 8. Commit / Push / CI

intended fixだけをcommitし、同じFinal Synthesis branchへpushしてください。

```text
agent/issue-42-fnd-04-final-code
```

PR #140はDraftのまま維持してください。

push後、新Head SHAを記録し、次の両方を取得してください。

1. direct-head push CI
2. PR merge-ref CI

両方についてactual checkout identityを区別し、build / pending-model / non-PG / real PGのSUCCESSを確認してください。

PR本文の古い`Exact Head CI`表記は、新HeadのCI欄で次のように分けて更新してください。

```text
Direct-head CI:
- run
- actual checkout SHA
- conclusion

PR merge-ref CI:
- run
- actual checkout SHA
- merged Base/Head identity
- conclusion
```

---

## 9. Duration

今回のMajor fix executionも分単位で明示記録してください。

```text
STARTED_AT_LOCAL: YYYY-MM-DD HH:MM
FINISHED_AT_LOCAL: YYYY-MM-DD HH:MM
DURATION_MINUTES: integer
```

GitHub timestampから後で推測しません。

---

## 10. Stop boundary

- PR Ready化禁止
- merge禁止
- Issue #42 close禁止
- main direct push禁止
- Formal Agent B review実施禁止
- benchmark control branch変更禁止

新Head + local verification + direct-head CI + PR merge-ref CI取得までで停止してください。

---

## 11. Final report

```text
## Result

Branch:
Old Head: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
New Head:
PR: #140
Direct-head CI:
PR merge-ref CI:

## Duration
STARTED_AT_LOCAL:
FINISHED_AT_LOCAL:
DURATION_MINUTES:

## G-01 fix
- changed files:
- positive failure-origin evidence:
- why unrelated non-zero failure no longer passes:

## Mutation sensitivity
- M1 off-blocklist destination: PASS if targeted test FAILED
- M2 factory-unreachable/tooling failure: PASS if targeted test FAILED
- recovery baseline: PASS if targeted test PASSED
- mutation residue: NONE

## Verification
- tool restore:
- restore:
- build:
- targeted baseline:
- non-PostgreSQL:
- real PostgreSQL:
- pending-model:
- git diff --check:

## Scope
- production code changed: NO
- benchmark/candidate changed: NO
- nonblocking Gold findings mixed into source fix: NO

## Known concerns
- ...

## Independent re-review handoff
- new Head
- changed files
- G-01 verification evidence
```

この報告後、独立Major-fix re-reviewへ引き渡して停止してください。