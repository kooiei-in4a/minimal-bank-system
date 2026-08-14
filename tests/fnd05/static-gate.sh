#!/usr/bin/env bash
set -Eeuo pipefail

readonly project_name="${FND05_PROJECT_NAME:-minimal-bank-system-fnd05}"
readonly source_root="${FND05_SOURCE_ROOT:-$(git rev-parse --show-toplevel)}"
readonly postgres_image="postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
readonly sdk_image="mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0"
readonly runtime_image="mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b"

require_command() {
  command -v "$1" >/dev/null
}

require_literal() {
  local literal="$1" path="$2" signature="$3"
  grep --fixed-strings --quiet "$literal" "$path" || {
    printf 'ORACLE_SIGNATURE=%s\n' "$signature" >&2
    return 1
  }
}

for command_name in docker git jq grep; do
  require_command "$command_name" || {
    printf 'Static gate prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

: "${MBS_DATABASE_BOOTSTRAP_PASSWORD:?MBS_DATABASE_BOOTSTRAP_PASSWORD is required for the Compose render check}"
: "${MBS_DATABASE_MIGRATOR_PASSWORD:?MBS_DATABASE_MIGRATOR_PASSWORD is required for the Compose render check}"
: "${MBS_DATABASE_API_PASSWORD:?MBS_DATABASE_API_PASSWORD is required for the Compose render check}"
: "${MBS_JWT_SIGNING_KEY:?MBS_JWT_SIGNING_KEY is required for the Compose render check}"

[[ "$MBS_DATABASE_MIGRATOR_PASSWORD" != "$MBS_DATABASE_API_PASSWORD" ]] || {
  printf 'ORACLE_SIGNATURE=equal-database-credential-values\n' >&2
  exit 1
}

git -C "$source_root" -c core.autocrlf=true diff --check

require_literal "$postgres_image" "$source_root/compose.yaml" 'postgres-image-digest-missing'
require_literal "$sdk_image" "$source_root/compose.yaml" 'sdk-image-digest-missing'
require_literal "$runtime_image" "$source_root/compose.yaml" 'runtime-image-digest-missing'
require_literal 'environment: MBS_DATABASE_BOOTSTRAP_PASSWORD' "$source_root/compose.yaml" 'bootstrap-secret-environment-source-missing'
require_literal 'environment: MBS_DATABASE_MIGRATOR_PASSWORD' "$source_root/compose.yaml" 'migrator-secret-environment-source-missing'
require_literal 'environment: MBS_DATABASE_API_PASSWORD' "$source_root/compose.yaml" 'api-secret-environment-source-missing'
require_literal 'environment: MBS_JWT_SIGNING_KEY' "$source_root/compose.yaml" 'jwt-secret-environment-source-missing'
require_literal 'POSTGRES_PASSWORD_FILE: /run/secrets/database_bootstrap_password' "$source_root/compose.yaml" 'postgres-secret-file-configuration-missing'
require_literal 'Authentication__Jwt__SigningKeyFile: /run/secrets/jwt_signing_key' "$source_root/compose.yaml" 'jwt-secret-file-configuration-missing'
require_literal 'target: jwt_signing_key' "$source_root/compose.yaml" 'jwt-secret-target-missing'
require_literal 'condition: service_completed_successfully' "$source_root/compose.yaml" 'migrator-completion-gate-missing'
require_literal 'condition: service_healthy' "$source_root/compose.yaml" 'postgres-health-gate-missing'

if grep --recursive --include='*.cs' --include='*.csproj' --extended-regexp 'MigrateAsync|\.Migrate\(|EnsureCreated' "$source_root/src/MinimalBankSystem.Api"; then
  printf 'API source contains a schema-evolution startup call.\n' >&2
  exit 1
fi

rendered="$(docker compose --project-directory "$source_root" -p "$project_name" -f "$source_root/compose.yaml" config --format json)"
if ! jq --exit-status \
  'any(.services.postgres.volumes[]; .type == "volume" and .source == "postgres_data" and .target == "/var/lib/postgresql")' \
  <<<"$rendered" >/dev/null; then
  printf 'ORACLE_SIGNATURE=named-volume-policy-violation\n' >&2
  exit 1
fi

jq --exit-status \
  --arg postgres_image "$postgres_image" \
  --arg sdk_image "$sdk_image" \
  --arg runtime_image "$runtime_image" \
  '
    .services.postgres.image == $postgres_image and
    .services.migrator.build.args.SDK_IMAGE == $sdk_image and
    .services.migrator.build.args.RUNTIME_IMAGE == $runtime_image and
    .services.api.build.args.SDK_IMAGE == $sdk_image and
    .services.api.build.args.RUNTIME_IMAGE == $runtime_image and
    any(.services.postgres.volumes[]; .type == "volume" and .source == "postgres_data" and .target == "/var/lib/postgresql") and
    .secrets.database_bootstrap_password.environment == "MBS_DATABASE_BOOTSTRAP_PASSWORD" and
    .secrets.database_migrator_password.environment == "MBS_DATABASE_MIGRATOR_PASSWORD" and
    .secrets.database_api_password.environment == "MBS_DATABASE_API_PASSWORD" and
    (.services.migrator.secrets | length) == 1 and
    (.services.api.secrets | length) == 2 and
    any(.services.api.secrets[]; .source == "database_api_password" and .target == "database_password") and
    any(.services.api.secrets[]; .source == "jwt_signing_key" and .target == "jwt_signing_key") and
    (.services.postgres.secrets | map(.source) | index("jwt_signing_key")) == null and
    (.services.migrator.secrets | map(.source) | index("jwt_signing_key")) == null
  ' <<<"$rendered" >/dev/null

# WP2-DB-01: the bootstrap, Migrator and API runtime principals and credentials must all be
# distinct, and the bootstrap credential must never be wired into the Migrator or API service.
jq --exit-status '
    .services.migrator.secrets[0].source == "database_migrator_password" and
    .services.migrator.secrets[0].target == "database_password" and
    .services.api.secrets[0].source == "database_api_password" and
    .services.api.secrets[0].target == "database_password" and
    .services.migrator.secrets[0].source != .services.api.secrets[0].source and
    (.services.postgres.secrets | map(.source) | sort) ==
      ["database_api_password", "database_bootstrap_password", "database_migrator_password"] and
    ([.services.migrator.secrets[0].source, .services.api.secrets[0].source] | all(. as $s | (["database_bootstrap_password"] | index($s)) == null))
  ' <<<"$rendered" >/dev/null || {
  printf 'ORACLE_SIGNATURE=credential-boundary-not-distinct\n' >&2
  exit 1
}

# WP2-DB-01: the Migrator and API runtime must authenticate as distinct, least-privilege
# PostgreSQL principals, both separate from the bootstrap/provisioning principal.
jq --exit-status '
    .services.postgres.environment.POSTGRES_USER == "minimal_bank_bootstrap" and
    .services.migrator.environment.POSTGRES_USERNAME == "minimal_bank_migrator" and
    .services.api.environment.POSTGRES_USERNAME == "minimal_bank_api" and
    (
      [.services.postgres.environment.POSTGRES_USER,
       .services.migrator.environment.POSTGRES_USERNAME,
       .services.api.environment.POSTGRES_USERNAME] | unique | length
    ) == 3
  ' <<<"$rendered" >/dev/null || {
  printf 'ORACLE_SIGNATURE=principal-boundary-not-distinct\n' >&2
  exit 1
}

if git -C "$source_root" ls-files | grep --extended-regexp '(^|/)\.env($|\.[^e])|\.local$'; then
  printf 'Tracked secret-like artifact is prohibited.\n' >&2
  exit 1
fi

printf 'STATIC_GATE: PASS\n'
