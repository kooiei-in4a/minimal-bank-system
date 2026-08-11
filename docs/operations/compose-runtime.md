# Compose runtime lifecycle (FND-05 / Issue #43)

Canonical Compose project identity:

```text
minimal-bank-system-fnd05
```

Supply the database password through the host environment variable consumed by the
Compose top-level secret (D-03). Do not commit real credentials.

```bash
export MBS_DATABASE_PASSWORD='replace-me'
```

## Validate

```bash
docker compose -p minimal-bank-system-fnd05 config --quiet
docker compose -p minimal-bank-system-fnd05 config --format json
```

## Start

```bash
docker compose -p minimal-bank-system-fnd05 up --build --detach --remove-orphans
```

Ordering contract:

1. PostgreSQL becomes usable (`service_healthy` / `pg_isready`).
2. The FND-04 Migrator runs once and must exit 0.
3. The API starts only after Migrator success (`service_completed_successfully`).

## Stop while retaining database data

```bash
docker compose -p minimal-bank-system-fnd05 down --remove-orphans
```

## Restart with migration-gate re-evaluation

```bash
docker compose -p minimal-bank-system-fnd05 down --remove-orphans
docker compose -p minimal-bank-system-fnd05 up --build --detach --remove-orphans
```

`docker compose restart` is not the canonical restart path for migration-gate evidence.

## Clean reset

```bash
docker compose -p minimal-bank-system-fnd05 down --volumes --remove-orphans
```

Verify absence with `docker compose ... ps -a --format json` and
`docker volume ls` / `docker volume inspect` for the project named volume.
Command exit code 0 alone is not sufficient evidence.

## External observation (D-05)

```bash
docker compose -p minimal-bank-system-fnd05 ps -a --format json
docker inspect <container-id>
docker volume inspect <volume>
docker compose -p minimal-bank-system-fnd05 logs --no-color --timestamps
```

Migration history:

```sql
SELECT "MigrationId"
FROM public."__EFMigrationsHistory"
ORDER BY "MigrationId";
```
