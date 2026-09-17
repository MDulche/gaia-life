# Audit — App.Core

Date : 2026-09-17 · Périmètre : `src/App.Core/**` (auth, health, Program, Admin, Identity, data).

---

## Sécurité

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `Program.cs` | 111–116 | **Majeur** | `ConfigureApplicationCookie` ne fixe ni `Cookie.SecurePolicy`, ni `Cookie.SameSite`, ni explicitement `HttpOnly`. Les défauts Identity (`HttpOnly=true`, `SameSite=Lax`, `SecurePolicy=SameAsRequest`) dépendent de la requête perçue ; combinés aux ForwardedHeaders permissifs, un client peut influencer le flag Secure. | En Production : `Cookie.SecurePolicy = CookieSecurePolicy.Always`, `Cookie.SameSite = SameSiteMode.Lax` (ou `Strict` si acceptable), garder `HttpOnly = true`. |
| `Program.cs` | 139–144 | **Majeur** | `KnownNetworks.Clear()` / `KnownProxies.Clear()` fait confiance à tout `X-Forwarded-*`. Un accès direct à Kestrel (port 8080 bindé en prod sur `127.0.0.1` seulement — OK) reste risqué en scénarios de rebind/misconfig : spoof de Proto/For. | Restreindre aux réseaux Docker/proxy connus, ou n’activer ForwardedHeaders que derrière nginx avec réseau interne documenté. |
| `Program.cs` | 221–235 | Mineur | `/health` et `/health/live` sont `AllowAnonymous` (voulu). `/health` expose le détail des checks (MariaDB, disque, backup) à quiconque atteint l’app. | En prod, restreindre `/health` au réseau interne / IP admin ; laisser `/health/live` public pour le orchestrateur. |
| `Data/*DbContextFactory.cs` (Core) | 21 | Mineur | Fallback design-time `Password=changeme` hardcodé dans la factory EF. | Accepter pour tooling local uniquement ; ne jamais déployer sans `ConnectionStrings:Default`. Documenter clairement. |
| `Identity/SeedConfiguration.cs` | 12–19 | — | Mots de passe seed lus depuis config / env (`SeedAdmin:Password`, `SEED_ADMIN_PASSWORD`) — conforme. | Conserver ; ne jamais committer `.env.prod` avec secrets. |
| `Components/Pages/Admin.razor` | 5 | — | `[Authorize(Roles = AppRoles.Admin)]` — OK. | — |
| `Components/Account/Login.razor`, `Logout.razor`, `AccessDenied.razor`, `Error.razor` | AllowAnonymous | — | Seules pages anonymes métier hors health/static — OK. | — |
| `Program.cs` | 118–124 | — | `FallbackPolicy = RequireAuthenticatedUser` : Home et pages métier sans attribut sont protégées. | — |
| Pages métier sans auth | — | — | Aucune page métier Core accessible sans auth hors `/Account/Login` (et logout/denied/error/health). | — |
| Serilog | Program / health | — | Logs de chemins logs/backups, pas de mots de passe observés. | Éviter de logger connection strings ou corps de login. |
| NuGet vulnérables (projets app) | — | — | `dotnet list … --vulnerable` : **aucun** package vulnérable sur App.Core. | — |

---

## Performance

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| `Program.cs` | 75–80, 82–90 | Mineur | Publisher health : période défaut 15 s (min 5) → ping MySQL régulier + checks disque/backup. Pas « agressif » au sens surcharge, mais charge continue. | Garder ≥ 15–30 s en prod ; documenter `HealthChecks:PublishPeriodSeconds`. |
| `Program.cs` | 226–234 | — | `/health/live` : `Predicate = _ => false` → pas de hit MariaDB (Docker HEALTHCHECK OK). | — |
| `Admin.razor` | ~648 | Mineur | `ExecuteSqlRawAsync("SELECT 1")` pour test connectivité — littéral sans concat utilisateur. | OK ; préférer health check existant si redondant. |
| DbContext factory | Program 95–100 | — | Factory + scoped Identity bien séparés. | — |
| `.Result` / `.Wait()` | — | — | Aucun dans App.Core. | — |

---

## Qualité / Commentaires

| Fichier | Lignes | Sévérité | Problème | Recommandation |
|---------|--------|----------|----------|----------------|
| Migrations Core | noms | — | `InitialCreate`, `AjoutLiaisonModules`, `AjoutAppParametrage` — alignés avec le contenu. | — |
| TODO/FIXME | — | — | Aucun TODO/FIXME dans App.Core. | — |
| `ActiveModuleGuard.cs` | — | — | Implémentation claire via `ModuleManager` + DB. | — |
| DataProtection | Program 130–135 | Mineur | Clés sous `../../.dataprotection-keys` relatif au ContentRoot — fragile selon cwd Docker vs host. | Monter un volume dédié et fixer un chemin absolu via config. |

---

## Synthèse module

Points **Majeur** : cookies Identity non verrouillés en Secure Always ; ForwardedHeaders trop permissif. Pas de Critique identifié dans Core.
