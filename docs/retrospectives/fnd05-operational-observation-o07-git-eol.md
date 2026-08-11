# FND-05 Operational Observation O-07 — Windows / WSL Git EOL contract

Status: `OBSERVED / FND-06+ IMPROVEMENT CANDIDATE`

この文書はFND-05実行中に確認した運用上の気づきを記録する補足メモである。

**Non-normative:** FND-05の現在のcontract、prompt、candidate、evaluation、Selection、Final Synthesis、review flow、Git設定を変更しない。FND-06以降の改善候補として扱う。

## Observation

FND-05のPre-Light handoff準備で、開始時はcleanだったworktreeに83件のtracked dirty fileが見えた。

調査の結果、83件は製品内容の変更ではなく、LF / CRLFの違いだけだった。

確認できた状態は次のとおり。

```text
system core.autocrlf = true
local worktree core.autocrlf = false
core.eol = unset
core.safecrlf = unset
.editorconfig end_of_line = lf
.gitattributes = not present
```

以前Git for Windows側の`core.autocrlf=true`でCRLFとして配置されたファイルが、現在のworktreeの`core.autocrlf=false`では差分として見えていた。

83件の代表diffは全行のLF→CRLF差だけで、`git diff --ignore-space-at-eol`でも実内容差は確認されなかった。

## Static Gate was not the cause

疑わしかった次のcommandをexact Headから作成した隔離worktreeで単独実行した。

```bash
git -c core.autocrlf=true diff --check
```

結果としてworktreeはdirtyにならなかった。

さらに同じ隔離worktreeでFND-05 Static Gate全体を実行してもPASSし、worktreeはdirtyにならなかった。

したがって今回の原因はStatic Gateの副作用ではなく、Windows / WSL間のGit設定と既存worktree実体の不一致と判断した。

## Key lesson

`.editorconfig`の`end_of_line = lf`はEditorやformatterへの方針であり、Git checkout時の改行変換ルールにはならない。

Windows / WSLをまたいで同じrepositoryを扱う場合、ユーザーやworktreeごとの`core.autocrlf`だけに依存すると、同じcommitでもworktree状態の見え方が変わる可能性がある。

## FND-05 treatment

FND-05ではbenchmark条件を途中で変えないため、次は行わない。

- `.gitattributes`追加
- repositoryの恒久EOL contract変更
- `core.autocrlf`方針変更
- Static Gate変更

83件のEOL-only差分は、一次証拠で対象を固定したうえで明示的にrestoreし、製品内容に変更がないことを確認した。

## FND-06+ improvement candidate

FND-06開始前またはFND-06の小規模改善として、次を検討する。

1. `.gitattributes`でGit側のtext / EOL contractを明示する必要があるか。
2. Windows GitとWSL Gitで同じrepository/worktreeを共有しない運用にできるか。
3. Gate実行前後で`git status --porcelain`、`git config --show-origin --get core.autocrlf`、`git ls-files --eol`を確認する軽量preflightが有効か。
4. CI / validatorはread-onlyであることを、隔離worktreeで必要に応じて確認できる形にするか。

目的はGit設定を増やすことではなく、**実内容の変更と環境由来のEOL差分を混同しないこと**である。

## Candidate future rule

> Windows / WSL混在環境では、EditorConfigだけをGitのEOL契約とみなさない。Git側のEOL contractを明示するか、実行環境を分離し、重要なGate前後では同じGit環境でworktree状態とEOL状態を確認する。

これはFND-05で確定した恒久ルールではなく、FND-06以降で採否を判断する改善候補とする。
