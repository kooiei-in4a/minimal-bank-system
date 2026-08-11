# FND-05 M-02 / M-06 Targeted Re-Review — Finding Owner

## ROLE

`finding_owner`

Heavy H2 で本人が提出し、Conditional Judge で UPHELD された 2 件の Major
(`H2-MAJ-02` / `H2-MAJ-01`) について、Targeted Fix 後の root cause 解消のみを確認した。
full H2 Heavy Review の再実行は行っていない。Review-only であり target は一切変更していない。

## REVIEWER_IDENTITY

```yaml
MODEL: Claude Opus 5
HARNESS: Claude Code
EFFORT_REQUESTED: xhigh
EFFORT_ACTUAL_LABEL: NOT_EXPOSED_TO_MODEL
CONTEXT: Fresh Context
ROLE: finding_owner
PROMPT_REVISION: fnd05-targeted-re-review-v2
```

`EFFORT_ACTUAL_LABEL` について: Claude Code は exact Effort label を model へ公開しない。
推測を避け、公開されない事実として記録する。要求値は `xhigh`。

## TARGET_VERIFICATION

```yaml
TARGET_ISSUE: 43
TARGET_PR: 153

PR_153_STATE: OPEN
PR_153_DRAFT: true
PR_153_MERGED: false
PR_153_BASE_REF: main
PR_153_HEAD_REF: agent/issue-43-fnd-05-final-code
PR_153_HEAD_OID: 9e704f53911be3fdf0d09538424d3bcd9012f96a

OLD_HEAD_SHA_EXPECTED: 59aa87f9c6c4c581a56257caef738318e8d09ec3
NEW_HEAD_SHA_EXPECTED: 9e704f53911be3fdf0d09538424d3bcd9012f96a

REMOTE_BRANCH_HEAD_OBSERVED:
  agent/issue-43-fnd-05-final-code: 9e704f53911be3fdf0d09538424d3bcd9012f96a

PASS: YES
```

remote control branch (`agent/issue-43-fnd-05-final-code`, `codex/fnd05-targeted-fix-handoff`)
は開始前に明示 fetch した。`git ls-remote --heads origin` で
`agent/issue-43-fnd-05-final-code = 9e704f53911be3fdf0d09538424d3bcd9012f96a`、
`codex/fnd05-targeted-fix-handoff = b7911db65406debddd962ef6907a8e7c54a73186` を確認した。

## SOURCE_FINDING_REFS

すべて exact Git blob から SHA-256 を再計算して照合した (`git cat-file blob <commit>:<path> | sha256sum`)。

```yaml
CONDITIONAL_JUDGE:
  PATH: docs/benchmarks/fnd05-model-comparison/reviews/fnd05-conditional-judge-composer-2.5.md
  PRODUCER_COMMIT: fb0b2f81e4817b494e2167547f537c1e774e919d
  SHA256_EXPECTED: ce44323a2728f0d6ca2dde3d28040074e77d8b59c96ae65bbd528080600f64bb
  SHA256_RECOMPUTED: ce44323a2728f0d6ca2dde3d28040074e77d8b59c96ae65bbd528080600f64bb
  MATCH: YES
  VERDICT: CHANGES_REQUIRED

ORIGINAL_H2_ARTIFACT:
  PATH: docs/benchmarks/fnd05-model-comparison/reviews/fnd05-heavy-h2-opus-claude-opus-5-claude-code.md
  PRODUCER_COMMIT: 4ca962b4a8f0dd9faeacc1a494ed86f919f5536a
  SHA256_EXPECTED: cc0e996707f83f4b9c338b3ecc5033d0829646c0976843aec30de39b3a275425
  SHA256_RECOMPUTED: cc0e996707f83f4b9c338b3ecc5033d0829646c0976843aec30de39b3a275425
  MATCH: YES

LOCKED_FINDING_IDS:
  - H2-MAJ-02
  - H2-MAJ-01
```

## FIX_ARTIFACT_REF

