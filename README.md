# Gaia-Life

Application familiale **Android** (.NET MAUI Blazor Hybrid + SQLite embarqué).

Le dashboard web Blazor Server a été **archivé** (jamais mis en prod) sous [`archive/web-dashboard/`](archive/web-dashboard/README.md).

## Prérequis

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) avec charge de travail **MAUI** (`dotnet workload install maui`)
- Android SDK (via Visual Studio / workload MAUI)

## Solution active

```bash
dotnet build GaiaLife.sln
dotnet build src/App.Mobile/App.Mobile.csproj -f net9.0-android
```

Projets : `App.Mobile` (hôte), `App.Shared`, `App.Modules.Finance|Travail|Course|Stock`, tests.

## Contrat de migration

Avant de porter un module, lire **[`docs/CONTRAT-MIGRATION-MOBILE.md`](docs/CONTRAT-MIGRATION-MOBILE.md)** (SQLite commun, bus async, `ArticleAcheteEvent`, mono-utilisateur, naming `*Async`).

## Archive web

```bash
dotnet build archive/web-dashboard/GaiaLife.WebArchive.sln
```

## Emulateur Android

Double-clic : [`scripts/lancer-emulateur-gaialife.bat`](scripts/lancer-emulateur-gaialife.bat)  
Guide : [`docs/EMULATEUR-ANDROID.md`](docs/EMULATEUR-ANDROID.md)
