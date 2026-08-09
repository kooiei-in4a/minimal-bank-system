#!/usr/bin/env bash
set -euo pipefail

git config user.name "FND-03 Benchmark Archive"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
git fetch origin '+refs/heads/*:refs/remotes/origin/*' --prune --tags

# tag|branch|head|pr|rank|score
candidates=(
  'benchmark/fnd03/deepseek-v4-pro-opencode|agent/issue-41-fnd-03-dsv4pro|a4eb670bd2fda4783cf7b475952c58aa0696b8d0|90|6|83'
  'benchmark/fnd03/qwen3.7-plus-opencode|agent/issue-41-fnd-03-qwen3.7-plus|cc62c1a3a094aff0a8a7a046d96457f0c92a6163|95|5|87'
  'benchmark/fnd03/gpt-5.6-luna-opencode|agent/issue-41-fnd-03-gpt5.6-luna|cab3b4d7250f4e73e7bc74072315ce5f2f2ef931|100|3|93'
  'benchmark/fnd03/deepseek-v4-flash-opencode|agent/issue-41-fnd-03-dsv4flash|881544509e467f0ced9adb02dffded79e58774bd|102|10|80'
  'benchmark/fnd03/mimo-v2.5-opencode|agent/issue-41-fnd-03-mimo-v2.5|95de194302ac6b04b8e0325054a30f362976816a|99|13|55'
  'benchmark/fnd03/mimo-v2.5-pro-opencode|agent/issue-41-fnd-03-mimo-v2.5-pro|18faff42429da3041d70850b42c849e855093e83|101|9|82'
  'benchmark/fnd03/gpt-5.6-luna-codex|agent/issue-41-fnd-03-gpt5.6-luna-codex|65aa774fe22c63e7d917aaa981825e51895bc448|96|6|83'
  'benchmark/fnd03/gpt-5.6-terra-codex|agent/issue-41-fnd-03-gpt5.6-terra-codex|6df0ab37833ee2a88c6c1ef4ba6ee4e6f858f5fa|98|2|94'
  'benchmark/fnd03/gpt-5.6-sol-codex|agent/issue-41-fnd-03-gpt5.6-sol-codex|bbf11099ef660363e333df3f29425d183542f71b|93|1|96'
  'benchmark/fnd03/grok-4.5-cursor|agent/issue-41-fnd-03-grok-4.5|34c2a5a35a3fb72858333a635e30f8006cffec46|88|12|68'
  'benchmark/fnd03/composer-2.5-cursor|agent/issue-41-fnd-03-composer-2.5|0322dd0d499f4ef449fdf5003fccced539800cce|89|10|80'
  'benchmark/fnd03/claude-sonnet-5-claude-code|agent/issue-41-fnd-03-claude-sonnet-5|917db64b91fae72ce7824ed39fc019f4e6398be3|94|4|92'
  'benchmark/fnd03/claude-opus-5-claude-code|agent/issue-41-fnd-03-claude-opus-5|aec5845ac7a61e0171ecdf837d950ccc0a3a9cdb|97|6|83'
  'benchmark/fnd03/minimax-m3-opencode|agent/issue-41-fnd-03-minimax-m3|95a8e50e6b68025e3386fdd0672bd73bcbaa60a0|0|NA|NA'
  'benchmark/fnd03/gpt-5.6-sol-codex-major-fix|agent/issue-41-fnd-03-fix-gpt-5.6-sol-codex|d3af857f71a62124842f96de9bced2b748b776be|108|1|94'
  'benchmark/fnd03/claude-opus-5-claude-code-major-fix|agent/issue-41-fnd-03-fix-claude-opus-5-claude-code|4859b736e69cdecdc3a5797ae7c69f849b13f2a7|109|2|80'
  'benchmark/fnd03/gpt-5.6-luna-codex-major-fix|agent/issue-41-fnd-03-fix-gpt-5.6-luna-codex|708213d132e7465eec6c777b5b5f6b4c7ab30d6e|116|3|77'
  'benchmark/fnd03/gpt-5.6-terra-codex-major-fix|agent/issue-41-fnd-03-fix-gpt-5.6-terra-codex|0c55d66c9ba6e748073cd88314fe40f78d291815|113|4|77'
  'benchmark/fnd03/gpt-5.6-luna-opencode-major-fix|agent/issue-41-fnd-03-fix-gpt-5.6-luna-opencode|bbc2ede9921cafb74b71b84667aa80bd472b37ae|115|5|76'
  'benchmark/fnd03/deepseek-v4-flash-opencode-major-fix|agent/issue-41-fnd-03-fix-deepseek-v4-flash-opencode|4ab6aaeeeb10188eca16b84e5cdba105f6a28a8f|114|6|74'
  'benchmark/fnd03/grok-4.5-cursor-major-fix|agent/issue-41-fnd-03-fix-grok-4.5-cursor|4a600940ab3d776d60086c74cb040155439b6d37|107|7|73'
  'benchmark/fnd03/claude-sonnet-5-claude-code-major-fix|agent/issue-41-fnd-03-fix-claude-sonnet-5-claude-code|51b9f1e54957576180244fa71cf28e468f2a33d3|118|8|67'
  'benchmark/fnd03/deepseek-v4-pro-opencode-major-fix|agent/issue-41-fnd-03-fix-deepseek-v4-pro-opencode|700569f30dda9d53a35d802ac048f45dc72255f3|111|9|62'
  'benchmark/fnd03/composer-2.5-cursor-major-fix|agent/issue-41-fnd-03-fix-composer-2.5-cursor|2f8d6afe47b5e48dc0b4a316571c0cdf1c920521|110|10|58'
  'benchmark/fnd03/minimax-m3-opencode-major-fix|agent/issue-41-fnd-03-fix-minimax-m3-opencode|352b6489d8d4723551eb2634fd9dd612433d2fa6|119|11|48'
  'benchmark/fnd03/qwen3.7-plus-opencode-major-fix|agent/issue-41-fnd-03-fix-qwen3.7-plus-opencode|9ab18236b9169b21b36689b0787a761267bfbdd8|112|12|42'
  'benchmark/fnd03/mimo-v2.5-pro-opencode-major-fix|agent/issue-41-fnd-03-fix-mimo-v2.5-pro-opencode|6f4f117ff076a2b828e35e1d832f923596ebc6bb|117|13|38'
  'benchmark/fnd03/mimo-v2.5-opencode-major-fix|agent/issue-41-fnd-03-fix-mimo-v2.5-opencode|8a37daa3d85016348910904dff7ac29c2811200e|120|14|34'
)

