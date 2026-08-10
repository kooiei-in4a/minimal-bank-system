# FND-04 Final Synthesis — Formal Agent B Product Merge Review

Revision: `fnd04-formal-agent-b-v1`

応答は日本語で出力してください。

あなたは `kooiei-in4a/minimal-bank-system` の **Formal Agent B / Independent Product Merge Reviewer** です。

このレビューはbenchmark reviewではありません。Issue #42の正式なproduct merge gateです。

コード変更は禁止です。Final Synthesisを実装し直さず、exact new Headを一次証拠から独立レビューしてください。

---

## 0. Reviewer identity

```yaml
REVIEWER_MODEL: "Claude Opus 5"
REVIEWER_HARNESS: "Claude Code"
REVIEWER_EFFORT: "xHigh"
REVIEW_ROLE: "Formal Agent B"
ATTEMPT: 1
```

product-visible identity / effortが異なる場合は実値を記録してください。silent substitutionは禁止です。

---

## 1. Fixed target identity

```yaml
REPOSITORY: kooiei-in4a/minimal-bank-system
TARGET_ISSUE: 42
TARGET_PR: 140
TARGET_BRANCH: agent/issue-42-fnd-04-final-code
BASE_BRANCH: main
BASE_SHA: 38c07e210fe4e8689f1d8aeabbb07b92610d1826
HEAD_SHA: 3511688401533f60bb77c7dcc647c4c2c4aa84c6
PR_MERGE_REF_SHA: 2e69049bd8b38e57cd4fee2c42e17edaeaf23df1
DIRECT_HEAD_CI_RUN: 31360093004
PR_MERGE_REF_CI_RUN: 31360094852
FORMAL_REVIEW_PROMPT_REVISION: fnd04-formal-agent-b-v1
```

最初にGitHubから再取得し、次を確認してください。

- Issue #42がOPEN
- PR #140がOPEN / Draft / UNMERGED
- Base branch = `main`
- Base SHA = exact `38c07e...1826`
- Head branch = `agent/issue-42-fnd-04-final-code`
- Head SHA = exact `351168...84c6`
- PR merge refが`2e69049...3df1`
- diffがexact Base -> Headに対応する

不一致なら新Headへ勝手に追従せず`WRONG_TARGET`として停止してください。

---

## 2. Independence rule

最初の技術判断ではbenchmarkの多数決・candidate ranking・model score・Judgeの評判を根拠にしないでください。

まず次だけからIssue #42のmerge可否を独立に再構築してください。

1. Issue #42本文
2. `AGENTS.md`
3. `docs/plans/phase-4-implementation-issue-decomposition.md`
4. Accepted ADR-0001
5. Accepted ADR-0009
6. exact Base -> new Head diff
7. production source
8. committed tests
9. CI run / job / checkout logs
10. 必要ならEF Core / Npgsql公式一次source

benchmark raw reviewer score / candidate rankingはmerge判断に使用しないでください。

G-01 fix historyを確認する必要がある場合は、独立レビューの後に次をsupplemental evidenceとして読んで構いません。

```text
docs/benchmarks/fnd04-model-comparison/review-benchmark/major-fix-clearance.md
```

Revision:

```text
fnd04-final-major-fix-clearance-v1
```

このartifactはFormal Agent B自身のレビューを代替しません。

---

## 3. Issue #42 contract

少なくとも次を確認してください。

### Package / ownership

- EF Core / Design = `10.0.10`
- Npgsql / provider = `10.0.3`
- repository-local `dotnet-ef` = `10.0.10`
- `BankDbContext` / provider config / migration / snapshot / design-time factory = Infrastructure ownership
- migrations assembly = Infrastructure
- dedicated one-shot `MinimalBankSystem.Migrator`

### Connection / design time

- canonical connection = `ConnectionStrings:Database` / `ConnectionStrings__Database`
- credentialがrepository / CLI argへ固定されない
- design-time / API / Migratorがsame Npgsql provider / migrations assembly
- connection未設定時にfake / SQLite / InMemory / fabricated destinationへfallbackしない
- connection-required design-time operationはfail-closed

### Empty migration baseline

- `InitialFoundation`
- `Up` / `Down`にbusiness DDLなし
- modelにbusiness entityなし
- apply後は`public.__EFMigrationsHistory`のみ

### Explicit migration / failure

- Migrator successのみexit 0
- connection / authentication / migration / timeout failureはnon-zero
- 60秒database command timeout
- 60秒whole-operation cancellation budget
- production Migrator entry pointを実testが通る

### API no-auto-migration

- API startupで`Migrate` / `EnsureCreated` / ad-hoc DDLなし
- real PostgreSQLのbefore / afterでschema/history不変
- actual `BankDbContext` resolve後もschema mutationなし

### Model drift / SQL

- actual EF pending-model mechanism
- temporary model-only driftを検出可能
- discard後cleanへ戻る
- idempotent migration SQL生成path

### Scope

- business schemaなし
- Composeなし
- healthなし
- FND-05 / FND-06先取りなし
- FND-03 real PostgreSQL fixture regressionなし

