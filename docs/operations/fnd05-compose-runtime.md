# FND-05 Docker Compose runtime

FND-05は、PostgreSQLがusableになった後にFND-04 Migratorを一度だけ実行し、Migratorが成功して期待するmigration historyを残した場合にのみAPIを起動する。APIの通常起動はschema migrationを実行しない。

PostgreSQL role作成、初期GRANT、およびdefault privilegesはbootstrap / provisioning authority（`mbs_bootstrap`）が所有する。通常のMigratorとAPI runtimeにはbootstrap credentialを渡さない。

必要なhost environmentは次の3つであり、互いに異なる値でなければならない。

- `MBS_BOOTSTRAP_PASSWORD` — bootstrap / `POSTGRES_USER` 初期化専用
- `MBS_MIGRATOR_PASSWORD` — Migrator principal `mbs_migrator`
- `MBS_API_PASSWORD` — API runtime principal `mbs_api`

値はCompose top-level secretから必要なserviceへだけマウントされ、repository、Compose設定、コンテナ設定、ログ、process argvへ渡してはならない。historicalな単一secret `MBS_DATABASE_PASSWORD` / `/run/secrets/database_password` へfallbackしてはならない。

```bash
export MBS_BOOTSTRAP_PASSWORD='local-bootstrap-test-value'
export MBS_MIGRATOR_PASSWORD='local-migrator-test-value'
export MBS_API_PASSWORD='local-api-test-value'

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
