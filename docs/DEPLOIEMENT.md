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

Au premier démarrage, l'application applique aussi les migrations EF Core automatiquement (Identity / `AppDbContext`, puis `FinanceDbContext` et `TravailDbContext` si ces modules sont enregistrés), puis crée les rôles `Admin`, `Membre` et `Lecture`. Vous pouvez également les appliquer à la main depuis l'hôte (MariaDB doit être joignable sur `localhost:3306`) :

```bash
dotnet ef database update --project src/App.Core --startup-project src/App.Core
dotnet ef database update --context FinanceDbContext --project src/App.Modules.Finance --startup-project src/App.Core
dotnet ef database update --context TravailDbContext --project src/App.Modules.Travail --startup-project src/App.Core
```

L'application est ensuite disponible sur http://localhost:8080.

## Migrations EF Core

Migrations appliquées par l'application au démarrage :

| Contexte | Projet | Nom |
| --- | --- | --- |
| `AppDbContext` | `src/App.Core` | `20260914101955_InitialCreate` |
| `FinanceDbContext` | `src/App.Modules.Finance` | `20260914115207_InitialFinance` |
| `TravailDbContext` | `src/App.Modules.Travail` | `20260914140730_InitialTravail` |

Après un `git pull` qui ajoute une migration, un redémarrage de `app-core` suffit en développement (`MigrateAsync`). En production, le même mécanisme s'exécute au démarrage du conteneur ; vous pouvez aussi lancer les commandes `dotnet ef database update` ci-dessus depuis une machine autorisée.

## DbContext : Identity Scoped et factories métier

`AppDbContext` est enregistré de **deux** façons, volontairement :

1. `AddDbContextFactory<AppDbContext>` — utilisé par les pages Blazor, le menu, `ActiveModuleGuard` et le seed des migrations. Chaque opération ouvre un contexte via `CreateDbContextAsync()` dans un `await using`, ce qui évite les accès concurrents au même `DbContext` (circuit Blazor + menu + page).
2. `AddDbContext<AppDbContext>` en **Scoped**, avec `optionsLifetime: Singleton` — exigé par ASP.NET Core Identity (`UserManager`, `RoleManager`, `SignInManager`, `AddEntityFrameworkStores`). Identity conserve le contexte pendant toute l'opération (login, changement de rôle, etc.). Le `optionsLifetime: Singleton` est obligatoire : sans lui, `IDbContextFactory` (singleton) ne peut pas consommer des `DbContextOptions` scoped.

Un contexte Identity séparé n'a pas été créé : les tables utilisateurs/rôles sont déjà dans `AppDbContext` (`IdentityDbContext`). Le double enregistrement (factory métier + Scoped Identity) est plus simple qu'un second contexte, et suit le modèle recommandé par EF Core.

`FinanceDbContext` et `TravailDbContext` ne sont enregistrés **que** via `AddDbContextFactory<T>`. `FinanceService` et `TravailService` n'injectent pas le contexte directement.

Les factories réutilisent `GaiaMariaDb` (Pomelo `EnableRetryOnFailure`, MariaDB 11.6).

Dans Travail, `SoldeConges.JoursPris` n'est **pas** persisté : il est calculé à partir des congés au statut `Valide` de l'année (`DateDebut`). Seul `JoursAcquis` est stocké (page `/travail/employeurs/{id}/solde-conges`).

## Données : foyer partagé, pas de cloisonnement par utilisateur

Gaia-Life est une application **mono-foyer** : Finances et Travail sont des données du foyer, visibles et saisissables par tout compte authentifié (Admin, Membre, Lecture). Il n'y a pas de filtrage « cet utilisateur ne voit que ses fiches de paie / ses employeurs ».

Les rôles servent à l'administration de l'app, pas à isoler les données :

- **Admin** : page `/admin`, activation des modules, validation / refus des congés (`ChangerStatutConge` refuse les non-Admin).
- **Membre** / **Lecture** : accès aux modules actifs, sans les boutons Valider / Refuser.

Si un cloisonnement multi-ménages devient nécessaire plus tard, il faudra une notion de foyer (ou `UserId`) sur les agrégats, ce qui n'existe pas aujourd'hui.

Ne pas résoudre `AppDbContext` dans un composant Blazor interactif pour les requêtes métier : passer par `IDbContextFactory<AppDbContext>`. Réserver le Scoped aux appels Identity.

## Mise à jour de l'environnement de production

Sur le serveur, à partir du clone du dépôt :

```bash
git pull
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --build
```

Cette séquence récupère le code, reconstruit l'image de `App.Core` et redémarre les services (`restart: unless-stopped`). MariaDB n'est pas exposée hors du réseau Docker interne. Le volume `./backups` est monté sur `/backups`.

Après un schéma de base nouveau, appliquez les migrations avant ou juste après le redémarrage, selon votre procédure (par exemple `dotnet ef database update` depuis une machine autorisée, ou un job dédié).