```yaml
FIX_ARTIFACT:
  PATH: docs/benchmarks/fnd05-model-comparison/final-synthesis/targeted-fix-m02-m06-result.md
  PRODUCER_COMMIT: a2e97d3baefb386a0a825a9a79e751ead4124016
  SHA256_EXPECTED: 53e8800472db7ba999abd713b5cc7171f6f42c96becc65f951a6924b76e40cce
  SHA256_RECOMPUTED: 53e8800472db7ba999abd713b5cc7171f6f42c96becc65f951a6924b76e40cce
  MATCH: YES

FIX_LOCK_COMMIT: b7911db65406debddd962ef6907a8e7c54a73186

RUN_REGISTRY:
  PATH: docs/benchmarks/fnd05-model-comparison/run.json
  AT_COMMIT: b7911db65406debddd962ef6907a8e7c54a73186
  SHA256_EXPECTED: 44dd3b670f3ae41d39e26d0c29d9804366b62f7a1d179b51a2f53108f14f8434
  SHA256_RECOMPUTED: 44dd3b670f3ae41d39e26d0c29d9804366b62f7a1d179b51a2f53108f14f8434
  MATCH: YES
```

備考: prompt の `run.json@b7911db...` は repository root ではなく
`docs/benchmarks/fnd05-model-comparison/run.json` に解決される。この tree path で SHA-256 が一致した。

Fix artifact の自己申告 (`M02_EVIDENCE` / `M06_EVIDENCE` の全項目 PASS、`VALID_KILL: YES`) は
以下の CI 一次証拠と矛盾しないことを独立に確認した。本 review の判定は
fix artifact の申告ではなく CI 一次証拠と changed surface の読解に基づく。

## CHANGE_SURFACE

```yaml
OLD_HEAD: 59aa87f9c6c4c581a56257caef738318e8d09ec3
NEW_HEAD: 9e704f53911be3fdf0d09538424d3bcd9012f96a

COMMITS_BETWEEN:
  - 9e704f5 fix(fnd05): make M-02 and M-06 mutation kills discriminating

CHANGED_FILES_OBSERVED:
  - M tests/fnd05/static-gate.sh       (+7  -0)
  - M tests/fnd05/verify-mutations.sh  (+90 -13)

ALLOWED_FILES:
  - tests/fnd05/verify-mutations.sh
  - tests/fnd05/static-gate.sh

OUT_OF_SCOPE_FILE_CHANGED: NONE
CHANGE_SURFACE_RESPECTED: YES
```

`git diff --name-status 59aa87f9..9e704f53` は上記 2 file のみを返し、
許可された surface 以外の変更は存在しない。

## DIRECT_HEAD_CI

merge-ref CI は代用していない。両 run とも `event: push`、`headSha` は Targeted Fix Head と一致する。

```yaml
BUILD_AND_TEST:
  RUN: 31515332416
  WORKFLOW: Build and Test
  EVENT: push
  HEAD_SHA: 9e704f53911be3fdf0d09538424d3bcd9012f96a
  CONCLUSION: success
  JOBS:
    build-test: success

FND05_COMPOSE:
  RUN: 31515332435
  WORKFLOW: FND-05 Compose verification
  EVENT: push
  HEAD_SHA: 9e704f53911be3fdf0d09538424d3bcd9012f96a
  CONCLUSION: success
  JOBS:
    fnd05-compose: success
    fnd05-mutations: success

ACTUAL_CHECKOUT_SHA_VERIFIED:
  fnd05-compose:   ACTUAL_CHECKOUT_SHA=9e704f53911be3fdf0d09538424d3bcd9012f96a
  fnd05-mutations: ACTUAL_CHECKOUT_SHA=9e704f53911be3fdf0d09538424d3bcd9012f96a

CHECKOUT_SHA_MATCHES_TARGET: YES
MERGE_REF_SUBSTITUTED: NO
```

両 job には `test "$(git rev-parse HEAD)" = "$GITHUB_SHA"` step が存在し、
これが成功した上で `ACTUAL_CHECKOUT_SHA` が出力されている。
`fnd05-mutations` job の checkout step も `git log -1 --format=%H` として
`9e704f53911be3fdf0d09538424d3bcd9012f96a` を記録している。
mutation 実行中の `git worktree add` 出力も
`HEAD is now at 9e704f5 fix(fnd05): make M-02 and M-06 mutation kills discriminating` を示す。

