#!/usr/bin/env bash
# Periodic MinIO/S3 object-store backup via `mc mirror` into a dated snapshot directory.
# Retention prunes snapshot directories older than BACKUP_RETENTION_DAYS.
set -uo pipefail

: "${MINIO_ENDPOINT:=http://storage:9000}"
: "${MINIO_ROOT_USER:?MINIO_ROOT_USER is required}"
: "${MINIO_ROOT_PASSWORD:?MINIO_ROOT_PASSWORD is required}"
: "${BACKUP_RETENTION_DAYS:=14}"
: "${BACKUP_INTERVAL_SECONDS:=86400}"
DEST=/backups

echo "[storage-backup] starting; endpoint=$MINIO_ENDPOINT retention=${BACKUP_RETENTION_DAYS}d"
until mc alias set src "$MINIO_ENDPOINT" "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null 2>&1; do
  echo "[storage-backup] waiting for object store..."; sleep 5
done
while true; do
  TS="$(date -u +%Y%m%d-%H%M%S)"
  SNAP="$DEST/$TS"
  echo "[storage-backup] $(date -u +%FT%TZ) mirroring all buckets -> $SNAP"
  if mc mirror --overwrite --remove src/ "$SNAP/"; then
    echo "[storage-backup] ok; pruning snapshots older than ${BACKUP_RETENTION_DAYS} days"
    find "$DEST" -mindepth 1 -maxdepth 1 -type d -mtime "+${BACKUP_RETENTION_DAYS}" -print -exec rm -rf {} +
  else
    echo "[storage-backup] ERROR: mirror failed." >&2
  fi
  sleep "$BACKUP_INTERVAL_SECONDS"
done
