#!/bin/sh
set -eu

log() {
    echo "[$(date -Iseconds)] $*"
}

if [ -f /etc/backup.env ]; then
    # Variables Docker recopiées par l'entrypoint (cron n'hérite pas de l'environnement).
    # shellcheck disable=SC1091
    . /etc/backup.env
fi

DB_HOST="${DB_HOST:-mariadb}"
DB_NAME="${DB_NAME:-gaia_life}"
DB_USER="${DB_USER:-gaia}"
DB_PASSWORD="${DB_PASSWORD:-}"
BACKUP_DIR="${BACKUP_DIR:-/backups}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"

if [ -z "$DB_PASSWORD" ]; then
    log "ERREUR: DB_PASSWORD est vide, dump abandonné."
    exit 1
fi

mkdir -p "$BACKUP_DIR"

STAMP="$(date +%Y%m%d_%H%M%S)"
OUT="${BACKUP_DIR}/quotidien_${STAMP}.sql.gz"
TMP="${BACKUP_DIR}/.tmp_${STAMP}_$$.sql.gz"

DUMP_BIN="mysqldump"
if command -v mariadb-dump >/dev/null 2>&1; then
    DUMP_BIN="mariadb-dump"
fi

log "Sauvegarde de ${DB_NAME} via ${DUMP_BIN} vers ${OUT}"

set +e
"$DUMP_BIN" \
    --host="$DB_HOST" \
    --user="$DB_USER" \
    --password="$DB_PASSWORD" \
    --single-transaction \
    --routines \
    --events \
    --hex-blob \
    --skip-ssl \
    "$DB_NAME" | gzip -9 > "$TMP"
status=$?
set -e

if [ "$status" -ne 0 ]; then
    log "ERREUR: ${DUMP_BIN} a échoué (code ${status}). Aucun fichier de sauvegarde n'est conservé."
    rm -f "$TMP"
    exit 1
fi

if [ ! -s "$TMP" ]; then
    log "ERREUR: le dump est vide, fichier abandonné."
    rm -f "$TMP"
    exit 1
fi

if ! gzip -t "$TMP"; then
    log "ERREUR: archive gzip invalide, fichier abandonné."
    rm -f "$TMP"
    exit 1
fi

mv "$TMP" "$OUT"
log "Sauvegarde OK: ${OUT} ($(wc -c < "$OUT") octets)"

find "$BACKUP_DIR" -maxdepth 1 -type f -name 'quotidien_*.sql.gz' -mtime "+${BACKUP_RETENTION_DAYS}" -print -delete
log "Rotation: fichiers quotidien_*.sql.gz de plus de ${BACKUP_RETENTION_DAYS} jours supprimés."
