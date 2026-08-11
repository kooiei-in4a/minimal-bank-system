#!/usr/bin/env bash
set -Eeuo pipefail

secret_file="${MBS_DATABASE_PASSWORD_FILE:-/run/secrets/database_password}"

if [[ ! -r "$secret_file" ]]; then
    printf '%s\n' 'Required database secret is unavailable; refusing to start.' >&2
    exit 78
fi

database_password="$(<"$secret_file")"
if [[ -z "$database_password" ]]; then
    printf '%s\n' 'Required database secret is empty; refusing to start.' >&2
    exit 78
fi

: "${MBS_DB_HOST:?MBS_DB_HOST is required}"
: "${MBS_DB_PORT:?MBS_DB_PORT is required}"
: "${MBS_DB_NAME:?MBS_DB_NAME is required}"
: "${MBS_DB_USER:?MBS_DB_USER is required}"

export ConnectionStrings__Database="Host=${MBS_DB_HOST};Port=${MBS_DB_PORT};Database=${MBS_DB_NAME};Username=${MBS_DB_USER};Password=${database_password};"
unset database_password

if (( $# == 0 )); then
    printf '%s\n' 'No application command was supplied; refusing to start.' >&2
    exit 64
fi

exec "$@"
