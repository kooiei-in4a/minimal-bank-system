#!/usr/bin/env bash
set -Eeuo pipefail

for required_name in POSTGRES_HOST POSTGRES_PORT POSTGRES_DATABASE POSTGRES_USERNAME MBS_DATABASE_PASSWORD_FILE; do
  if [[ -z "${!required_name:-}" ]]; then
    printf 'FND-05 configuration error: %s is required.\n' "$required_name" >&2
    exit 78
  fi
done

if [[ "$POSTGRES_USERNAME" == "mbs_bootstrap" ]]; then
  printf 'FND-05 configuration error: bootstrap principal is not permitted for this process.\n' >&2
  exit 78
fi

secret_file="$MBS_DATABASE_PASSWORD_FILE"

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

exec "$@"
