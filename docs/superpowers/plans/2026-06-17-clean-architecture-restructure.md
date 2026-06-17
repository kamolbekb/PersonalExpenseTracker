# Backend Clean Architecture Restructure — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restructure the single-project backend into a four-project Clean Architecture solution (Domain / Application / Infrastructure / Api) with compiler-enforced boundaries, **preserving behavior** — all 26 tests stay green, the HTTP/JSON contract is unchanged, and auto-migration on startup is retained.

**Architecture:** Dependencies point inward: Domain (no refs) ← Application (refs Domain, + EF Core abstractions for `IApplicationDbContext`) ← Infrastructure (EF/Npgsql, HTTP, crypto, claims) ← Api (composition root, thin endpoints). Pragmatic use-case services; EF via an `IApplicationDbContext` port; no MediatR/CQRS/repositories.

**Tech Stack:** .NET 8, ASP.NET Core minimal APIs, EF Core 8 + Npgsql, xUnit + FluentAssertions + Testcontainers.

## Global Constraints

- Target framework `net8.0` for all five projects.
- This is a REFACTOR: do not change HTTP routes, verbs, status codes, or JSON shapes (camelCase). The React frontend and existing integration tests depend on the exact contract — only test `using` namespaces may change.
- Behavior-preservation safety net: after each task, `dotnet build ExpenseTracker.sln` is clean (0 warnings) and `dotnet test` is green (26 tests). A task is not done until both hold.
- Auto-migration on startup MUST remain in `Program.cs` (`Database.Migrate()` on the EF context) — explicitly required by the user.
- Dependency rule is structural: Domain references nothing; Application must NOT reference Npgsql, ASP.NET Core, HttpClient, or System.Security.Cryptography; Infrastructure holds all those adapters; Api is the only entry point and keeps `public partial class Program {}`.
- The published app is still `ExpenseTracker.Api.dll` (the Dockerfile publishes the Api project) — do not rename the Api project or its output.
- Connection string + bot token still come from config (`ConnectionStrings__Default`, `BotToken`); the fail-fast guard on a missing connection string is preserved (moves into Infrastructure's `AddInfrastructure`).
- Namespaces: `ExpenseTracker.Domain[.Entities]`, `ExpenseTracker.Application.*`, `ExpenseTracker.Infrastructure.*`, `ExpenseTracker.Api.*`.

---

## File Structure (target)

```
server/
├── ExpenseTracker.Domain/
│   ├── ExpenseTracker.Domain.csproj            (no refs, no packages)
│   ├── Entities/{User,Category,Expense,Budget,ExchangeRate,Setting}.cs
│   └── DefaultCategories.cs
├── ExpenseTracker.Application/
│   ├── ExpenseTracker.Application.csproj        (ref: Domain; pkg: Microsoft.EntityFrameworkCore)
│   ├── Common/Interfaces/{IApplicationDbContext,IExchangeRateProvider,IUserContext,ICurrentUser}.cs
│   ├── Common/{CurrencyConverter.cs,OperationResult.cs}
│   ├── DependencyInjection.cs                    (AddApplication)
│   ├── Expenses/{ExpenseService.cs,ExpenseDtos.cs}
│   ├── Categories/{CategoryService.cs,CategoryDtos.cs}
│   ├── Budgets/{BudgetService.cs,BudgetDtos.cs}
│   ├── Reports/{ReportService.cs,ReportDtos.cs}
│   ├── Settings/{SettingService.cs,SettingDtos.cs}
│   ├── Users/UserProvisioningService.cs          (implements ICurrentUser)
│   └── ExchangeRates/ExchangeRateService.cs
├── ExpenseTracker.Infrastructure/
│   ├── ExpenseTracker.Infrastructure.csproj      (refs: Application, Domain; pkgs: Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore.Design, Microsoft.Extensions.Http, Microsoft.AspNetCore.Http.Abstractions, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.Options, Microsoft.Extensions.DependencyInjection.Abstractions)
│   ├── DependencyInjection.cs                     (AddInfrastructure)
│   ├── Persistence/ApplicationDbContext.cs        (: DbContext, IApplicationDbContext)
│   ├── Persistence/Migrations/*                    (moved InitialCreate + snapshot)
│   ├── ExchangeRates/FrankfurterRateProvider.cs
│   └── Identity/{TelegramInitDataValidator.cs,TelegramUser.cs,UserContext.cs}
├── ExpenseTracker.Api/
│   ├── ExpenseTracker.Api.csproj                  (refs: Application, Infrastructure)
│   ├── Program.cs
│   ├── Auth/{TelegramAuthHandler.cs,BotOptions.cs}
│   ├── Endpoints/{ExpenseEndpoints,CategoryEndpoints,BudgetEndpoints,ReportEndpoints,SettingEndpoints}.cs
│   ├── appsettings.json
│   └── wwwroot/ (gitignored build artifact)
└── ExpenseTracker.Tests/  (ref: Api; usings updated to new namespaces)
```

---

### Task 1: Scaffold the four-project reference graph

**Files:** Create the 3 new `.csproj` (Domain, Application, Infrastructure); modify `ExpenseTracker.Api.csproj` (add project refs, drop Npgsql/EF-Design package refs — they move to Infrastructure); modify `ExpenseTracker.sln`.

**Interfaces:** Produces empty, compiling Domain/Application/Infrastructure projects and the reference graph. No code moves yet — the Api still contains everything and still builds.

- [ ] **Step 1: Create the three projects and add to the solution**
```bash
cd /mnt/projects/data/kamol/dev/PersonalExpenseTracker
dotnet new classlib -n ExpenseTracker.Domain -o server/ExpenseTracker.Domain --framework net8.0
dotnet new classlib -n ExpenseTracker.Application -o server/ExpenseTracker.Application --framework net8.0
dotnet new classlib -n ExpenseTracker.Infrastructure -o server/ExpenseTracker.Infrastructure --framework net8.0
rm -f server/ExpenseTracker.Domain/Class1.cs server/ExpenseTracker.Application/Class1.cs server/ExpenseTracker.Infrastructure/Class1.cs
dotnet sln add server/ExpenseTracker.Domain server/ExpenseTracker.Application server/ExpenseTracker.Infrastructure
```

- [ ] **Step 2: Wire the reference graph and packages**
```bash
dotnet add server/ExpenseTracker.Application reference server/ExpenseTracker.Domain
dotnet add server/ExpenseTracker.Infrastructure reference server/ExpenseTracker.Application server/ExpenseTracker.Domain
dotnet add server/ExpenseTracker.Api reference server/ExpenseTracker.Application server/ExpenseTracker.Infrastructure
# packages onto their new homes
dotnet add server/ExpenseTracker.Application package Microsoft.EntityFrameworkCore
dotnet add server/ExpenseTracker.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add server/ExpenseTracker.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add server/ExpenseTracker.Infrastructure package Microsoft.Extensions.Http
dotnet add server/ExpenseTracker.Infrastructure package Microsoft.Extensions.Configuration.Abstractions
dotnet add server/ExpenseTracker.Infrastructure package Microsoft.Extensions.Options
dotnet add server/ExpenseTracker.Infrastructure package Microsoft.Extensions.DependencyInjection.Abstractions
# Infrastructure needs IHttpContextAccessor + AuthN abstractions; the simplest correct way for a classlib
# is the FrameworkReference to ASP.NET Core (it stays out of Domain/Application):
```
Add to `server/ExpenseTracker.Infrastructure/ExpenseTracker.Infrastructure.csproj` inside an `<ItemGroup>`:
```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```
(This gives Infrastructure `IHttpContextAccessor`/claims without pulling individual packages. Domain and Application get NO such reference.)

- [ ] **Step 3: Verify the Api still builds with everything in place**
Run: `dotnet build ExpenseTracker.sln`
Expected: Build succeeded, 0 errors. (The Api still has Npgsql/EF via the SDK + its remaining refs; if removing the Npgsql package from Api caused an error because code there still uses it, that's expected to be resolved as code moves — but at THIS step the Api still references Npgsql through Infrastructure, so it builds. If it does not build, keep the Npgsql package on Api until Task 5 moves the DbContext, then remove it.)

- [ ] **Step 4: Run the suite to confirm no regression**
Run: `dotnet test`
Expected: 26 passed.

- [ ] **Step 5: Commit**
```bash
git add -A && git commit -m "refactor: scaffold Domain/Application/Infrastructure projects and reference graph"
```

---

### Task 2: Move the Domain layer

**Files:** Move `server/ExpenseTracker.Api/Domain/*.cs` → `server/ExpenseTracker.Domain/Entities/*.cs`; move `server/ExpenseTracker.Api/Data/DefaultCategories.cs` → `server/ExpenseTracker.Domain/DefaultCategories.cs`. Update every `using`/namespace that referenced them across the Api and Tests.

**Interfaces:** Produces `ExpenseTracker.Domain.Entities.{User,Category,Expense,Budget,ExchangeRate,Setting}` and `ExpenseTracker.Domain.DefaultCategories` (`IReadOnlyList<(string Name,string Emoji)> All`). Consumed by Application, Infrastructure, Api.

- [ ] **Step 1: Move the entity files and DefaultCategories**
Move the six entity files into `server/ExpenseTracker.Domain/Entities/` and change each namespace from `ExpenseTracker.Api.Domain` to `ExpenseTracker.Domain.Entities`. Move `DefaultCategories.cs` to `server/ExpenseTracker.Domain/` with namespace `ExpenseTracker.Domain`. Entity bodies are unchanged.

- [ ] **Step 2: Update references**
In the Api (`AppDbContext.cs`, `Auth/CurrentUserAccessor.cs`, every `Features/*Endpoints.cs` that names an entity) and in the Tests, replace `using ExpenseTracker.Api.Domain;` with `using ExpenseTracker.Domain.Entities;` and add `using ExpenseTracker.Domain;` where `DefaultCategories` is used. (`AppDbContext` references `ExpenseTracker.Api.Data` for DefaultCategories today — update it.)

- [ ] **Step 3: Build + test**
Run: `dotnet build ExpenseTracker.sln && dotnet test`
Expected: Build clean; 26 passed.

- [ ] **Step 4: Commit**
```bash
git add -A && git commit -m "refactor: move entities and DefaultCategories to Domain project"
```

---

### Task 3: Application ports, DTOs, pure helpers, and the result type

**Files:** Create in `ExpenseTracker.Application`: `Common/Interfaces/IApplicationDbContext.cs`, `Common/Interfaces/IExchangeRateProvider.cs` (move from Api `Currency/IExchangeRateProvider.cs`), `Common/Interfaces/IUserContext.cs`, `Common/Interfaces/ICurrentUser.cs`, `Common/CurrencyConverter.cs` (move from Api `Currency/CurrencyConverter.cs`), `Common/OperationResult.cs`. Move the five `*Dtos.cs` from `Api/Features/*` into `Application/<Feature>/`. Make Api's `AppDbContext` implement `IApplicationDbContext` (temporary home until Task 5).

**Interfaces (Produces):**
- `IApplicationDbContext` — `DbSet<User> Users; DbSet<Category> Categories; DbSet<Expense> Expenses; DbSet<Budget> Budgets; DbSet<ExchangeRate> ExchangeRates; DbSet<Setting> Settings; Task<int> SaveChangesAsync(CancellationToken ct = default);`
- `IExchangeRateProvider` — unchanged signature, namespace `ExpenseTracker.Application.Common.Interfaces`.
- `IUserContext` — `long? TelegramUserId { get; } string? FirstName { get; } string? Username { get; }`.
- `ICurrentUser` — `Task<User> GetOrCreateAsync();` (returns the provisioned domain User).
- `OperationResult<T>` — see Step 4.
- DTO records keep their exact names/shapes, new namespaces `ExpenseTracker.Application.<Feature>`.

- [ ] **Step 1: Define the DbContext port**
`server/ExpenseTracker.Application/Common/Interfaces/IApplicationDbContext.cs`:
```csharp
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<Setting> Settings { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Move `IExchangeRateProvider` and `CurrencyConverter`; add `IUserContext`/`ICurrentUser`**
Move `IExchangeRateProvider` to `Common/Interfaces/IExchangeRateProvider.cs` (namespace `ExpenseTracker.Application.Common.Interfaces`). Move `CurrencyConverter` to `Common/CurrencyConverter.cs` (namespace `ExpenseTracker.Application.Common`). Create:
`Common/Interfaces/IUserContext.cs`:
```csharp
namespace ExpenseTracker.Application.Common.Interfaces;

public interface IUserContext
{
    long? TelegramUserId { get; }
    string? FirstName { get; }
    string? Username { get; }
}
```
`Common/Interfaces/ICurrentUser.cs`:
```csharp
using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Application.Common.Interfaces;

public interface ICurrentUser
{
    Task<User> GetOrCreateAsync();
}
```

- [ ] **Step 3: Move the DTOs**
Move each `Api/Features/<F>/<F>Dtos.cs` to `Application/<F>/<F>Dtos.cs`, changing namespace to `ExpenseTracker.Application.<F>` (e.g. `ExpenseTracker.Application.Expenses`). Record shapes unchanged.

- [ ] **Step 4: Add the outcome type (keeps Application free of ASP.NET Core)**
`server/ExpenseTracker.Application/Common/OperationResult.cs`:
```csharp
namespace ExpenseTracker.Application.Common;

public enum ResultStatus { Ok, Created, NotFound, BadRequest, NoContent }

public sealed record OperationResult<T>(ResultStatus Status, T? Value = default, string? Error = null)
{
    public static OperationResult<T> Ok(T value) => new(ResultStatus.Ok, value);
    public static OperationResult<T> Created(T value) => new(ResultStatus.Created, value);
    public static OperationResult<T> NotFound() => new(ResultStatus.NotFound);
    public static OperationResult<T> Bad(string error) => new(ResultStatus.BadRequest, Error: error);
    public static OperationResult<T> NoContent() => new(ResultStatus.NoContent);
}
```

- [ ] **Step 5: Make Api's `AppDbContext` implement the port (temporary)**
In `server/ExpenseTracker.Api/Data/AppDbContext.cs`, add `: DbContext(options), IApplicationDbContext` (it already exposes the six `DbSet`s and `SaveChangesAsync`, so it satisfies the interface) and `using ExpenseTracker.Application.Common.Interfaces;`. Register the interface in `Program.cs`: `builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());`. Update Api `using`s for the moved DTOs/`CurrencyConverter`/`IExchangeRateProvider`.

- [ ] **Step 6: Build + test**
Run: `dotnet build ExpenseTracker.sln && dotnet test`
Expected: Build clean; 26 passed. (Endpoints still hold logic but now reference Application DTOs/interfaces.)

- [ ] **Step 7: Commit**
```bash
git add -A && git commit -m "refactor: add Application ports, DTOs, CurrencyConverter, and OperationResult"
```

---

### Task 4: Extract endpoint logic into Application use-case services

**Files:** Create `Application/<Feature>/<Feature>Service.cs` for Expenses, Categories, Budgets, Reports, Settings; `Application/Users/UserProvisioningService.cs` (implements `ICurrentUser`); move `Currency/ExchangeRateService.cs` → `Application/ExchangeRates/ExchangeRateService.cs`; `Application/DependencyInjection.cs`. Slim the Api `Features/*Endpoints.cs` to call the services. Move provisioning logic out of `Auth/CurrentUserAccessor.cs`.

**Interfaces (Produces):** service classes whose methods take the current `User` (and inputs) and return `OperationResult<T>` / domain results. Exact method set mirrors today's endpoints:
- `ExpenseService`: `Task<IReadOnlyList<ExpenseDto>> ListAsync(int userId, DateOnly? from, DateOnly? to, int? categoryId)`, `Task<OperationResult<ExpenseDto>> CreateAsync(int userId, ExpenseInput)`, `Task<OperationResult<ExpenseDto>> UpdateAsync(int userId, int id, ExpenseInput)`, `Task<OperationResult<ExpenseDto>> DeleteAsync(int userId, int id)`.
- `CategoryService`: `ListAsync(int userId)`, `CreateAsync(int userId, CategoryInput)`, `UpdateAsync(int userId, int id, CategoryInput)`.
- `BudgetService`: `ListAsync(int userId)`, `UpsertAsync(int userId, BudgetInput)`.
- `ReportService`: `SummaryAsync(int userId, DateOnly? from, DateOnly? to)`.
- `SettingService`: `GetAsync(int userId)`, `UpdateAsync(int userId, SettingDto)`.
- `ICurrentUser`/`UserProvisioningService`: `GetOrCreateAsync()` using `IUserContext` + `IApplicationDbContext` (find-or-create + seed `DefaultCategories.All` + `Setting{BaseCurrency="USD"}`).
- `ExchangeRateService`: unchanged behavior, now consuming `IApplicationDbContext` + `IExchangeRateProvider`.

- [ ] **Step 1: Move `ExchangeRateService` and write `DependencyInjection`**
Move `ExchangeRateService` into `Application/ExchangeRates/`, namespace `ExpenseTracker.Application.ExchangeRates`, swapping its `AppDbContext` dependency for `IApplicationDbContext`. Create `server/ExpenseTracker.Application/DependencyInjection.cs`:
```csharp
using ExpenseTracker.Application.Budgets;
using ExpenseTracker.Application.Categories;
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.ExchangeRates;
using ExpenseTracker.Application.Expenses;
using ExpenseTracker.Application.Reports;
using ExpenseTracker.Application.Settings;
using ExpenseTracker.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<CurrencyConverter>();
        services.AddScoped<ExchangeRateService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<ReportService>();
        services.AddScoped<SettingService>();
        return services;
    }
}
```
(Add `Microsoft.Extensions.DependencyInjection.Abstractions` package to Application.) `ICurrentUser → UserProvisioningService` is registered in Infrastructure or Api alongside `IUserContext`; keep it in Api/Infrastructure DI (Task 5/6) since it needs the HttpContext-backed `IUserContext`.

- [ ] **Step 2: Write the use-case services by extracting endpoint logic**
For each feature, create `<Feature>Service.cs` (namespace `ExpenseTracker.Application.<Feature>`) and MOVE the body of the corresponding `Api/Features/<Feature>Endpoints.cs` handlers into service methods with the signatures in the Interfaces block. Replace `db` (AppDbContext) with an injected `IApplicationDbContext db`. Replace `Results.BadRequest("x")`/`Results.NotFound()`/`Results.Ok(dto)`/`Results.Created(...)`/`Results.NoContent()` with the matching `OperationResult<T>.Bad("x")`/`.NotFound()`/`.Ok(dto)`/`.Created(dto)`/`.NoContent()`. Preserve EVERY check verbatim (amount>0, currency null-guard + uppercase, category-ownership on create AND update, per-user `UserId` filters, the 404-before-validation order, budget category-ownership, settings upper-casing). `ReportService` injects `ExchangeRateService` + `CurrencyConverter`. Do not change query logic.
Write `Application/Users/UserProvisioningService.cs` implementing `ICurrentUser`: inject `IUserContext` + `IApplicationDbContext`; `GetOrCreateAsync()` reads `IUserContext.TelegramUserId` (throw `InvalidOperationException` if null — it is always set behind auth), find-or-create the `User`, and on create seed `DefaultCategories.All` + `Setting{BaseCurrency="USD"}` (logic moved verbatim from `CurrentUserAccessor`).

- [ ] **Step 3: Slim the endpoints to call services**
Rewrite each `Api/Features/<Feature>Endpoints.cs` handler to: resolve the user via `ICurrentUser` (`var user = await cu.GetOrCreateAsync();`), call the service method with `user.Id`, and map the `OperationResult<T>` to `Results.*`. Add a private helper in Api to map outcomes, e.g.:
```csharp
static IResult ToHttp<T>(OperationResult<T> r, string? createdAtPath = null) => r.Status switch
{
    ResultStatus.Ok => Results.Ok(r.Value),
    ResultStatus.Created => Results.Created(createdAtPath ?? "", r.Value),
    ResultStatus.NoContent => Results.NoContent(),
    ResultStatus.NotFound => Results.NotFound(),
    ResultStatus.BadRequest => Results.BadRequest(r.Error),
    _ => Results.StatusCode(500),
};
```
For `POST /expenses` the created path is `$"/api/expenses/{dto.Id}"` — preserve the `Location`/201 behavior the integration test expects (it asserts `HttpStatusCode.Created`). Keep all routes/verbs identical.

- [ ] **Step 4: Register `AddApplication()` in Program.cs and delete the now-empty inline logic**
In `Program.cs` add `builder.Services.AddApplication();`. Remove the old `AddScoped<ExchangeRateService>()`/`AddSingleton<CurrencyConverter>()`/`AddHttpClient<IExchangeRateProvider,...>()` lines that are now owned by Application/Infrastructure DI (the HttpClient registration moves to Infrastructure in Task 5; keep it working until then — if needed, leave the `AddHttpClient` line in Program.cs temporarily and remove it in Task 5).

- [ ] **Step 5: Build + test**
Run: `dotnet build ExpenseTracker.sln && dotnet test`
Expected: Build clean; 26 passed. This task is the behavior-sensitive one — the green integration suite proves the extraction preserved semantics.

- [ ] **Step 6: Commit**
```bash
git add -A && git commit -m "refactor: extract endpoint logic into Application use-case services"
```

---

### Task 5: Move Infrastructure (DbContext + migrations, FX provider, identity)

**Files:** Move `Api/Data/AppDbContext.cs` → `Infrastructure/Persistence/ApplicationDbContext.cs` (rename class to `ApplicationDbContext`); move `Api/Data/Migrations/*` → `Infrastructure/Persistence/Migrations/*`; move `Api/Currency/FrankfurterRateProvider.cs` → `Infrastructure/ExchangeRates/`; move `Api/Auth/TelegramInitDataValidator.cs` + `TelegramUser.cs` → `Infrastructure/Identity/`; create `Infrastructure/Identity/UserContext.cs` (implements `IUserContext`) and `Infrastructure/DependencyInjection.cs` (`AddInfrastructure`). Remove the moved files + Npgsql package from Api.

**Interfaces (Produces):** `ApplicationDbContext : DbContext, IApplicationDbContext` (model config moves here); `AddInfrastructure(IServiceCollection, IConfiguration)` registering the context (+ `IApplicationDbContext`), the Frankfurter typed HttpClient, `TelegramInitDataValidator`, `IUserContext → UserContext`, and `ICurrentUser → UserProvisioningService`.

- [ ] **Step 1: Move and rename the DbContext + migrations**
Move `AppDbContext.cs` to `Infrastructure/Persistence/ApplicationDbContext.cs`; rename the class `AppDbContext` → `ApplicationDbContext`; namespace `ExpenseTracker.Infrastructure.Persistence`; implement `IApplicationDbContext`; keep `OnModelCreating` (indexes + decimal types) verbatim; `using ExpenseTracker.Domain.Entities;`. Move the three migration files to `Infrastructure/Persistence/Migrations/` and change their namespace to `ExpenseTracker.Infrastructure.Persistence.Migrations` (update the `[DbContext(typeof(AppDbContext))]` attribute in the `.Designer.cs` and snapshot to `typeof(ApplicationDbContext)`).

- [ ] **Step 2: Move the FX provider and Telegram identity**
Move `FrankfurterRateProvider.cs` to `Infrastructure/ExchangeRates/` (namespace `ExpenseTracker.Infrastructure.ExchangeRates`; implements `ExpenseTracker.Application.Common.Interfaces.IExchangeRateProvider`). Move `TelegramInitDataValidator.cs` + `TelegramUser.cs` to `Infrastructure/Identity/` (namespace `ExpenseTracker.Infrastructure.Identity`).

- [ ] **Step 3: Implement `IUserContext` from claims**
`server/ExpenseTracker.Infrastructure/Identity/UserContext.cs`:
```csharp
using System.Security.Claims;
using ExpenseTracker.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ExpenseTracker.Infrastructure.Identity;

public class UserContext(IHttpContextAccessor accessor) : IUserContext
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public long? TelegramUserId =>
        long.TryParse(Principal?.FindFirstValue("tg_id"), out var id) ? id : null;
    public string? FirstName => Principal?.FindFirstValue(ClaimTypes.GivenName);
    public string? Username => Principal?.FindFirstValue("tg_username");
}
```

- [ ] **Step 4: Write `AddInfrastructure`**
`server/ExpenseTracker.Infrastructure/DependencyInjection.cs`:
```csharp
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Users;
using ExpenseTracker.Infrastructure.ExchangeRates;
using ExpenseTracker.Infrastructure.Identity;
using ExpenseTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set the ConnectionStrings__Default environment variable.");
        services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(cs));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<ICurrentUser, UserProvisioningService>();
        services.AddSingleton<TelegramInitDataValidator>();
        services.AddHttpClient<IExchangeRateProvider, FrankfurterRateProvider>(c =>
            c.BaseAddress = new Uri("https://api.frankfurter.app/"));
        return services;
    }
}
```

- [ ] **Step 5: Remove moved files + Npgsql package from Api; delete `Api/Data`, `Api/Currency`, the old validator**
Delete the now-moved files from the Api project (`Data/AppDbContext.cs`, `Data/Migrations/*`, `Currency/*`, `Auth/TelegramInitDataValidator.cs`, `Auth/TelegramUser.cs`). Remove the Npgsql package from the Api csproj (`dotnet remove server/ExpenseTracker.Api package Npgsql.EntityFrameworkCore.PostgreSQL` and the EF Design package if present). The Api no longer references EF/Npgsql directly.

- [ ] **Step 6: Build (Program.cs is updated in Task 6, so expect Program.cs compile errors here)**
This task leaves `Program.cs` still referencing the old `AppDbContext`/registrations — it is rewired in Task 6. To keep Task 5 independently green, perform Task 6's `Program.cs` rewrite as the final part of THIS commit if the build is otherwise broken. (Practically: do Steps 1–5, then immediately apply Task 6 Step 1 so the solution compiles before committing.) Then `dotnet build ExpenseTracker.sln && dotnet test` → clean + 26 passed.

- [ ] **Step 7: Commit**
```bash
git add -A && git commit -m "refactor: move EF context, migrations, FX provider, and Telegram identity into Infrastructure"
```

---

### Task 6: Rewire the Api (composition root + thin endpoints) and finalize

**Files:** Rewrite `server/ExpenseTracker.Api/Program.cs`; rename `Api/Features/` → `Api/Endpoints/` (the thinned mappers from Task 4); keep `Api/Auth/{TelegramAuthHandler,BotOptions}.cs`; update `ExpenseTracker.Tests` `using`s.

**Interfaces:** Produces the final composition root using `AddApplication()` + `AddInfrastructure(config)`, auto-migration on the `ApplicationDbContext`, auth, static files + SPA fallback, endpoint mapping; `public partial class Program {}` retained.

- [ ] **Step 1: Rewrite `Program.cs`**
```csharp
using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Endpoints;
using ExpenseTracker.Application;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<BotOptions>(o => o.Token = builder.Configuration["BotToken"] ?? "");
builder.Services.AddAuthentication(TelegramAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, TelegramAuthHandler>(TelegramAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Text("ok"));

var api = app.MapGroup("/api").RequireAuthorization();
api.MapExpenseEndpoints();
api.MapCategoryEndpoints();
api.MapBudgetEndpoints();
api.MapReportEndpoints();
api.MapSettingEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
```
(Note: `TelegramInitDataValidator` is now registered inside `AddInfrastructure`; `TelegramAuthHandler` consumes it via DI. Confirm the handler's `using` points at `ExpenseTracker.Infrastructure.Identity`.)

- [ ] **Step 2: Rename `Features/` → `Endpoints/` and fix namespaces/usings**
Move `Api/Features/<F>/<F>Endpoints.cs` to `Api/Endpoints/<F>Endpoints.cs` (flatten ok), namespace `ExpenseTracker.Api.Endpoints`; ensure they reference `ExpenseTracker.Application.<F>` (DTOs/services) and `ExpenseTracker.Application.Common` (`ICurrentUser`, `OperationResult`). Update `TelegramAuthHandler.cs` `using` for the validator's new namespace.

- [ ] **Step 3: Update the test project usings**
In `ExpenseTracker.Tests`, update `using`s: DTOs now `ExpenseTracker.Application.<F>`; `InitDataBuilder`/validator tests reference `ExpenseTracker.Infrastructure.Identity`; `ApiFactory` references `ExpenseTracker.Infrastructure.Persistence` for `ApplicationDbContext` (it currently migrates via `AppDbContext` — update to `ApplicationDbContext`). `WebApplicationFactory<Program>` is unchanged (`Program` still in Api). The `CurrencyConverterTests` now `using ExpenseTracker.Application.Common;`.

- [ ] **Step 4: Full build + full suite + clean-build check**
Run: `dotnet clean ExpenseTracker.sln && dotnet build ExpenseTracker.sln` → 0 warnings, 0 errors.
Run: `dotnet test` → 26 passed.

- [ ] **Step 5: Verify the dependency rule holds (structural check)**
Confirm no forbidden references crept in:
```bash
grep -rl "Npgsql\|HttpClient\|Microsoft.AspNetCore\|System.Security.Cryptography" server/ExpenseTracker.Domain/ server/ExpenseTracker.Application/ --include=*.cs || echo "clean: Domain/Application free of infra deps"
```
Expected: the line prints "clean…" (Application may legitimately reference `Microsoft.EntityFrameworkCore` for `DbSet`, which is NOT matched above; if any file matches the forbidden set, move it to Infrastructure).

- [ ] **Step 6: Commit**
```bash
git add -A && git commit -m "refactor: Api composition root with AddApplication/AddInfrastructure and thin endpoints"
```

---

### Task 7: Critical end-to-end run (acceptance gate — not a code change)

**Files:** none (verification). Produces a documented, real run of the assembled app.

- [ ] **Step 1: Start a real Postgres and run the Api**
```bash
docker run -d --rm --name et-e2e-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=expenses -p 55433:5432 postgres:16-alpine
# build the SPA into wwwroot so "/" serves the app
cd web && npm run build && cd ..
rm -rf server/ExpenseTracker.Api/wwwroot && cp -r web/dist server/ExpenseTracker.Api/wwwroot
ConnectionStrings__Default="Host=localhost;Port=55433;Database=expenses;Username=postgres;Password=postgres" \
  BotToken="123456:E2ETESTBOTTOKEN" ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="http://localhost:5099" \
  dotnet run --project server/ExpenseTracker.Api --no-launch-profile &
# wait for startup + auto-migration
```

- [ ] **Step 2: Generate a correctly-signed initData for `BotToken=123456:E2ETESTBOTTOKEN`**
Use a throwaway C# script or `dotnet fsi`, or reuse the test `InitDataBuilder` algorithm: secret=`HMAC_SHA256("WebAppData", botToken)`, hash=`HMAC_SHA256(secret, dataCheckString)` over `auth_date=<now>` + `user={"id":777,"first_name":"E2E","username":"e2e"}` sorted by key. Capture the `initData` query string.

- [ ] **Step 3: Exercise the live API critically (record every status)**
```bash
H="Authorization: tma <initData>"
curl -s -o /dev/null -w "healthz %{http_code}\n" localhost:5099/healthz
curl -s -o /dev/null -w "root(SPA) %{http_code}\n" localhost:5099/
curl -s -w "\ncats %{http_code}\n" -H "$H" localhost:5099/api/categories            # provisions user 777 + 8 cats
curl -s -w "\nnoauth %{http_code}\n" -o /dev/null localhost:5099/api/categories       # expect 401
curl -s -w "\ncreate %{http_code}\n" -H "$H" -H "Content-Type: application/json" \
  -d '{"amount":12.50,"currencyCode":"EUR","categoryId":<id>,"spentOn":"2026-06-01","note":"lunch"}' \
  localhost:5099/api/expenses                                                          # non-USD → real FX fetch
curl -s -w "\nlist %{http_code}\n" -H "$H" localhost:5099/api/expenses
curl -s -w "\nbudget %{http_code}\n" -H "$H" -H "Content-Type: application/json" \
  -d '{"categoryId":null,"limitAmount":500,"currencyCode":"USD"}' -X PUT localhost:5099/api/budgets
curl -s -w "\nsettings %{http_code}\n" -H "$H" -H "Content-Type: application/json" \
  -d '{"baseCurrency":"EUR"}' -X PUT localhost:5099/api/settings
curl -s -w "\nreport %{http_code}\n" -H "$H" "localhost:5099/api/reports/summary?from=2026-06-01&to=2026-06-30"  # converts to EUR
```
Expected: healthz/root/cats/create/list/budget/settings/report = 2xx; noauth = 401; report JSON totals in EUR; the EUR expense drove a real Frankfurter fetch + an `ExchangeRate` cache row.

- [ ] **Step 4: Tear down and document**
Stop the dotnet process; `docker stop et-e2e-pg`. Record actual outputs (and any failures, honestly) in the report.

---

## Self-Review

**Spec coverage (spec §→task):** §2 layout/deps → Task 1; §3 Domain → Task 2, Application ports/DTOs → Task 3, services → Task 4, Infrastructure → Task 5, Api → Task 6; §4 thin endpoints + ports → Tasks 4&6; §5 no-contract-change → enforced by the unchanged 26 tests each task; §6 testing + critical run → every task's green gate + Task 7; §7 risks (migration relocation → Task 5 Step 1; namespace churn → build gates; endpoint extraction → Task 4 green suite). Auto-migration → Task 6 Step 1 (verbatim block). ✓

**Placeholder scan:** the only `<...>` tokens are runtime values to fill during the live run (`<initData>`, `<id>`) in Task 7, which is interactive verification, not code. No "TBD"/"add validation"-style gaps; the behavior-preserving moves point at exact existing handlers.

**Type consistency:** `IApplicationDbContext`, `ICurrentUser.GetOrCreateAsync()`, `IUserContext`, `OperationResult<T>`/`ResultStatus`, `AddApplication()`/`AddInfrastructure(config)`, and the `<Feature>Service` method signatures are referenced identically across Tasks 3–6. `AppDbContext`→`ApplicationDbContext` rename is applied consistently (Task 5 Step 1; tests updated Task 6 Step 3). `TelegramAuthHandler.SchemeName` matches the existing constant.

**Note on task independence:** Tasks 5 and 6 are tightly coupled (moving the DbContext breaks `Program.cs` until rewired). Task 5 Step 6 explicitly folds the `Program.cs` rewrite in so the commit compiles — these two may be executed and committed together if cleaner.
