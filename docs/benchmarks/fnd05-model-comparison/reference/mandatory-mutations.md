# FND-05 Mandatory Mutation Set

Revision: `fnd05-mutations-v2`

目的は、Final Synthesisのtest / validatorが「何らかの失敗」を見るだけでなく、守るべきdefect classを実際に検出できることを確認することである。

## 1. Disclosure and execution rules

Candidateへ開示するもの:

- mutation ID
- protected contract / defect class
- test oracleが観測すべきproperty

Candidateへ実装課題として与えないもの:

- evaluator専用のexact injection recipe
- mutation patchそのもの

Exact injection mechanismはpre-runでevaluator側へlockし、candidateは既知のpatchへ過学習するのではなく、contractを守るtest / validatorを実装する。

共通実行ルール:

- mutationはcandidate開始前にdefect classとして定義する。
- Final Synthesisではmandatory mutationをすべて実行する。
- mutation前のbaseline GREENを確認する。
- 一度に1 mutationだけ入れる。
- mutation後にtarget testがREDになることを確認する。
- REDの理由がexpected defect classと一致することを確認する。
- build / syntax / missing executable等の無関係failureをkillとして数えない。
- mutationを完全に戻し、GREENへ回復することを確認する。
- `git status`、source scan、Compose/config render等でresidue 0を確認する。

## 2. M-01 — API start permission is weakened

### Protected contract

Migrator success後だけAPI startを許可する。

### Evaluator defect class

APIがMigrator successful completionを待たず、単なるservice/process start等で開始可能になるようorderingを弱める。

### Expected detection

- ordering test RED
- APIがMigrator success前にstart可能なことを外部state / ordering evidenceで検出

### Invalid kill

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

### Expected detection

- no-auto-migration regression RED
- API起動前後のmigration history / schema差を検出

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

### Expected detection

- cleanup state test RED
- **同じCompose project identityに属する**actual container / network / named volume残存を検出

他projectのresourceを誤検出しない。

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
INJECTION_ARTIFACT_REF:
BASELINE_RESULT:
MUTATED_RESULT:
EXPECTED_RED_OBSERVED: YES / NO
FAILURE_REASON_MATCHED: YES / NO
RESTORED_RESULT:
RESIDUE_CHECK:
EVIDENCE:
```

`EXPECTED_RED_OBSERVED: NO`またはfailure reason不一致はmerge-blocking Majorとして扱う。
