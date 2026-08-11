#!/bin/bash
# FND-05 D-03: reads the mounted secret file, builds ConnectionStrings__Database from the
# secret plus non-secret parameters, exports it, then execs the real entrypoint. The secret
# value is never written to argv, stdout/stderr or any rendered file.
set -euo pipefail

: "${DB_PASSWORD_FILE:=/run/secrets/postgres_password}"
: "${DB_HOST:?DB_HOST is required}"
: "${DB_PORT:?DB_PORT is required}"
: "${DB_NAME:?DB_NAME is required}"
: "${DB_USER:?DB_USER is required}"

if [[ ! -s "${DB_PASSWORD_FILE}" ]]; then
  echo "entrypoint-with-secret: required secret file '${DB_PASSWORD_FILE}' is missing or empty; failing closed" >&2
  exit 1
fi

db_password="$(< "${DB_PASSWORD_FILE}")"

if [[ -z "${db_password}" ]]; then
  echo "entrypoint-with-secret: required secret file '${DB_PASSWORD_FILE}' contained no value; failing closed" >&2
  exit 1
fi

export ConnectionStrings__Database="Host=${DB_HOST};Port=${DB_PORT};Database=${DB_NAME};Username=${DB_USER};Password=${db_password}"
unset db_password

exec "$@"
