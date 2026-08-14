#!/usr/bin/env bash
set -Eeuo pipefail

secret_file="${MBS_DATABASE_PASSWORD_FILE:-/run/secrets/database_password}"

for required_name in POSTGRES_HOST POSTGRES_PORT POSTGRES_DATABASE POSTGRES_USERNAME; do
  if [[ -z "${!required_name:-}" ]]; then
    printf 'FND-05 configuration error: %s is required.\n' "$required_name" >&2
    exit 78
  fi
done

if [[ ! -r "$secret_file" ]]; then
  printf 'FND-05 configuration error: database secret file is unavailable.\n' >&2
  exit 78
fi

database_password="$(<"$secret_file")"
if [[ -z "$database_password" ]]; then
  printf 'FND-05 configuration error: database secret file is empty.\n' >&2
  exit 78
fi

export ConnectionStrings__Database="Host=${POSTGRES_HOST};Port=${POSTGRES_PORT};Database=${POSTGRES_DATABASE};Username=${POSTGRES_USERNAME};Password=${database_password};Pooling=false"
unset database_password

# WP2-AUTHN-01: the JWT signing key is optional at this shared entrypoint because only the API
# service mounts it; the Migrator has no JWT concern. When present, it is injected the same way
# as the database password: read from a mounted secret file into a private environment variable,
# never passed as a command-line argument.
jwt_signing_key_file="${MBS_JWT_SIGNING_KEY_FILE:-/run/secrets/jwt_signing_key}"
if [[ -r "$jwt_signing_key_file" ]]; then
  jwt_signing_key="$(<"$jwt_signing_key_file")"
  if [[ -z "$jwt_signing_key" ]]; then
    printf 'FND-05 configuration error: JWT signing key secret file is empty.\n' >&2
    exit 78
  fi
  export MBS_JWT_SIGNING_KEY="$jwt_signing_key"
  unset jwt_signing_key
fi

exec "$@"
