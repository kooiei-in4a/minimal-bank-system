#!/usr/bin/env bash
set -euo pipefail

git config user.name "FND-03 Benchmark Archive"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
git fetch origin '+refs/heads/*:refs/remotes/origin/*' --prune --tags

manifest_path='docs/benchmarks/fnd03-model-comparison/archive-manifest.json'
manifest_ref='agent/fnd03-benchmark-archive'
manifest_json="$(gh api "repos/$GITHUB_REPOSITORY/contents/$manifest_path?ref=$manifest_ref" --jq .content | tr -d '\n' | base64 -d)"

candidate_rows() {
  jq -c '.initial_candidates[] , .major_fix_candidates[]' <<< "$manifest_json"
}

field() {
  local json="$1" query="$2"
  jq -r "$query" <<< "$json"
}

tag_deref() {
  local tag="$1"
  git ls-remote origin "refs/tags/$tag^{}" | awk '{print $1}'
}

branch_head() {
  local branch="$1"
  git ls-remote origin "refs/heads/$branch" | awk '{print $1}'
}

echo '1/6 verify existing snapshots; create only missing annotated tags'
while IFS= read -r row; do
  tag="$(field "$row" '.snapshot_tag')"
  branch="$(field "$row" '.branch')"
  head="$(field "$row" '.head_sha')"
  pr="$(field "$row" '.pr // 0')"
  rank="$(field "$row" '.rank // "N/A"')"
  score="$(field "$row" '.score // "N/A"')"

  deref="$(tag_deref "$tag")"
  if [[ -n "$deref" ]]; then
    [[ "$deref" == "$head" ]] || { echo "TAG MISMATCH: $tag expected=$head actual=$deref" >&2; exit 1; }
    continue
  fi

  actual_branch="$(branch_head "$branch")"
  [[ "$actual_branch" == "$head" ]] || {
    echo "NO VALID SNAPSHOT SOURCE: tag=$tag branch=$branch expected=$head actual_branch=${actual_branch:-MISSING}" >&2
    exit 1
  }

  annotation=$(printf 'FND-03 benchmark candidate\nBranch: %s\nHead: %s\nPR: %s\nFinal rank: %s\nFinal score: %s\nFinal synthesis: PR #104' "$branch" "$head" "$pr" "$rank" "$score")
  git tag -a "$tag" "$head" -m "$annotation"
  git push origin "refs/tags/$tag"
  [[ "$(tag_deref "$tag")" == "$head" ]] || { echo "REMOTE TAG VERIFY FAILED: $tag" >&2; exit 1; }
done < <(candidate_rows)

echo '2/6 verify all annotated tags before PR or branch mutation'
while IFS= read -r row; do
  tag="$(field "$row" '.snapshot_tag')"
  head="$(field "$row" '.head_sha')"
  deref="$(tag_deref "$tag")"
  [[ "$deref" == "$head" ]] || { echo "TAG VERIFY FAILED: $tag expected=$head actual=${deref:-MISSING}" >&2; exit 1; }
done < <(candidate_rows)

echo '3/6 verify candidate PR identity where PR exists'
while IFS= read -r row; do
  pr="$(field "$row" '.pr // 0')"
  [[ "$pr" == '0' ]] && continue
  head="$(field "$row" '.head_sha')"
  read -r state pr_head < <(gh pr view "$pr" --repo "$GITHUB_REPOSITORY" --json state,headRefOid --jq '[.state,(.headRefOid // "")] | @tsv')
  if [[ -n "$pr_head" && "$pr_head" != "$head" ]]; then
    echo "PR HEAD MISMATCH: #$pr expected=$head actual=$pr_head" >&2
    exit 1
  fi
  [[ "$state" == 'OPEN' || "$state" == 'CLOSED' ]] || { echo "Unexpected PR state: #$pr $state" >&2; exit 1; }
done < <(candidate_rows)

echo '4/6 record benchmark completion and close candidate PRs unmerged'
while IFS= read -r row; do
  pr="$(field "$row" '.pr // 0')"
  [[ "$pr" == '0' ]] && continue
  tag="$(field "$row" '.snapshot_tag')"
  head="$(field "$row" '.head_sha')"
  rank="$(field "$row" '.rank // "N/A"')"
  score="$(field "$row" '.score // "N/A"')"

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
done < <(candidate_rows)

echo '5/6 delete any candidate working branches that still exist'
while IFS= read -r row; do
  branch="$(field "$row" '.branch')"
  if git ls-remote --exit-code origin "refs/heads/$branch" >/dev/null 2>&1; then
    git push origin --delete "$branch"
  fi
done < <(candidate_rows)

if git ls-remote --exit-code origin 'refs/heads/agent/fnd03-benchmark-archive-review-backup' >/dev/null 2>&1; then
  git push origin --delete 'agent/fnd03-benchmark-archive-review-backup'
fi

echo '6/6 final remote verification'
while IFS= read -r row; do
  tag="$(field "$row" '.snapshot_tag')"
  branch="$(field "$row" '.branch')"
  head="$(field "$row" '.head_sha')"
  pr="$(field "$row" '.pr // 0')"

  [[ "$(tag_deref "$tag")" == "$head" ]] || { echo "Final tag mismatch: $tag" >&2; exit 1; }
  ! git ls-remote --exit-code origin "refs/heads/$branch" >/dev/null 2>&1 || { echo "Candidate branch still exists: $branch" >&2; exit 1; }

  if [[ "$pr" != '0' ]]; then
    read -r state merged_at < <(gh pr view "$pr" --repo "$GITHUB_REPOSITORY" --json state,mergedAt --jq '[.state,(.mergedAt // "")] | @tsv')
    [[ "$state" == 'CLOSED' ]] || { echo "Candidate PR not closed: #$pr state=$state" >&2; exit 1; }
    [[ -z "$merged_at" ]] || { echo "Candidate PR merged unexpectedly: #$pr" >&2; exit 1; }
  fi
done < <(candidate_rows)

echo 'FND-03 candidate archive lifecycle COMPLETE.'
