# Audit — App.Shared

Date : 2026-09-17 · Périmètre : `src/App.Shared/**` (modules, events, contrats transverses).

---

## Sécurité

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| Ensemble Shared | — | — | Pas de routes Razor, pas de secrets, pas de SQL. Contrats (`IActiveModuleGuard`, `IEvenementBus`, liaisons) uniquement. | — |
| `Events/EvenementBus.cs` | 10–25 | Mineur | Bus synchrone in-process : les handlers bloquent l’appelant (`Publier`). Les subscribers Finance/Stock utilisent `GetAwaiter().GetResult()` côté modules (voir rapports Finance/Stock) — risque de deadlock sur sync-context Blazor/UI thread si le publisher est appelé depuis un circuit. | Faire évoluer le bus vers `Func<T, Task>` / `PublierAsync` pour permettre des handlers async sans sync-over-async. |
| NuGet | — | — | Aucun package vulnérable signalé sur App.Shared. | — |

---

## Performance

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `EvenementBus.cs` | 19–24 | Mineur | Handlers exécutés séquentiellement dans `Publier` ; un handler lent bloque les suivants et l’appelant (ex. `MarquerAchete`). | Async + éventuellement file / fire-and-forget contrôlé avec journalisation d’échecs. |
| Lazy loading | — | — | N/A (pas d’EF dans Shared). | — |

---

## Qualité / Commentaires

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `Modules/IActiveModuleGuard.cs` | — | — | Interface documentée / claire ; utilisée de façon homogène via `*Access.EnsureActiveAsync` dans les modules. | — |
| Conventions | — | Mineur | Shared définit les contrats ; les modules implémentent des helpers `FinanceAccess` / `TravailAccess` / `CourseAccess` / `StockAccess` quasi identiques — duplication volontaire OK, mais pas centralisée. | Optionnel : helper générique partagé `ModuleAccess.EnsureActiveAsync(key, …)`. |
| TODO/FIXME | — | — | Aucun. | — |
| XML docs | interfaces clés | — | Présents sur bus et guard. | Maintenir sur tout nouveau contrat public. |

---

## Synthèse module

Pas de Critique / Majeur propre à Shared ; le bus synchrone alimente les **Majeurs** des subscribers Course→Finance/Stock (documentés dans ces modules).
