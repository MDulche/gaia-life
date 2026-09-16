#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

if [ ! -f .env.prod ]; then
    echo "Fichier .env.prod introuvable dans ${ROOT}." >&2
    exit 1
fi

COMPOSE=(docker compose --env-file .env.prod -f docker-compose.prod.yml)

echo "==> git pull origin main"
git pull origin main

echo "==> Sauvegarde avant mise à jour des conteneurs"
"${COMPOSE[@]}" up -d mariadb mariadb-backup
cid="$("${COMPOSE[@]}" ps -q mariadb)"
if [ -z "$cid" ]; then
    echo "ERREUR: le service mariadb n'a pas démarré." >&2
    exit 1
fi

echo "Attente du healthcheck MariaDB…"
elapsed=0
while [ "$elapsed" -lt 120 ]; do
    status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$cid")"
    if [ "$status" = "healthy" ]; then
        break
    fi
    sleep 2
    elapsed=$((elapsed + 2))
done
if [ "$status" != "healthy" ]; then
    echo "ERREUR: MariaDB n'est pas healthy (${status}). Sauvegarde annulée." >&2
    exit 1
fi

"${COMPOSE[@]}" exec -T mariadb-backup sh /scripts/backup.sh

echo "==> docker compose build (image locale)"
"${COMPOSE[@]}" build

echo "==> docker compose up -d"
"${COMPOSE[@]}" up -d

echo "==> Attente du healthcheck app-core"
app_cid="$("${COMPOSE[@]}" ps -q app-core)"
if [ -z "$app_cid" ]; then
    echo "ERREUR: le service app-core n'a pas démarré." >&2
    exit 1
fi

elapsed=0
status=""
while [ "$elapsed" -lt 180 ]; do
    status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}starting{{end}}' "$app_cid")"
    if [ "$status" = "healthy" ]; then
        echo "Déploiement OK : app-core est healthy."
        exit 0
    fi
    if [ "$status" = "unhealthy" ]; then
        echo "ERREUR: app-core est unhealthy. Consultez : docker compose --env-file .env.prod -f docker-compose.prod.yml logs app-core" >&2
        exit 1
    fi
    sleep 3
    elapsed=$((elapsed + 3))
done

echo "ERREUR: timeout en attendant le healthcheck app-core (dernier état : ${status})." >&2
exit 1
