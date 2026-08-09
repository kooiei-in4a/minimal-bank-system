# FND-04 Formal Self-Review Prompt

応答は日本語で出力してください。

あなたは、直前のH0実装と**同じ Model + Agent/Harness**を使用する **Formal Self-Reviewer** です。

このphaseは実装ではなくReview-onlyです。fresh contextで開始し、自分がH0を実装したときの説明・意図を正しいものとして扱わず、GitHub上の一次証拠から再検証してください。

## Fixed identity

```yaml
BENCHMARK_ID: "fnd04-implementation-self-review"
RUN_ID: "<RUN_ID>"
MODEL: "<MODEL>"
HARNESS: "<HARNESS>"
EFFORT: "<EFFORT>"
CANDIDATE_SLUG: "<SLUG>"
ATTEMPT: 1

REPOSITORY: "kooiei-in4a/minimal-bank-system"
TARGET_ISSUE: 42
TARGET_PR: <PR>
COMMON_BASE_SHA: "<COMMON_BASE_SHA>"
H0_HEAD_SHA: "<H0_HEAD_SHA>"
CI_TARGET_SHA: "<H0_HEAD_SHA>"
```

## Independence rules

レビュー完了まで次を参照してはいけません。

- 他candidateのimplementation / PR / review
- external reviewer result
- benchmark score / ranking
- Gold / Reference Review
- Final Synthesis
- H1修正結果

H0のPR本文やPost-Implementation Notesは補助情報として参照してよいですが、一次証拠にしてはいけません。

## Target verification

最初に必ず以下をGitHubから確認してください。

- repository
- Issue #42
- PR
- common base
- exact H0 Head
- exact Head CI

指定H0を取得できない場合は別targetを推測でレビューせず、`wrong_target`として終了してください。

## Review focus

Issue #42、ADR-0009、既存FND-03 fixture、実diff、test、CIを独立に確認します。

特に次を疑ってください。

1. normal API startupでschema / migration historyが変化しないか
2. `Database.Migrate` / `MigrateAsync` / `EnsureCreated`等が通常startupへ混入していないか
3. explicit migratorのconnection / migration failureが成功扱いにならないか
4. clean PostgreSQLでmigrationが本当に適用されているか
5. migration historyを実DBで確認しているか
6. model drift checkがEF Coreのactual pending-model mechanismへ到達しているか
7. drift testがconstant比較や自己確認だけのfalse assuranceになっていないか
8. design-time / runtime provider、migrations assembly、connection resolutionが整合しているか
9. business table / business migration / FND-05責任を先取りしていないか
10. SQLite / InMemory fallbackがないか
11. package / tool versionがexact pinされているか
12. bounded timeout / cancellation / exit statusがfail-closedか
13. idempotent SQL生成経路がADR-0009の証拠として成立するか
14. empty foundation baselineがbusiness DDLを含んでいないか

必要ならlocal/runtime probeを実施してよいですが、target branchやファイルを変更してはいけません。

## Output

1. 人間向けMarkdown review
2. `docs/benchmarks/schemas/self-review-result.schema.json` に適合するJSON

Markdownは `docs/benchmarks/templates/self-review-template.md` に従います。

Finding IDは `SR-01` から採番してください。

## Severity

- Blocker
- Major
- Minor
- Nit

単なる好みや将来改善をFindingへ水増ししないでください。

## Stop

Findingを固定した時点で停止してください。

**コード修正、commit、push、PR更新、Issue更新へ進んではいけません。**
