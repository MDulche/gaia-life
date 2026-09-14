# Gaia-Life

Application interne de gestion familiale (finance, travail et modules à venir).

## Prérequis

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

## Démarrage rapide (développement)

```bash
cp .env.dev.example .env.dev
docker compose --env-file .env.dev -f docker-compose.dev.yml up
```

Compte administrateur de développement (créé au premier démarrage si absent, **uniquement** si `ASPNETCORE_ENVIRONMENT=Development`) :

Les identifiants viennent de `.env.dev` (`SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`), jamais du code C#.

La documentation de déploiement se trouve dans [docs/DEPLOIEMENT.md](docs/DEPLOIEMENT.md).
