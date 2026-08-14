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

# AUTHN's signing key is optional for the Migrator and required only when the API issues a JWT.
# It is injected through a Compose secret and never appears in the command line.
jwt_signing_key_file="${MBS_JWT_SIGNING_KEY_FILE:-}"
if [[ -n "$jwt_signing_key_file" ]]; then
  if [[ ! -r "$jwt_signing_key_file" ]]; then
    printf 'AUTHN configuration error: JWT signing-key secret file is unavailable.\n' >&2
    exit 78
  fi

  jwt_signing_key="$(<"$jwt_signing_key_file")"
  if [[ -z "$jwt_signing_key" ]]; then
    printf 'AUTHN configuration error: JWT signing-key secret file is empty.\n' >&2
    exit 78
  fi

  export Authentication__Jwt__SigningKey="$jwt_signing_key"
  unset jwt_signing_key
fi

exec "$@"
