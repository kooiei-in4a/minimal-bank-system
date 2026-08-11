# Docker Compose execution

FND-05 owns the local and closed-environment execution path. The normal API
startup does not apply EF Core migrations; the dedicated FND-04 Migrator must
complete successfully before Compose permits the API to start.

## Runtime contract

The root `compose.yaml` has three runtime roles:

- `postgres`: PostgreSQL 18.4 with the locked digest and a named `postgres_data`
  volume. Its healthcheck is the readiness gate.
- `migrator`: the production `MinimalBankSystem.Migrator` executable. It is a
  one-shot service and depends on `postgres` being healthy.
- `api`: the normal ASP.NET Core host. It depends on the Migrator completing
  successfully, so a non-zero Migrator exit does not permit API startup.

The database password is supplied only by the host environment variable
`MBS_DATABASE_PASSWORD`. Compose exposes it as the top-level
`database_password` secret, with an explicit grant to each service that needs
it. The .NET services read the mounted file in
`docker/read-secret-and-exec.sh`, build `ConnectionStrings__Database` in the
wrapper process, and `exec` the application. The secret is never a command
argument, committed literal, rendered Compose value, or log message.

## Canonical lifecycle

The benchmark project identity is `minimal-bank-system-fnd05`.

```bash
export MBS_DATABASE_PASSWORD="$(openssl rand -hex 32)"

docker compose -p minimal-bank-system-fnd05 config --quiet
docker compose -p minimal-bank-system-fnd05 config --format json
docker compose -p minimal-bank-system-fnd05 up --build --detach --remove-orphans
docker compose -p minimal-bank-system-fnd05 down --remove-orphans

# Restart re-evaluates the Migrator gate.
docker compose -p minimal-bank-system-fnd05 down --remove-orphans
docker compose -p minimal-bank-system-fnd05 up --build --detach --remove-orphans

docker compose -p minimal-bank-system-fnd05 down --volumes --remove-orphans
```

Normal `down --remove-orphans` retains the named database volume. Clean reset
explicitly removes it. Cleanup is verified through Compose/Docker state; an
exit code of zero alone is not treated as proof of absence.

## Reproducible evidence

The required external evidence commands are:

```bash
docker compose -p minimal-bank-system-fnd05 ps -a --format json
docker inspect <container-id>
docker volume inspect <volume>
docker compose -p minimal-bank-system-fnd05 config --format json
docker compose -p minimal-bank-system-fnd05 logs --no-color --timestamps

docker compose -p minimal-bank-system-fnd05 exec -T postgres \
  psql -U minimal_bank_system -d minimal_bank_system -Atc \
  'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";'
```

On a successful run, evidence must show Migrator exit `0`, the
`InitialFoundation` history row, API `StartedAt` not earlier than Migrator
`FinishedAt`, and API `running`. On a failure probe, evidence must show a
non-zero Migrator exit and an API that was never started; a started-then-exited
API is a failure, not a successful never-started assertion.

## Verification and mutation protocol

Run the complete external validator with a test-only sentinel password:

```bash
export MBS_DATABASE_PASSWORD="FND05_LOCAL_SENTINEL_$(openssl rand -hex 16)"
bash scripts/fnd05/verify-compose.sh
```

The validator exercises clean start, migration history, API ordering, secret
non-disclosure, restart with retained data, clean reset, and an isolated
Migrator connection-failure probe. The failure fixture overrides only the
database port and remains outside the production default Compose path.

For M-01 through M-10 evaluator probes, establish the locked deterministic
precondition and controlled barrier/fixture first, then run exactly one
mutation. A valid result is:

```text
baseline GREEN
-> deterministic precondition PASS
-> expected defect injected
-> expected failure signature RED
-> mutation restored
-> GREEN
-> Compose project / volume / network residue 0
```

Natural races, fixed sleeps, unrelated build/YAML/CLI failures, missing
fixtures, and an exit code without the expected external state are invalid
mutation kills.

When multiple independent probes share a Docker host, set `FND05_PROJECT` and
`FND05_FAILURE_PROJECT` to unique project names. The default remains the
canonical benchmark identity above; mutation probes must always use unique
project identities.
