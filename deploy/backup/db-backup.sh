#!/usr/bin/env bash
# Periodic SQL Server backup. BACKUP DATABASE writes the .bak on the SQL Server itself, so the
# destination directory (/var/opt/mssql/backups) is a volume shared with the db container.
# Retention prunes .bak files older than BACKUP_RETENTION_DAYS.
set -uo pipefail

: "${DB_HOST:=db}"
: "${DB_NAME:=SMS}"
: "${DB_SA_PASSWORD:?DB_SA_PASSWORD is required}"
: "${BACKUP_RETENTION_DAYS:=14}"
: "${BACKUP_INTERVAL_SECONDS:=86400}"   # daily
DEST=/var/opt/mssql/backups
SQLCMD="$(command -v sqlcmd || echo /opt/mssql-tools18/bin/sqlcmd)"

echo "[db-backup] starting; db=$DB_HOST/$DB_NAME retention=${BACKUP_RETENTION_DAYS}d interval=${BACKUP_INTERVAL_SECONDS}s"
while true; do
  TS="$(date -u +%Y%m%d-%H%M%S)"
  FILE="$DEST/${DB_NAME}_${TS}.bak"
  echo "[db-backup] $(date -u +%FT%TZ) backing up $DB_NAME -> $FILE"
  # -C trusts the server's self-signed TLS cert on the internal network.
  "$SQLCMD" -S "$DB_HOST" -U sa -P "$DB_SA_PASSWORD" -C -b -Q \
    "BACKUP DATABASE [$DB_NAME] TO DISK = N'$FILE' WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;"
  if [ $? -eq 0 ]; then
    echo "[db-backup] ok; pruning backups older than ${BACKUP_RETENTION_DAYS} days"
    find "$DEST" -name "${DB_NAME}_*.bak" -type f -mtime "+${BACKUP_RETENTION_DAYS}" -print -delete
  else
    echo "[db-backup] ERROR: backup failed (see above)." >&2
  fi
  sleep "$BACKUP_INTERVAL_SECONDS"
done
