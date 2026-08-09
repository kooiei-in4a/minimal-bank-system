# FND-04 H1 Self-Review Fix Prompt

応答は日本語で出力してください。

あなたはFND-04 Benchmark Candidateの **Agent A / H1 Fix Author** です。

H0 exact Headに対するFormal Self-Reviewは既に完了し、Findingが固定済みです。このphaseでは、そのFindingだけを入力としてH1候補を作ります。

## Fixed identity

```yaml
MODEL: "<MODEL>"
HARNESS: "<HARNESS>"
EFFORT: "<EFFORT>"
CANDIDATE_SLUG: "<SLUG>"
ATTEMPT: 1

REPOSITORY: "kooiei-in4a/minimal-bank-system"
TARGET_ISSUE: 42
TARGET_BRANCH: "<BRANCH>"
COMMON_BASE_SHA: "<COMMON_BASE_SHA>"
H0_HEAD_SHA: "<H0_HEAD_SHA>"
FORMAL_SELF_REVIEW_ARTIFACT: "<ARTIFACT>"
```

## Rules

- 他candidate、外部reviewer、Gold、ranking、Final Synthesisを参照しない。
- H0と固定済みFormal Self-Review Findingを一次入力とする。
- Findingを自動的に全採用しない。
- 各Findingを`accepted / rejected`へ分類し、理由を残す。
- accepted Findingだけを必要最小限で修正する。
- Formal Self-Reviewにない新規scopeや改善を便乗実装しない。
- Issue #42 / ADR-0009 / AGENTS.mdがSelf-Reviewより上位の正本である。

## Workflow

1. H0 exact Headを確認する。
2. Formal Self-Review artifactを確認する。
3. Finding dispositionを固定する。
4. accepted Findingを修正する。
5. required verificationを再実行する。
6. Draft PR本文へH0 / SR / H1対応を追記する。
7. exact H1 Head CIを確認する。
8. H1 full Headを固定して停止する。

## Required verification

H0と同じverificationを最低限再実行します。

- restore
- build 0 warnings / 0 errors
- non-PostgreSQL tests
- real PostgreSQL tests
- clean migration apply
- migration history inspection
- migrator failure path
- API startup no-auto-migration
- pending model changes check
- idempotent SQL generation path
- business schema / scope check
- `git diff --check`

Formal Self-Review Findingを直したことで別のregressionを導入していないかも確認してください。

## Final report

```text
## FND-04 H1 Result

Model:
Harness:
Effort:

Branch:
Common Base:
H0 Head:
H1 Head:
Draft PR:
Exact H1 CI:

Self-Review Findings:
- SR-xx: accepted / rejected — reason

Accepted findings fixed:
Rejected findings:
Unfixed valid concerns:
New regressions observed:

Verification:
- Restore:
- Build:
- non-PostgreSQL tests:
- PostgreSQL tests:
- clean migration apply:
- migration history:
- migrator failure:
- API no-auto-migration:
- model drift check:
- idempotent SQL generation:
- git diff --check:

Duration H1 fix:
H1 snapshot:
LOCKED / NOT LOCKED
```

H1固定後は外部評価開始までcandidateを変更しないでください。
