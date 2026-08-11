# FND-05 Mandatory Mutation Set

Revision: `fnd05-mutations-v2`

目的は、Final Synthesisのtest / validatorが「何らかの失敗」を見るだけでなく、守るべきdefect classを実際に検出できることを確認することである。

## 1. Disclosure and execution rules

Candidateへ開示するもの:

- mutation ID
- protected contract / defect class
- test oracleが観測すべきproperty
- `mutation-determinism-contract.md`で定義するdeterministic precondition property
- controlled barrier / fixture class
- expected / invalid failure signature class

Candidateへ実装課題として与えないもの:

- evaluator専用のexact injection recipe
- mutation patchそのもの
- exact source edit

Exact injection mechanismはpre-runでevaluator側へlockし、candidateは既知のpatchへ過学習するのではなく、contractを守るtest / validatorを実装する。

Mutation executionの決定性は`reference/mutation-determinism-contract.md` revision `fnd05-mutation-determinism-v1`を必須overlayとする。D-06 lockは同contractのschemaを満たさなければ成立しない。

共通実行ルール:

- mutationはcandidate開始前にdefect classとして定義する。
- mutationごとのdeterministic precondition / controlled barrier or fixture / expected failure signatureをD-06でlockする。
- preconditionが成立しないrunを`KILLED`または`SURVIVED`として数えない。
- 自然なrace、偶然のtiming、既に満たされたstate、存在しないcleanup targetに依存してkillを主張しない。
- Final Synthesisではmandatory mutationをすべて実行する。
- mutation前のbaseline GREENを確認する。
- 一度に1 mutationだけ入れる。
- mutation後にtarget testがREDになることを確認する。
- REDの理由がexpected defect class / expected failure signatureと一致することを確認する。
- build / syntax / missing executable等の無関係failureをkillとして数えない。
- mutationを完全に戻し、GREENへ回復することを確認する。
- `git status`、source scan、Compose/config render等でresidue 0を確認する。

## 2. M-01 — API start permission is weakened

### Protected contract

Migrator success後だけAPI startを許可する。

### Evaluator defect class

APIがMigrator successful completionを待たず、単なるservice/process start等で開始可能になるようorderingを弱める。

### Deterministic execution requirement

- PostgreSQL usableかつMigrator production pathへ到達可能であることを確認する。
- Migrator successful completionをcontrolled barrier / fixtureで意図的に未完了へ保持する。
- 固定sleepや自然raceだけで前後関係を作らない。
- exact barrier recipeはD-06 evaluator evidenceへ隔離する。

### Expected detection

- ordering test RED
- Migratorがsuccessful completionしていない状態でAPI startを外部state / ordering evidenceで検出

### Invalid kill

- mutation観測前にMigratorが既に完了している
- elapsed timeだけでorderingを推測する
- YAML syntax error
- build failure
- image missing

## 3. M-02 — Migrator failure becomes success

### Protected contract

Migration failureをsuccessとして扱わず、APIを開始しない。

### Evaluator defect class

Migratorのnon-zero failureをwrapper等でexit 0へ変換する。

### Expected detection

- failure test RED
- intended migration failureへ到達している
- expected non-zeroとobserved exit 0の不一致を検出
- API startが起きた場合は追加検出

## 4. M-03 — API startup auto-migration

### Protected contract

API startupはschema evolutionを行わない。

### Evaluator defect class

API startup pathへ`MigrateAsync`等のschema mutationを一時追加する。

### Deterministic execution requirement

- 実PostgreSQLを、auto-migrationが存在すれば必ずobservable migration-state deltaを作る既知のpending migration stateへ置く。
- baseline API startupではmigration history / schema stateが変わらないことを確認する。
- DBが既にlatestで何も変化しない状態をmutation kill判定に使わない。
- exact temporary fixture recipeはD-06 evaluator evidenceへ隔離する。

### Expected detection

- no-auto-migration regression RED
- baselineでは不変、mutated API startupではmigration history / schema差を検出

### Invalid kill

- source scanだけでruntime auto-migration検出とする
- APIのunrelated startup failure
- pending migration precondition未成立

## 5. M-04 — Secret enters process arguments

### Protected contract

secret valueをrepository / argv / inappropriate rendered config / logsへ露出しない。

### Evaluator defect class

test sentinelをprocess arguments等の禁止面へ意図的に展開する。

### Expected detection

- secret validator RED
- actual observation面でsentinelを検出

Real credentialは使用しない。

## 6. M-05 — Image digest is removed

### Protected contract

approved image contentをdigestで固定する。

### Evaluator defect class

PostgreSQLまたは.NET base imageをtag-onlyへ変更する。

### Expected detection

- static image policy test RED
- resolved image / Dockerfile scanでdigest absenceを検出

Registry outageだけで失敗した場合はkillにしない。

## 7. M-06 — Named database volume is replaced

### Protected contract

PostgreSQL dataをnamed volumeで保持する。

### Evaluator defect class

database data volumeをanonymous volumeまたはcontract外のstorageへ変更する。

