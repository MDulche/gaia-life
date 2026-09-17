# Synthèse audit Gaia-Life — Critique & Majeur

> **Document historique** — audite le dashboard web désormais archivé sous `archive/web-dashboard/`, ne concerne plus le dépôt actif Android.

Date : 2026-09-17 · Uniquement sévérités **Critique** et **Majeur**, toutes catégories, tri **Critique → Majeur** puis **module**.

Aucun point **Critique** n’a été confirmé sur l’ensemble du périmètre audité.

Détails et Mineurs : voir `docs/audit/<module>.md`.

---

## Critique

*(aucun)*

---

## Majeur

### App.Core

1. **Cookies Identity** — `Program.cs` ~111–116 — `SecurePolicy` / `SameSite` non figés pour la Production (`SameAsRequest` + ForwardedHeaders).  
   → Forcer `CookieSecurePolicy.Always` (+ SameSite explicite) en Production.

2. **ForwardedHeaders** — `Program.cs` ~139–144 — `KnownNetworks` / `KnownProxies` vidés → confiance totale aux en-têtes `X-Forwarded-*`.  
   → Restreindre au réseau du reverse proxy Docker.

### App.Modules.Finance

3. **Sync-over-async** — `Liaisons/ArticleAcheteFinanceSubscriber.cs` ~42 — `HandleAsync(…).GetAwaiter().GetResult()` sur événement Course.  
   → Handlers / bus async sans `.GetResult()`.

### App.Modules.Travail

4. **N+1 EF** — `TravailService.ListerSoldesEmployeursActifsAsync` ~609–636 — requêtes solde + jours pris **par** employeur actif.  
   → Agrégation SQL / chargement groupé.

### App.Modules.Course

5. **Recalcul UI coûteux** — `CourseHome.razor` `ReloadAsync` après chaque `MarquerAchete` — listes + `RepartitionParCategorie` à chaque coche.  
   → Mise à jour locale / recalcul camembert différé.

6. *(Impact)* Publication synchrone vers bus → déclenche les Majeurs Finance/Stock subscribers.

### App.Modules.Stock

7. **Sync-over-async** — `Liaisons/ArticleAcheteStockSubscriber.cs` ~42 — même pattern que Finance.  
   → Idem correction async.

8. **CVE transitive (tests)** — `SQLitePCLRaw.lib.e_sqlite3` 2.1.10 — High — [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) via `App.Modules.Stock.Tests`.  
   → Mettre à jour la dépendance de test (projets runtime app : aucun package vulnérable signalé).

### Infrastructure

9. **USER root** — `Dockerfile` runtime — pas d’utilisateur non-root.  
   → `USER` dédié non-root.

10. **Restore Docker incomplet** — `Dockerfile` ~4–10 — `App.Modules.Stock.csproj` non copié avant `dotnet restore`, puis `publish --no-restore`.  
    → Inclure le csproj Stock (et tout projet de la solution) avant restore.

11. **Secrets par défaut prod** — `docker-compose.prod.yml` — fallbacks `changeme` / `changeme_root`.  
    → Exiger des variables obligatoires sans défaut faible.

12. **Exposition réseau dev** — `docker-compose.dev.yml` — `8080` et `3306` publiés sans `127.0.0.1`.  
    → Bind localhost (ou ne pas publier MariaDB).

---

## Hors synthèse (rappel utile)

- **Authorize + ActiveModuleGuard** : OK sur les pages métier Finance / Travail / Course / Stock (y compris routes secondaires Finance/Travail). Admin module sections = rôle Admin, sans guard module (attendu).
- **Index** Demandés (Transaction date/compte, EmployeurId, MagasinId, ArticleStockId) : présents via index composites.
- **SQL concat dangereux** : non trouvé ; `ExecuteSqlInterpolated` Stock OK ; `SELECT 1` Admin OK.
- **Health `/health/live`** : n’interroge pas MariaDB ; publisher ~15 s acceptable.

Fichiers du rapport : `App.Core.md`, `App.Shared.md`, `App.Modules.Finance.md`, `App.Modules.Travail.md`, `App.Modules.Course.md`, `App.Modules.Stock.md`, `Infrastructure.md`, `SYNTHESE.md`.
