#!/usr/bin/env bash
# Reads the Compose-mounted database password secret and exports
# ConnectionStrings__Database before exec'ing the application process.
# Secret values are never placed on argv.
set -euo pipefail

readonly password_file="${MBS_DB_PASSWORD_FILE:-/run/secrets/database_password}"
readonly host="${MBS_DB_HOST:?MBS_DB_HOST is required}"
readonly port="${MBS_DB_PORT:?MBS_DB_PORT is required}"
readonly database="${MBS_DB_NAME:?MBS_DB_NAME is required}"
readonly username="${MBS_DB_USER:?MBS_DB_USER is required}"

if [[ ! -r "${password_file}" ]]; then
  echo "database password secret file is missing or unreadable: ${password_file}" >&2
  exit 1
fi

password="$(tr -d '\r\n' <"${password_file}")"
if [[ -z "${password}" ]]; then
  echo "database password secret file is empty: ${password_file}" >&2
  exit 1
fi

export ConnectionStrings__Database="Host=${host};Port=${port};Database=${database};Username=${username};Password=${password}"
unset password

exec "$@"
