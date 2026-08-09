# Benchmark Archive Conventions

- Status: Active
- Scope: AI model implementation benchmark candidate archive operations
- Parent policy: `docs/benchmarks/model-implementation-benchmark-methodology.md` §16–17

この文書は、benchmark候補のarchive時に毎回再判断していた命名と配置を固定する運用規約です。製品仕様、ADR、Issue scope、採点基準は変更しません。矛盾がある場合はparent policyを優先します。

## 1. Issue slug

archive用の`<issue-slug>`は、対象Issueで既に使われている短い識別子を小文字化して使用します。

例:

- FND-01 → `fnd01`
- FND-02 → `fnd02`
- FND-03 → `fnd03`

一つのbenchmark run内で途中変更しません。

## 2. Candidate tag

各candidate Headは、working branch削除前に次のannotated tagへ固定します。

```text
benchmark/<issue-slug>/<model-slug>-<agent-slug>
```

例:

```text
benchmark/fnd02/gpt5.6-sol-codex
benchmark/fnd02/deepseek-v4-pro-opencode
benchmark/fnd02/claude-opus-5-claude-code
```

`model-slug`と`agent-slug`はbenchmark manifest内で一意かつ安定した表記を使用します。

## 3. Archive branch

Issue別archive作業branchは次を標準とします。

```text
agent/<issue-slug>-benchmark-archive
```

例:

```text
agent/fnd02-benchmark-archive
agent/fnd03-benchmark-archive
```

Final synthesis branchとは分離します。

## 4. Report / manifest path

Issue別benchmark reportは次を標準pathとします。

```text
docs/benchmarks/<issue-slug>-model-comparison/analysis.md
```

同等の既存reportが存在する場合は新規重複ファイルを作らず、そのreportへarchive manifestを追加します。

## 5. Archive PR title

archive PR titleは次を標準とします。

```text
docs(benchmark): archive <ISSUE-ID> model comparison results
```

例:

```text
docs(benchmark): archive FND-02 model comparison results
```

## 6. Required operation order

candidateごとに、次の順序を崩しません。

```text
candidate branch / full Head SHA確認
→ candidate PR / CI確認
→ annotated benchmark tag作成
→ remoteへtag push
→ remote tagが期待full Head SHAへ解決することを確認
→ benchmark report / manifestへ記録
→ candidate PRを未mergeでClose
→ candidate working branch削除
```

remote tagの解決確認より前にworking branchを削除してはいけません。

## 7. Final synthesis

Final synthesisはcandidateではありません。

- candidate tag一覧へ混ぜない
- 「N+1番目のモデル」として採点しない
- candidate archiveを理由にbranchやPRをCloseしない
- 通常のIssue実装、Agent B review、merge、Issue closeの流れで扱う

## 8. Manifest minimum fields

archive manifestには最低限、candidateごとに次を記録します。

- Model
- Agent / Harness
- Original branch
- Full Head SHA
- Benchmark tag
- PR
- CI
- Coding Score（正式記録がない場合は`未記録`）
- Final disposition
- Selected

Final synthesisがある場合は、candidate表とは分離してbranch / Head / PR / dispositionを記録します。

## 9. Reference examples

実績例:

- `docs/benchmarks/fnd01-model-comparison/analysis.md`
- `docs/benchmarks/fnd02-model-comparison/analysis.md`

新しいbenchmarkでは、parent policyと本規約を確認したうえで、直近のarchive実績を参考例として使用します。

## 10. H0 / H1 snapshot handling

Formal Self-Review modeを使用したcandidateでは、最終archive tagは原則H1へ付与する。

H0は削除せず、run / manifestへfull SHAとして記録する。H0がH1のancestorとしてrepository historyから到達可能であることを確認する。

H0を別tagへ固定する必要があるのは、history rewrite等で到達不能になる場合、またはH0自体を独立公開artifactとして扱う場合に限る。

## 11. Product completion is not blocked by research archive

Final production PRのmerge / Issue close後、candidate archiveが未完了でも、次のproduct Issueの開始条件を満たしていれば進行してよい。

archive作業は別のbenchmark operationとして実施する。ただし次を維持する。

```text
expected Head確認
-> annotated tag作成
-> remote dereference確認
-> manifest更新
-> candidate PR unmerged Close
-> candidate working branch削除
```

product進行を優先することは、snapshot保全を省略する理由にはしない。

## 12. Archive automation

候補数が多いbenchmarkでは、tag / dereference verification / PR close / branch cleanupを再利用可能なoperatorで自動化してよい。

operatorは次を満たすこと。

- mutation前に全candidate identityを検証する
- tag mismatch時はfail closed
- tag確認前にbranchを削除しない
- candidate PRをmergeしない
- final-code / artifact / archive branchをcandidate cleanupへ含めない
- 一時的に権限を拡張したworkflow / scriptは作業後に除去する
