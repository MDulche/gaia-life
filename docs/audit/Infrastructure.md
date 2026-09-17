# Audit — Infrastructure (Docker, compose, scripts)

> **Document historique** — audite le dashboard web désormais archivé sous `archive/web-dashboard/`, ne concerne plus le dépôt actif Android.

Date : 2026-09-17 · Périmètre : `Dockerfile`, `docker-compose.*.yml`, `scripts/*.sh`, nginx/backup associés.

---

## Sécurité

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `Dockerfile` | 15–25 | **Majeur** | Image runtime `aspnet:9.0` sans `USER` non-root → processus `dotnet` en **root** dans le conteneur. | Créer utilisateur/groupe non-root (`app` / uid 1000), `chown` `/app`, `USER app` avant ENTRYPOINT. |
| `Dockerfile` | 4–10 | **Majeur** | `COPY` des csproj **sans** `src/App.Modules.Stock/App.Modules.Stock.csproj` avant `dotnet restore GaiaLife.sln`, puis `publish --no-restore`. Build prod fragile / restore incomplet dès que la solution référence Stock. | Ajouter le COPY du csproj Stock (et tests si dans la sln) avant restore. |
| `docker-compose.prod.yml` | 15–16, 82–84 | — | Ports publiés : `127.0.0.1:8080:8080` (app), `80`/`443` (proxy). **MariaDB non publié** — conforme (app + reverse proxy seulement en exposition utile). | Conserver le bind localhost sur 8080 ; ne pas exposer 3306. |
| `docker-compose.prod.yml` | 12, 38–41, 65 | **Majeur** | Mots de passe par défaut `${DB_PASSWORD:-changeme}` / `changeme_root` si `.env.prod` incomplet — déploiement « silencieux » avec secrets faibles. | Échouer le démarrage si secrets absents / trop courts ; retirer les defaults en prod. |
| `docker-compose.dev.yml` | 21–22, 60–61 | **Majeur** (dev) | `8080:8080` et `3306:3306` sans bind `127.0.0.1` → accessibles sur toutes les interfaces LAN. | Publier `127.0.0.1:8080:8080` et `127.0.0.1:3306:3306` (ou ne pas exposer MariaDB). |
| `scripts/deploy-prod.sh` | — | — | Pas de secret en dur ; s’appuie sur `.env.prod`. | OK. |
| `scripts/backup.sh` / restore | — | Mineur | Credentials via env compose — vérifier absence d’echo du mot de passe dans les logs cron. | Rediriger stderr sensible ; ne pas `set -x` avec env. |
| NuGet CVE | tests | Voir Stock | SQLitePCLRaw High dans projet de tests uniquement. | MAJ package tests. |

---

## Performance

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `Dockerfile` HEALTHCHECK | 23–24 | — | Intervalle 10 s sur `/health/live` (sans MariaDB) — OK. | — |
| `docker-compose.prod.yml` | 24–28 | — | Healthcheck app 10 s → live only. | OK. |
| `docker-compose.prod.yml` | 46–50 | Mineur | Healthcheck MariaDB `interval: 10s` — raisonnable. | — |
| `docker-compose.dev.yml` | 66–67 | Mineur | MariaDB healthcheck `interval: 5s` un peu plus agressif (dev only). | Remonter à 10 s si bruit inutile. |
| Publisher app | Core | — | Défaut 15 s MySQL — acceptable (voir App.Core). | — |

---

## Qualité / Commentaires

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `deploy-prod.sh` | 14–46 | — | Enchaînement clair : pull → backup → build → up → attente healthy. | Documenter prérequis `.env.prod` / secrets obligatoires. |
| Compose Stock volumes (dev) | `docker-compose.dev.yml` | Mineur | Vérifier que bin/obj Stock sont bien exclus comme les autres modules (sinon hot-reload trompeur). | Aligner les volumes anonymes Stock si manquants. |
| TODO/FIXME scripts | — | — | Aucun. | — |

---

## Synthèse module

**Majeurs** : conteneur root ; csproj Stock oublié au restore ; defaults `changeme` prod ; ports dev ouverts sur `0.0.0.0`. Pas d’exposition MariaDB en prod (point positif).
