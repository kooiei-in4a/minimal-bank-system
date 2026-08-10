# FND-04 Model Comparison — Final Analysis / Archive

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Status: **PRODUCT COMPLETE / BENCHMARK ARCHIVE PENDING TAG OPERATOR**

## 1. Final outcome

FND-04の製品実装は完了済みである。

```yaml
FINAL_SYNTHESIS_PR: 140
FINAL_SYNTHESIS_BRANCH: agent/issue-42-fnd-04-final-code
FINAL_REVIEWED_HEAD: 3511688401533f60bb77c7dcc647c4c2c4aa84c6
MERGE_COMMIT: 9a352a3a61945647273ccc7dfbc8e1816c3ca07c
ISSUE_42: CLOSED / COMPLETED
FORMAL_AGENT_B: APPROVE
BLOCKER: 0
MAJOR: 0
```

Final Synthesisはcandidateではないため、candidate archive tag一覧やランキングへ混ぜない。final-code branchは保持する。

## 2. Final candidate ranking

Durationは全candidateで一貫収集できなかったため正式比較ではN/Aとする。GitHub timestampから補完しない。

| Rank | Candidate | Model / Harness / Effort | H0 | H1 | H1 Score | H1 CI | Verdict |
| ---: | --- | --- | ---: | ---: | ---: | ---: | --- |
| 1 | `claude-opus-5-claude-code` | Claude Opus 5 / Claude Code / xHigh | 98 | 99 | **99** | `31318759813` | APPROVE |
| 2 | `gpt-5.6-sol-codex` | GPT-5.6 Sol / Codex / xHigh | 98 | 98 | **98** | `31311626063` | APPROVE |
| 3 | `claude-sonnet-5-claude-code` | Claude Sonnet 5 / Claude Code / xHigh | 95 | 98 | **98** | `31319675490` | APPROVE |
| 4 | `grok-4.5-cursor` | Grok 4.5 / Cursor / high | 93 | 93 | **93** | `31311256920` | APPROVE_WITH_MINOR |
| 5 | `gpt-5.6-terra-codex` | GPT-5.6 Terra / Codex / xHigh | 92 | 92 | **92** | `31312881852` | APPROVE_WITH_MINOR |
| 6 | `gpt-5.6-luna-opencode` | GPT-5.6 Luna / Open Code / Max | 89 | 91 | **91** | `31318715856` | APPROVE_WITH_MINOR |
| 7 | `gpt-5.6-luna-codex` | GPT-5.6 Luna / Codex / xHigh | 90 | 90 | **90** | `31313942061` | APPROVE_WITH_MINOR |
| 8 | `deepseek-v4-flash-opencode` | DeepSeek V4 Flash / Open Code / Max | 79 | 80 | **80** | `31319390472` | CHANGES_REQUIRED |

Key observations:

- H1 winner: Claude Opus 5 / Claude Code — 99
- H0 winner: GPT-5.6 Sol / Codex — 98
- Self-Review gain最大: Claude Sonnet 5 / Claude Code — +3
- exact-head CI: 8 / 8 SUCCESS
- merge-ready candidate: 7 / 8
- non-merge-ready: DeepSeek V4 Flash / Open Code — C8-M01 Major

`green CI != failure-path correctness` がFND-04でも再確認された。

## 3. Selection / Adjudication

Final Synthesisへcandidate branchをmerge / cherry-pickしていない。current mainからcurated implementationを構築した。

### Primary

`claude-opus-5-claude-code`

- H1 Head: `3a788cc31b3f65177d60dd3995842231dd505187`
- H1 Score: 99
- core architecture / real PostgreSQL failure evidenceの主軸として採用

### Partial adopt

`gpt-5.6-sol-codex`

- H1 Head: `7025c256b8b1ec1f0f4b9904f71a1047faac4cca`
- failed Migrator outputのsecret non-disclosure regressionを追加採用

### Learning only

`claude-sonnet-5-claude-code`

- Formal Self-Reviewで60秒timeout testのfalse assuranceを発見
- TimeProvider seam自体はFinal Synthesisへ初期採用しなかった

### Rejected pattern

`deepseek-v4-flash-opencode`

- C8-M01: missing design-time connection時にfabricated localhost destinationを生成
- Final Synthesisでは再発防止regressionを必須化して排除

## 4. Candidate archive manifest

Archive conventionに従い、最終H1 snapshotへannotated tagを付与してからworking branchを削除する。

