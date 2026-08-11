# Docker Compose execution baseline (FND-05, Issue #43)

PostgreSQL, the FND-04 explicit Migrator, and the API run as one Compose project. The Migrator
must finish successfully before the API is allowed to start; if the Migrator fails, the API
never starts. Normal API startup never evolves the schema (ADR-0009).

```text
PostgreSQL becomes usable
  -> Migrator runs (FND-04 MinimalBankSystem.Migrator, unmodified)
     - exit 0   -> API start is permitted
     - non-zero -> API never starts
```

## Ownership

| Concern | Owner |
| :--- | :--- |
| Applying migrations | `MinimalBankSystem.Migrator` (FND-04, Issue #42) |
| Compose services, ordering, image pinning, secret injection, lifecycle | FND-05 (this directory + `compose.yaml`) |
| Health endpoint contracts | FND-06 (not implemented here) |

## Canonical project identity

```text
minimal-bank-system-fnd05
```

Declared in `compose.yaml` (`name:`); the lifecycle commands below also pass `-p` explicitly.

## Secret setup

The database password is a Compose top-level secret sourced from the host environment
(`POSTGRES_PASSWORD`). It is never committed, never a Compose file literal, and never passed as
a command-line argument. `postgres` reads it via `POSTGRES_PASSWORD_FILE`; `migrator` and `api`
read the same mounted secret file through `docker/entrypoint-with-secret.sh`, which builds
`ConnectionStrings__Database` in-process and `exec`s the application — the password never
appears in argv, logs, or rendered config.

For interactive use, export a password before running any command below:

```bash
export POSTGRES_PASSWORD="$(openssl rand -hex 32)"
```

`scripts/fnd05/*.sh` generate an ephemeral value automatically when `POSTGRES_PASSWORD` is
unset, for unattended/CI use only.

## Canonical lifecycle commands (D-04)

```bash
# validate
docker compose -p minimal-bank-system-fnd05 config --quiet

# render machine-readable config evidence
docker compose -p minimal-bank-system-fnd05 config --format json

# start / start-after-stop (clean database -> Migrator runs -> API starts on success)
docker compose -p minimal-bank-system-fnd05 up --build --detach --remove-orphans

# stop, retaining the named PostgreSQL volume
docker compose -p minimal-bank-system-fnd05 down --remove-orphans

# restart (re-evaluates the Migrator gate; `docker compose restart` is not used for this)
docker compose -p minimal-bank-system-fnd05 down --remove-orphans
docker compose -p minimal-bank-system-fnd05 up --build --detach --remove-orphans

# clean reset (removes the named volume too)
docker compose -p minimal-bank-system-fnd05 down --volumes --remove-orphans
```

Exit code `0` from a lifecycle command is not treated as sufficient evidence by itself; see the
external-state commands below.

## External state evidence (D-05)

```bash
docker compose -p minimal-bank-system-fnd05 ps -a --format json
docker inspect <container-id>
docker volume inspect minimal-bank-system-fnd05_postgres-data
docker compose -p minimal-bank-system-fnd05 logs --no-color --timestamps

# migration history, queried against the running postgres service
docker compose -p minimal-bank-system-fnd05 exec -T postgres \
  psql -U minimal_bank_system -d minimal_bank_system \
  -c 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";'
```

Success is `Migrator ExitCode == 0` + expected migration history present + `API StartedAt` not
before `Migrator FinishedAt` + API running. Failure is `Migrator ExitCode != 0` + API never
started (`docker inspect` shows `State.Status == "created"` and `State.StartedAt` at the zero
value) — a container that started and then exited is not the same as one that never started.

## Automated verification scripts

`scripts/fnd05/` implements the Completion Checks against the real Compose stack (no
source-scan-only assertions):

| Script | Checks |
| :--- | :--- |
| `validate.sh` | Static config: digest pinning, named volume, secret grant, dependency conditions |
| `digest-check.sh` | Every base image is pinned by the exact locked digest, not a bare tag |
| `clean-start.sh` | Clean volume -> Migrator exit 0 -> expected history -> API running after Migrator finished |
| `rerun.sh` | Re-running the Migrator on an already-migrated database stays success and does not duplicate history |
| `api-no-auto-migration.sh` | Restarting only the API never changes migration history or schema |
| `lifecycle.sh` | Stop retains the named volume; restart re-evaluates the Migrator gate |
| `migration-failure.sh` | An induced Migrator failure (unreachable port, via a test-only override) leaves the API never started |
| `secret-sentinel.sh` | A sentinel password is genuinely exercised end-to-end and never leaks into the repo, rendered config, logs, `docker inspect`, or `docker top` |
| `clean-reset.sh` | `down --volumes` leaves zero containers/volumes for this project |
| `run-all.sh` | Runs the full sequence in dependency order and tears down at the end |

Run everything:

```bash
bash scripts/fnd05/run-all.sh
```

`docker/compose.override.migration-failure.yaml` is a test-only fixture (an environment
override that points the Migrator at a deterministically unreachable port). It is never part of
the default `docker compose up` path and only takes effect when explicitly layered on top of
`compose.yaml`.

## Known boundaries

- No `/health/live` or `/health/ready` endpoint exists yet; API readiness is observed through
  Compose/`docker inspect` container state, not an HTTP health contract (FND-06).
- No business endpoint, schema, or seed data is added here.
- No backup/restore, monitoring, or production orchestrator is added here (out of scope for
  Issue #43).
- `docker compose restart` is deliberately not the canonical D-04 restart, because it does not
  demonstrate that the Migrator gate was re-evaluated; `api-no-auto-migration.sh` uses it only to
  isolate the API's own startup path from the Migrator.
- On a Docker host shared with other Compose projects, `docker compose down --volumes` for this
  project name only affects resources labeled for `minimal-bank-system-fnd05`; running the
  verification scripts concurrently with another process managing the same project name on the
  same Docker daemon is not supported and can produce misleading results.
