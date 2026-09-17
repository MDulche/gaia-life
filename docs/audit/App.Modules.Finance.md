# Audit — App.Modules.Finance

Date : 2026-09-17 · Périmètre : pages, services, data, liaisons Finance.

---

## Sécurité

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| Pages métier | `FinanceHome`, `CompteDetail`, `TransactionForm`, `VirementForm`, `FinanceSynthese` | — | Toutes ont `[Authorize]` + `FinanceAccess.EnsureActiveAsync` (guard module) y compris `/finance/comptes/{id}`, formulaires. | Conserver le duo Authorize + guard sur toute nouvelle page. |
| Composants Admin | `AdminFinance*` | Mineur | Pas d’`EnsureActiveAsync` module (normal : Admin gère modules inactifs) ; protection via page Admin `[Authorize(Roles=Admin)]`. | OK si toujours rendus uniquement sous `/admin`. |
| `Liaisons/ArticleAcheteFinanceSubscriber.cs` | 38–42 | **Majeur** | `HandleAsync(evenement).GetAwaiter().GetResult()` dans handler synchrone du bus → sync-over-async (deadlock / thread pool starvation possibles sous charge). | Rendre le bus async ou `Task.Run` + scope DI avec journalisation ; idéalement `async` end-to-end. |
| `Data/FinanceDbContextFactory.cs` | ~23 | Mineur | Fallback `Password=changeme` design-time. | Idem Core. |
| SQL brut | — | — | Aucun `FromSqlRaw` / concat utilisateur. | — |
| Validation serveur | `FinanceService` (ex. `AjouterTransaction`, `EffectuerVirement`, charges) | Mineur | Validations métier impératives dans le service (pas systématiquement DataAnnotations avant `SaveChanges`). Les pages passent par le service — OK tant qu’aucun accès DbContext direct depuis UI. | Garder toute écriture via le service ; renforcer contrôles montant/compte/catégorie sur chaque méthode d’écriture. |
| Logs subscriber | ligne 46 | — | Log erreur avec `ArticleId` seulement — pas de montant/PII en clair. | OK. |
| Secrets hardcodés runtime | — | — | Aucun hors factory design-time. | — |

**ActiveModuleGuard** : appliqué sur toutes les routes pages listées ci-dessus. Aucune page Finance métier sans auth détectée.

---

## Performance

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| Index | `FinanceDbContext` 65–67 | — | Index `(CompteId, Date)`, `TransfertId`, `EstVirementInterne` — couvre filtres/tris fréquents par compte+date. | Index `Date` seul inutile si les requêtes filtrent toujours par compte. |
| `ListerTransactions` | 361+ | Mineur | Variante non paginée encore exposée (UI détail utilise `ListerTransactionsPaged` ~ CompteDetail:204). | Déprécier / limiter la variante non paginée ; forcer Take max. |
| `DernieresTransactionsAsync` / totaux | 500+, 552 | Mineur | Chargements bornés ou agrégés — OK pour dashboard ; `TotauxParMois` charge transactions sur N mois en mémoire pour regrouper. | Si volume élevé : grouper en SQL (`GroupBy` mois). |
| `IDbContextFactory` | tout le service | — | `await using` systématique — conforme. | — |
| Lazy loading | — | — | Pas de proxies ; `Include` là où besoin (`Compte` sur charges, objectif). | — |
| Recalcul UI | `FinanceHome` | — | Données chargées dans `OnInitializedAsync` ; pas de recalcul camembert à chaque checkbox (pas de liste interactive lourde sur cette page). | OK. |
| `.Result` / `.Wait()` | subscriber | **Majeur** | Voir Sécurité (sync-over-async). | — |

---

## Qualité / Commentaires

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| Commentaires provisoires | `FinanceService` 928+, `FinanceHome` 58 | — | Prévision fin de mois documentée comme estimation d’affichage — présent et à jour. | Conserver. |
| Nommage | service | Mineur | Mélange `Async` suffix / sans (`SoldeActuel`, `AjouterTransaction`, `ListerTransactions` vs `ListerComptesAsync`). | Aligner progressivement sur `*Async` pour toutes les méthodes async. |
| XML `///` | nombreuses méthodes publiques | Mineur | Plusieurs méthodes publiques sans summary (ex. `ListerComptesAsync`, `GetCompteAsync`, `AjouterCompteAsync`, `ModifierCompteAsync`, CRUD charges, etc.) alors que d’autres sont documentées. | Ajouter un `/// <summary>` minimal sur chaque API publique. |
| Migrations | `InitialFinance`, `AjoutChargesEtPrincipal`, `AjoutTypeCompteEtObjectifEpargne`, `AjoutVirementInterne` | — | Noms cohérents avec le contenu. | — |
| TODO/FIXME | — | — | Aucun. | — |
| Code mort | — | — | Pas d’ancien `BasculerAchete` (Finance). Boutons ticket « Coming soon » sur FinanceHome — placeholder UI assumé. | Remplacer ou retirer quand le besoin tickets est cadré. |

---

## Synthèse module

**Majeur** : sync-over-async dans `ArticleAcheteFinanceSubscriber`. Reste essentiellement Mineur (docs XML, naming Async, liste non paginée legacy).
