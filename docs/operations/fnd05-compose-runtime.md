# FND-05 Docker Compose runtime

FND-05は、PostgreSQLがusableになった後にFND-04 Migratorを一度だけ実行し、Migratorが成功して期待するmigration historyを残した場合にのみAPIを起動する。APIの通常起動はschema migrationを実行しない。

必要なhost environmentは`MBS_DATABASE_PASSWORD`だけである。値はCompose top-level secretから必要なserviceへだけマウントされ、repository、Compose設定、コンテナ設定、ログ、process argvへ渡してはならない。

```bash
export MBS_DATABASE_PASSWORD='local-test-value'

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
