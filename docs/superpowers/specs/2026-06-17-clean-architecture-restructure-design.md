# Backend Clean Architecture Restructure — Design

**Date:** 2026-06-17
**Status:** Approved (design)

## 1. Goal

Restructure the backend from a single `ExpenseTracker.Api` project (with `Domain/`, `Auth/`, `Currency/`, `Data/`, `Features/` folders) into a four-project Clean Architecture solution with compiler-enforced layer boundaries and conventional layer names. **Behavior must be identical** — all 26 existing tests stay green and the public API is unchanged. Auto-migration on startup is preserved.

This is a **refactor**: no new features, no API contract changes, no DTO field renames (the React frontend depends on the camelCase JSON contract).

## 2. Project layout & dependency rule

```
server/
├── ExpenseTracker.Domain/          → (no project references)
├── ExpenseTracker.Application/     → Domain
├── ExpenseTracker.Infrastructure/  → Application, Domain
├── ExpenseTracker.Api/             → Application, Infrastructure
└── ExpenseTracker.Tests/           → Api (transitively all)
```

Dependencies point inward. Domain references nothing (not EF, not HTTP). Application references `Microsoft.EntityFrameworkCore` (for `DbSet<>`/`IQueryable` in the `IApplicationDbContext` port) but **not** Npgsql. Npgsql/HTTP/crypto live only in Infrastructure. The Api is the composition root.

Style: **pragmatic use-case services** — no MediatR/CQRS, no repository pattern. EF Core is accessed through an `IApplicationDbContext` interface (the DbContext is the unit of work). This is the Jason-Taylor-style pragmatic CA.

## 3. Layer contents (mapping from today)

### Domain (`ExpenseTracker.Domain`)
- `Entities/`: `User, Category, Expense, Budget, ExchangeRate, Setting` (moved from `Domain/`).
- `DefaultCategories` (moved from `Data/DefaultCategories.cs`) — pure seed data.
- Namespace `ExpenseTracker.Domain.Entities` for entities, `ExpenseTracker.Domain` for `DefaultCategories`.

### Application (`ExpenseTracker.Application`)
- `Common/Interfaces/`:
  - `IApplicationDbContext` — exposes `DbSet<User> Users`, `DbSet<Category> Categories`, `DbSet<Expense> Expenses`, `DbSet<Budget> Budgets`, `DbSet<ExchangeRate> ExchangeRates`, `DbSet<Setting> Settings`, and `Task<int> SaveChangesAsync(CancellationToken ct = default)`.
  - `IExchangeRateProvider` (moved) — `Task<decimal> GetRateAsync(string from, string to, DateOnly date)`.
  - `IUserContext` — exposes the authenticated Telegram identity from the current request: `long? TelegramUserId`, `string? FirstName`, `string? Username`. (The HttpContext-reading implementation lives in Api/Infrastructure.)
- `Common/CurrencyConverter.cs` (moved) — pure math.
- Use-case services (the logic currently **inline in** `Features/*Endpoints.cs`), each in its own folder with its DTOs:
  - `Expenses/ExpenseService.cs` + `ExpenseDtos.cs` — list/create/update/delete with per-user isolation, amount>0, category-ownership, currency-normalization, currency null-guard.
  - `Categories/CategoryService.cs` + `CategoryDtos.cs`.
  - `Budgets/BudgetService.cs` + `BudgetDtos.cs` — upsert with category-ownership + currency null-guard.
  - `Reports/ReportService.cs` + `ReportDtos.cs` — base-currency conversion using `ExchangeRateService` + `CurrencyConverter`, per-expense-date rate.
  - `Settings/SettingService.cs` + `SettingDtos.cs`.
  - `Users/UserProvisioningService.cs` + interface `ICurrentUser` returning `Task<User> GetOrCreateAsync()` — find-or-create by Telegram id (from `IUserContext`), seed default categories + USD setting. (Logic moved from `CurrentUserAccessor`.)
  - `ExchangeRates/ExchangeRateService.cs` (moved) — cache-through using `IApplicationDbContext` + `IExchangeRateProvider`.
- `DependencyInjection.AddApplication(this IServiceCollection)` — registers the services + `CurrencyConverter`.
- Service methods take the resolved `User` (or use `ICurrentUser`) so isolation is enforced centrally; they return DTOs. Validation failures surface as a small `Result`/exception the Api maps to 400/404 — **keep it simple**: services return a discriminated result (`record OperationResult` with success/NotFound/BadRequest + payload) OR throw typed exceptions the Api translates. Chosen: services return `Results`-agnostic outcomes via a tiny `enum`-tagged result type so Application has no dependency on ASP.NET Core. The Api maps outcome → `Results.Ok/Created/NotFound/BadRequest`.

