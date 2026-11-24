# Backups & restore

Two sidecar containers (built from `deploy/Dockerfile.backup`) take scheduled backups and
prune old ones. Both run daily by default and keep `BACKUP_RETENTION_DAYS` (default 14) of history.

| Service | What it backs up | How | Stored in volume |
|---|---|---|---|
| `db-backup` | SQL Server `SMS` database | `BACKUP DATABASE … WITH INIT, COMPRESSION, CHECKSUM` | `mssql-backups` (`/var/opt/mssql/backups/SMS_<ts>.bak`) |
| `storage-backup` | MinIO buckets (documents) | `mc mirror` into a dated snapshot | `minio-backups` (`/backups/<ts>/`) |

> The database `.bak` is written by SQL Server itself, so the `mssql-backups` volume is mounted
> into **both** the `db` and `db-backup` containers.

## Copy backups off the host
Volumes live on the server; copy them somewhere durable (another host, NAS, or object storage) on
your own schedule — e.g. a nightly `rsync`/`restic` of the Docker volume mount points, or run
`storage-backup` against an **offsite** MinIO/S3 endpoint.

## Restore — database
```bash
# 1. Pick a backup
docker compose exec db ls -1 /var/opt/mssql/backups

# 2. Restore it (WITH REPLACE overwrites the live DB — make sure that's intended)
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_SA_PASSWORD" -C -b -Q \
  "RESTORE DATABASE [SMS] FROM DISK = N'/var/opt/mssql/backups/SMS_YYYYMMDD-HHMMSS.bak' WITH REPLACE, RECOVERY, STATS = 10;"
```
For a clean target (new server), create the instance first, then run the same `RESTORE`.

## Restore — object store
```bash
# Mirror a chosen snapshot back into the live MinIO
docker compose run --rm --entrypoint sh storage-backup -c '
  mc alias set dst "$MINIO_ENDPOINT" "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" &&
  mc mirror --overwrite /backups/YYYYMMDD-HHMMSS/ dst/'
```

## Verify your backups
A backup you have never restored is a hope, not a backup. Periodically restore the latest `.bak`
into a scratch database and run the app's `/health/ready` probe against it:
```bash
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$DB_SA_PASSWORD" -C -Q \
  "RESTORE VERIFYONLY FROM DISK = N'/var/opt/mssql/backups/SMS_YYYYMMDD-HHMMSS.bak';"
```

## Tuning
- `BACKUP_RETENTION_DAYS` — days of history to keep (both services).
- `BACKUP_INTERVAL_SECONDS` — override the daily cadence (e.g. `21600` for every 6 h).
