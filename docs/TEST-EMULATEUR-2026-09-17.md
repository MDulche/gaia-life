# Rapport de test émulateur — App.Mobile

**Date :** 2026-09-17  
**Cible :** émulateur `GaiaLifeTest` (`emulator-5554`), package `com.gaialife.mobile`  
**Méthode :** déploiement existant + interaction UI via WebView DevTools (CDP) + vérifications `adb` / SQLite  
**Périmètre :** diagnostic uniquement — **aucune correction de code** dans ce passage  

Artefacts : `docs/test-artifacts/` (captures PNG, scripts CDP, extractions DB).

---

## Synthèse

| Indicateur | Valeur |
|---|---|
| Modules testés | Course, Stock, Finance, Travail (+ transverses) |
| Modules portés (assemblies + nav) | **4/4** — aucun placeholder « non porté » |
| Tests exécutés (hors N-A) | **28** |
| PASS | **18** |
| FAIL | **10** |
| N-A | **3** |
| Taux de réussite (hors N-A) | **~64 %** |

### Bugs bloquants avant de continuer le portage

1. **Travail — crash à l’ouverture d’un employeur (P0)**  
   `EmployeurDetail` / `TravailService.ListerHeuresSupAsync` :  
   `SQLite does not support expressions of type 'TimeSpan' in ORDER BY clauses`  
   (`ThenByDescending(h => h.HeureDebut)`).  
   Empêche fiches de paie, congés, planning / jours fériés.

2. **Finance — comptes Épargne homonymes acceptés (P1)**  
   Deux comptes `Livret test` créés ; pas de contrainte d’unicité visible.

3. **UI — libellés « admin » / « administrateur » résiduels (P2 contrat)**  
   Ex. « Catégories & admin », « Un administrateur peut en créer depuis l'administration. »

### Non bloquants / à retester

- Saisie manuelle Entrée/Sortie : formulaires présents ; le harness CDP n’a pas réussi à binder les `InputNumber`/radios Blazor (le virement SQLite fonctionne).
- Réordonnancement magasins Course : UI ↑↓ dans `AdminCourseMagasins`, non exercée (0 magasin en base).
- Quantité stock &lt; 0 hors motif inventaire : pas d’UI de mouvement « libre » ; le chemin inventaire bloque une cible négative avec message clair.

---

## Transverses

| Test | Résultat | Détail |
|---|---|---|
| App démarre sans crash, Shell visible | **PASS** | Accueil « Gaia-Life », hamburger, nav Accueil/Finances/Travail/Courses/Stock |
| Chaque module porté accessible (pas de placeholder) | **PASS** | Routes `/finance`, `/travail`, `/course`, `/stock` OK ; `Routes.razor` charge les 4 assemblies |
| Persistance après force-stop + relance | **PASS** | Après `am force-stop` : comptes Finance, employeur Travail, stock Riz, suppression Course conservés |
| Fichier `gaialife.db` présent et croît | **PASS** | `files/gaialife.db` ~4 Ko → **221 184** o (+ WAL ~387 Ko après activité) |
| Pas d’IA / réseau activable dans l’UI | **PASS** | Aucun contrôle OpenAI/ChatGPT/sync cloud observé sur les écrans parcourus |
| Pas de login / rôles Admin·Membre·Lecture | **FAIL** | Pas de login ; mais libellés **« admin »** / **« administrateur »** / **« administration »** encore visibles (Stock, Finance) |

---

## Module Course

| Test | Résultat | Détail |
|---|---|---|
| Ajouter un article → apparaît tout de suite | **PASS** | « Lait test emulator » listé immédiatement |
| `MarquerAchete` + taps rapides (idempotence) | **PASS** | 3 clics rapides → liste vide, « Achats du mois / Divers / 1 article » (pas de double comptage) |
| Camembert / répartition maj sans reload complet | **PASS** | Bloc « Achats du mois » mis à jour in-place après coche |
| Supprimer → absent après redémarrage | **PASS** | Confirm JS ; après kill/relaunch : plus de « Lait », `CourseArticles` sans cet id ; DB confirmée |
| Réordonner magasins, ordre conservé | **N-A** | 0 magasin ; UI ↑↓ existe dans `AdminCourseMagasins` mais non testée bout-en-bout |
| Liaison Course → Stock (`ArticleAcheteEvent`) | **PASS** | Achat « Riz courses lien » lié → `StockArticles.Quantite` 8→**9** ; mouvement motif `Achat Courses` |

