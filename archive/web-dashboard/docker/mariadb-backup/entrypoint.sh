#!/bin/sh
set -eu

quote() {
    printf "%s" "$1" | sed "s/'/'\\\\''/g"
}

DB_HOST="${DB_HOST:-mariadb}"
DB_NAME="${DB_NAME:-gaia_life}"
DB_USER="${DB_USER:-gaia}"
DB_PASSWORD="${DB_PASSWORD:-}"
BACKUP_DIR="${BACKUP_DIR:-/backups}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
BACKUP_CRON_SCHEDULE="${BACKUP_CRON_SCHEDULE:-0 3 * * *}"

umask 077
cat > /etc/backup.env <<EOF
export DB_HOST='$(quote "$DB_HOST")'
export DB_NAME='$(quote "$DB_NAME")'
export DB_USER='$(quote "$DB_USER")'
export DB_PASSWORD='$(quote "$DB_PASSWORD")'
export BACKUP_DIR='$(quote "$BACKUP_DIR")'
export BACKUP_RETENTION_DAYS='$(quote "$BACKUP_RETENTION_DAYS")'
export TZ='$(quote "${TZ:-Europe/Paris}")'
EOF

printenv | sort > /etc/environment || true

echo "Attente de MariaDB (${DB_HOST})…"
i=0
while [ "$i" -lt 60 ]; do
    if mariadb-admin ping -h "$DB_HOST" -u "$DB_USER" -p"$DB_PASSWORD" --skip-ssl --silent; then
        echo "MariaDB est joignable."
        break
    fi
    i=$((i + 1))
    sleep 2
done

if [ "$i" -ge 60 ]; then
    echo "ERREUR: MariaDB injoignable après 2 minutes."
    exit 1
fi

CRON_FILE=/etc/cron.d/gaia-backup
{
    echo "SHELL=/bin/sh"
    echo "PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
    echo "${BACKUP_CRON_SCHEDULE} root /scripts/backup.sh >> /proc/1/fd/1 2>> /proc/1/fd/2"
} > "$CRON_FILE"
chmod 0644 "$CRON_FILE"

echo "Cron de sauvegarde : ${BACKUP_CRON_SCHEDULE}"
exec cron -f
