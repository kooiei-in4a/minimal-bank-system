# FND-05 Docker Compose runtime

FND-05は、PostgreSQLがusableになった後にFND-04 Migratorを一度だけ実行し、Migratorが成功して期待するmigration historyを残した場合にのみAPIを起動する。APIの通常起動はschema migrationを実行しない。

必要なhost environmentは、用途別に分離した次の3つである。

- `MBS_DATABASE_BOOTSTRAP_PASSWORD`: PostgreSQL初期管理者と、least-privilege roleを作成する一時的な`db-provisioner`だけが使用する。
- `MBS_DATABASE_MIGRATOR_PASSWORD`: one-shot Migratorだけが使用する`mbs_migrator`の資格情報。
- `MBS_DATABASE_RUNTIME_PASSWORD`: APIだけが使用する`mbs_runtime`の資格情報。

`mbs_bootstrap`はroleとgrantの初期化専用、`mbs_migrator`はschema migrationの所有者、`mbs_runtime`は通常のbusiness table操作とmigration historyのread-only参照だけを担う。bootstrap credentialはMigrator/APIへ渡さず、Migrator credentialはAPIへ渡さない。値はCompose top-level secretから必要なserviceへだけマウントされ、repository、Compose設定、コンテナ設定、ログ、process argvへ渡してはならない。いずれかのrequired secretが欠落した場合、依存serviceは起動せずfail closedする。

```bash
export MBS_DATABASE_BOOTSTRAP_PASSWORD='local-bootstrap-test-value'
export MBS_DATABASE_MIGRATOR_PASSWORD='local-migrator-test-value'
export MBS_DATABASE_RUNTIME_PASSWORD='local-runtime-test-value'

# validate
docker compose -p minimal-bank-system-fnd05 config --quiet

# rendered configuration (secret valueを含まないことを確認する)
docker compose -p minimal-bank-system-fnd05 config --format json

# clean start
docker compose -p minimal-bank-system-fnd05 up --build --detach --remove-orphans

# stop while retaining PostgreSQL data
docker compose -p minimal-bank-system-fnd05 down --remove-orphans

# restart: Migrator gateを再評価する
docker compose -p minimal-bank-system-fnd05 down --remove-orphans
docker compose -p minimal-bank-system-fnd05 up --build --detach --remove-orphans

# clean reset: canonical command直後にproject-scoped container / volume / networkがないことを確認する
docker compose -p minimal-bank-system-fnd05 down --volumes --remove-orphans
```

FND-05はhealth endpoint、business endpoint、business schema/data、backup/restore、production deploymentを提供しない。APIのrunning stateはFND-05のstartup/order evidenceであり、FND-06 health contractではない。
