# FND-05 Mutation Determinism Contract

Revision: `fnd05-mutation-determinism-v1`

Status: **PRE-RUN DRAFT / D-06 LOCK REQUIRED / IMPLEMENTATION PROHIBITED**

このcontractは`mandatory-mutations.md`のdefect classを置き換えない。各mutationを「偶然ではなく、狙った欠陥として確実に発火・観測できる」状態へするためのD-06 execution contractである。

対象Finding: `FND05-PSR-005`

## 1. Purpose

Mutation killは、単にmutated runがREDになっただけでは成立しない。

```text
baseline GREEN
→ deterministic precondition成立を確認
→ controlled barrier / fixtureを成立
→ defectを注入
→ expected failure signatureでRED
→ defectをrevert
→ GREEN
→ cleanup / residue 0
```

自然なrace、偶然のtiming、既に満たされているstate、存在しないcleanup target、unrelated build / YAML / CLI failureをmutation killとして数えない。

## 2. Candidate disclosure boundary

Candidateへ開示するもの:

- mutation ID
- protected contract / defect class
- observable property
- deterministic precondition property
- controlled barrier / fixture class
- expected failure signature class
- invalid failure signature class

Candidateへ開示しないもの:

- evaluator専用のexact patch
- exact source edit
- exact evaluator-only injection recipe

Candidateは既知patchへの適合ではなく、上記propertyを検出できるtest / validatorを設計する。

## 3. D-06 lock schema

D-06は各applicable mutationについて最低限次をlockする。

```yaml
MUTATION_ID:
PRECONDITION_PROPERTY:
CONTROLLED_BARRIER_OR_FIXTURE_CLASS:
INJECTION_POINT_CLASS:
EXPECTED_FAILURE_SIGNATURE:
INVALID_FAILURE_SIGNATURES:
CLEANUP_REQUIREMENT:
RESIDUE_CHECK:
EVIDENCE_REFS:
```

`PRECONDITION_PROPERTY`または`EXPECTED_FAILURE_SIGNATURE`を一次証拠で確認できないmutationは、`KILLED` / `SURVIVED`ではなく`BLOCKED — PRECONDITION NOT ESTABLISHED`とする。

## 4. M-01 — Ordering weaken determinism

### Protected contract

Migrator successful completion後だけAPI startを許可する。

### Required precondition property

PostgreSQLはusableであり、Migrator production pathへ到達できる一方、Migrator successful completionをevaluatorが意図的に未完了状態へ保持できること。

### Controlled barrier / fixture class

Test-only / evaluator-onlyのcontrolled barrierでMigrator completionを保持する。固定sleepや自然なraceだけに依存しない。

### Injection point class

API start permissionをMigrator successful completion待ちから、service/process start等の弱い条件へ変更するruntime / Compose execution-path mutation。

### Expected failure signature

Migratorがまだsuccessful completionしていないことを外部証拠で確認した状態で、API startが観測されるためordering oracleがREDになる。

### Invalid failure signatures

- mutation観測前にMigratorが既に完了していた
- elapsed timeだけで前後関係を推測した
- YAML / build / image / CLI failure
- APIが別理由で起動不能だっただけ

## 5. M-03 — API auto-migration determinism

### Protected contract

通常API startupはschema evolutionを行わない。

### Required precondition property

実PostgreSQLを、API startup auto-migrationが存在すれば必ずobservable migration-state deltaを発生させる既知のpending migration stateへ置けること。

### Controlled barrier / fixture class

Evaluator-onlyのisolated DB / migration fixtureを使用し、baseline API startupではmigration history / schema stateが変化しないことを先に確認する。

### Injection point class

API startup pathへ`MigrateAsync`等のschema evolution処理を一時注入する。

### Expected failure signature

同じpreconditionから、baseline API startupではmigration state不変、mutated API startupではmigration historyまたはschema stateが変化し、no-auto-migration oracleがREDになる。

### Invalid failure signatures

- DBが既にlatestでobservable deltaが起きない
- source scanだけでauto-migrationを検出したことにする
- APIが別理由で起動失敗
- temporary fixtureがproduction成果物へ残る

## 6. M-08 — Success without migration state

### Protected contract

Migrator exit 0だけではなくexpected migration stateを外部確認する。

### Required precondition property

実DBへ到達可能で、mutated Migrator実行前にはexpected `InitialFoundation` stateが存在しないことを確認できる。

### Controlled barrier / fixture class

Test / validatorを変更せず、runtime / Migrator execution pathだけを一時変更してexit 0のままexpected migration stateを作らない。

### Expected failure signature

Migrator exit 0かつexpected migration row不在をunchanged oracleが検出してREDになる。

### Invalid failure signatures

- history assertion / validator自体を変更
- build / YAML / CLI failure
- Migrator runtime pathへ未到達

## 7. M-10 — Cleanup determinism

### Protected contract

Clean reset後、D-04で対象としたsame Compose project identityのresourceが残存しない。

### Required precondition property

mutation対象のcleanup責任に対応するproject-scoped resourceが、clean reset前に実在することを確認する。

### Controlled barrier / fixture class

- volume deletionを検証する場合: 対象named volumeを実際に作成してproject identityを確認する
- orphan cleanupを検証する場合: 対象orphan resourceをevaluator fixtureとして実在させる
- container / network cleanupを検証する場合: 対象resourceの存在をmachine-readableに確認する

### Injection point class

存在確認済みresourceに対応するclean-reset deletion / orphan cleanup責任だけを一時的に外す。

### Expected failure signature

Clean reset実行後、同じCompose project identityに属するtarget resourceの残存を外部stateで検出してREDになる。

### Invalid failure signatures

- mutation前からtarget resourceが存在しなかった
- 他projectのresourceを誤検出
- cleanup command自体のunrelated failureだけでRED
- mutationとは無関係なresource残存

## 8. Other mutations

M-02 / M-04 / M-05 / M-06 / M-07 / M-09もD-06 lock時にSection 3のschemaを持つ。

上記4 mutationほどの既知のdeterminism riskがなくても、precondition未確認・expected failure signature不明のままkillとして数えない。

## 9. Mutation report requirement

Final Synthesis / evaluator evidenceでは最低限次を記録する。

```text
MUTATION_ID:
PRECONDITION_RESULT:
CONTROLLED_BARRIER_OR_FIXTURE:
INJECTION_POINT_CLASS:
BASELINE_RESULT:
EXPECTED_FAILURE_SIGNATURE:
OBSERVED_FAILURE_SIGNATURE:
INVALID_FAILURE_MATCHED: YES / NO
EXPECTED_RED_OBSERVED: YES / NO
RESTORED_RESULT:
CLEANUP_RESULT:
RESIDUE_CHECK:
EVIDENCE:
```

`PRECONDITION_RESULT != PASS`、`INVALID_FAILURE_MATCHED = YES`、failure signature不一致のいずれかは有効なkillとして数えない。

## 10. Lock boundary

このcontractはD-06の**lock要件**を定義するだけであり、D-06の実値やexact evaluator injection recipeをここでは確定しない。

- D-01〜D-08は引き続きTO_LOCK
- Issue Readyは未実施
- Koo start authorizationは未付与
- FND-05 implementationは禁止