## H2_MAJ_02_RESULT

```yaml
VERDICT: FIXED
```

### 元の root cause

intended real Migrator failure へ到達したことを確認する前に exit 0 だけから
kill signature を出していたため、mask-only と real-failure+masking を区別できなかった。

### 解消の構造

`tests/fnd05/verify-mutations.sh` は M-02 を 2 段構成に変更した。

1. **mask-only control** — masking wrapper だけを適用する override
   (`dotnet MinimalBankSystem.Migrator.dll || { code=$?; printf 'FND05_M02_MASKED_NONZERO=%s\n' "$code"; exit 0; }`) を
   正常 migration path 上で実行する。`success_oracle` が GREEN であることを確認した上で、
   `expect_red m02-intended-failure-not-reached m02_failure_oracle` を課す。
2. **real failure + mask** — 同じ wrapper に `POSTGRES_PORT: "1"` を加えて
   実 DB 接続失敗を注入し、precondition を machine-readable に確認した後にのみ
   `expect_red migrator-nonzero-masked-after-intended-failure m02_failure_oracle` を課す。

precondition 判定は `m02_intended_failure_reached()` に集約されている。

- Migrator 自身の fail-closed message `Migration failed. The deployment must not continue.`
  (`src/MinimalBankSystem.Migrator/MigratorLog.cs:34` に定義された production log message) の存在
- かつ `FND05_M02_MASKED_NONZERO=[1-9][0-9]*` (非 0 のみを受理する regex) の存在

の両方を要求する。`m02_failure_oracle()` はこの precondition を最初に評価し、
未達なら `ORACLE_SIGNATURE=m02-intended-failure-not-reached` を返して valid kill signature へ到達しない。
つまり valid signature `migrator-nonzero-masked-after-intended-failure` は
intended real failure 到達後にのみ出力可能な構造になっている。

### 判別性 (SIGNATURE_DISCRIMINATES) の一次証拠

`expect_red` は「非 0 終了」かつ「期待 signature の部分一致」の双方を要求する。
mask-only control に対して `expect_red m02-intended-failure-not-reached` が成功したという事実は、
mask-only 実行が `migrator-nonzero-masked-after-intended-failure` を出力しなかったことの
機械的証明である (出力していれば期待 signature 文字列が不在となり job が失敗する)。
CI log 上で `M-02: PRECONDITION_CONTROL_REJECTED` が印字されている以上、
masking wrapper 単独では valid kill signature は出ない。

`m02_failure_oracle` は設計上つねに非 0 を返すため、判別は signature 一致が担っている。
GREEN 側の担保は mask-only control で先行実行される `success_oracle`
(migrator exit 0 / API running / listener ready / expected migration 適用済) が負う。
この二者の組み合わせにより mask-only と real-failure+masking は分離されている。

### M02_MUTATION_RESULTS

`fnd05-mutations` job (run 31515332435, job 93858946211) の一次 log から採取。

```text
M-02: BASELINE_GREEN
M-02: MASK_ONLY_CONTROL_EXECUTED
M-02: PRECONDITION_CONTROL_REJECTED
M-02: INTENDED_FAILURE_REACHED
M-02: MACHINE_READABLE_PRECONDITION
M-02: MASKED_NONZERO_OBSERVED
M-02: EXPECTED_RED
M-02: EXPECTED_FAILURE_SIGNATURE=migrator-nonzero-masked-after-intended-failure
M-02: RESTORED_GREEN
M-02: RESIDUE_ZERO
M-02: KILLED
```

```yaml
M02:
  BASELINE_GREEN: PASS

  MASK_ONLY_CONTROL:
    EXECUTED: PASS
    INTENDED_FAILURE_REACHED: NO
    VALID_KILL_REJECTED: PASS
    SYSTEM_STATE: GREEN (success_oracle passed)

  REAL_FAILURE_PLUS_MASK:
    INTENDED_FAILURE_REACHED: PASS
    FAILURE_MARKER_OBSERVED: PASS
    MASKED_NONZERO_OBSERVED: PASS
    EXPECTED_RED: PASS
    EXPECTED_SIGNATURE: migrator-nonzero-masked-after-intended-failure

  SIGNATURE_DISCRIMINATES: PASS
  RESTORED_GREEN: PASS
  RESIDUE_ZERO: PASS
  VALID_KILL: YES
```

