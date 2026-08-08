# FND-02 Model Comparison — Benchmark Archive

Target Issue: [#40](https://github.com/kooiei-in4a/minimal-bank-system/issues/40) `[FND-02] 共通API実行契約を確立する`  
Parent / Control Issue: [#3](https://github.com/kooiei-in4a/minimal-bank-system/issues/3)  
Methodology: `docs/benchmarks/model-implementation-benchmark-methodology.md`  
Archive pattern: Closed benchmark PR + benchmark tag + deleted working branch + benchmark report / manifest（FND-01 / PR #63 と同方式）

この文書はIssue #40の14候補を比較実験結果として保存するためのアーカイブ台帳です。候補実装の削除ではなく、各candidate Headを再現可能なannotated tagとして固定し、PRとCIを証跡として残したうえでworking branchだけを整理します。

# FND-02 benchmark archive manifest

各候補のbranch Headと最終PR HeadをGitHub上で再確認し、40文字のfull SHAをsnapshot対象にしました。

候補別のCoding Scoreは、既存の正式な比較結果として記録された値を確認できなかったため、**本アーカイブ作業では再採点していません**。`未記録`は0点や失敗を意味しません。

Claude Sonnet 5 / Opus 5についてはbranch rename履歴（PR #73 / #75 / #77）があります。これらは追加candidateとして数えず、現在の最終PR（#80 / #79）と現在のbranch Headだけをmanifestへ記録しています。

| # | Model | Agent | Original branch | Full Head | Benchmark tag | PR | CI | Coding Score | Final disposition | Selected |
|---:|---|---|---|---|---|---:|---|---:|---|---|
| 1 | DeepSeek V4 Pro | Open Code | `agent/issue-40-fnd-02-dsv4pro` | `869e75d8b314311af732618a281f4453a14f8e25` | `benchmark/fnd02/deepseek-v4-pro-opencode` | [#67](https://github.com/kooiei-in4a/minimal-bank-system/pull/67) | [31214309925](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31214309925) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 2 | Qwen3.7 Plus | Open Code | `agent/issue-40-fnd-02-qwen3.7-plus` | `e2233f64e8f5902eec3060a4b4b04922bb8657be` | `benchmark/fnd02/qwen3.7-plus-opencode` | [#70](https://github.com/kooiei-in4a/minimal-bank-system/pull/70) | [31216844889](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31216844889) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 3 | GPT-5.6 Luna | Open Code | `agent/issue-40-fnd-02-gpt5.6-luna` | `86eeacac1147890b8e7f01555a3bb37e5c1433e1` | `benchmark/fnd02/gpt5.6-luna-opencode` | [#72](https://github.com/kooiei-in4a/minimal-bank-system/pull/72) | [31220034464](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31220034464) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 4 | DeepSeek V4 Flash | Open Code | `agent/issue-40-fnd-02-dsv4flash` | `9181417fb806b574d4ba664af5595ab0b77fcb9f` | `benchmark/fnd02/deepseek-v4-flash-opencode` | [#81](https://github.com/kooiei-in4a/minimal-bank-system/pull/81) | [31228219484](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31228219484) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 5 | MiMo-V2.5 | Open Code | `agent/issue-40-fnd-02-mimo-v2.5` | `413a955bcb71b34f1160a0979907f4fbe6297b31` | `benchmark/fnd02/mimo-v2.5-opencode` | [#74](https://github.com/kooiei-in4a/minimal-bank-system/pull/74) | [31221412552](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31221412552) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 6 | MiMo-V2.5-Pro | Open Code | `agent/issue-40-fnd-02-mimo-v2.5-pro` | `bc27e5e0ac6b95a122f55cd0c7eda3295e4515ae` | `benchmark/fnd02/mimo-v2.5-pro-opencode` | [#78](https://github.com/kooiei-in4a/minimal-bank-system/pull/78) | [31222600990](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31222600990) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 7 | MiniMax M3 | Open Code | `agent/issue-40-fnd-02-minimax-m3` | `4e6a7b32b6c9f4532f7ec61dfb8217cdff7a368d` | `benchmark/fnd02/minimax-m3-opencode` | [#76](https://github.com/kooiei-in4a/minimal-bank-system/pull/76) | [31222258404](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31222258404) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 8 | GPT-5.6 Luna | Codex | `agent/issue-40-fnd-02-gpt5.6-luna-codex` | `b7cb2a541ed557d163110edc0543dfc94a175d68` | `benchmark/fnd02/gpt5.6-luna-codex` | [#66](https://github.com/kooiei-in4a/minimal-bank-system/pull/66) | [31212793320](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31212793320) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 9 | GPT-5.6 Terra | Codex | `agent/issue-40-fnd-02-gpt5.6-terra-codex` | `c5e5f782750ca4cde9a1138f7cb1893357dc444a` | `benchmark/fnd02/gpt5.6-terra-codex` | [#68](https://github.com/kooiei-in4a/minimal-bank-system/pull/68) | [31215597642](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31215597642) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 10 | GPT-5.6 Sol | Codex | `agent/issue-40-fnd-02-gpt5.6-sol-codex` | `e9457cbc0d0de76054685877fb62e58ffed07bb3` | `benchmark/fnd02/gpt5.6-sol-codex` | [#71](https://github.com/kooiei-in4a/minimal-bank-system/pull/71) | [31219084120](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31219084120) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 11 | Grok 4.5 | Cursor | `agent/issue-40-fnd-02-grok-4.5` | `70f736c18f259c3bda1072620469fb7014c939fa` | `benchmark/fnd02/grok-4.5-cursor` | [#65](https://github.com/kooiei-in4a/minimal-bank-system/pull/65) | [31212480399](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31212480399) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 12 | Composer 2.5 | Cursor | `agent/issue-40-fnd-02-composer-2.5` | `aaf6ae84b2ae833b8a17cbb39609f5a0a31278f4` | `benchmark/fnd02/composer-2.5-cursor` | [#69](https://github.com/kooiei-in4a/minimal-bank-system/pull/69) | [31216486765](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31216486765) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 13 | Claude Sonnet 5 | Claude Code | `agent/issue-40-fnd-02-claude-sonnet-5` | `395e1e85ca6867acec10a111a7a9e1110e258e3b` | `benchmark/fnd02/claude-sonnet-5-claude-code` | [#80](https://github.com/kooiei-in4a/minimal-bank-system/pull/80) | [31222956728](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31222956728) SUCCESS | 未記録 | Closed benchmark candidate | No |
| 14 | Claude Opus 5 | Claude Code | `agent/issue-40-fnd-02-claude-opus-5` | `f40c6046355583c35e6f6346a47798d87165ba80` | `benchmark/fnd02/claude-opus-5-claude-code` | [#79](https://github.com/kooiei-in4a/minimal-bank-system/pull/79) | [31222766908](https://github.com/kooiei-in4a/minimal-bank-system/actions/runs/31222766908) SUCCESS | 未記録 | Closed benchmark candidate | No |

全14候補について、branch Headと最終PR Headは一致し、14個のannotated tagがremoteで期待commitへ解決することを確認しました。最終candidate PRはmergeせず、比較候補としてcloseしています。PRの差分・CI・会話履歴は各PRから引き続き参照できます。

# Final synthesis

14候補の比較後に、良い設計・検証方法を比較して作成したcurated / synthesized implementationです。単独モデルの15番目の候補として扱いません。

```yaml
Final synthesis:
  Branch: agent/issue-40-fnd-02-final-code
  Head: 8e7c8c48eefccf1a5ab85efd2a22af2f66eef033
  PR: 83
  Candidate: false
  Disposition: KEEP
```

- Branch: `agent/issue-40-fnd-02-final-code`
- Head: `8e7c8c48eefccf1a5ab85efd2a22af2f66eef033`
- Prior synthesis commit (archive開始時点の記録値): `2306c634abc40b4e5330c9492c8bcee8c0d6a5cc`
- Additional commit observed during archive (not authored by this archive operation): `8e7c8c48eefccf1a5ab85efd2a22af2f66eef033` (`fix(fnd-02): close approved error contract findings`)
- PR: [#83](https://github.com/kooiei-in4a/minimal-bank-system/pull/83)（OPEN / Draft / NOT MERGED）
- Disposition: curated / synthesized implementation
- Selected candidate: **No**（14候補とは別の統合成果物）
- Archive operationによる変更: **NO**

# Archive notes

- candidate closeは実装失敗を意味しない。benchmark candidate archiveとしての整理である。
- PR #73 / #75 / #77 はClaude candidateのbranch rename過程で発生した履歴PRであり、manifestへは含めない。
- PR #82（methodology learnings）および PR #83（final synthesis）は本archiveの削除・close対象外である。
- Issue #40、Parent Issue #3、Work Package #33 はこのarchive作業では変更しない。
- application code / specification / ADR は変更しない。benchmark report / manifestのみを記録する。
