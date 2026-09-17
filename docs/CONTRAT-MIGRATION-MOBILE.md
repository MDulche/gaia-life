# Contrat de migration — Gaia-Life Mobile (MAUI Blazor Hybrid)

Référence commune pour les agents qui portent Finance, Travail, Course et Stock.
Décision actée le **17/09/2026** : développement actif = app Android native ; le dashboard web est archivé sous `archive/web-dashboard/` (jamais mis en prod réelle).

---

## 1. Structure du dépôt

| Zone | Rôle |
| --- | --- |
| `GaiaLife.sln` | Solution **active** : `App.Mobile`, `App.Shared`, `App.Modules.*`, tests |
| `archive/web-dashboard/` | Dashboard Blazor Server + Docker + Identity (non maintenu) |
| `archive/web-dashboard/GaiaLife.WebArchive.sln` | Ouverture ponctuelle du web |
| `docs/CONTRAT-MIGRATION-MOBILE.md` | Ce fichier |

---

## 2. Hôte mobile (`App.Mobile`)

- Projet **.NET MAUI Blazor Hybrid**, cible principale `net9.0-android`.
- Shell de navigation avec routes `/finance`, `/travail`, `/course`, `/stock` (placeholders jusqu’au portage module).
- **Mono-utilisateur** : pas d’écran de login, pas de rôles `Admin` / `Membre` / `Lecture`, **pas** de `[Authorize]` / Identity côté mobile.
- Activation des modules : `ModuleManagerLocal` (Preferences / stockage local), pas de table d’admin multi-utilisateur.
- Aucune fonctionnalité IA ni réseau à ce stade ; placeholders web « coming soon » **absents**.

---

## 3. Modules métier (à porter par agent)

Chaque agent travaille dans `App.Modules.<Module>` déjà présent (entités, `DbContext`, services, composants Razor).

### 3.1 SQLite — un fichier commun

- **Un seul fichier** : `{FileSystem.AppDataDirectory}/gaialife.db`
- Chaque module garde son `DbContext` (entités + `OnModelCreating` inchangés).
- Migrations SQLite dans `App.Modules.<Module>/Migrations/Sqlite/` avec un `IDesignTimeDbContextFactory` **dédié** (ne pas régénérer / toucher les migrations MariaDB historiques, conservées dans l’archive web).
- Tous les `DbContext` pointent vers la **même** chaîne SQLite (`Data Source=…/gaialife.db`).

### 3.2 Bus d’événements (figé)

Canal unique entre modules : `IEvenementBus` **async** (`PublierAsync` / `Abonner(Func<T,Task>)`).  
Interdit : `.GetAwaiter().GetResult()`, `.Result`, `.Wait()` sur ce flux.

#### `ArticleAcheteEvent` (définition figée)

```csharp
namespace App.Shared.Events;

/// <summary>Publié par Courses au passage false → true (article acheté).</summary>
public sealed record ArticleAcheteEvent(
    int ArticleId,
    int? ArticleStockId,
    decimal? PrixEstime,
    DateTime DateAchat);
```

| Champ | Type | Notes |
| --- | --- | --- |
| `ArticleId` | `int` | Id article Course |
| `ArticleStockId` | `int?` | Lien Stock si renseigné |
| `PrixEstime` | `decimal?` | Pour sortie Finance si liaison active |
| `DateAchat` | `DateTime` | Date/heure d’achat |

Course publie ; Finance / Stock consomment. **Aucun** module ne référence directement un autre module métier.

### 3.3 Nommage async

Toutes les méthodes asynchrones des services publics se terminent par **`Async`** (`ListerComptesAsync`, `MarquerAcheteAsync`, etc.). À appliquer sur le code porté (corrige l’incohérence d’audit web).

### 3.4 Activation module

- Utiliser `IActiveModuleGuard` / `ModuleManagerLocal` (pas Identity).
- Si un module est désactivé : ne pas afficher la route / afficher le placeholder hôte.

---

## 4. Ce que chaque agent de module NE doit PAS faire

- Réintroduire Identity, cookies, rôles, `[Authorize]`.
- Ajouter un second fichier `.db` par module (sauf décision explicitement documentée ici).
- Modifier les migrations MariaDB de l’archive web.
- Dépendre de MariaDB / Docker / Serilog fichier / health checks web.
- Ajouter IA, sync cloud, ou appels réseau « à venir ».

---

## 5. Ordre de travail suggéré par module

1. Factory SQLite + dossier `Migrations/Sqlite/` + migrate au démarrage mobile.
2. Brancher DI dans `MauiProgram` via `IAppModule.ConfigureServices` (chaîne SQLite injectée).
3. Remplacer le placeholder de route par les pages Razor du module (adaptées mobile).
4. Suffixe `Async` + tests unitaires SQLite.
5. Liaisons via `IEvenementBus` uniquement (Course → Finance/Stock).

---

## 6. Références code

- Bus : `src/App.Shared/Events/`
- Contrats modules : `src/App.Shared/Modules/`
- Catalogue / activation locale : `ModuleCatalog` + `ModuleManagerLocal` (Shared / Mobile)
- Archive web : `archive/web-dashboard/README.md`