| PR | Candidate | H0 Head | H1 / archive Head | Planned tag | Selected | Current disposition |
| ---: | --- | --- | --- | --- | --- | --- |
| #131 | `gpt-5.6-luna-opencode` | `27c8d2e16647f1e9710f93880841d7c20d603c5a` | `207f80eecd4b659e86267b6b143bf934e26ae5ea` | `benchmark/fnd04/gpt-5.6-luna-opencode` | No | TAG PENDING / PR OPEN / BRANCH PRESERVED |
| #132 | `grok-4.5-cursor` | `bf3de0da179975fc8d0ec7ad51d9a13153fe876e` | `bf3de0da179975fc8d0ec7ad51d9a13153fe876e` | `benchmark/fnd04/grok-4.5-cursor` | No | TAG PENDING / PR OPEN / BRANCH PRESERVED |
| #133 | `gpt-5.6-sol-codex` | `7025c256b8b1ec1f0f4b9904f71a1047faac4cca` | `7025c256b8b1ec1f0f4b9904f71a1047faac4cca` | `benchmark/fnd04/gpt-5.6-sol-codex` | Partial | TAG PENDING / PR OPEN / BRANCH PRESERVED |
| #134 | `claude-opus-5-claude-code` | `14160b99375113ee4dae07c5d7f8b2f29225e7ec` | `3a788cc31b3f65177d60dd3995842231dd505187` | `benchmark/fnd04/claude-opus-5-claude-code` | Primary | TAG PENDING / PR OPEN / BRANCH PRESERVED |
| #135 | `deepseek-v4-flash-opencode` | `a2c5bd4e7aa1e5a3f0aded4ec3e5d3aeddd2ea90` | `8af19e033b79d42ab8a03b32521ec809fd0a8588` | `benchmark/fnd04/deepseek-v4-flash-opencode` | No | TAG PENDING / PR OPEN / BRANCH PRESERVED |
| #136 | `gpt-5.6-terra-codex` | `427cba0527dd467b7c4eddefad885b1563a7880f` | `427cba0527dd467b7c4eddefad885b1563a7880f` | `benchmark/fnd04/gpt-5.6-terra-codex` | No | TAG PENDING / PR OPEN / BRANCH PRESERVED |
| #137 | `claude-sonnet-5-claude-code` | `200281152e02cb72b09b556dd5f7dc263ffbdc84` | `af7bdc27f8daaae682a602946b04b122b50dee38` | `benchmark/fnd04/claude-sonnet-5-claude-code` | Learning only | TAG PENDING / PR OPEN / BRANCH PRESERVED |
| #138 | `gpt-5.6-luna-codex` | `006319444f1e18172beb5664b045b5bccb2bcdf8` | `006319444f1e18172beb5664b045b5bccb2bcdf8` | `benchmark/fnd04/gpt-5.6-luna-codex` | No | TAG PENDING / PR OPEN / BRANCH PRESERVED |

All eight planned tag names were checked before archive work and no existing ref was found.

## 5. Required cleanup order

Do not reorder:

```text
exact branch / Head / PR identity verified
→ annotated tag created at exact H1 Head
→ remote tag pushed
→ remote tag dereferences to expected full SHA
→ this manifest updated to VERIFIED
→ candidate PR closed unmerged
→ candidate working branch deleted
→ final verification: tag present / PR closed / branch absent
```

A candidate branch must not be deleted before remote tag verification.

## 6. Already cleaned non-candidate PRs

The following old FND-04-related auxiliary PRs were cleaned separately because they are not candidate snapshots:

- PR #139 duration-policy proposal — `SUPERSEDED / CLOSED / NOT MERGED`
- PR #141 retrospective A — `CONSUMED BY #144 / CLOSED / NOT MERGED`
- PR #142 retrospective B — `CONSUMED BY #144 / CLOSED / NOT MERGED`
- PR #143 retrospective C — `CONSUMED BY #144 / CLOSED / NOT MERGED`

The source evidence remains available in the closed PR history.

## 7. State intentionally preserved

Do not clean as candidate artifacts:

- PR #140 final product PR — merged
- `agent/issue-42-fnd-04-final-code` — final-code branch preserved
- `agent/fnd04-benchmark-control` — benchmark evidence/control preserved
- PR #144 — final retrospective / FND-05 policy, still active
- PR #145+ — active FND-05 preparation/review work

## 8. Archive completion condition

```yaml
EXPECTED_CANDIDATE_TAGS: 8
VERIFIED_CANDIDATE_TAGS: 0
EXPECTED_CANDIDATE_PRS_CLOSED_UNMERGED: 8
CANDIDATE_PRS_CLOSED_UNMERGED: 0
EXPECTED_CANDIDATE_BRANCHES_DELETED: 8
CANDIDATE_BRANCHES_DELETED: 0
ARCHIVE_STATUS: pending_external_tag_and_ref_cleanup_operator
```

The connected GitHub app used during this cleanup does not expose annotated-tag creation or branch-ref deletion. Therefore the archive remains fail-closed until those two operations are performed by a Git-capable operator and remotely verified.
