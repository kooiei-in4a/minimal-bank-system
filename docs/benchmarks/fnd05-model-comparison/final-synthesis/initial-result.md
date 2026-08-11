# FND-05 Curated Final Synthesis — Initial Result

## Execution identity

- Model: GPT-5.6 Terra
- Harness: Codex
- Effort: xHigh
- Context: Fresh Context
- Identity source: external harness selection; operator attested

## Immutable inputs and implementation target

- Base: `ee8abbb15758c1a2cfb624791482b755be578da2`
- Pre-execution head: `3344c9025c2f0b2cf1dc1baa685fb872fcb44120`
- Implementation head: `32777b7a7fc67ff94e66eacd85b26694b09ddb61`
- Selection: `selection-adjudication.md@3d3ec6f93bf4bea448f8413f711018f24c50c38d`, SHA-256 `547e0fe8d1171932b62bc1d8ea9ba4dd0f2ff23d888acafb9006a60c351797ee`
- Evaluation: `implementation-evaluation-gpt-5.6-sol-codex-xhigh-attempt-1.md@43a49e8a06c544d0810cfc3ff6de3c722ab334f9`, SHA-256 `15d96bf366b4f1fe9bd766806badf5e114e45e9a62ba27bfab04e76bc20a04cd`
- Candidate references inspected only: PR #150 (`c3599c9bd4bc920b5c87c80148d81b8a53aa95fc`), PR #151 (`146ea92a4e815a5a08fe81562ef80f70f80c551b`), and PR #152 (`b69910dd00bca56254f3340fd7f5954da38b2814`). No candidate, evaluation, or selection commit was merged or cherry-picked.

## Delivered runtime design

- `compose.yaml` defines the fixed `minimal-bank-system-fnd05` project, Postgres, one-shot migrator, and API dependency order.
- The fixed Postgres, SDK, and ASP.NET runtime image digests and `linux/amd64` platform are declared in the Compose and Dockerfile paths.
- The host secret is a top-level Compose environment secret, mounted only into Postgres, migrator, and API. Postgres consumes `POSTGRES_PASSWORD_FILE`; the two application containers build `ConnectionStrings__Database` internally in the shared wrapper, then `exec` the intended .NET process.
- The ordinary API startup performs no schema migration. A failed migration emits the positive failure marker and prevents API start.
- The operations guide defines clean start, retained down/restart, and the canonical `down --volumes --remove-orphans` reset.

## Local validation evidence

- `dotnet build MinimalBankSystem.slnx`: PASS (0 warnings, 0 errors)
- Non-Postgres automated tests: PASS (42 tests)
- PostgreSQL automated tests: PASS (23 tests)
- `dotnet ef migrations has-pending-model-changes`: PASS (no pending model changes)
- `git diff --cached --check`: PASS
- `tests/fnd05/static-gate.sh`: PASS
- `tests/fnd05/verify-compose.sh`: PASS — clean start, external state inspection, retained restart, canonical reset residue assertion, and isolated migration failure path
- `tests/fnd05/verify-mutations.sh`: PASS — all mandatory mutations M-01 through M-10 executed against isolated projects and cleaned up

## Mandatory mutation report

| ID | Injected defect | Oracle | Result |
| --- | --- | --- | --- |
| M-01 | API dependency weakened so it can race the migrator | Actual start order / API listener state | Red signal observed; baseline order remains enforced |
| M-02 | Migrator exit failure masked after a failed migration | Failure marker and non-zero exit contract | Red signal observed; baseline fails closed |
| M-03 | API startup changed to run migrations | Pending-migration listener/state observation | Red signal observed; baseline API does not migrate |
| M-04 | Secret placed in process arguments | `docker top` argv inspection | Red signal observed; baseline has no secret in argv |
| M-05 | Digest-pinned image changed to tag-only form | Static image-reference gate | Red signal observed; baseline is digest-pinned |
| M-06 | Named database volume changed to anonymous | Rendered Compose volume inspection | Red signal observed; baseline uses the named retained volume |
| M-07 | Wrapper precondition disabled before executable startup | Positive failure-marker observation | Red signal observed; baseline emits the marker and prevents unsafe start |
| M-08 | Migrator made successful without applying migration | Migration-history inspection | Red signal observed; baseline history contains the required migration |
| M-09 | API allowed to start and then die after migration | Listener/process lifecycle distinction | Red signal observed; baseline success requires a running API, not merely prior start |
| M-10 | Reset changed to omit volume removal | External project-volume residue inspection | Red signal observed; canonical baseline reset removes the volume |

## CI and review boundary

The implementation and this result lock are committed on the designated draft-PR branch. Direct-head GitHub Actions verification is required after push. PR #153 remains draft; no Ready for review action, merge, candidate merge/cherry-pick, main update, or Issue #43 closure is performed by this synthesis.

## Known concerns

- The FND-05 deployment relies on Docker Engine/Compose availability and uses a caller-provided `MBS_DATABASE_PASSWORD`; no secret value is recorded in repository files, logs, environment dumps, or process arguments.
- CI evidence is intentionally recorded outside this initial result once the pushed direct head has completed.