補助証拠として、mask-only control 区間の compose 出力は
migrator が `Started` (17:01:28.68) → `Exited` (17:01:30.18) の後に api が `Started` (17:01:30.29) し、
続いて `M-02: MASK_ONLY_CONTROL_EXECUTED` / `PRECONDITION_CONTROL_REJECTED` が印字されている。
masking wrapper 単独では stack が正常に立ち上がり、kill 扱いされないことと一致する。

`ORACLE_SIGNATURE=...` 文字列そのものは `expect_red` の command substitution
(`output="$("$@" 2>&1)"`) に取り込まれるため CI log には現れない。
本 review はこの点を踏まえ、signature そのものではなく
「assertion 成功時のみ到達可能な marker」を証拠として採用している。

## H2_MAJ_01_RESULT

```yaml
VERDICT: FIXED
```

### 元の root cause

named-volume mutation を inline self-check するだけで、
shipped volume-policy oracle を実行せず KILLED としていた。

### 解消の構造

`run_m06()` は inline `compose config` + inline `jq` self-check を廃止し、
M-05 と同じ shipped-oracle 実行形式へ置換された。

1. `git worktree add --detach` で HEAD の detached worktree を作成
2. `FND05_SOURCE_ROOT=$repository_root` で shipped `tests/fnd05/static-gate.sh` を実行 → BASELINE_GREEN
3. worktree の `compose.yaml` に対し precondition
   (`- postgres_data:/var/lib/postgresql` の存在) を確認
4. `sed -i 's#postgres_data:/var/lib/postgresql#/var/lib/postgresql#'` で named volume を anonymous volume へ変異、適用を再確認
5. `expect_red named-volume-policy-violation env FND05_SOURCE_ROOT=$worktree ... bash $repository_root/tests/fnd05/static-gate.sh`
   — **mutated tree に対して同一の shipped oracle を実行**
6. worktree 撤去後、再び shipped oracle を実行 → RESTORED_GREEN
7. `assert_residue_zero`

判定を担うのは shipped `static-gate.sh` に新設された次の check である。

```bash
if ! jq --exit-status \
  'any(.services.postgres.volumes[]; .type == "volume" and .source == "postgres_data" and .target == "/var/lib/postgresql")' \
  <<<"$rendered" >/dev/null; then
  printf 'ORACLE_SIGNATURE=named-volume-policy-violation\n' >&2
  exit 1
fi
```

これは既存の複合 jq assertion (同一 predicate を含む) の判定内容を変えず、
resolved compose render 上の named-volume policy 違反にだけ
機械可読な failure reason を付与するものである。

### shipped oracle であることの確認

`tests/fnd05/static-gate.sh` は mutation harness 専用ではない。

- `tests/fnd05/verify-compose.sh:226` が `bash tests/fnd05/static-gate.sh` を呼ぶ
- `.github/workflows/fnd05-compose.yml` の `fnd05-compose` job が
  `bash tests/fnd05/verify-compose.sh` を実行する
- その job log で `STATIC_GATE: PASS` (17:00:14.4158988Z) が出力されている

したがって M-06 が RED を要求している oracle は、production verification path で
実際に実行されている shipped gate と同一である。inline self-check のみを
KILLED 根拠とする構造は残っていない。

### M06_MUTATION_RESULTS

```text
M-06: BASELINE_GREEN
M-06: MUTATION_PRECONDITION
M-06: MUTATION_APPLIED
M-06: SHIPPED_ORACLE_EXECUTED
M-06: EXPECTED_RED
M-06: EXPECTED_FAILURE_SIGNATURE=named-volume-policy-violation
M-06: RESTORED_GREEN
M-06: RESIDUE_ZERO
M-06: KILLED
```

