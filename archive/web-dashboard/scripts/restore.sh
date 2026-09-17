#!/usr/bin/env bash
set -euo pipefail

usage() {
    echo "Usage: $0 chemin/vers/quotidien_YYYYMMDD_HHMMSS.sql.gz"
    echo "Variables optionnelles : COMPOSE_FILE, ENV_FILE, RESTORE_CONFIRM=OUI (non interactif, tests uniquement)."
}

FILE="${1:-}"
if [ -z "$FILE" ] || [ ! -f "$FILE" ]; then
    usage
    exit 1
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

if [ -z "${COMPOSE_FILE:-}" ]; then
    if [ -f .env.prod ] && docker compose --env-file .env.prod -f docker-compose.prod.yml ps -q mariadb 2>/dev/null | grep -q .; then
        COMPOSE_FILE=docker-compose.prod.yml
        ENV_FILE="${ENV_FILE:-.env.prod}"
    else
        COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.dev.yml}"
        ENV_FILE="${ENV_FILE:-.env.dev}"
    fi
fi
ENV_FILE="${ENV_FILE:-.env.dev}"

if [ ! -f "$ENV_FILE" ]; then
    echo "Fichier d'environnement introuvable : ${ENV_FILE}" >&2
    exit 1
fi

# Extraire les clés utiles sans sourcer tout le fichier (le cron contient des espaces).
env_get() {
    tr -d '\r' < "$ENV_FILE" | awk -F= -v key="$1" '
        $1 == key {
            val = substr($0, index($0, "=") + 1)
            gsub(/^["'\'']|["'\'']$/, "", val)
            print val
            exit
        }'
}

DB_NAME="$(env_get DB_NAME)"
DB_PASSWORD="$(env_get DB_PASSWORD)"
DB_NAME="${DB_NAME:-gaia_life}"
DB_USER="${MARIADB_USER:-gaia}"

if [ -z "$DB_PASSWORD" ]; then
    echo "DB_PASSWORD est vide dans ${ENV_FILE}." >&2
    exit 1
fi

echo "Fichier : ${FILE}"
echo "Compose : ${COMPOSE_FILE} (env ${ENV_FILE})"
echo "Cette opération ÉCRASE la base « ${DB_NAME} »."

if [ "${RESTORE_CONFIRM:-}" = "OUI" ]; then
    confirm=OUI
else
    read -r -p "Tapez OUI pour confirmer : " confirm
fi

if [ "$confirm" != "OUI" ]; then
    echo "Annulé."
    exit 1
fi

ABS="$(cd "$(dirname "$FILE")" && pwd)/$(basename "$FILE")"
CONTAINER_PATH="/backups/$(basename "$ABS")"

# Le volume ./backups est monté sur /backups dans mariadb.
if [ ! -f "./backups/$(basename "$ABS")" ]; then
    echo "Copie vers ./backups pour le volume du conteneur…"
    mkdir -p ./backups
    cp "$ABS" "./backups/$(basename "$ABS")"
fi

echo "Restauration en cours…"
docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE" exec -T mariadb \
    sh -c "gunzip -c \"${CONTAINER_PATH}\" | mariadb --user=\"${DB_USER}\" --password=\"${DB_PASSWORD}\" --skip-ssl \"${DB_NAME}\""

echo "Restauration terminée."