### Expected detection

- volume policy / lifecycle test RED
- resolved configまたはactual volume identityで違反を検出

## 8. M-07 — Negative test fails before intended path

### Protected contract

Negative testは意図したcomponent / path / failure reasonへ到達する。

### Evaluator defect class

実行対象を壊し、Migrator / Compose runtime pathへ到達する前に無関係なnon-zero failureを発生させる。

例:

- executable path invalid
- required build artifact missing
- invalid CLI option

### Expected detection

- target test RED
- expected path marker / failure reason marker absenceを検出

`exit != 0`だけではPASSしない。

M-07はproduct defectではなく**oracle-quality meta mutation**として分類する。

## 9. M-08 — Migrator reports success without applying expected migration

### Protected contract

Migrator exit 0だけでは不十分で、expected migrationが実DB stateへ反映されていることを外部確認する。

### Evaluator defect class

**test / validatorは変更しない。** Runtime / Migrator execution pathだけを一時変更し、Migratorがexit 0を返す一方でexpected `InitialFoundation` migrationが`public.__EFMigrationsHistory`へ記録されない状態を作る。

Exact injection mechanismはD-06でlockする。

### Deterministic execution requirement

- mutated run前にexpected migration stateが存在しないことを確認する。
- real DB / Migrator runtime pathへ到達する。
- oracleはbaselineとmutated runで同一のまま維持する。

### Expected detection

- clean-start / migration-history oracle RED
- Migrator exitは0のまま
- expected migration row absenceまたは不一致を検出
- API stateやsuccess logだけではGREENにならない

### Invalid kill

- history assertion自体を削除 /常時failureへ変更する
- validator resultを直接書き換える
- build / YAML / CLI failureでruntime pathへ到達しない

## 10. M-09 — API starts and immediately exits

### Protected contract

Success pathではAPIがrunningであり、「never started」と「started then exited」を区別する。

### Evaluator defect class

APIをstart後すぐexitするprocessへ一時置換する。

### Expected detection

- success-path state test RED
- started / exited状態をnever-startedと誤認しない

## 11. M-10 — Clean reset leaves project resource

### Protected contract

Clean reset後、D-04で対象としたproject resourceが残存しない。

### Evaluator defect class

clean resetからvolume削除または必要なorphan cleanupを外す。

### Deterministic execution requirement

- mutation対象のcleanup責任に対応するsame-project resourceがclean reset前に実在することをmachine-readableに確認する。
- volume deletionを検証する場合は対象named volumeを実際に存在させる。
- orphan cleanupを検証する場合は対象orphan fixtureを実際に存在させる。
- 存在しないresourceのcleanup codeを削除してGREENでもmutation resultとして数えない。

### Expected detection

- cleanup state test RED
- **同じCompose project identityに属する**actual target container / network / named volume残存を検出

### Invalid kill

- mutation前からtarget resourceが存在しない
- 他projectのresourceを誤検出
- cleanup commandのunrelated failureだけでRED

## 12. Applicability

| Mutation | Candidate must design for | Final Synthesis |
| --- | --- | --- |
| M-01 | success-before-start ordering oracle | mandatory |
| M-02 | non-zero propagation / API non-start oracle | mandatory |
| M-03 | API no-auto-migration regression | mandatory |
| M-04 | secret non-disclosure oracle | mandatory |
| M-05 | digest pinning oracle | mandatory |
| M-06 | named volume / lifecycle oracle | mandatory |
| M-07 | positive path / reason markers | mandatory |
| M-08 | migration-history external assertion | mandatory |
| M-09 | running-state assertion | mandatory |
| M-10 | project-scoped resource absence assertion | mandatory |

Candidate比較時に全mutationを各3候補へ常時実行する必要はない。Evaluatorはriskに応じてcandidateへprobeできる。Final SynthesisではM-01〜M-10を原則すべて実行する。

## 13. Mutation report format

```text
MUTATION_ID:
CLASS: PRODUCT_DEFECT / ORACLE_META_DEFECT
BASELINE_HEAD:
TARGET_TEST:
PRECONDITION_RESULT:
CONTROLLED_BARRIER_OR_FIXTURE:
INJECTION_POINT_CLASS:
INJECTION_ARTIFACT_REF:
BASELINE_RESULT:
EXPECTED_FAILURE_SIGNATURE:
OBSERVED_FAILURE_SIGNATURE:
INVALID_FAILURE_MATCHED: YES / NO
MUTATED_RESULT:
EXPECTED_RED_OBSERVED: YES / NO
FAILURE_REASON_MATCHED: YES / NO
RESTORED_RESULT:
CLEANUP_RESULT:
RESIDUE_CHECK:
EVIDENCE:
```

`PRECONDITION_RESULT != PASS`、`INVALID_FAILURE_MATCHED: YES`、`EXPECTED_RED_OBSERVED: NO`、failure signature不一致のいずれかは有効なkillとして数えない。Final Synthesisのmandatory mutationで発生した場合はmerge-blocking Majorとして扱う。
