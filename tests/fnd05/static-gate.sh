#!/usr/bin/env bash
set -Eeuo pipefail

readonly project_name="${FND05_PROJECT_NAME:-minimal-bank-system-fnd05}"
readonly source_root="${FND05_SOURCE_ROOT:-$(git rev-parse --show-toplevel)}"
readonly postgres_image="postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
readonly sdk_image="mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0"
readonly runtime_image="mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b"
readonly wrapper_path="$source_root/deployment/fnd05/with-database-secret.sh"
readonly init_path="$source_root/deployment/postgres/init-roles.sh"

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

forbid_literal() {
  local literal="$1" path="$2" signature="$3"
  if grep --fixed-strings --quiet "$literal" "$path"; then
    printf 'ORACLE_SIGNATURE=%s\n' "$signature" >&2
    return 1
  fi
}

for command_name in docker git jq grep; do
  require_command "$command_name" || {
    printf 'Static gate prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

: "${MBS_BOOTSTRAP_PASSWORD:?MBS_BOOTSTRAP_PASSWORD is required for the Compose render check}"
: "${MBS_MIGRATOR_PASSWORD:?MBS_MIGRATOR_PASSWORD is required for the Compose render check}"
: "${MBS_API_PASSWORD:?MBS_API_PASSWORD is required for the Compose render check}"

[[ "$MBS_BOOTSTRAP_PASSWORD" != "$MBS_MIGRATOR_PASSWORD" &&
   "$MBS_BOOTSTRAP_PASSWORD" != "$MBS_API_PASSWORD" &&
   "$MBS_MIGRATOR_PASSWORD" != "$MBS_API_PASSWORD" ]] || {
  printf 'ORACLE_SIGNATURE=host-secrets-not-distinct\n' >&2
  exit 1
}

git -C "$source_root" -c core.autocrlf=true diff --check

require_literal "$postgres_image" "$source_root/compose.yaml" 'postgres-image-digest-missing'
require_literal "$sdk_image" "$source_root/compose.yaml" 'sdk-image-digest-missing'
require_literal "$runtime_image" "$source_root/compose.yaml" 'runtime-image-digest-missing'
require_literal 'environment: MBS_BOOTSTRAP_PASSWORD' "$source_root/compose.yaml" 'secret-environment-source-missing'
require_literal 'environment: MBS_MIGRATOR_PASSWORD' "$source_root/compose.yaml" 'secret-environment-source-missing'
require_literal 'environment: MBS_API_PASSWORD' "$source_root/compose.yaml" 'secret-environment-source-missing'
require_literal 'POSTGRES_PASSWORD_FILE: /run/secrets/bootstrap_password' "$source_root/compose.yaml" 'postgres-secret-file-configuration-missing'
require_literal 'POSTGRES_USER: mbs_bootstrap' "$source_root/compose.yaml" 'bootstrap-principal-missing'
require_literal 'POSTGRES_USERNAME: mbs_migrator' "$source_root/compose.yaml" 'migrator-principal-missing'
require_literal 'POSTGRES_USERNAME: mbs_api' "$source_root/compose.yaml" 'api-principal-missing'
require_literal 'condition: service_completed_successfully' "$source_root/compose.yaml" 'migrator-completion-gate-missing'
require_literal 'condition: service_healthy' "$source_root/compose.yaml" 'postgres-health-gate-missing'
require_literal 'CREATE ROLE mbs_migrator' "$init_path" 'bootstrap-migrator-role-missing'
require_literal 'CREATE ROLE mbs_api' "$init_path" 'bootstrap-api-role-missing'
require_literal 'NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS' "$init_path" 'privilege-ceiling-missing'
require_literal 'MBS_DATABASE_PASSWORD_FILE' "$wrapper_path" 'runtime-secret-path-unspecified'
forbid_literal '/run/secrets/database_password' "$wrapper_path" 'historical-single-credential-fallback'
forbid_literal 'environment: MBS_DATABASE_PASSWORD' "$source_root/compose.yaml" 'historical-single-credential-fallback'
forbid_literal 'source: database_password' "$source_root/compose.yaml" 'historical-single-credential-fallback'
forbid_literal 'target: database_password' "$source_root/compose.yaml" 'historical-single-credential-fallback'

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
    any(.services.postgres.volumes[]; .type == "bind" and (.target | endswith("/01-init-roles.sh"))) and
    .secrets.bootstrap_password.environment == "MBS_BOOTSTRAP_PASSWORD" and
    .secrets.migrator_password.environment == "MBS_MIGRATOR_PASSWORD" and
    .secrets.api_password.environment == "MBS_API_PASSWORD" and
    (.secrets | keys | length) == 3 and
    (.services.postgres.environment.POSTGRES_USER == "mbs_bootstrap") and
    (.services.migrator.environment.POSTGRES_USERNAME == "mbs_migrator") and
    (.services.api.environment.POSTGRES_USERNAME == "mbs_api") and
    (.services.migrator.environment.MBS_DATABASE_PASSWORD_FILE == "/run/secrets/migrator_password") and
    (.services.api.environment.MBS_DATABASE_PASSWORD_FILE == "/run/secrets/api_password") and
    (.services.migrator.secrets | length) == 1 and
    (.services.migrator.secrets[0].source == "migrator_password") and
    (.services.api.secrets | length) == 1 and
    (.services.api.secrets[0].source == "api_password") and
    (.services.postgres.secrets | length) == 3 and
    ([.services.postgres.secrets[].source] | sort) == ["api_password", "bootstrap_password", "migrator_password"] and
    ([.services.api.secrets[].source] | index("bootstrap_password") | not) and
    ([.services.migrator.secrets[].source] | index("bootstrap_password") | not) and
    ([.services.api.secrets[].source] | index("migrator_password") | not) and
    ([.services.migrator.secrets[].source] | index("api_password") | not)
  ' <<<"$rendered" >/dev/null

if git -C "$source_root" ls-files | grep --extended-regexp '(^|/)\.env($|\.[^e])|\.local$'; then
  printf 'Tracked secret-like artifact is prohibited.\n' >&2
  exit 1
fi

printf 'STATIC_GATE: PASS\n'
