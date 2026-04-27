#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="${ROOT_DIR:-/opt/b2b}"
ENV_FILE="${ENV_FILE:-$ROOT_DIR/.env}"
BACKUP_DIR="${BACKUP_DIR:-$ROOT_DIR/backups}"
DB_NAME="${DB_NAME:-B2B}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing env file: $ENV_FILE" >&2
  exit 1
fi

mkdir -p "$BACKUP_DIR"

# Read MSSQL_SA_PASSWORD from .env (simple KEY=VALUE format)
SA_PASSWORD="$(grep -E '^MSSQL_SA_PASSWORD=' "$ENV_FILE" | tail -n 1 | cut -d= -f2- | tr -d '\r')"
if [[ -z "${SA_PASSWORD:-}" ]]; then
  echo "MSSQL_SA_PASSWORD is empty in $ENV_FILE" >&2
  exit 1
fi

ts="$(date -u +%Y%m%dT%H%M%SZ)"
file="${DB_NAME}_${ts}.bak"
container_path="/backups/${file}"

echo "Creating backup: ${container_path}"

docker compose -f "$ROOT_DIR/docker-compose.yml" exec -T db \
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C \
  -Q "BACKUP DATABASE [$DB_NAME] TO DISK = N'$container_path' WITH INIT, COMPRESSION"

echo "Backup completed: $BACKUP_DIR/$file"

echo "Pruning backups older than ${RETENTION_DAYS} days..."
find "$BACKUP_DIR" -maxdepth 1 -type f -name "${DB_NAME}_*.bak" -mtime +"$RETENTION_DAYS" -print -delete || true

