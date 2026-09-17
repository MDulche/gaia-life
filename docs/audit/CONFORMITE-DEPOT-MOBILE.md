# Rapport de conformité — dépôt actif = app Android uniquement

**Date** : 2026-09-17 (mise à jour post-corrections)  
**Périmètre** : dépôt Gaia-Life **hors** `archive/web-dashboard/`  
**Verdict global** : **CONFORME**

---

## Résumé des corrections appliquées

| Priorité | Action | Statut |
| --- | --- | --- |
| P0 | Retrait Pomelo / `GaiaMariaDb` / dual-path MariaDB des modules actifs | Fait |
| P0 | Migrations MariaDB + factories déplacées vers `archive/web-dashboard/src/App.Modules.*/` | Fait |
| P0 | `GaiaMariaDb` autonome sous `archive/web-dashboard/src/App.Core/Data/` | Fait |
| P1 | `.dockerignore` racine → archive ; bandeaux `docs/audit/*` | Fait |
| P2 | Doublon SQLite Course retiré ; `Course.Tests` dans `GaiaLife.sln` ; tools PWA → archive | Fait |
| Extra | Remplacement `FrameworkReference` AspNetCore.App par packages (build MAUI Android) | Fait |

**Build** : `dotnet build GaiaLife.sln` — **succès** (exit 0).

---

## Étape 1 — Inventaire zone active (post-correction)

| Élément | Statut |
| --- | --- |
| `GaiaLife.sln` | Shared + 4 modules + Mobile + 4 projets de tests |
| `src/App.Mobile`, `App.Shared`, `App.Modules.*` | Présents |
| `docs/CONTRAT-MIGRATION-MOBILE.md` | Présent |
| `Dockerfile` / compose / scripts / `.env*` racine | Absents |
| `.dockerignore` racine | **Absent** (copie dans l’archive) |
| `src/App.Core` | Absent |

---

## Étape 2 — Traces Docker / web / MariaDB hors archive

| Catégorie | Zone active | Notes |
| --- | --- | --- |
| Docker | **Aucune** | `.dockerignore` uniquement sous `archive/web-dashboard/` |
| Pomelo / `GaiaMariaDb` / `UseMySql` | **Aucune** dans `src/` | Copie archive : `App.Core/Data/GaiaMariaDb.cs` |
| Migrations MariaDB | **Aucune** dans `src/` | Sous `archive/web-dashboard/src/App.Modules.*/Data/Migrations/` |
| Factories MariaDB | **Aucune** dans `src/` | Archivées |
| `appsettings*` / SeedAdmin / HealthChecks | **Aucune** hors archive | — |
| Docs audit | Bandeaux historiques | Contenu non réécrit (valeur historique) |

---

## Étape 4 — Projets légitimes

| Attendu | Statut |
| --- | --- |
| App.Shared / Finance / Travail / Course / Stock / Mobile | **OK** |
| `docs/CONTRAT-MIGRATION-MOBILE.md` | **OK** |
| `Migrations/Sqlite/` + factory design-time par module | **OK** (chemin unique `Migrations/Sqlite/`) |
| Pas de `Data/Migrations` MariaDB actif | **OK** |

---

## Étape 5 — `GaiaLife.sln`

| Contrôle | Résultat |
| --- | --- |
| Pas d’`App.Core` | **OK** |
| Pas de Docker | **OK** |
| `App.Modules.Course.Tests` | **OK** (ajouté) |

---

## Archive (hors périmètre actif, cohérente)

- `archive/web-dashboard/` : App.Core, Docker, scripts, env examples, migrations MariaDB modules, factories, `.dockerignore`, `docs/tools/*.py`
- `GaiaMariaDb` local à App.Core — l’archive ne dépend plus de Shared pour MariaDB

---

## Verdict

**CONFORME** — dépôt actif Android-only (SQLite), sans trace Pomelo/MariaDB/Docker hors annotations historiques dans `docs/audit/` et hors `archive/web-dashboard/`.
