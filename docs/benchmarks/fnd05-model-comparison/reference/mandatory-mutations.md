# FND-05 Mandatory Mutation Set

Revision: `fnd05-mutations-v1`

目的は、Final Synthesisのtest / validatorが「何らかの失敗」を見るだけでなく、守るべきdefect classを実際に検出できることを確認することである。

## 1. Rules

- mutationはcandidate開始前に定義する。
- candidateへmutation内容の答えを与えるのではなく、守るcontractと必要なtest oracleを共通promptへ与える。
- Final Synthesisではapplicable mutationをすべて実行する。
- mutationは一度に1つだけ入れる。
- mutation前のbaseline GREENを確認する。
- mutation後にtarget testがREDになることを確認する。
- mutationを完全に戻し、baseline GREENへ回復することを確認する。
- `git status`、source scan、Compose renderでresidue 0を確認する。
- mutation failureの原因が別のbuild / syntax errorでないことを確認する。

## 2. M-01 — API waits only for Migrator start

### Defect

APIのdependency conditionをMigrator successful completionから単なるservice startへ弱める。

### Protected contract

Migrator exit 0後だけAPIを開始する。

### Expected detection

- startup-order testがRED
- API started timestampがMigrator finished timestampより前、またはMigrator running中にAPIがstartしたことを検出

### Positive markers

- mutation後もCompose configはvalid
- Migrator processへ実際に到達
- API container stateを外部観測

### Invalid detection

- YAML syntax error
- build failure
- missing image

## 3. M-02 — Migrator failure becomes exit 0

### Defect

wrapperまたはentrypointでMigrator non-zeroを0へ変換する。

### Protected contract

failureをsuccessとして扱わず、APIを開始しない。

### Expected detection

- failure-injection testがRED
- expected non-zeroとobserved exit 0の不一致を検出
- API startが発生した場合は追加で検出

### Positive markers

- invalid credential / malformed history等の実failureへ到達
- Migrator error markerあり

## 4. M-03 — API startup auto-migration

### Defect

API startup pathへ`MigrateAsync`または同等schema mutationを一時追加する。

### Protected contract

API startupはschemaを進化させない。

### Expected detection

- no-auto-migration regressionがRED
- API起動前後のmigration history / schema差を検出

### Positive markers

- API hostが実際にstart
- real PostgreSQLへ接続

## 5. M-04 — Secret is expanded into command arguments

### Defect

password sentinelをCompose `command:`またはentrypoint argvへ展開する。

### Protected contract

secretをrepository / rendered config / process args / logsへ露出しない。

### Expected detection

- secret validatorがRED
- process argsまたはrendered configでsentinel検出

### Positive markers

- sentinelがtest inputとして設定済み
- scan対象がactual container / rendered config

### Safety

- real credentialを使用しない
- test-only sentinelを使用する

## 6. M-05 — Image digest removed

### Defect

PostgreSQLまたは.NET base imageをtag-onlyへ変更する。

### Protected contract

approved image contentをdigestで固定する。

### Expected detection

- static image policy testがRED
- resolved image / Dockerfile FROM scanでdigest absenceを検出

### Invalid detection

- registry outageだけで失敗すること

## 7. M-06 — Named volume replaced

### Defect

PostgreSQL data volumeをanonymous volumeまたはtemporary bindへ変更する。

### Protected contract

normal lifecycleでnamed volumeを保持し、clean resetだけが削除する。

### Expected detection

- static volume policy testまたはexisting-volume rerun testがRED
- resolved Compose configでnamed volume contract違反を検出

## 8. M-07 — Test fails before intended path

### Defect

failure testの実行対象を壊し、Migrator / Compose pathへ到達する前に無関係なerrorでnon-zeroにする。

例:

- executable pathを無効化
- build artifactを意図的に欠落
- invalid CLI option

### Protected contract

negative testは意図したcomponent / failure reasonへ到達する。

### Expected detection

- testがRED
- expected path marker / failure marker absenceを検出

### Required assertion

`exit != 0`だけではPASSしない。

## 9. M-08 — Migration history ignored

### Defect

Migrator exit 0だけを見て、`__EFMigrationsHistory`確認を削除または常にsuccessへする。

### Protected contract

migrationの実適用を外部状態で確認する。

### Expected detection

- clean-start testがRED
- expected migration row absenceまたはduplicateを検出

## 10. M-09 — API container starts and immediately exits

### Defect

API commandを短時間でexit 0するcommandへ置き換える、またはAPI processを起動後すぐ終了させる。

### Protected contract

「API非起動」と「一度起動して終了」を区別し、success pathではAPIがrunningである。

### Expected detection

- success-path state testがRED
- API state `exited`を検出

## 11. M-10 — Clean reset leaves resource

### Defect

clean resetからvolume削除またはorphan cleanupを外す。

### Protected contract

clean reset後にcontainer / network / named volumeが残らない。

### Expected detection

- cleanup state testがRED
- actual Docker resourceの残存を検出

## 12. Applicability

| Mutation | Candidate expectation | Final Synthesis |
| --- | --- | --- |
| M-01 | testable ordering evidenceを作る | mandatory |
| M-02 | non-zero propagation evidenceを作る | mandatory |
| M-03 | FND-04 regression維持 | mandatory |
| M-04 | secret-safe design / validator | mandatory |
| M-05 | static pinning test | mandatory |
| M-06 | named volume test | mandatory |
| M-07 | positive path / reason markers | mandatory |
| M-08 | migration history assertion | mandatory |
| M-09 | running state assertion | mandatory |
| M-10 | resource absence assertion | mandatory |

candidate比較時に全mutationを各3候補へ実行する必要はない。Evaluatorはriskに応じてcandidateへprobeを行える。Final Synthesisでは10件すべてを原則実行する。

## 13. Mutation report format

```text
MUTATION_ID:
BASELINE_HEAD:
TARGET_TEST:
INJECTED_CHANGE:
BASELINE_RESULT:
MUTATED_RESULT:
EXPECTED_RED_OBSERVED: YES / NO
FAILURE_REASON_MATCHED: YES / NO
RESTORED_RESULT:
RESIDUE_CHECK:
EVIDENCE:
```

`EXPECTED_RED_OBSERVED: NO`はmerge-blocking Majorとして扱う。