---

## Module Stock

| Test | Résultat | Détail |
|---|---|---|
| Ajouter + ajuster quantité +/− | **PASS** | Catégorie « Alimentaire », « Riz test » ; inventaire 5→8 (mouvements `Stock initial` + `Ajustement inventaire`) |
| Quantité &lt; 0 sans motif inventaire | **PASS*** | UI inventaire refuse cible −1 : *« La quantité cible ne peut pas être négative. »* (garde-fou UI). Le motif libre hors inventaire n’est pas exposé en UI. |
| Unicité nom (casse/espaces) / fusion | **PASS** | « riz TEST » → proposition **Fusionner** |
| Liaison depuis Course | **PASS** | Voir Course ci-dessus |

\*Conforme à l’esprit du test côté utilisateur ; le chemin service `AjusterQuantiteAsync` hors inventaire n’a pas d’écran dédié.

---

## Module Finance

| Test | Résultat | Détail |
|---|---|---|
| Créer compte + Entrée + Sortie, soldes OK | **FAIL** | Comptes créés OK. Formulaire transaction présent (`/transactions/nouvelle`) ; soumission CDP **n’a pas** créé de lignes (solde resté 100 €, table `Transactions` sans Entrée/Sortie manuelles). À revalider manuellement. |
| Virement épargne atomique + `EstVirementInterne` | **PASS** | Courant 100→**80** €, Livret 50→**70** € ; 2 txs même `TransfertId` `01BC8612-…`, `EstVirementInterne=1`, catégories « Virement interne » |
| Virement exclu camembert / totaux mensuels | **PASS** | Home : « Dépenses du mois — Aucune sortie enregistrée ce mois-ci » malgré −20 € de virement |
| Supprimer catégorie utilisée → blocage + réassignation | **FAIL** | Non démontré bout-en-bout (pas de catégorie métier utilisée par une tx manuelle). Code : `CategorieEncoreUtiliseeException` + `ReassignerEtSupprimerCategorieAsync` présents — **non vérifié UI**. |
| Unicité comptes (observation) | **FAIL** | Deux `Livret test` (id 2 et 3) en base |

---

## Module Travail

| Test | Résultat | Détail |
|---|---|---|
| Ajouter employeur | **PASS** | « Employeur Test SA » listé ; persiste après restart |
| Fiche de paie + affichage net/brut | **FAIL** | Ouverture détail employeur → banner *An unhandled error has occurred* |
| Congé Payé &gt; solde → bloqué | **FAIL** | Inaccessible (crash détail) |
| Chevauchement congés à la création | **FAIL** | Inaccessible (crash détail) |
| Planning mensuel + jour férié FR | **FAIL** | Inaccessible (crash détail) |

**Cause racine (logcat) :**

```text
SQLite does not support expressions of type 'TimeSpan' in ORDER BY clauses
  at TravailService.ListerHeuresSupAsync
  at EmployeurDetail.ReloadAsync / OnParametersSetAsync
```

Fichier suspect : `src/App.Modules.Travail/Services/TravailService.cs` (~L164–167 : `OrderByDescending(Date).ThenByDescending(HeureDebut)`). Même risque sur le planning (`OrderBy(h => h.HeureDebut)`).

---

## Preuves SQLite (extrait final)

| Table | Constat |
|---|---|
| `gaialife.db` | 221 184 octets (+ WAL actif) |
| `StockArticles` | Riz test qty **9** |
| `StockMouvements` | +5 initial, +3 inventaire, +1 `Achat Courses` |
| `Comptes` | 3 (dont 2× Livret test) |
| `Transactions` | 2 jambes virement, même transfert |
| `Employeurs` | 1 |
| `Conges` / `FichePaies` | 0 (détail inaccessible) |

---

## Priorisation suggérée des prompts de correction

1. **P0** — SQLite/`TimeSpan` ORDER BY dans Travail (`ListerHeuresSupAsync` + planning).  
2. **P1** — Unicité nom de compte Finance (ou fusion).  
3. **P1** — Retester manuellement Entrée/Sortie + suppression catégorie utilisée.  
4. **P2** — Renommer libellés Admin/administrateur pour le contrat mobile.  
5. **P3** — Exercer réordonnancement magasins Course (créer 2+ magasins puis ↑↓ + restart).

---

*Rapport généré le 2026-09-17 — pas de patch appliqué.*
