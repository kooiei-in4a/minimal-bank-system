# FND-04 Candidate Cleanup Preflight

Status: **PREFLIGHT COMPLETE / DESTRUCTIVE CLEANUP NOT STARTED**

Target Issue: #42 `[FND-04] EF Core・明示的migration実行基盤を確立する`

Cleanup policy follows the established FND-01 / FND-02 order:

```text
1. verify candidate PR / branch / exact full Head
2. create annotated benchmark tag at exact candidate Head
3. verify remote tag resolves to the expected candidate Head
4. record archive manifest / report
5. close candidate PR
6. delete candidate working branch
7. verify PR closed + branch absent + tag preserved
```

If any identity does not match, stop that candidate and do not close/delete it.

## Preserved non-candidate state

The following are explicitly outside candidate cleanup:

- Final Synthesis PR #140 — MERGED
- Final Synthesis branch `agent/issue-42-fnd-04-final-code` — preserve
- Final reviewed Head `3511688401533f60bb77c7dcc647c4c2c4aa84c6` — preserve through merged history
- Final merge commit `9a352a3a61945647273ccc7dfbc8e1816c3ca07c`
- Benchmark control branch `agent/fnd04-benchmark-control` — preserve
- PR #139 duration-policy work — non-candidate; not part of candidate cleanup
- PR #141–#144 retrospective / FND-05 process work — not part of candidate cleanup
- PR #145+ FND-05 preparation/review work — not part of FND-04 candidate cleanup

Issue #42 is already completed/closed. Cleanup does not reopen or modify Issue #42.

## Candidate archive map

All eight candidate PRs are OPEN / DRAFT / UNMERGED at cleanup preflight time.
All use common base:

`38c07e210fe4e8689f1d8aeabbb07b92610d1826`

| PR | Candidate slug | Working branch | Exact candidate Head | Planned annotated tag | Preflight identity |
| ---: | --- | --- | --- | --- | --- |
| #131 | `gpt-5.6-luna-opencode` | `agent/issue-42-fnd-04-gpt-5.6-luna-opencode` | `207f80eecd4b659e86267b6b143bf934e26ae5ea` | `benchmark/fnd04/gpt-5.6-luna-opencode` | VERIFIED |
| #132 | `grok-4.5-cursor` | `agent/issue-42-fnd-04-grok-4.5-cursor` | `bf3de0da179975fc8d0ec7ad51d9a13153fe876e` | `benchmark/fnd04/grok-4.5-cursor` | VERIFIED |
| #133 | `gpt-5.6-sol-codex` | `agent/issue-42-fnd-04-gpt-5.6-sol-codex` | `7025c256b8b1ec1f0f4b9904f71a1047faac4cca` | `benchmark/fnd04/gpt-5.6-sol-codex` | VERIFIED |
| #134 | `claude-opus-5-claude-code` | `agent/issue-42-fnd-04-claude-opus-5-claude-code` | `3a788cc31b3f65177d60dd3995842231dd505187` | `benchmark/fnd04/claude-opus-5-claude-code` | VERIFIED |
| #135 | `deepseek-v4-flash-opencode` | `agent/issue-42-fnd-04-deepseek-v4-flash-opencode` | `8af19e033b79d42ab8a03b32521ec809fd0a8588` | `benchmark/fnd04/deepseek-v4-flash-opencode` | VERIFIED |
| #136 | `gpt-5.6-terra-codex` | `agent/issue-42-fnd-04-gpt-5.6-terra-codex` | `427cba0527dd467b7c4eddefad885b1563a7880f` | `benchmark/fnd04/gpt-5.6-terra-codex` | VERIFIED |
| #137 | `claude-sonnet-5-claude-code` | `agent/issue-42-fnd-04-claude-sonnet-5-claude-code` | `af7bdc27f8daaae682a602946b04b122b50dee38` | `benchmark/fnd04/claude-sonnet-5-claude-code` | VERIFIED |
| #138 | `gpt-5.6-luna-codex` | `agent/issue-42-fnd-04-gpt-5.6-luna-codex` | `006319444f1e18172beb5664b045b5bccb2bcdf8` | `benchmark/fnd04/gpt-5.6-luna-codex` | VERIFIED |

## Expected annotated tag messages

Use one annotated tag per candidate. Suggested message format:

```text
FND-04 benchmark candidate <candidate-slug>
Issue #42
PR #<number>
Archived candidate Head <full-sha>
```

## Cleanup state

```yaml
CANDIDATE_COUNT: 8
IDENTITY_VERIFIED: 8_of_8
ANNOTATED_TAGS_CREATED: 0_of_8
REMOTE_TAGS_VERIFIED: 0_of_8
CANDIDATE_PRS_CLOSED: 0_of_8
CANDIDATE_BRANCHES_DELETED: 0_of_8
FINAL_SYNTHESIS_PRESERVED: YES
BENCHMARK_CONTROL_PRESERVED: YES
ISSUE_42_CHANGED: NO
```

## Current blocker

The connected GitHub tool surface available in this session can read PR/branch identities and update PR/file state, but does not expose Git tag creation or branch-ref deletion operations.

Per the established FND cleanup safety order, candidate PR close and branch deletion must not be performed before annotated tags are created and remotely verified.

Therefore this preflight intentionally stops before any candidate PR close/delete operation.

Once annotated tags are created and verified at the exact SHAs above, continue from step 4 without changing the mapping.
