#!/usr/bin/env bash
# Minimal HTTP status probe for the shipped API runtime image.
# The aspnet runtime image does not include curl/wget/nc; bash /dev/tcp is available.
set -Eeuo pipefail

host="${1:-127.0.0.1}"
port="${2:-8080}"
path="${3:-/health/ready}"

exec 3<>"/dev/tcp/${host}/${port}"
printf 'GET %s HTTP/1.1\r\nHost: %s\r\nConnection: close\r\n\r\n' "$path" "$host" >&3

status_line=""
IFS= read -r -u 3 status_line || true
exec 3<&- || true
exec 3>&- || true

status_line="${status_line%$'\r'}"
remainder="${status_line#* }"
status_code="${remainder%% *}"

case "$status_code" in
  2[0-9][0-9])
    exit 0
    ;;
  *)
    exit 1
    ;;
esac
