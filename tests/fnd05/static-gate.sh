#!/usr/bin/env bash
set -Eeuo pipefail

readonly project_name="${FND05_PROJECT_NAME:-minimal-bank-system-fnd05}"
readonly postgres_image="postgres:18.4@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"
readonly sdk_image="mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0"
readonly runtime_image="mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b"

require_command() {
  command -v "$1" >/dev/null
}

for command_name in docker git jq grep; do
  require_command "$command_name" || {
    printf 'Static gate prerequisite missing: %s\n' "$command_name" >&2
    exit 69
  }
done

: "${MBS_DATABASE_PASSWORD:?MBS_DATABASE_PASSWORD is required for the Compose render check}"

git -c core.autocrlf=true diff --check

grep --fixed-strings --quiet "$postgres_image" compose.yaml
grep --fixed-strings --quiet "$sdk_image" compose.yaml
grep --fixed-strings --quiet "$runtime_image" compose.yaml
grep --fixed-strings --quiet 'environment: MBS_DATABASE_PASSWORD' compose.yaml
grep --fixed-strings --quiet 'POSTGRES_PASSWORD_FILE: /run/secrets/database_password' compose.yaml
grep --fixed-strings --quiet 'condition: service_completed_successfully' compose.yaml
grep --fixed-strings --quiet 'condition: service_healthy' compose.yaml

if grep --recursive --include='*.cs' --include='*.csproj' --extended-regexp 'MigrateAsync|\.Migrate\(|EnsureCreated' src/MinimalBankSystem.Api; then
  printf 'API source contains a schema-evolution startup call.\n' >&2
  exit 1
fi

rendered="$(docker compose -p "$project_name" config --format json)"
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
    .secrets.database_password.environment == "MBS_DATABASE_PASSWORD" and
    (.services.migrator.secrets | length) == 1 and
    (.services.api.secrets | length) == 1
  ' <<<"$rendered" >/dev/null

if git ls-files | grep --extended-regexp '(^|/)\.env($|\.[^e])|\.local$'; then
  printf 'Tracked secret-like artifact is prohibited.\n' >&2
  exit 1
fi

printf 'STATIC_GATE: PASS\n'
