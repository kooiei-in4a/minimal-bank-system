# FND-03 Final Code Major Fix — Candidate Registry

- Target Issue: #41
- Target PR: #104
- Common Base SHA: `91e3fca181558cd1523390347f4f2f80d6014d26`
- Candidate count: 14
- Status: **PREPARED / IMPLEMENTATION NOT COLLECTED**

## Purpose

Final Synthesis reviewで確定したTestcontainers disposal Majorについて、複数のModel + Agent/Harnessへ同一条件で独立修正させ、その結果をGitHub上のbranch / Head / Draft PR / CIを一次証拠として比較する。

## Candidate branches

| Slug | Branch |
| --- | --- |
| `gpt-5.6-sol-codex` | `agent/issue-41-fnd-03-fix-gpt-5.6-sol-codex` |
| `gpt-5.6-terra-codex` | `agent/issue-41-fnd-03-fix-gpt-5.6-terra-codex` |
| `gpt-5.6-luna-codex` | `agent/issue-41-fnd-03-fix-gpt-5.6-luna-codex` |
| `claude-opus-5-claude-code` | `agent/issue-41-fnd-03-fix-claude-opus-5-claude-code` |
| `claude-sonnet-5-claude-code` | `agent/issue-41-fnd-03-fix-claude-sonnet-5-claude-code` |
| `grok-4.5-cursor` | `agent/issue-41-fnd-03-fix-grok-4.5-cursor` |
| `composer-2.5-cursor` | `agent/issue-41-fnd-03-fix-composer-2.5-cursor` |
| `deepseek-v4-pro-opencode` | `agent/issue-41-fnd-03-fix-deepseek-v4-pro-opencode` |
| `deepseek-v4-flash-opencode` | `agent/issue-41-fnd-03-fix-deepseek-v4-flash-opencode` |
| `qwen3.7-plus-opencode` | `agent/issue-41-fnd-03-fix-qwen3.7-plus-opencode` |
| `gpt-5.6-luna-opencode` | `agent/issue-41-fnd-03-fix-gpt-5.6-luna-opencode` |
| `mimo-v2.5-pro-opencode` | `agent/issue-41-fnd-03-fix-mimo-v2.5-pro-opencode` |
| `mimo-v2.5-opencode` | `agent/issue-41-fnd-03-fix-mimo-v2.5-opencode` |
| `minimax-m3-opencode` | `agent/issue-41-fnd-03-fix-minimax-m3-opencode` |

## Collection rule

各candidateの自己申告本文は補助情報とし、比較評価の一次証拠はGitHub上の以下とする。

- branch / Head SHA
- Base-to-Head diff
- Draft PR
- CI run / job log
- actual tests and implementation

候補実行が完了したら、このbranch上の`run.json`をGitHubから機械的に更新する。ユーザーによる各結果の手作業コピペは不要とする。

## Isolation

- candidate同士のbranch / PR / implementationを相互参照しない。
- 全candidateの結果固定前に比較評価を開始しない。
- PR #104 / `agent/issue-41-fnd-03-final-code` はcandidate比較完了まで変更しない。
- benchmark raw reviewer artifactsは変更しない。

## Next

各Agentへ共通実装promptと自分の`TARGET_BRANCH`だけを渡して実行する。全candidate終了後、ChatGPTへ「結果を収集して」と依頼すれば、GitHubからHead / PR / CIを探索して`run.json`を更新する。