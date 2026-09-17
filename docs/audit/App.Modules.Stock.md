# Audit — App.Modules.Stock

> **Annotation** — les mentions MariaDB / dashboard de ce rapport sont historiques ; l’app active est SQLite-only (Android).

Date : 2026-09-17 · Périmètre : pages, services, data, liaisons Stock.

---

## Sécurité

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `StockHome.razor` | 3, 5, 177 | — | `[Authorize]` + `StockAccess.EnsureActiveAsync`. | OK. |
| Admin Stock | — | Mineur | Sous Admin role, sans guard module. | OK. |
| `Liaisons/ArticleAcheteStockSubscriber.cs` | ~42 | **Majeur** | Même pattern que Finance : `HandleAsync(…).GetAwaiter().GetResult()`. | Migrer vers handlers async / bus async. |
| `StockService.VerrouillerArticleAsync` | 392–394 | — | `ExecuteSqlInterpolatedAsync` avec `{articleStockId}` — **paramétré** (pas de concat string) — conforme. | Conserver Interpolated, jamais Raw concaténé. |
| Validation | `AjusterQuantiteAsync`, `AjouterArticleAsync`, inventaire | — | Règles quantité / unicité nom normalisé côté service avant Save. | OK. |
| Secrets | factory | Mineur | Fallback design-time. | Idem. |
| NuGet | `App.Modules.Stock.Tests` | **Majeur** (tests) / Mineur (prod) | Transitif `SQLitePCLRaw.lib.e_sqlite3` **2.1.10** — gravité **High** — [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q). Projets app Stock : aucun vulnérable. | Mettre à jour le package de test / EF SQLite vers une version corrigée ; ne pas livrer ce binaire en prod (tests only aujourd’hui). |

---

## Performance

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| Index | `StockDbContext` 48–62 | — | `ArticleStockId`, `DateMouvement`, `NomNormalise` — OK pour mouvements / filtres. | OK. |
| `ListerArticlesAsync` | 22–28 | Mineur | Charge **tous** les articles + catégorie sans pagination. | Paginer / virtualiser si catalogue large. |
| `ListerMouvementsAsync` | 39–54 | — | `Take(take)` défaut 50 — bonne pratique. | Exposer pagination UI si besoin. |
| Factory + transaction | ajustements | — | Contextes courts ; verrou `FOR UPDATE` sur MariaDB pour courses concurrentes. | OK. |
| Lazy loading | — | — | Include Categorie sur listes. | — |
| `.Result` | subscriber | **Majeur** | Voir Sécurité. | — |

---

## Qualité / Commentaires

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| Nommage | service | — | Plus cohérent `*Async` que Course/Travail sur plusieurs méthodes (`ListerArticlesAsync`, `AjusterQuantiteAsync`). | Prendre Stock comme référence de naming. |
| XML docs | service | Mineur | Doc sur ajustement / ajout / inventaire ; manquante sur listes / CRUD catégories / suppression. | Compléter. |
| Migrations | `InitialStock`, `StockMouvementsEtUnicite` | — | Noms alignés (mouvements + unicité nom). | — |
| TODO/FIXME | — | — | Aucun. | — |
| Dockerfile (impact build) | voir infra | **Majeur** | `App.Modules.Stock.csproj` absent du `COPY` avant `dotnet restore` → restore solution incomplet puis `publish --no-restore` fragile. | Traité dans rapport Infrastructure. |

---

## Synthèse module

**Majeur** : sync-over-async subscriber ; CVE transitive tests SQLitePCLRaw ; (infra) restore Docker sans csproj Stock.
