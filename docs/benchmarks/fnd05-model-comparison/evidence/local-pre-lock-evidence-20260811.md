# FND-05 Local Pre-Lock Evidence — 2026-08-11

Status: **COLLECTED / NO IMPLEMENTATION**

Repository: `kooiei-in4a/minimal-bank-system`

This artifact preserves the operator-collected local evidence used for D-01 / D-02 / D-06 / D-07 locking. No product code, product tests, Issue, PR metadata, candidate branch, or candidate implementation was changed by the evidence-collection run.

```text
REPOSITORY: kooiei-in4a/minimal-bank-system
WORKTREE_CLEAN_BEFORE: YES
WORKTREE_CLEAN_AFTER: YES

D-01:
  DOCKER_CLIENT: 29.6.2 / API 1.55 / windows/amd64 / context=desktop-linux
  DOCKER_SERVER: Docker Desktop 4.85.0 / Engine 29.6.2 / API 1.55 / linux/amd64
  COMPOSE: v5.3.1
  BUILDX: v0.35.0-desktop.2 / b554ce1decd8b509893b1e7c6227eabfb923d094
  MIN_COMPOSE_2_38_2: YES
  SERVICE_HEALTHY: PASS — healthy serviceがhealthyになった後にdependentが起動
  SERVICE_COMPLETED_SUCCESSFULLY: PASS — one-shot serviceがexit 0後にdependentが起動
  SECRETS_ENVIRONMENT: PASS — top-level secrets.environmentをparseし、service起動成功
  CONFIG_QUIET: PASS
  PS_JSON: PASS — docker compose ps --format jsonおよびps -a --format jsonを確認

D-02:
  POSTGRES_TAG: postgres:18.4
  POSTGRES_INDEX_DIGEST: sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636
  DOTNET_SDK_TAG: mcr.microsoft.com/dotnet/sdk:10.0-noble
  DOTNET_SDK_INDEX_DIGEST: sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0
  DOTNET_ASPNET_TAG: mcr.microsoft.com/dotnet/aspnet:10.0-noble
  DOTNET_ASPNET_INDEX_DIGEST: sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b

D-06:
  M01_CONTROLLED_BARRIER_FEASIBLE: YES — evaluator-only複数-f override、/bin/bash wrapper、compose wait、pause/unpause、kill --signal、execが利用可能
  M03_ISOLATED_PENDING_STATE_FEASIBLE: YES — detached git worktree、worktree内temporary migration、Compose -pによるproject分離、named volumeを利用可能。mutation自体は未実行
  M08_ISOLATED_RUNTIME_MUTATION_FEASIBLE: YES — git worktree add --detachによる一時worktree内変更が可能。production branchへのcommitは不要。mutation自体は未実行
  M10_PREEXISTING_RESOURCE_FIXTURE_FEASIBLE: YES — named volumeの作成・識別・削除およびproject/volume labelsのJSON取得を実証

D-07:
  OS: Ubuntu
  VERSION: 24.04.4 LTS (Noble Numbat)
  ARCH: x86_64
  KERNEL: 6.6.87.2-microsoft-standard-WSL2
  WSL: YES — WSL2、default distribution=Ubuntu
  BASH: /bin/bash / GNU bash 5.2.21(1)-release
  JQ: jq-1.7
  GIT_AUTOCRLF: true
  DOCKER_OS_TYPE: linux
  DOCKER_ARCH: x86_64

TEMP_ARTIFACTS_REMOVED: YES — Compose定義はstdin経由、temporary file作成なし
CONTAINERS_REMOVED: YES — residue 0
NETWORKS_REMOVED: YES — residue 0
VOLUMES_REMOVED: YES — residue 0

PRODUCT_CODE_CHANGED: NO
PRODUCT_TEST_CHANGED: NO
GIT_DIFF: NONE

UNVERIFIED:
  - PostgreSQLの以前のweb観測digestとcurrent registry実測値が不一致。evidence collectorはどちらもlockしなかった
  - D-03 / D-04 / D-05 / D-08はevidence collectorでは未決定
  - M-01 / M-03 / M-08はcapability確認のみ。mutationは未実行
  - Windows + WSL2 Ubuntu + linux/amd64 Docker以外の環境では未実行
  - probeでpullされたalpine:3.22は共有image cacheから削除していない
```

Additional project-identity evidence:

- temporary project: `fnd05-evidence-40e82836`
- container labels observed machine-readably: `com.docker.compose.project`, `com.docker.compose.service`
- volume labels observed machine-readably: `com.docker.compose.project`, `com.docker.compose.volume`
- temporary project resources removed after probe

This artifact records evidence only. D-01〜D-08 lock authority is `reference/pre-run-decision-locks.md` and `run.json`.
