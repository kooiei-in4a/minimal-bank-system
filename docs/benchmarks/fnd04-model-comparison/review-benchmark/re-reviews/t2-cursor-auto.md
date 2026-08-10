# FND-04 G-01 Major-Fix Re-Review

Reviewer:

- Slot: T2
- Model: NOT_EXPOSED
- Harness: Cursor
- Effort: Auto

Target:

- Result: PASS
- Old Head: `99cee4386ea049ad84e9c087c6fdf1e25cc20f3e`
- New Head: `3511688401533f60bb77c7dcc647c4c2c4aa84c6`（PR #140 `headRefOid`一致）
- Delta: 1 commit / 1 file / `+18 / -0`
  `tests/MinimalBankSystem.IntegrationTests/Persistence/DesignTimeConnectionSafetyTests.cs`
  production code change: なし

G-01 verdict:

- G01_FIXED

Evidence:

- baseline: PASS（`DesignTimeConnectionSafetyTests` 1/1、一時copy@`3511688`）
- M1: FAIL as expected（model-only pathへ `Host=db;...Database=ambient_fallback...` 注入。`Assert.Contains("The ConnectionString property has not been initialized.")` でFAIL。blocklistへ`db`/`ambient_fallback`追加なし）
- M2: FAIL as expected（Infrastructure/Migrator の bin/obj 退避。`Unable to retrieve project metadata...` のtool/build failureで、positive marker未充足によりFAIL）
- recovery: PASS（未改変 baseline 再実行）
- mutation residue: NONE（main repo clean、一時worktree/copy削除済み）

CI:

- direct-head: Run `31360093004` SUCCESS
  checkout `3511688401533f60bb77c7dcc647c4c2c4aa84c6`
  pending-model PASS / non-PostgreSQL 42 PASS / real PostgreSQL 23 PASS
- merge-ref: Run `31360094852` SUCCESS
  checkout `2e69049bd8b38e57cd4fee2c42e17edaeaf23df1`
  `Merge 3511688... into 38c07e2...`
  pending-model PASS / non-PostgreSQL 42 PASS / real PostgreSQL 23 PASS

New findings introduced by fix:

- Blocker: NONE
- Major: NONE

Final rationale:

- Gold G-01（`exit != 0` + 固定blocklistのみのfalse assurance）に対し、新assertは uninitialized ConnectionString / empty destination / Npgsql / EF Migrations をpositiveにpinしており、単なるnonzeroではPASSしない。
- 独立M1/M2でred、baseline/recoveryでgreenを確認。差分はtest-onlyでSCOPE_DRIFTなし。CI両run SUCCESS。よって `G01_FIXED`。
