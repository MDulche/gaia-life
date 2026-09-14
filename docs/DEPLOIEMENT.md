# Déploiement Gaia-Life

## Fichiers d'environnement

Les fichiers `.env.dev` et `.env.prod` **ne doivent jamais être commités**. Ils sont déjà listés dans `.gitignore`.

Copiez les exemples, puis renseignez des mots de passe réels :

```bash
cp .env.dev.example .env.dev
cp .env.prod.example .env.prod
```

## Environnement de développement

L'application tourne avec `dotnet watch` (hot-reload) et MariaDB 11.6. Les ports `8080` (app) et `3306` (MariaDB) sont exposés sur la machine hôte.

```bash
cp .env.dev.example .env.dev
docker compose --env-file .env.dev -f docker-compose.dev.yml up
```

`--env-file .env.dev` est nécessaire pour interpoler `DB_NAME`, `DB_PASSWORD` et `DB_ROOT_PASSWORD` dans le fichier Compose. Sans cet argument, Compose ne lit que le fichier `.env` à la racine.

Équivalent si vous avez copié `.env.dev` vers `.env` :

```bash
docker compose -f docker-compose.dev.yml up
```

Au premier démarrage, l'application applique aussi les migrations EF Core automatiquement, puis crée les rôles `Admin`, `Membre` et `Lecture`. Vous pouvez également les appliquer à la main depuis l'hôte (MariaDB doit être joignable sur `localhost:3306`) :

```bash
dotnet ef database update --project src/App.Core --startup-project src/App.Core
```

L'application est ensuite disponible sur http://localhost:8080.

## Mise à jour de l'environnement de production

Sur le serveur, à partir du clone du dépôt :

```bash
git pull
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --build
```

Cette séquence récupère le code, reconstruit l'image de `App.Core` et redémarre les services (`restart: unless-stopped`). MariaDB n'est pas exposée hors du réseau Docker interne. Le volume `./backups` est monté sur `/backups`.

Après un schéma de base nouveau, appliquez les migrations avant ou juste après le redémarrage, selon votre procédure (par exemple `dotnet ef database update` depuis une machine autorisée, ou un job dédié).