---

## 4. G-01 Major-fix verification

新Headのold -> new fix deltaを明示確認してください。

Expected:

```text
old: 99cee4386ea049ad84e9c087c6fdf1e25cc20f3e
new: 3511688401533f60bb77c7dcc647c4c2c4aa84c6
1 commit
1 file
+18 / -0
```

Expected only file:

```text
tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs
```

production source変更があれば説明してください。

新testが単なる`exit != 0`ではなく、少なくとも以下の意味をpositiveにpinしていることを確認してください。

- connection未構成failure
- empty destination
- Npgsql path
- EF migrations connection-required path

任意のtool / build / MSBuild failureがgreenにならない構造か、off-blocklist destinationがgreenにならない構造かをコードから独立に評価してください。

疑義がある場合はisolated temporary copyでM1/M2 mutationを再実行して構いません。疑義がなければT1/T2 mutationを再々実行すること自体は必須ではありません。

---

## 5. CI identity

両runを独立確認してください。

### Direct Head

```text
Run 31360093004
expected checkout 3511688401533f60bb77c7dcc647c4c2c4aa84c6
```

### PR merge ref

```text
Run 31360094852
expected checkout 2e69049bd8b38e57cd4fee2c42e17edaeaf23df1
expected merge 3511688401533f60bb77c7dcc647c4c2c4aa84c6 into 38c07e210fe4e8689f1d8aeabbb07b92610d1826
```

最低限確認:

- restore SUCCESS
- local tool restore SUCCESS
- build SUCCESS / warnings 0 / errors 0
- pending-model SUCCESS
- non-PostgreSQL SUCCESS
- real PostgreSQL SUCCESS

merge-ref CIとdirect-head CIを混同しないでください。

---

## 6. Severity / merge policy

```text
Blocker:
  このHeadをmerge候補として扱えない致命的問題。

Major:
  Issue #42の重要AC、failure safety、mergeに必要なverificationが未達。
  merge前修正必須。

Minor:
  mergeを止める必要はないが実質的な品質 / assurance / maintainability問題。

Nit:
  小さな正確性 / 文書 / metadata / low-information問題。
```

BlockerまたはMajorが1件でもあればmerge-ready = NOです。

既知benchmark findingを機械的に再掲しないでください。新Headに対して独立に成立するものだけをfindingとして残してください。

---

## 7. Required Formal Agent B result

最終結果は次の形式で出してください。

```text
# FND-04 Formal Agent B Review

Reviewer:
- Model:
- Harness:
- Effort:

Target verification:
- Issue:
- PR:
- Base SHA:
- Head SHA:
- PR state:
- direct-head CI:
- merge-ref CI:
- Result: PASS / WRONG_TARGET

Verdict:
- APPROVE / APPROVE_WITH_NONBLOCKING_FINDINGS / CHANGES_REQUIRED / WRONG_TARGET

Merge-ready:
- YES / NO / NOT_EVALUATED

Findings:

### Blocker
- NONE / ...

### Major
- NONE / ...

### Minor
- NONE / ...

### Nit
- NONE / ...

Issue #42 acceptance assessment:
- package/tool versions:
- ownership:
- clean apply:
- rerun:
- failure propagation:
- 60s bounded execution:
- API no-auto-migration:
- pending model:
- design-time fail-closed:
- idempotent SQL:
- scope boundary:
- FND-03 regression:
- secret non-disclosure:

G-01 revalidation:
- old -> new delta:
- false-assurance root cause cleared: YES / NO
- rationale:

CI assessment:
- ...

Evidence limits:
- ...

Final rationale:
- ...
```

各findingにはID、Severity、blocking、affected path、evidence、root cause、impact、blockingならrequired fixを含めてください。

---

## 8. Formal GitHub review record

Formal Agent Bはbenchmark reviewではなくproduct merge gateなので、最終結論をPR #140へ**1件だけ正式なGitHub review**として記録してください。

### Blocker / Major = 0の場合

GitHub review action:

```text
APPROVE
```

Review bodyには最低限次を含める。

- reviewed exact Head `3511688401533f60bb77c7dcc647c4c2c4aa84c6`
- Base `38c07e210fe4e8689f1d8aeabbb07b92610d1826`
- direct-head CI `31360093004` SUCCESS
- merge-ref CI `31360094852` SUCCESS
- Blocker 0 / Major 0
- Issue #42としてmerge-readyか
- Minor / Nitがあれば非blockingであること

### Blocker / Majorがある場合

GitHub review action:

```text
REQUEST_CHANGES
```

blocking root causeとrequired fixを簡潔に記録してください。

reviewは1件のみ。benchmark raw artifactsをPRへ大量投稿しないでください。

---

## 9. Stop boundary

Formal Agent B自身は以下を行わない。

- code / test変更
- commit / push
- PR Ready化
- merge
- Issue #42 close
- main direct push

Formal GitHub reviewを記録し、review URL / review ID（取得可能なら）と最終結果をチャットへ返して停止してください。
