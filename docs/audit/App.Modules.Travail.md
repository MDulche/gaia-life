# Audit — App.Modules.Travail

Date : 2026-09-17 · Périmètre : pages, services, data Travail.

---

## Sécurité

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `TravailHome.razor`, `EmployeurDetail.razor`, `SoldeConges.razor` | Authorize + guard | — | `[Authorize]` + `TravailAccess.EnsureActiveAsync` sur home, détail employeur (`/travail/employeurs/{id}`), ancienne route solde. | OK. |
| Admin `AdminTravail*` | — | Mineur | Pas de guard module (attendu sous Admin role). | OK. |
| SQL brut | — | — | Aucun. | — |
| Validation serveur | `AjouterFichePaie`, `DemanderConge`, `ChangerStatutConge`, HS, soldes | — | Règles métier (net≤brut, chevauchements, solde Paye, rôle Admin pour validation) appliquées dans le service avant `SaveChanges`. | Conserver ; ne pas écrire depuis les pages directement. |
| Secrets | `TravailDbContextFactory` | Mineur | Fallback design-time `changeme`. | Idem autres modules. |
| Logs | — | — | Pas de log de données paie/congé en clair repéré. | — |

**Pages métier sans auth** : aucune détectée hors redirections Account.

---

## Performance

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `TravailService.ListerSoldesEmployeursActifsAsync` | 609–636 | **Majeur** | Boucle `foreach` employeurs actifs avec **2 requêtes async par employeur** (`SoldeConges` + `JoursPrisValidesAsync`) → N+1 classique. | Charger soldes / congés validés en 1–2 requêtes groupées (`Where EmployeurId in …`, `GroupBy`), puis assembler en mémoire. |
| Index | `TravailDbContext` 62–101 | — | `(EmployeurId, Mois)` fiches, `(EmployeurId, DateDebut)` congés, `(EmployeurId, Annee)` soldes, `(EmployeurId, Date)` HS — conformes aux filtres demandés. | OK. |
| Listes | `ListerConges`, `ListerFichesPaie`, `ListerHeuresSup` | Mineur | Pas de pagination ; volumes foyer typiquement faibles. | Ajouter Take/Skip si historique multi-années grossit. |
| `IDbContextFactory` | — | — | `await using` respecté. | — |
| Lazy loading | — | — | Désactivé ; pas d’Include manquant critique identifié sur les listes principales. | — |
| Recalcul graphiques | `EmployeurDetail` | Mineur | Points salaire construits au reload ; vérifier qu’aucune interaction mineure ne rappelle tout le planning sans besoin. | Séparer reload partiel (congés vs fiches vs HS). |
| `.Result` / `.Wait()` | — | — | Aucun dans Travail. | — |

---

## Qualité / Commentaires

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `SoldeConges.razor` | 1–19 | Mineur | Ancienne route `/travail/employeurs/{id}/solde-conges` conservée comme redirect vers `/admin/travail/soldes-conges` — pas « morte », mais legacy. | Garder pour bookmarks ou supprimer après période de transition documentée. |
| Commentaires provisoires | `TravailService` 639–641, 964 | — | Projection solde fin d’année marquée « provisoire » — présents. | OK. |
| Graphique « à définir » | — | Mineur | Ancien placeholder camembert Travail remplacé par `TravailSalaireNetChart` réel sur EmployeurDetail — commentaire maquette obsolète non retrouvé (positif). | Si un autre bloc « à définir » subsiste en UI, le documenter ou le retirer. |
| Nommage vs Finance | service | Mineur | Même mélange `Async` / non (`ListerFichesPaie`, `SoldeCongesActuel` vs `ListerEmployeursAsync`). | Harmoniser avec Finance une fois convention figée. |
| XML docs | méthodes publiques | Mineur | Beaucoup de CRUD / listes sans `///` (`ListerEmployeursAsync`, `AjouterEmployeurAsync`, `ModifierFichePaie`, `ListerHeuresSup`, etc.). | Documenter les API publiques. |
| Migrations | `InitialTravail`, `CouleurTypeConge`, `AjoutHeuresSupplementaires` | — | Noms alignés. | — |
| TODO/FIXME | — | — | Aucun. | — |

---

## Synthèse module

**Majeur** : N+1 dans `ListerSoldesEmployeursActifsAsync`. Sécurité routes/guard globalement saine.
