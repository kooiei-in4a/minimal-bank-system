# FND-05 Koo Start Authorization

Date: 2026-08-11 JST

Authorization statement from Koo:

> FND-05のcandidate preparation / execution開始を許可します。

Scope of this authorization:

- candidate preparation may proceed after the pre-execution common-base contract is satisfied
- candidate execution may proceed only after common base / branch / Draft PR / exact identity / candidate-output-zero verification is complete
- this authorization does not by itself authorize merge of PR #144 or PR #145
- this authorization does not waive the requirement that candidate branches contain or can authoritatively consume the locked FND-05 pre-run contracts
- implementation_permitted remains false until the pre-execution identity gate passes

Current repository observation at authorization handling time:

- current `main`: `9a352a3a61945647273ccc7dfbc8e1816c3ca07c`
- PR #144: OPEN / Draft / not merged; Head `8f76b400e90e4d965e6c423c57bbb61b00c8dcbd`
- PR #145: OPEN / Draft / not merged; Head before this authorization artifact `3a4609cd2f20c5b79ed376238d74ce117d086fb4`
- locked FND-05 `run.json` and pre-run contracts are not present on current `main`
- candidate branches / PRs are not created yet

Blocking condition before safe candidate branch creation:

The common base must include the authoritative locked FND-05 pre-run contracts. Current `main` does not. PR #144 / PR #145 retain an explicit no-merge-without-separate-authorization constraint, so this authorization alone is not treated as permission to merge those PRs.

Until that common-base condition is satisfied:

- `koo_start_authorized` is evidenced by this artifact and Issue #43 record
- `implementation_permitted` remains false
- candidate branches / PRs remain uncreated
- candidate execution remains not started
