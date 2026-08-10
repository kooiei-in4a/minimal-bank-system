# FND-04 Final Synthesis Independent Review

Reviewer:

- Slot: R3
- Model: GPT-5.6 Luna
- Harness: Codex
- Effort: xHigh
- Primary role: specification_scope
- Attempt: 1

Target verification:

- Repository: `kooiei-in4a/minimal-bank-system`
- PR: #140
- Base SHA: `38c07e210fe4e8689f1d8aeabbb07b9261e...` (固定値と一致)
- Head SHA: `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- PR state: Open / Draft / Unmerged
- CI identity: merge-ref run `31350916189` と direct-head push run `31350870902` を区別して確認
- Result: PASS

Verdict:

- APPROVE_WITH_FINDINGS

Merge-ready:

- YES（Minor findingは非blocking）

Findings:

### Blocker

- NONE

### Major

- NONE

### Minor

- **R3-F01 / Minor / blocking: no**
  - Affected path/component: `MigrationModelTests`, `BankDbContext`、evaluator-only model-drift probe
  - Evidence: committed codeは実EFの`HasPendingModelChanges()`を使用し、clean modelのCI検証は成功。Temporary model-only changeのnegative probeはPR本文のlocal-only claimのみで、Head上の一次証拠として独立再現できない。
  - Root cause: evaluator-only probeをcommitへ残していない。
  - Impact: P08のnegative verificationを独立証拠としてPASSにはできない。
  - Required fix direction: Issue close前に、synthetic entityをHeadへ残さず、exact Headで一時model変更→pending検出→変更破棄→clean復帰を再実行し、証拠を記録する。

### Nit

- NONE

Probe matrix:

- P01: PASS — exact package/tool version、Infrastructure ownership、provider/migrations assembly、dedicated Migratorを確認。
- P02: PASS — real PostgreSQL clean apply、exit 0、`InitialFoundation`、history 1件、business tableなし。
- P03: PASS — rerun成功、history不変。
- P04: PASS — missing connection、unreachable、rejected credentials、malformed historyがnon-zero。
- P05: PASS — production Migrator自身の60秒 cancellation budgetとNpgsql command timeout、lock testを確認。
- P06: PASS — API startupはDI登録のみ。real PostgreSQL before/afterとDbContext resolve後のschema不変を検証。
- P07: PASS — `HasPendingModelChanges()`とrepository-local `dotnet-ef` commandを使用。
- P08: PARTIAL — 実装設計は妥当だが、temporary negative probeはlocal-only claim。
- P09: PASS — API startup migration、`EnsureCreated`、ad-hoc DDLなし。
- P10: PASS — design-time/runtimeともNpgsql・Infrastructure migrations assembly。接続なしのfactoryはfake providerやlocalhostを生成しない。
- P11: PASS — idempotent SQLの標準commandを文書化し、EF migratorのidempotent生成経路をテスト。
- P12: PASS — business schema、Compose、health、authentication/authorizationの先取りなし。
- P13: PASS — FND-03のreal PostgreSQL fixtureを再利用。
- P14: PASS — production Migrator child processのstdout/stderrでsentinel password非漏えいを確認。
- P15: PASS — direct-head CIとmerge-ref CIを区別して確認。

Primary-role deep dive:

Issue #42のACは、Infrastructure-owned `BankDbContext`、migrations、snapshot、design-time factory、Npgsql configuration、専用Migrator、canonical connection key、empty baseline、60秒failure budget、API no-auto-migration、model drift、idempotent SQL、schema-owner向け手順まで実装されている。API→InfrastructureおよびMigrator→Infrastructureの依存方向はADR-0001と整合する。business schema、FND-05 Compose、FND-06 health、認証認可のscope creepは確認されなかった。

CI assessment:

- `31350916189`: pull-request event。checkoutはmerge ref `d12de2ae...`。成功。
- `31350870902`: push event。direct Head `99cee438...`をcheckout。成功。
- Build: warnings 0 / errors 0
- Pending-model check: success
- Non-PostgreSQL: 38 passed
- Real PostgreSQL: 23 passed

Unverified / evidence limits:

- ローカルcheckoutはbase `main`のままで、exact Headのローカル実行は行っていない。
- P08のtemporary model-drift negative probeはGitHub一次証拠として再現できず、PR本文の自己申告をPASS証拠へ昇格していない。
- Compose、health、release deploymentはIssue #42の対象外。

Final rationale:

固定target、実diff、Issue #42、Accepted ADR、実コード、テスト、direct-head CIを照合した結果、Issue #42は必要十分に実装されている。Blocker/Majorはない。P08の独立証拠だけをMinor findingとして残し、技術的merge-readyはYESと判定する。