echo '1/7 verify candidate branch Heads'
for row in "${candidates[@]}"; do
  IFS='|' read -r tag branch head pr rank score <<< "$row"
  actual="$(git ls-remote origin "refs/heads/$branch" | awk '{print $1}')"
  [[ "$actual" == "$head" ]] || { echo "HEAD MISMATCH: $branch expected=$head actual=${actual:-MISSING}" >&2; exit 1; }
done

echo '2/7 verify canonical candidate PR identities'
for row in "${candidates[@]}"; do
  IFS='|' read -r tag branch head pr rank score <<< "$row"
  [[ "$pr" == '0' ]] && continue
  read -r state pr_head < <(gh pr view "$pr" --repo "$GITHUB_REPOSITORY" --json state,headRefOid --jq '[.state,.headRefOid] | @tsv')
  [[ "$pr_head" == "$head" ]] || { echo "PR HEAD MISMATCH: #$pr expected=$head actual=$pr_head" >&2; exit 1; }
  [[ "$state" == 'OPEN' || "$state" == 'CLOSED' ]] || { echo "Unexpected PR state: #$pr $state" >&2; exit 1; }
done

echo '3/7 create annotated tags atomically'
tag_refs=()
for row in "${candidates[@]}"; do
  IFS='|' read -r tag branch head pr rank score <<< "$row"
  remote_deref="$(git ls-remote origin "refs/tags/$tag^{}" | awk '{print $1}')"
  remote_obj="$(git ls-remote origin "refs/tags/$tag" | awk '{print $1}')"
  if [[ -n "$remote_obj" || -n "$remote_deref" ]]; then
    [[ "$remote_deref" == "$head" ]] || { echo "TAG MISMATCH OR LIGHTWEIGHT TAG: $tag" >&2; exit 1; }
    continue
  fi
  annotation=$(printf 'FND-03 benchmark candidate\nBranch: %s\nHead: %s\nPR: %s\nFinal rank: %s\nFinal score: %s\nFinal synthesis: PR #104' "$branch" "$head" "$pr" "$rank" "$score")
  git tag -a "$tag" "$head" -m "$annotation"
  tag_refs+=("refs/tags/$tag")
