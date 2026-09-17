# Audit — App.Modules.Course

Date : 2026-09-17 · Périmètre : pages, services, data, admin Course.

---

## Sécurité

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `CourseHome.razor` | 3, 8, 328 | — | `[Authorize]` + `CourseAccess.EnsureActiveAsync`. Seule page métier module (pas de détail secondaire type `/course/{id}`). | OK. |
| `AdminCourseCompteFinance.razor` | 3, 46 | Mineur | Injecte `IActiveModuleGuard` pour savoir si Finance est actif, pas pour guard Course ; sous Admin. | OK. |
| Validation | `CourseService.AjouterArticle` / `ModifierArticle` / `MarquerAchete` | Mineur | Contrôles null / existence ; pas de DataAnnotations centralisées systématiques avant Save. | Valider prix, nom, magasin côté service de façon uniforme. |
| SQL | — | — | Aucun SQL brut. | — |
| Secrets | factory design-time | Mineur | `Password=changeme`. | Idem. |
| Publication événement | `MarquerAchete` → bus | **Majeur** (impact) | `Publier` synchrone déclenche handlers Finance/Stock en `.GetResult()` (voir ces modules) depuis le flux UI Blazor. | Corriger les subscribers / bus (pas Course seul). |

---

## Performance

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| Index | `CourseDbContext` 56–58 | — | `Achete`, `(MagasinId, Achete)`, `DateAchat` — couvre filtres MagasinId / listes. | OK. |
| `ListerArticlesAEnAcheter` / `ListerArticlesAchetes` | 23–45 | Mineur | `Include` Categorie+Magasin ; listes complètes sans pagination (historique achetés peut grossir). | Paginer l’historique ; `ViderHistoriqueAchetesAsync` existe déjà — l’exposer clairement en Admin. |
| `CourseHome.ReloadAsync` | 337–342, 378, 461 | **Majeur** | Chaque bascule acheté / mutation rappelle `ReloadAsync` qui recharge listes **et** `RepartitionParCategorie` (camembert) — recalcul complet à chaque checkbox. | Après `MarquerAchete` : maj locale de la liste + recalcul camembert différé ou incrémental ; éviter double round-trip complet. |
| Factory | — | — | `await using` OK. | — |
| Lazy loading | — | — | Includes explicites. | — |

---

## Qualité / Commentaires

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| Code mort | — | — | Pas de `BasculerAchete` ; `MarquerAchete` est l’API unique. | OK. |
| Nommage | `ListerArticlesAEnAcheter`, `MarquerAchete` sans Async | Mineur | Écart vs Stock (`ListerArticlesAsync`) et vs suffixe Async Finance partiel. | Harmoniser. |
| XML docs | service | Mineur | Seules quelques méthodes ont `///` (`MarquerAchete`, `NormaliserOrdreMagasinsAsync`) ; majorité des CRUD sans doc. | Compléter. |
| Migrations | `InitialCourse`, `CourseLiaisonsEtOrdreUnique` | — | Noms cohérents (liaisons compte défaut + unicité ordre). | — |
| TODO/FIXME | — | — | Aucun. | — |
| Commentaires maquette | — | — | Pas de marqueur « provisoire » Course (OK métier liste). | — |

---

## Synthèse module

**Majeur** : reload complet + camembert à chaque coche ; effet de bord sync-over-async via bus (documenté aussi Finance/Stock).
