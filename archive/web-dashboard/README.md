# Archive — Dashboard web Gaia-Life

Ce dossier contient le **dashboard web Blazor Server** (Modular Monolith .NET 9 + MariaDB + Docker) développé et testé localement, **jamais mis en production réelle**.

## Statut

- **Non maintenu** : le développement actif a basculé vers l'app Android (`App.Mobile`, .NET MAUI Blazor Hybrid + SQLite).
- **Conservé comme référence / historique** : architecture modules, Identity, health checks, déploiement Docker, migrations MariaDB.
- **Ne pas déployer** et ne pas faire évoluer ce code sauf besoin ponctuel de consultation.

## Contenu

| Élément | Description |
| --- | --- |
| `src/App.Core/` | Hôte Blazor Server (Program.cs, Identity, Admin, health) |
| `Dockerfile`, `docker-compose.*.yml`, `docker/` | Build et runtime web |
| `scripts/` | backup / restore / deploy-prod / mkcert |
| `.env.*.example` | Exemples d'environnement web |
| `docs/DEPLOIEMENT.md` | Runbook déploiement web |

## Rouvrir / builder ponctuellement

Utiliser la solution dédiée à la racine du dépôt :

```bash
dotnet build archive/web-dashboard/GaiaLife.WebArchive.sln
```

Les projets métier (`App.Shared`, `App.Modules.*`) restent dans `src/` (solution active `GaiaLife.sln`) ; l'archive y fait référence.

Décision actée le 17/09/2026.