done
if (( ${#tag_refs[@]} > 0 )); then
  git push --atomic origin "${tag_refs[@]}"
fi

echo '4/7 verify remote annotated tags'
for row in "${candidates[@]}"; do
  IFS='|' read -r tag branch head pr rank score <<< "$row"
  remote_deref="$(git ls-remote origin "refs/tags/$tag^{}" | awk '{print $1}')"
  [[ "$remote_deref" == "$head" ]] || { echo "REMOTE TAG VERIFY FAILED: $tag expected=$head actual=${remote_deref:-MISSING}" >&2; exit 1; }
done

echo '5/7 comment and close candidate PRs unmerged'
for row in "${candidates[@]}"; do
  IFS='|' read -r tag branch head pr rank score <<< "$row"
  [[ "$pr" == '0' ]] && continue
  state="$(gh pr view "$pr" --repo "$GITHUB_REPOSITORY" --json state --jq .state)"
  if [[ "$state" == 'OPEN' ]]; then
    gh pr comment "$pr" --repo "$GITHUB_REPOSITORY" --body "## Benchmark Completion

This PR was an independent FND-03 benchmark candidate.

- Candidate Head: \`$head\`
- Final rank: \`$rank\`
- Final score: \`$score\`
- Immutable annotated snapshot: \`$tag\`
- Final implementation: PR #104
- Issue #41: CLOSED / COMPLETED

This candidate is intentionally not merged. The source snapshot is fixed by the benchmark tag, so the PR is closed unmerged."
    gh pr close "$pr" --repo "$GITHUB_REPOSITORY"
  fi
  merged_at="$(gh pr view "$pr" --repo "$GITHUB_REPOSITORY" --json mergedAt --jq '.mergedAt // ""')"
  [[ -z "$merged_at" ]] || { echo "Candidate PR unexpectedly merged: #$pr" >&2; exit 1; }
done

echo '6/7 delete candidate working branches'
for row in "${candidates[@]}"; do
  IFS='|' read -r tag branch head pr rank score <<< "$row"
  if git ls-remote --exit-code origin "refs/heads/$branch" >/dev/null 2>&1; then
    git push origin --delete "$branch"
  fi
done
if git ls-remote --exit-code origin 'refs/heads/agent/fnd03-benchmark-archive-review-backup' >/dev/null 2>&1; then
  git push origin --delete 'agent/fnd03-benchmark-archive-review-backup'
fi

echo '7/7 final verification'
for row in "${candidates[@]}"; do
  IFS='|' read -r tag branch head pr rank score <<< "$row"
  remote_deref="$(git ls-remote origin "refs/tags/$tag^{}" | awk '{print $1}')"
  [[ "$remote_deref" == "$head" ]] || { echo "Tag missing after cleanup: $tag" >&2; exit 1; }
  ! git ls-remote --exit-code origin "refs/heads/$branch" >/dev/null 2>&1 || { echo "Candidate branch still exists: $branch" >&2; exit 1; }
  if [[ "$pr" != '0' ]]; then
    state="$(gh pr view "$pr" --repo "$GITHUB_REPOSITORY" --json state --jq .state)"
    [[ "$state" == 'CLOSED' ]] || { echo "Candidate PR not closed: #$pr state=$state" >&2; exit 1; }
  fi
done

echo 'FND-03 candidate archive lifecycle COMPLETE.'