```yaml
M06:
  BASELINE_GREEN: PASS
  PRECONDITION: PASS
  MUTATION_APPLIED: PASS
  SHIPPED_ORACLE_EXECUTED: PASS
  EXPECTED_RED: PASS
  EXPECTED_SIGNATURE: named-volume-policy-violation
  FAILURE_REASON_MATCHED: PASS
  RESTORED_GREEN: PASS
  RESIDUE_ZERO: PASS
  VALID_KILL: YES
```

M-06 区間では shipped gate が 3 回実行され、baseline / restore の
`STATIC_GATE: PASS` (17:02:41.0762470Z / 17:02:41.3084712Z) が log 上に現れる。
その間の mutated 実行だけが `expect_red` により RED として消費されている
(`M-06: EXPECTED_RED` は assertion 成功後にのみ到達する)。

## ADJACENT_REGRESSION

```yaml
RESULT: PASS
```

| 確認項目 | 結果 | 一次証拠 |
| --- | --- | --- |
| Static Gate baseline GREEN | PASS | `fnd05-compose` job で `STATIC_GATE: PASS`。`fnd05-mutations` job 内でも計 4 回 `STATIC_GATE: PASS` |
| M-02 以外の failure oracle semantics 非破壊 | PASS | `failure_oracle` / `success_oracle` / `secret_oracle` は無改変。M-01/M-07 は `failure_oracle`、M-08/M-09 は `success_oracle` を従来どおり使用し KILLED |
| M-06 以外の mutation semantics 非破壊 | PASS | M-01, M-03, M-04, M-05, M-07〜M-10 の実装は無改変。M-05 は digest `require_literal` が render より前段にあるため新 check の影響を受けない |
| full mutation suite が CI で PASS | PASS | `M-01: KILLED` 〜 `M-10: KILLED` 全 10 件、および `MUTATION_SUITE: PASS` |
| Build and Test | PASS | run 31515332416 / job `build-test` = success |
| 変更 surface 内の new Blocker / Major | NONE | 下記参照 |

新設された `named-volume-policy-violation` check の実行順序も確認した。
`static-gate.sh` では image digest の `require_literal` 群 (line 33-39) が
compose render (line 46) より前段にあるため、M-05 の digest mutation は
新 check に到達する前に `postgres-image-digest-missing` で RED になる。
signature の取り違えは発生しない。

`expect_red` は部分一致判定であり、`migrator-nonzero-masked` は
`migrator-nonzero-masked-after-intended-failure` の接頭辞である。
現行 suite で bare `migrator-nonzero-masked` を expected signature として渡す
呼び出しは存在しない (M-07 は `intended-failure-marker-absent` を期待する) ため、
偽陽性経路は成立しない。

## NEW_BLOCKER_MAJOR_IN_CHANGED_SURFACE

```yaml
COUNT: 0
FINDINGS: NONE
```

changed surface 内で新規の Blocker / Major は発見しなかった。
以下は判定に影響しない非 blocking の観察であり、finding として起票しない。

- `m02_failure_oracle()` は設計上つねに非 0 を返すため、`expect_red` の
  「非 0 終了」条件は当該 oracle に対して常に真になる。判別は signature 一致が担っており、
  GREEN 側は mask-only control で先行する `success_oracle` が担保するため、
  Judge が要求した判別性は成立している。
- M-06 の `assert_residue_zero` は static render のみを行う区間に対する検査であり、
  当該 project label の container / volume / network が元より生成されないため
  実質的に自明に成立する。主張自体は正確で、file system 側の residue は
  `git worktree remove --force` と `temporary_worktrees` の trap 経由 cleanup が担う。
  これは既存 M-05 と同一の受理済 pattern である。

## RESIDUE

```yaml
M02_RESIDUE_ZERO: PASS
M06_RESIDUE_ZERO: PASS
SUITE_LEVEL_RESIDUE: none observed
```

