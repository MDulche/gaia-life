# Déploiement Gaia-Life

## Table des matières

1. [Fichiers d'environnement](#fichiers-denvironnement)
2. [Environnement de développement](#environnement-de-développement)
3. [HTTPS, reverse proxy et PWA](#https-et-pwa)
4. [Migrations EF Core](#migrations-ef-core)
5. [DbContext : Identity Scoped et factories métier](#dbcontext--identity-scoped-et-factories-métier)
6. [Données : foyer partagé](#données--foyer-partagé-pas-de-cloisonnement-par-utilisateur)
7. [Sauvegarde et restauration](#sauvegarde-et-restauration)
8. [Logs et health checks](#logs-et-health-checks)
9. [GitHub Container Registry](#github-container-registry)
10. [Mise à jour de production](#mise-à-jour-de-production)
11. [Rollback](#rollback)
12. [Vérification phase 4](#vérification-phase-4)
13. [Vérification phase 5](#vérification-phase-5)
14. [Vérification phase 6](#vérification-phase-6)

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

Au premier démarrage, l'application applique aussi les migrations EF Core automatiquement (Identity / `AppDbContext`, puis `FinanceDbContext`, `TravailDbContext` et `CourseDbContext` si ces modules sont enregistrés), puis crée les rôles `Admin`, `Membre` et `Lecture`. Vous pouvez également les appliquer à la main depuis l'hôte (MariaDB doit être joignable sur `localhost:3306`) :

```bash
dotnet ef database update --project src/App.Core --startup-project src/App.Core
dotnet ef database update --context FinanceDbContext --project src/App.Modules.Finance --startup-project src/App.Core
dotnet ef database update --context TravailDbContext --project src/App.Modules.Travail --startup-project src/App.Core
dotnet ef database update --context CourseDbContext --project src/App.Modules.Course --startup-project src/App.Core
```

L'application est ensuite disponible sur http://localhost:8080. Avec le reverse proxy (certificats mkcert générés, voir ci-dessous) : https://gaia.local ou https://\<IP-LAN\>.

En développement, le service worker PWA **n'est pas** enregistré (hot-reload). Le manifeste et les icônes sont tout de même servis.

## HTTPS et PWA

Les Service Workers et l'installation « Ajouter à l'écran d'accueil » n'agissent que sur une **origine sécurisée** :

| Contexte | Origine | Comportement |
| --- | --- | --- |
| `dotnet run` profil `https` (`launchSettings.json`) | `https://localhost:7078` | Certificat de développement .NET. Service worker seulement si `ASPNETCORE_ENVIRONMENT` n'est pas `Development`. |
| Docker, HTTP direct | `http://localhost:8080` | `localhost` est une exception Chrome/Firefox. Pas de SW en Development. |
| Homelab (téléphones du LAN) | `https://gaia.local` ou `https://<IP-LAN>` | Nginx termine le TLS (mkcert), Kestrel reste en HTTP `:8080`. |
| Production avec nom public | HTTPS Let's Encrypt | Même Nginx, certificats certbot à la place de mkcert. |

Dans Docker, `app-core` n'écoute **pas** en HTTPS : `UseHttpsRedirection` est désactivé si `DOTNET_RUNNING_IN_CONTAINER=true`. Le TLS se termine sur le reverse proxy Nginx (`service` `proxy`). `app-core` lit `X-Forwarded-Proto` (`UseForwardedHeaders`) pour Identity / HSTS.

### Certificat mkcert

Sur le serveur homelab (une fois) :

```powershell
# Windows
.\scripts\generate-mkcert.ps1
```

```bash
# Linux
chmod +x scripts/generate-mkcert.sh
./scripts/generate-mkcert.sh
```

Le script installe mkcert si besoin, exécute `mkcert -install` (crée la CA locale et l'ajoute au magasin de l'hôte), puis génère `certs/gaia.pem` + `certs/gaia-key.pem` pour `gaia.local`, l'IP LAN détectée, `localhost` et `127.0.0.1`. Il copie aussi `certs/rootCA.pem` (partie **publique** de la CA) pour les téléphones.

Si l'IP LAN n'est pas la bonne :

```powershell
.\scripts\generate-mkcert.ps1 -HostName gaia.local -LanIp 172.16.100.170
```

```bash
GAIA_HOST=gaia.local GAIA_LAN_IP=172.16.100.170 ./scripts/generate-mkcert.sh
```

Les fichiers `certs/gaia-key.pem` et `rootCA-key.pem` (dans le répertoire `mkcert -CAROOT`) **ne doivent jamais être commités**. `certs/` est dans `.gitignore`.

Sur l'hôte, pointez `gaia.local` vers la machine :

```text
# C:\Windows\System32\drivers\etc\hosts  ou  /etc/hosts
127.0.0.1    gaia.local
172.16.100.170 gaia.local
```

(remplacez l'IP par celle du serveur). Sur les téléphones, soit le routeur résout `gaia.local`, soit vous ouvrez `https://<IP-LAN>` (l'IP est dans le certificat).

Ouvrez le pare-feu de l'hôte pour TCP 80 et 443 si les téléphones ne joignent pas le proxy (Windows : règle entrante « Gaia-Life HTTPS »).

`curl` Windows (schannel) peut refuser le certificat mkcert (`CRYPT_E_NO_REVOCATION_CHECK`). Chrome et Edge font confiance à la CA après `mkcert -install`. Pour un test en ligne de commande : `curl.exe --ssl-no-revoke https://127.0.0.1/health/live` (liveness) ou `https://127.0.0.1/health` (JSON détaillé).

Ensuite : `docker compose --env-file .env.dev -f docker-compose.dev.yml up -d` (ou `.env.prod` / `docker-compose.prod.yml`). Nginx écoute 80 (redirige vers HTTPS) et 443, et proxifie vers `app-core:8080` (WebSocket Blazor Server inclus). En production, le port 8080 n'est publié que sur `127.0.0.1`.

### Let's Encrypt (domaine public)

Si un nom de domaine public est disponible, remplacez les chemins de certificats Nginx par ceux de certbot, par exemple :

```nginx
ssl_certificate     /etc/letsencrypt/live/gaia.exemple.fr/fullchain.pem;
ssl_certificate_key /etc/letsencrypt/live/gaia.exemple.fr/privkey.pem;
```

Traefik peut jouer le même rôle (routeur HTTPS + ACME) ; le dépôt livre Nginx, déjà branché dans Compose.

### Installer rootCA.pem sur un téléphone

Sans cette étape, Chrome / Safari affichent un avertissement (autorité inconnue). Copiez `certs/rootCA.pem` (ou `%LOCALAPPDATA%\mkcert\rootCA.pem` / `$(mkcert -CAROOT)/rootCA.pem`) via USB, e-mail, dossier partagé ou AirDrop. Ce fichier n'est **pas** la clé privée.

**Android (Chrome)**

1. Copiez `rootCA.pem` dans Téléchargements (renommez en `gaia-rootCA.crt` si le téléphone n'affiche pas le `.pem`).
2. Paramètres → **Sécurité** (ou **Mots de passe et sécurité**) → **Chiffrement et identifiants** → **Installer un certificat** → **Certificat CA**.
3. Acceptez l'avertissement « votre connexion ne sera plus privée » (CA utilisateur) et donnez un nom, ex. `Gaia-Life mkcert`.
4. Ouvrez Chrome sur `https://gaia.local` ou `https://<IP-LAN>` : le cadenas doit être normal (pas « Non sécurisé »).
5. Android 7+ : les applications hors Chrome ignorent souvent les CA utilisateur ; Chrome et le flux PWA suffisent pour Gaia-Life.

**iPhone / iPad (Safari)**

1. Envoyez `rootCA.pem` sur l'iPhone (AirDrop, Mail, Fichiers). Ouvrez-le.
2. Une bannière **Profil téléchargé** apparaît. Allez dans Réglages → **Profil téléchargé** (ou **Général** → **VPN et gestion de l'appareil**) → **Installer** (code de l'appareil).
3. Réglages → **Général** → **Informations** → **Réglages des certificats** (tout en bas) → activez **Faire pleinement confiance** pour la CA `mkcert …`.
4. Safari → `https://gaia.local` ou `https://<IP-LAN>` : pas d'alerte de certificat.
5. Partager → **Sur l'écran d'accueil** pour installer la PWA.

Si l'IP du serveur change (DHCP), régénérez le certificat (`generate-mkcert`) et redémarrez `proxy` (`docker compose ... up -d proxy`).

Hors Docker, le profil `https` de `src/App.Core/Properties/launchSettings.json` utilise le certificat de développement .NET (`dotnet dev-certs https --trust`).

Installation PWA (après HTTPS reconnu comme sûr) :

1. Android / Chrome : menu → **Ajouter à l'écran d'accueil** / **Installer l'application**. L'app s'ouvre en `display: standalone` (sans barre de navigateur).
2. iOS / Safari : bouton Partager → **Sur l'écran d'accueil**. Safari ignore le service worker pour l'installation mais utilise `apple-touch-icon` et `apple-mobile-web-app-capable`.

Le service worker met en cache uniquement les assets statiques (CSS, manifeste, icônes) en **network first**. Les pages Blazor Server / SignalR ne sont pas mises en cache. Hors ligne, une navigation affiche `offline.html` ; si l'app était déjà ouverte, le bandeau « Vous êtes hors ligne » et le modal de reconnexion Blazor s'affichent.

Lighthouse 12 n'a plus de catégorie « PWA » (les audits d'installabilité sont dans l'onglet Application de Chrome). Contrôles à faire en **Production** (HTTPS ou `localhost`) : manifeste valide (`name`, `short_name`, `start_url`, `display: standalone`, icônes 192 et 512 PNG, icône maskable), service worker enregistré, `theme-color`, `apple-touch-icon`. En Development le SW n'est volontairement pas enregistré (hot-reload).

## Migrations EF Core

Migrations appliquées par l'application au démarrage :

| Contexte | Projet | Nom |
| --- | --- | --- |
| `AppDbContext` | `src/App.Core` | `20260914101955_InitialCreate` |
| `FinanceDbContext` | `src/App.Modules.Finance` | `20260914115207_InitialFinance`, `20260916071850_AjoutChargesEtPrincipal` |
| `TravailDbContext` | `src/App.Modules.Travail` | `20260914140730_InitialTravail`, `20260916082127_CouleurTypeConge` |
| `CourseDbContext` | `src/App.Modules.Course` | `20260916075347_InitialCourse` |

Après un `git pull` qui ajoute une migration, un redémarrage de `app-core` suffit en développement (`MigrateAsync`). En production, le même mécanisme s'exécute au démarrage du conteneur ; vous pouvez aussi lancer les commandes `dotnet ef database update` ci-dessus depuis une machine autorisée.

## DbContext : Identity Scoped et factories métier

`AppDbContext` est enregistré de **deux** façons, volontairement :

1. `AddDbContextFactory<AppDbContext>` — utilisé par les pages Blazor, le menu, `ActiveModuleGuard` et le seed des migrations. Chaque opération ouvre un contexte via `CreateDbContextAsync()` dans un `await using`, ce qui évite les accès concurrents au même `DbContext` (circuit Blazor + menu + page).
2. `AddDbContext<AppDbContext>` en **Scoped**, avec `optionsLifetime: Singleton` — exigé par ASP.NET Core Identity (`UserManager`, `RoleManager`, `SignInManager`, `AddEntityFrameworkStores`). Identity conserve le contexte pendant toute l'opération (login, changement de rôle, etc.). Le `optionsLifetime: Singleton` est obligatoire : sans lui, `IDbContextFactory` (singleton) ne peut pas consommer des `DbContextOptions` scoped.

Un contexte Identity séparé n'a pas été créé : les tables utilisateurs/rôles sont déjà dans `AppDbContext` (`IdentityDbContext`). Le double enregistrement (factory métier + Scoped Identity) est plus simple qu'un second contexte, et suit le modèle recommandé par EF Core.

`FinanceDbContext`, `TravailDbContext` et `CourseDbContext` ne sont enregistrés **que** via `AddDbContextFactory<T>`. `FinanceService`, `TravailService` et `CourseService` n'injectent pas le contexte directement.

Les factories réutilisent `GaiaMariaDb` (Pomelo `EnableRetryOnFailure`, MariaDB 11.6).

Dans Travail, `SoldeConges.JoursPris` n'est **pas** persisté : il est calculé à partir des congés au statut `Valide` de l'année (`DateDebut`). Seul `JoursAcquis` est stocké, saisi depuis `/admin/travail/soldes-conges`.

L'ancienne page `/travail/employeurs/{id}/solde-conges` redirige vers `/admin/travail/soldes-conges` pour n'avoir qu'un seul chemin de saisie (réservé Admin). Le tableau de bord quotidien reste `/travail/employeurs/{id}`.

## Données : foyer partagé, pas de cloisonnement par utilisateur

Gaia-Life est une application **mono-foyer** : Finances, Travail et Courses sont des données du foyer, visibles et saisissables par tout compte authentifié (Admin, Membre, Lecture). Il n'y a pas de filtrage « cet utilisateur ne voit que ses fiches de paie / ses employeurs ».

Les rôles servent à l'administration de l'app, pas à isoler les données :

- **Admin** : pages `/admin/modules`, `/admin/finance` (si le module Finance est actif), `/admin/travail` (si le module Travail est actif, y compris `/admin/travail/soldes-conges`), `/admin/course` (si le module Courses est actif), `/admin/utilisateurs`, `/admin/systeme`, `/admin/supervision` (raccourci `/admin` → modules), activation des modules, validation / refus des congés (`ChangerStatutConge` refuse les non-Admin).
- **Membre** / **Lecture** : accès aux modules actifs, sans les boutons Valider / Refuser.

Si un cloisonnement multi-ménages devient nécessaire plus tard, il faudra une notion de foyer (ou `UserId`) sur les agrégats, ce qui n'existe pas aujourd'hui.

Ne pas résoudre `AppDbContext` dans un composant Blazor interactif pour les requêtes métier : passer par `IDbContextFactory<AppDbContext>`. Réserver le Scoped aux appels Identity.

## Sauvegarde et restauration

Le service Compose `mariadb-backup` (image `mariadb:11.6` + cron) n'expose aucun port. Il attend que `mariadb` soit `healthy`, puis lance `scripts/backup.sh` selon `BACKUP_CRON_SCHEDULE` (défaut `0 3 * * *`, heure `TZ`, défaut `Europe/Paris`).

Chaque dump :

- utilise `mariadb-dump` / `mysqldump` sur `DB_NAME` (identifiants `DB_USER` / `DB_PASSWORD`, mêmes variables que `app-core`) ;
- compresse en gzip ;
- nomme le fichier `quotidien_YYYYMMDD_HHMMSS.sql.gz` dans le volume `./backups` (`/backups` dans le conteneur) ;
- refuse un fichier vide ou gzip invalide (erreur visible dans `docker compose logs mariadb-backup`) ;
- supprime les `quotidien_*.sql.gz` de plus de `BACKUP_RETENTION_DAYS` jours (défaut 14).

### Sauvegarde manuelle

Depuis la racine du dépôt (dev) :

```bash
docker compose --env-file .env.dev -f docker-compose.dev.yml exec mariadb-backup sh scripts/backup.sh
```

Production :

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml exec mariadb-backup sh scripts/backup.sh
```

Vérifier le fichier sur l'hôte :

```bash
ls -l backups/quotidien_*.sql.gz
gzip -t backups/quotidien_*.sql.gz
```

Logs en cas d'échec :

```bash
docker compose --env-file .env.dev -f docker-compose.dev.yml logs --tail=50 mariadb-backup
```

### Restauration

`scripts/restore.sh` demande **OUI** (majuscules) avant d'écraser la base. Le fichier `.sql.gz` doit être accessible via le volume `./backups`.

```bash
# Dev (Git Bash / WSL / hôte Linux)
COMPOSE_FILE=docker-compose.dev.yml ENV_FILE=.env.dev ./scripts/restore.sh backups/quotidien_YYYYMMDD_HHMMSS.sql.gz
```

Production :

```bash
COMPOSE_FILE=docker-compose.prod.yml ENV_FILE=.env.prod ./scripts/restore.sh backups/quotidien_YYYYMMDD_HHMMSS.sql.gz
```

Le script détecte aussi quel Compose est en cours s'il trouve un `mariadb` prod déjà démarré.

Cycle de test recommandé :

1. Créer une transaction Finance (ou une donnée visible).
2. Lancer une sauvegarde manuelle.
3. Supprimer la donnée depuis `/finance`.
4. Restaurer le fichier `.sql.gz` (confirmation `OUI`).
5. Recharger `/finance` : la donnée est revenue.

## Logs et health checks

### Fichiers de logs (Serilog)

`App.Core` journalise avec Serilog (console + fichier). Propriétés structurées (pas de `string.Format`) pour : activation de module, changement de rôle, échec de connexion MariaDB, health checks non OK.

| Destination | Où | Niveau | Rotation |
| --- | --- | --- | --- |
| Console (`docker compose logs app-core`) | stdout du conteneur | Information en Development, Warning en Production | — |
| Fichier | hôte `./logs/gaia-YYYYMMDD.log` → conteneur `/logs` | Information et plus | un fichier par jour, **14** fichiers conservés |

Le bind-mount `./logs:/logs` survit à la recréation du conteneur. Variables Compose : `HealthChecks__LogsPath=/logs`, `HealthChecks__BackupsPath=/backups`.

Niveaux (namespaces) :

- Production (`appsettings.json`) : `Microsoft.EntityFrameworkCore.Database.Command` = **Warning** (pas chaque `SELECT`).
- Development (`appsettings.Development.json`) : Command = **Information** (SQL visible pour le débogage).

Les dumps `mariadb-backup` utilisent le même préfixe d'horodatage : `yyyy-MM-dd HH:mm:ss [INF|ERR] …` (`docker compose logs mariadb-backup`).

### Endpoints

| URL | Rôle | Docker HEALTHCHECK |
| --- | --- | --- |
| `GET /health/live` | Texte `ok` si le processus répond. Aucun check (base, disque, sauvegarde). | **Oui** (`Dockerfile` et `docker-compose.prod.yml`) |
| `GET /health` | JSON détaillé (statut par check, via HealthChecks UI ResponseWriter). | Non : un cron de backup en retard ne doit pas faire redémarrer `app-core`. |

Les deux routes sont anonymes. En HTTPS : `curl.exe --ssl-no-revoke https://127.0.0.1/health`.

Un *publisher* exécute les checks toutes les **15 s** (après 5 s au démarrage) et écrit un Warning/Error Serilog si un check n'est pas Healthy — MariaDB down apparaît sans attendre un clic utilisateur.

### Liste des checks et seuils

| Nom | Unhealthy | Degraded (orange) | Healthy |
| --- | --- | --- | --- |
| `mariadb` | Connexion MySQL/MariaDB impossible (timeout 3 s, `AspNetCore.HealthChecks.MySql`) | — | `SELECT` ping OK |
| `espace-disque` | Espace libre du volume de `/backups` **< 1 Gio** (`HealthChecks:DiskUnhealthyBytes`) | Sous **5 Gio** (`DiskWarningBytes`) | Au-dessus de 5 Gio |
| `derniere-sauvegarde` | Aucun `quotidien_*.sql.gz`, dossier absent, ou dump le plus récent **> 26 h** (`BackupMaxAgeHours`, marge sur le cron `0 3 * * *`) | — | Dump plus récent que 26 h |

La page `/admin/supervision` (rôle Admin) : badges Vert / Orange / Rouge **avec icône** (coche / point d'exclamation / croix), 20 dernières lignes Warning/Error du fichier du jour, taille et nombre de dumps, bouton **Rafraîchir** (circuit Blazor, sans recharger la page). Un point d'alerte apparaît aussi sur l'entrée **Administration** du menu et sur l'onglet Supervision dès qu'un check n'est pas Healthy.

Raccourcis d'urgence (favoris, téléphone) :

| URL | Onglet |
| --- | --- |
| `/admin` | Redirige vers `/admin/modules` |
| `/admin/modules` | Activer / désactiver Finance, Travail et Courses |
| `/admin/finance` | Comptes, charges, catégories, période de prévision (module Finance actif) |
| `/admin/travail` | Employeurs, soldes de congés, couleurs des types (module Travail actif) |
| `/admin/travail/soldes-conges` | Jours acquis par employeur et par année (redirige depuis `/travail/employeurs/{id}/solde-conges`) |
| `/admin/course` | Magasins, catégories Courses, purge d'historique (module Courses actif) |
| `/admin/utilisateurs` | Rôles Identity |
| `/admin/systeme` | Environnement et ping MariaDB |
| `/admin/supervision` | Health checks, dumps, logs WRN/ERR |

Pour un test rapide du check sauvegarde sans attendre 26 h : `HealthChecks__BackupMaxAgeHours=0.01` le temps du test, ou reculer la date du fichier (`touch -d '2 days ago'` dans le conteneur, ou `LastWriteTime` sur l'hôte Windows).

## GitHub Container Registry

L'image de production est :

- `ghcr.io/mdulche/gaia-life:latest`
- `ghcr.io/mdulche/gaia-life:<sha-court>` (7 premiers caractères du commit)

Le workflow `.github/workflows/build.yml` construit, teste (s'il existe des projets de test), puis pousse ces deux tags **uniquement** sur un push vers `main` (pas les pull requests). Permissions requises : `packages: write` (déjà dans le workflow, via `GITHUB_TOKEN`).

Le package GitHub apparaît sous le dépôt : **Packages** → `gaia-life`. La visibilité suit le dépôt (privé si le repo est privé). À ajuster dans les paramètres du package si vous voulez le rendre public.

### Connexion Docker sur le serveur prod

Le `GITHUB_TOKEN` de la CI ne sert **pas** sur le serveur. Créez un Personal Access Token (fine-grained ou classic) en **lecture seule** sur les packages (`read:packages`), distinct du token CI.

```bash
echo VOTRE_PAT | docker login ghcr.io -u mdulche --password-stdin
```

`docker-compose.prod.yml` référence `ghcr.io/mdulche/gaia-life:${APP_IMAGE_TAG:-latest}`. `APP_IMAGE_TAG` se règle dans `.env.prod` (exemple : `latest` ou un sha court pour pinner une version).

Un `docker compose ... --build` local reste possible : Compose tague alors l'image construite avec le même nom.

## Mise à jour de production

Le déploiement prod est **manuel** : pas de webhook ni de mise à jour automatique. Cela évite une surprise sur l'infra du foyer.

Sur le serveur, dans le clone du dépôt :

```bash
chmod +x scripts/deploy-prod.sh scripts/restore.sh scripts/backup.sh
./scripts/deploy-prod.sh
```

Le script :

1. `git pull origin main` (compose, Dockerfile, scripts).
2. Sauvegarde MariaDB via `mariadb-backup` (`scripts/backup.sh`).
3. `docker compose --env-file .env.prod -f docker-compose.prod.yml pull` (image GHCR).
4. `docker compose ... up -d` (recrée les conteneurs dont l'image a changé).
5. Attend que le healthcheck `app-core` (`GET /health/live`) soit `healthy`, code de retour non zéro sinon.

Équivalent manuel historique (sans pull GHCR, reconstruction locale) :

```bash
git pull
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --build
```

MariaDB n'est pas exposée hors du réseau Docker interne. Le volume `./backups` est monté sur `/backups`. Les logs Serilog sont sur `./logs` → `/logs`.

Après un schéma de base nouveau, les migrations s'appliquent au démarrage de `app-core` (`MigrateAsync`). Vous pouvez aussi lancer `dotnet ef database update` depuis une machine autorisée.

## Rollback

1. Identifier le sha court précédent (Packages GHCR ou `git log`).
2. Sauvegarder l'état actuel (`scripts/backup.sh`) au cas où.
3. Dans `.env.prod`, poser `APP_IMAGE_TAG=<sha-court-précédent>`.
4. `docker compose --env-file .env.prod -f docker-compose.prod.yml pull && docker compose --env-file .env.prod -f docker-compose.prod.yml up -d`
5. Si le schéma de base a avancé et n'est plus compatible, restaurer aussi le dump d'avant la mise à jour (`scripts/restore.sh`), **après** confirmation `OUI`.

## Vérification phase 4

À relire après un déploiement réel (homelab + téléphone) :

| Volet | Attendu | Comment vérifier |
| --- | --- | --- |
| PWA | Installation Android Chrome et, si possible, iOS Safari | HTTPS (proxy) ; menu « Ajouter à l'écran d'accueil » ; ouverture `standalone` sans barre d'URL. En Dev, le SW est absent (normal). |
| Sauvegarde | Dump nocturne + restauration testée | `ls -l backups/` (horodatage le plus récent) ; cycle insert → dump → suppression → `restore.sh`. |
| GHCR / déploiement | Image poussée, `deploy-prod.sh` OK | Après un push `main` : package `ghcr.io/mdulche/gaia-life`. Sur le serveur : `./scripts/deploy-prod.sh` (sauvegarde puis healthcheck `/health/live`). |

Le déploiement prod reste **manuel** (pas de webhook).

## Vérification phase 5

À relire après un changement logs / health / `/admin` :

| Volet | Attendu | Comment vérifier |
| --- | --- | --- |
| Serilog console | Lignes `[HH:mm:ss INF]` / `WRN` / `ERR` | `docker compose --env-file .env.dev -f docker-compose.dev.yml logs app-core` |
| Serilog fichier | Fichier du jour sur l'hôte | `Get-ChildItem logs` (Windows) ou `ls -l logs/` ; ouvrir `logs/gaia-YYYYMMDD.log` |
| `/health/live` | `ok`, HTTP 200 même si un dump a du retard | `curl.exe --ssl-no-revoke https://127.0.0.1/health/live` ou `http://localhost:8080/health/live` |
| `/health` | JSON `entries.mariadb`, `espace-disque`, `derniere-sauvegarde` | Même URL sans `/live` |
| MariaDB coupée | `mariadb` Unhealthy, badge rouge `/admin/supervision`, logs WRN/ERR | Déjà connecté sur `/admin/supervision` ; `docker compose ... stop mariadb` ; **Rafraîchir** sous 15–20 s ; puis `start mariadb` |
| Sauvegarde trop vieille | `derniere-sauvegarde` Unhealthy | Reculer `LastWriteTime` d'un `quotidien_*.sql.gz` de 27 h, Rafraîchir ; restaurer la date ensuite |
| Supervision | 20 lignes WRN/ERR + stats `/backups` | Onglet `/admin/supervision` après les deux tests ci-dessus |

## Vérification phase 6

À relire après un changement de navigation / accueil / onglets admin :

| Volet | Attendu | Comment vérifier |
| --- | --- | --- |
| Sidebar desktop | Section Modules + Administration en bas, bouton « replier » | Viewport ≥ 641 px ; le menu réduit n'affiche plus que les icônes. |
| Hamburger | Menu fermé par défaut, ouverture au pictogramme | Viewport 375 px ; Accueil, Finance, Travail, Courses, Administration accessibles. |
| Menu si MariaDB coupée | Accueil / Administration restent visibles | `docker compose ... stop mariadb` ; le menu ne doit pas afficher d'erreur 500. |
| Onglets `/admin` | 7 URLs distinctes | `/admin/modules`, `/admin/finance` (si Finance actif), `/admin/travail` (si Travail actif), `/admin/course` (si Courses actif), `/admin/utilisateurs`, `/admin/systeme`, `/admin/supervision` |
| Badge alerte | Point sur Administration + onglet Supervision | Couper MariaDB : le pictogramme × apparaît ; au retour de la base, il disparaît (≤ 15 s ou Rafraîchir). |
| Widgets | `WidgetCard` commun, grille 2 / 1 colonnes | Accueil : cartes Finance, Travail et Courses mêmes titres / métriques ; 375 px = une colonne. |
| État vide | Carte unique dans la grille, bouton vers `/admin/modules` | Désactiver les modules, ou couper MariaDB : le menu Accueil / Administration reste affiché. |