### Infrastructure (`ExpenseTracker.Infrastructure`)
- `Persistence/ApplicationDbContext.cs` — implements `IApplicationDbContext`; the EF model config (indexes, decimal types) moves here; Npgsql provider. Renamed from `AppDbContext`.
- `Persistence/Migrations/` — the existing `InitialCreate` migration + snapshot, moved here (namespace `ExpenseTracker.Infrastructure.Persistence.Migrations`). Because the DbContext now lives in Infrastructure, the migrations assembly is Infrastructure by default.
- `ExchangeRates/FrankfurterRateProvider.cs` (moved) — implements `IExchangeRateProvider`.
- `Identity/TelegramInitDataValidator.cs` (moved) — pure HMAC validation; `TelegramUser` record moves with it (or to Application — kept with the validator in Infrastructure/Identity).
- `Identity/UserContext.cs` — implements `IUserContext` by reading claims from `IHttpContextAccessor`.
- `DependencyInjection.AddInfrastructure(this IServiceCollection, IConfiguration)` — registers `ApplicationDbContext` (+ `IApplicationDbContext` → it), `FrankfurterRateProvider` typed HttpClient, `TelegramInitDataValidator`, `IUserContext`, reads the connection string with the fail-fast guard.

### Api (`ExpenseTracker.Api`)
- `Program.cs` — composition root: `AddApplication()` + `AddInfrastructure(config)`, auth (`TelegramAuthHandler`), `AddHttpContextAccessor`, the `BotOptions`, **auto-migration on startup** (`scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate()`), static files + SPA fallback, endpoint mapping. Keep `public partial class Program {}`.
- `Auth/TelegramAuthHandler.cs` + `BotOptions.cs` (moved) — the handler uses `TelegramInitDataValidator` (Infrastructure) and sets the `tg_id`/name/username claims.
- `Endpoints/` — thin minimal-API mappers (`ExpenseEndpoints`, `CategoryEndpoints`, `BudgetEndpoints`, `ReportEndpoints`, `SettingEndpoints`): resolve the current `User` via `ICurrentUser`, call the Application service, map the outcome to `Results.*`. No business logic here.

## 4. Key principle changes (vs today)

1. **Thin endpoints**: all logic currently inline in `Features/*Endpoints.cs` moves into Application services. Endpoints become adapters (parse query/body → call service → map result to HTTP).
2. **Ports & adapters**: `IApplicationDbContext`, `IExchangeRateProvider`, `IUserContext`, `ICurrentUser` are interfaces in Application; their concrete adapters live in Infrastructure (and the HttpContext claim reader). Application never references EF provider, HTTP, ASP.NET Core, or crypto.
3. **Compiler-enforced boundaries**: the project reference graph makes the dependency rule structural, not conventional.

## 5. What does NOT change

- Public HTTP API: routes, verbs, status codes, request/response JSON shapes (camelCase) — identical, so the React frontend and the existing integration tests pass unchanged (only their `using` namespaces update).
- Auth model (Telegram initData HMAC, `tma` scheme, per-user isolation).
- Database schema and the existing migration (moved, not regenerated — same snapshot).
- Auto-migration on startup (preserved, explicitly required).
- The Dockerfile (`COPY server/ ./server/` + publish the Api project still works; verify publish path/dll name unchanged: `ExpenseTracker.Api.dll`).

## 6. Testing & verification

- All 26 existing tests must stay green (unit: `CurrencyConverter`, `TelegramInitDataValidator`; integration: auth/expenses/categories/budgets/settings/reports via Testcontainers). Test `using`s update to the new namespaces; `ApiFactory : WebApplicationFactory<Program>` still works because `Program` stays in Api.
- Clean build: 0 warnings.
- **Critical end-to-end run** (the acceptance gate): start a real Postgres, run the Api with a known `BotToken`, generate a correctly-signed `initData`, and exercise the live API — provision-on-first-call, create/list/update/delete expense (+ isolation), categories, budgets, settings, reports (incl. a non-USD currency to drive a real Frankfurter fetch), `/healthz`, and the SPA at `/`. Report actual HTTP results and any failures honestly.

## 7. Risks

- **EF migration relocation**: moving the DbContext + migration to Infrastructure must keep the migration discoverable (same assembly as the DbContext). Verify `dotnet ef`/startup migration still applies cleanly and the model snapshot matches (no spurious new migration needed).
- **Namespace churn**: ~40 files change namespace; mechanical but must be consistent. Build is the safety net.
- **Endpoint→service extraction**: the one place behavior could drift. Each service method must preserve the exact validation/isolation/status-code semantics; the integration tests are the guard.