M-02 は mutation override 撤去後に `clean_current` (compose down --volumes --remove-orphans +
`assert_residue_zero`) を実行し、restore baseline 後に再度 `clean_current` と
`assert_residue_zero` を経て `M-02: RESIDUE_ZERO` に到達している。
M-06 は worktree 撤去と `assert_residue_zero` を経て `M-06: RESIDUE_ZERO` に到達している。
`verify-mutations.sh` は `trap cleanup_all EXIT` を保持しており、
異常終了時も current project の teardown と temporary worktree の撤去が行われる。

## OUT_OF_SCOPE (本 review では確認していない)

```text
full architecture review
full lifecycle review
M-01〜M-10 の新規探索的 review
新規 optional improvement
style / naming / formatter
PR #153 本文の stale 記述
run.json の historical top-level state
既に解決済みの Light findings
```

## FINAL_VERDICT

```yaml
H2_MAJ_02: FIXED
H2_MAJ_01: FIXED
ADJACENT_REGRESSION: PASS
NEW_BLOCKER_MAJOR_IN_CHANGED_SURFACE: 0

FINAL_VERDICT: FIXED
```

## MERGE_READY_FROM_THIS_SCOPE

```yaml
MERGE_READY_FROM_THIS_SCOPE: YES
```

これは PR #153 の総合 merge authorization ではない。
本 scope (`H2-MAJ-02` / `H2-MAJ-01` の root cause 解消および
changed surface 内の regression 不在) に限った判定である。

## UNVERIFIED

```yaml
- EFFORT_ACTUAL_LABEL:
    Claude Code が exact Effort label を model へ公開しないため未確認。要求値は xhigh。

- ORACLE_SIGNATURE_RAW_STRINGS:
    expect_red の command substitution に取り込まれるため CI log に raw 文字列としては現れない。
    assertion 成功時のみ到達可能な marker 印字から間接的に確認した。

- LOCAL_DOCKER_REPRODUCTION:
    直接 head CI (run 31515332416 / 31515332435) の一次証拠が §9 要求 marker を
    全て充足しているため、local targeted probe は実行していない。

- OUT_OF_SCOPE_ITEMS:
    §4 で除外された領域は一切確認していない。
```

## ARTIFACT_LOCK

```yaml
ARTIFACT_PATH:
  docs/benchmarks/fnd05-model-comparison/reviews/fnd05-targeted-rereview-m02-m06-finding-owner-opus.md

PROMPT_REVISION: fnd05-targeted-re-review-v2
TARGET_HEAD_SHA: 9e704f53911be3fdf0d09538424d3bcd9012f96a
PRODUCER_SLOT: finding_owner:H2
OUTPUT_BRANCH: claude/fnd05-targeted-rereview-m02-m06-opus
OUTPUT_BASE: b7911db65406debddd962ef6907a8e7c54a73186

REGISTRY_STAGE_KEY:
  stage_artifacts.targeted_re_review_m02_m06_finding_owner

HISTORICAL_TARGETED_RE_REVIEW_SLOT_CHANGED: NO

SOURCE_ARTIFACT_REFS:
  - docs/benchmarks/fnd05-model-comparison/final-synthesis/targeted-fix-m02-m06-result.md@a2e97d3baefb386a0a825a9a79e751ead4124016#sha256:53e8800472db7ba999abd713b5cc7171f6f42c96becc65f951a6924b76e40cce
  - docs/benchmarks/fnd05-model-comparison/reviews/fnd05-conditional-judge-composer-2.5.md@fb0b2f81e4817b494e2167547f537c1e774e919d#sha256:ce44323a2728f0d6ca2dde3d28040074e77d8b59c96ae65bbd528080600f64bb
  - docs/benchmarks/fnd05-model-comparison/reviews/fnd05-heavy-h2-opus-claude-opus-5-claude-code.md@4ca962b4a8f0dd9faeacc1a494ed86f919f5536a#sha256:cc0e996707f83f4b9c338b3ecc5033d0829646c0976843aec30de39b3a275425
  - run.json@b7911db65406debddd962ef6907a8e7c54a73186#sha256:44dd3b670f3ae41d39e26d0c29d9804366b62f7a1d179b51a2f53108f14f8434
  - pr:153@9e704f53911be3fdf0d09538424d3bcd9012f96a
  - github-actions-run:31515332416
  - github-actions-run:31515332435
```
