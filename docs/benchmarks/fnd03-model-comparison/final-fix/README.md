# FND-03 Final Code Major Fix — Candidate Registry

- Target Issue: #41
- Target PR: #104
- Common Base SHA: `91e3fca181558cd1523390347f4f2f80d6014d26`
- Candidate count: 14
- Status: **COMPLETE / ADJUDICATED / 14 OF 14 CI SUCCESS**

## Purpose

Final Synthesis reviewで確定したTestcontainers disposal Majorについて、複数のModel + Agent/Harnessへ同一条件で独立修正させ、その結果をGitHub上のbranch / Head / Draft PR / CIを一次証拠として比較する。

## Collection result

全14 candidateについて、candidate branch、immutable Head SHA、Draft PR、実行時間、exact Headに紐づくGitHub Actions `pull_request` runを収集した。

| Slug | PR | Head | CI Run | CI | Duration min |
| --- | ---: | --- | ---: | --- | ---: |
| `gpt-5.6-sol-codex` | #108 | `d3af857f71a62124842f96de9bced2b748b776be` | 31290367847 | SUCCESS | 28.68 |
| `gpt-5.6-terra-codex` | #113 | `0c55d66c9ba6e748073cd88314fe40f78d291815` | 31291508903 | SUCCESS | 21 |
| `gpt-5.6-luna-codex` | #116 | `708213d132e7465eec6c777b5b5f6b4c7ab30d6e` | 31292206197 | SUCCESS | 17.65 |
| `claude-opus-5-claude-code` | #109 | `4859b736e69cdecdc3a5797ae7c69f849b13f2a7` | 31290330550 | SUCCESS | 28 |
| `claude-sonnet-5-claude-code` | #118 | `51b9f1e54957576180244fa71cf28e468f2a33d3` | 31292745071 | SUCCESS | 55 |
| `grok-4.5-cursor` | #107 | `4a600940ab3d776d60086c74cb040155439b6d37` | 31289676226 | SUCCESS | 9.1 |
| `composer-2.5-cursor` | #110 | `2f8d6afe47b5e48dc0b4a316571c0cdf1c920521` | 31291017508 | SUCCESS | 6 |
| `deepseek-v4-pro-opencode` | #111 | `700569f30dda9d53a35d802ac048f45dc72255f3` | 31291241829 | SUCCESS | 53 |
| `deepseek-v4-flash-opencode` | #114 | `4ab6aaeeeb10188eca16b84e5cdba105f6a28a8f` | 31291986595 | SUCCESS | 75 |
| `qwen3.7-plus-opencode` | #112 | `9ab18236b9169b21b36689b0787a761267bfbdd8` | 31291287279 | SUCCESS | 54 |
| `gpt-5.6-luna-opencode` | #115 | `bbc2ede9921cafb74b71b84667aa80bd472b37ae` | 31291994899 | SUCCESS | 17 |
| `mimo-v2.5-pro-opencode` | #117 | `6f4f117ff076a2b828e35e1d832f923596ebc6bb` | 31292576719 | SUCCESS | 12 |
| `mimo-v2.5-opencode` | #120 | `8a37daa3d85016348910904dff7ac29c2811200e` | 31294256088 | SUCCESS | 110 |
| `minimax-m3-opencode` | #119 | `352b6489d8d4723551eb2634fd9dd612433d2fa6` | 31293843630 | SUCCESS | 65 |

`run.json`を機械可読な正本とする。Durationは各candidate Draft PR本文の自己申告値、CIはGitHub Actions上でexact Head SHAに紐づくpull-request-triggered runを独立確認した値である。

## Canonical adjudication

- [`final-evaluation.md`](./final-evaluation.md): `judges/synthesis.md`を正本sourceとする最終adjudicated ranking
- [`judges/manifest.json`](./judges/manifest.json): 3 Judge raw artifactのprovenance
- [`judges/synthesis.md`](./judges/synthesis.md): raw Judge結果を裁定したsource synthesis
- Judge count: 3
- Final rank 1: GPT-5.6 Sol / Codex / PR #108 / `94 / 100`
- Merge-ready: `1 / 14`

このstageのexact-head CIが全14件SUCCESSであることは記録するが、green CIだけではfailure-path correctnessを保証しない。

## Important identity note

準備時registryの`effort`と、candidate PRが自己申告した`reported_effort`が一致しないcandidateがあるため、`run.json`では両方を保持する。特にQwen3.7 Plusは準備値`MAX`に対してPR本文が`default`と報告している。評価時にこの差を黙って補正しない。

## Collection rule

各candidateの自己申告本文は補助情報とし、比較評価の一次証拠はGitHub上の以下とする。

- fixed Head SHA
- Common Base SHAからのdiff
- Draft PR metadata
- exact HeadのCI run / job log
- actual tests and implementation

候補branchが今後動いた場合でも、比較対象はこの文書と`run.json`に固定したHead SHAとする。

## Isolation

- candidate同士のbranch / PR / implementationを相互参照しない。
- 全candidateの結果固定前に比較評価を開始しない。**この条件は現在満たされた。**
- PR #104 / `agent/issue-41-fnd-03-final-code` はcandidate比較完了まで変更しない。
- benchmark raw reviewer artifactsは変更しない。

## Next

次工程は14 candidateの比較評価である。比較開始後は、各branchの現在tipではなく、本registryで固定したHead SHAを必ず対象とする。
