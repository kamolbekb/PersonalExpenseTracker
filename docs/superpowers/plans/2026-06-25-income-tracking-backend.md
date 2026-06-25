# Income Tracking — Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a backend income slice — `Income` + `IncomeCategory` entities, `/api/incomes` CRUD, `/api/income-categories`, `/api/reports/income-summary`, and an `IncomeTrackingEnabled` settings flag — fully separate from expenses, mirroring the existing expense slice.

**Architecture:** ASP.NET Core 8 minimal APIs over EF Core 8 / PostgreSQL, clean architecture (Domain → Application → Api/Infrastructure). Each income piece mirrors its expense counterpart 1:1, reusing `OperationResult`, `EndpointResults`, `ICurrentUser`, the `ExchangeRateService`/`CurrencyConverter`, and the `ReportSummary` DTOs. Income has its own tables and its own (read-only, seeded) categories.

**Tech Stack:** C# / .NET 8, EF Core 8 (Npgsql), xUnit + FluentAssertions + Testcontainers (PostgreSQL) integration tests.

## Global Constraints

- **Mirror the expense slice exactly.** Match naming, validation, and structure of `Expense*`/`Category*` code. Currency stored `ToUpperInvariant()`; money columns `decimal(18,2)`; all rows scoped by `UserId`.
- **Income is separate from expenses.** New `Incomes` and `IncomeCategories` tables; income never appears in expense queries/reports and vice-versa. Do **not** add a `Type` discriminator to the existing `Category`.
- **Income categories are read-only and seeded.** Defaults (in order): `("Salary","💼"), ("Freelance","💻"), ("Gifts","🎁"), ("Investments","📈"), ("Other","💰")`. No `IsArchived`, no create/update/delete endpoints.
- **Settings flag:** `Setting.IncomeTrackingEnabled` is a non-nullable `bool` defaulting to `false`. Existing rows default to `false` via the migration. The flag only gates UI; it never deletes data.
- **One migration** named `AddIncomeTracking`, output to `Persistence/Migrations`, generated via a new design-time factory (so `dotnet ef migrations add` never executes `Program.cs`'s startup `Database.Migrate()`).
- **Tests require Docker** (Testcontainers spins up `postgres:16-alpine`). `dotnet` 8 and Docker are available in this environment.
- All `dotnet` commands run from the repo root unless noted; the solution is `server/ExpenseTracker.sln`.

## File Structure

- Domain: `Entities/Income.cs`, `Entities/IncomeCategory.cs` (new); `Entities/Setting.cs` (+1 field); `DefaultIncomeCategories.cs` (new).
- Application: `Incomes/IncomeDtos.cs`, `Incomes/IncomeService.cs`, `IncomeCategories/IncomeCategoryDtos.cs`, `IncomeCategories/IncomeCategoryService.cs`, `Reports/IncomeReportService.cs` (new); `Common/Interfaces/IApplicationDbContext.cs`, `Users/UserProvisioningService.cs`, `Settings/SettingDtos.cs`, `Settings/SettingService.cs`, `DependencyInjection.cs` (modified).
- Infrastructure: `Persistence/ApplicationDbContext.cs` (modified), `Persistence/ApplicationDbContextFactory.cs` (new), `Persistence/Migrations/*AddIncomeTracking*` (generated).
- Api: `Endpoints/IncomeEndpoints.cs`, `Endpoints/IncomeCategoryEndpoints.cs`, `Endpoints/IncomeReportEndpoints.cs` (new); `Program.cs` (modified).
- Tests: `Integration/IncomeCategoriesApiTests.cs`, `Integration/IncomesApiTests.cs`, `Integration/IncomeReportsApiTests.cs` (new); `Integration/BudgetsAndSettingsApiTests.cs` (modified).

---

### Task B1: Income schema, provisioning, and migration

**Files:**
- Create: `server/ExpenseTracker.Domain/Entities/Income.cs`, `server/ExpenseTracker.Domain/Entities/IncomeCategory.cs`, `server/ExpenseTracker.Domain/DefaultIncomeCategories.cs`, `server/ExpenseTracker.Infrastructure/Persistence/ApplicationDbContextFactory.cs`
- Modify: `server/ExpenseTracker.Domain/Entities/Setting.cs`, `server/ExpenseTracker.Application/Common/Interfaces/IApplicationDbContext.cs`, `server/ExpenseTracker.Infrastructure/Persistence/ApplicationDbContext.cs`, `server/ExpenseTracker.Application/Users/UserProvisioningService.cs`
- Generated: `server/ExpenseTracker.Infrastructure/Persistence/Migrations/<timestamp>_AddIncomeTracking.cs`

**Interfaces:**
- Produces: `Income { int Id; int UserId; decimal Amount; string CurrencyCode; int IncomeCategoryId; DateOnly ReceivedOn; string? Note; DateTimeOffset CreatedAt }`; `IncomeCategory { int Id; int UserId; string Name; string Emoji }`; `Setting.IncomeTrackingEnabled : bool`; `IApplicationDbContext.Incomes : DbSet<Income>`, `IApplicationDbContext.IncomeCategories : DbSet<IncomeCategory>`; `DefaultIncomeCategories.All : IReadOnlyList<(string Name, string Emoji)>`.

- [ ] **Step 1: Create the `Income` entity**

`server/ExpenseTracker.Domain/Entities/Income.cs`:
```csharp
namespace ExpenseTracker.Domain.Entities;

public class Income
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }            // in CurrencyCode
    public string CurrencyCode { get; set; } = "UZS";
    public int IncomeCategoryId { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

- [ ] **Step 2: Create the `IncomeCategory` entity**

`server/ExpenseTracker.Domain/Entities/IncomeCategory.cs`:
```csharp
namespace ExpenseTracker.Domain.Entities;

public class IncomeCategory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
}
```

- [ ] **Step 3: Create the default income categories**

`server/ExpenseTracker.Domain/DefaultIncomeCategories.cs`:
```csharp
namespace ExpenseTracker.Domain;

public static class DefaultIncomeCategories
{
    public static readonly IReadOnlyList<(string Name, string Emoji)> All = new[]
    {
        ("Salary", "💼"), ("Freelance", "💻"), ("Gifts", "🎁"),
        ("Investments", "📈"), ("Other", "💰"),
    };
}
```

- [ ] **Step 4: Add the settings flag**

Edit `server/ExpenseTracker.Domain/Entities/Setting.cs` to add the field after `BaseCurrency`:
```csharp
namespace ExpenseTracker.Domain.Entities;

public class Setting
{
    public int Id { get; set; }
    public int UserId { get; set; }                // unique
    public string BaseCurrency { get; set; } = "UZS";
    public bool IncomeTrackingEnabled { get; set; }
}
```

- [ ] **Step 5: Add DbSets to the context interface**

Edit `server/ExpenseTracker.Application/Common/Interfaces/IApplicationDbContext.cs` — add two members alongside the others:
```csharp
    DbSet<Income> Incomes { get; }
    DbSet<IncomeCategory> IncomeCategories { get; }
```

- [ ] **Step 6: Add DbSets + EF config to the context**

Edit `server/ExpenseTracker.Infrastructure/Persistence/ApplicationDbContext.cs`. Add the two DbSet properties next to `Settings`:
```csharp
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<IncomeCategory> IncomeCategories => Set<IncomeCategory>();
```
And in `OnModelCreating`, add after the existing `Category` index line:
```csharp
        b.Entity<Income>().HasIndex(i => new { i.UserId, i.ReceivedOn });
        b.Entity<IncomeCategory>().HasIndex(c => c.UserId);
        b.Entity<Income>().Property(i => i.Amount).HasColumnType("decimal(18,2)");
```

- [ ] **Step 7: Seed income categories + flag on provisioning**

Edit `server/ExpenseTracker.Application/Users/UserProvisioningService.cs`. Replace the block that adds default categories and the setting with:
```csharp
        db.Categories.AddRange(DefaultCategories.All.Select(c =>
            new Category { UserId = user.Id, Name = c.Name, Emoji = c.Emoji }));
        db.IncomeCategories.AddRange(DefaultIncomeCategories.All.Select(c =>
            new IncomeCategory { UserId = user.Id, Name = c.Name, Emoji = c.Emoji }));
        db.Settings.Add(new Setting { UserId = user.Id, BaseCurrency = "UZS", IncomeTrackingEnabled = false });
        await db.SaveChangesAsync();
```

- [ ] **Step 8: Add the design-time DbContext factory**

`server/ExpenseTracker.Infrastructure/Persistence/ApplicationDbContextFactory.cs` (lets `dotnet ef` build the model without running `Program.cs`; the dummy connection string is never connected to for `migrations add`):
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExpenseTracker.Infrastructure.Persistence;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=design;Username=design;Password=design")
            .Options;
        return new ApplicationDbContext(options);
    }
}
```

- [ ] **Step 9: Build the solution**

Run: `dotnet build server/ExpenseTracker.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 10: Generate the migration**

Run:
```bash
dotnet tool restore 2>/dev/null; dotnet ef migrations add AddIncomeTracking \
  --project server/ExpenseTracker.Infrastructure \
  --startup-project server/ExpenseTracker.Api \
  --output-dir Persistence/Migrations
```
(If `dotnet ef` is not found, install it first: `dotnet tool install --global dotnet-ef --version 8.*` and re-run.)
Expected: a new `Persistence/Migrations/<timestamp>_AddIncomeTracking.cs` is created. Open it and confirm its `Up` creates the `Incomes` and `IncomeCategories` tables and adds the `IncomeTrackingEnabled` column to `Settings` (with `defaultValue: false`). Then `dotnet build server/ExpenseTracker.sln` → succeeds.

- [ ] **Step 11: Commit**

```bash
git add server/ExpenseTracker.Domain server/ExpenseTracker.Application/Common/Interfaces/IApplicationDbContext.cs \
  server/ExpenseTracker.Application/Users/UserProvisioningService.cs \
  server/ExpenseTracker.Infrastructure/Persistence
git commit -m "feat(server): income schema, provisioning defaults, settings flag, migration"
```

---

### Task B2: Income categories endpoint

**Files:**
- Create: `server/ExpenseTracker.Application/IncomeCategories/IncomeCategoryDtos.cs`, `server/ExpenseTracker.Application/IncomeCategories/IncomeCategoryService.cs`, `server/ExpenseTracker.Api/Endpoints/IncomeCategoryEndpoints.cs`, `server/ExpenseTracker.Tests/Integration/IncomeCategoriesApiTests.cs`
- Modify: `server/ExpenseTracker.Application/DependencyInjection.cs`, `server/ExpenseTracker.Api/Program.cs`

**Interfaces:**
- Consumes: `IApplicationDbContext.IncomeCategories`, `DefaultIncomeCategories.All`, `ICurrentUser.GetOrCreateAsync()`.
- Produces: `IncomeCategoryDto(int Id, string Name, string Emoji)`; `GET /api/income-categories → List<IncomeCategoryDto>` (seeds defaults if the user has none, for pre-existing users).

- [ ] **Step 1: Write the failing integration test**

`server/ExpenseTracker.Tests/Integration/IncomeCategoriesApiTests.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.IncomeCategories;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class IncomeCategoriesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    [Fact]
    public async Task New_user_gets_seeded_income_categories()
    {
        var client = ClientFor(21001);
        var cats = await client.GetFromJsonAsync<List<IncomeCategoryDto>>("/api/income-categories");
        cats!.Should().NotBeEmpty();
        cats.Should().Contain(c => c.Name == "Salary");
    }

    [Fact]
    public async Task Income_categories_are_user_scoped()
    {
        var aliceCats = await ClientFor(21101).GetFromJsonAsync<List<IncomeCategoryDto>>("/api/income-categories");
        var bobCats = await ClientFor(21102).GetFromJsonAsync<List<IncomeCategoryDto>>("/api/income-categories");
        aliceCats!.Select(c => c.Id).Should().NotIntersectWith(bobCats!.Select(c => c.Id));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test server/ExpenseTracker.sln --filter "FullyQualifiedName~IncomeCategoriesApiTests"`
Expected: FAIL to compile (namespace `ExpenseTracker.Application.IncomeCategories` / `IncomeCategoryDto` do not exist yet).

- [ ] **Step 3: Create the DTO**

`server/ExpenseTracker.Application/IncomeCategories/IncomeCategoryDtos.cs`:
```csharp
namespace ExpenseTracker.Application.IncomeCategories;

public record IncomeCategoryDto(int Id, string Name, string Emoji);
```

- [ ] **Step 4: Create the service**

`server/ExpenseTracker.Application/IncomeCategories/IncomeCategoryService.cs` (seeds defaults if the user has none — covers users provisioned before this feature):
```csharp
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.IncomeCategories;

public class IncomeCategoryService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<IncomeCategoryDto>> ListAsync(int userId)
    {
        var exists = await db.IncomeCategories.AnyAsync(c => c.UserId == userId);
        if (!exists)
        {
            db.IncomeCategories.AddRange(DefaultIncomeCategories.All.Select(c =>
                new IncomeCategory { UserId = userId, Name = c.Name, Emoji = c.Emoji }));
            await db.SaveChangesAsync();
        }
        return await db.IncomeCategories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Id)
            .Select(c => new IncomeCategoryDto(c.Id, c.Name, c.Emoji))
            .ToListAsync();
    }
}
```

- [ ] **Step 5: Create the endpoint**

`server/ExpenseTracker.Api/Endpoints/IncomeCategoryEndpoints.cs`:
```csharp
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.IncomeCategories;

namespace ExpenseTracker.Api.Endpoints;

public static class IncomeCategoryEndpoints
{
    public static IEndpointRouteBuilder MapIncomeCategoryEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/income-categories", async (ICurrentUser cu, IncomeCategoryService svc) =>
        {
            var user = await cu.GetOrCreateAsync();
            return Results.Ok(await svc.ListAsync(user.Id));
        });
        return api;
    }
}
```

- [ ] **Step 6: Register the service and endpoint**

In `server/ExpenseTracker.Application/DependencyInjection.cs`: add `using ExpenseTracker.Application.IncomeCategories;` and, after `services.AddScoped<CategoryService>();`, add:
```csharp
        services.AddScoped<IncomeCategoryService>();
```
In `server/ExpenseTracker.Api/Program.cs`: after `api.MapCategoryEndpoints();` add:
```csharp
api.MapIncomeCategoryEndpoints();
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test server/ExpenseTracker.sln --filter "FullyQualifiedName~IncomeCategoriesApiTests"`
Expected: PASS (2/2).

- [ ] **Step 8: Commit**

```bash
git add server/ExpenseTracker.Application/IncomeCategories server/ExpenseTracker.Api/Endpoints/IncomeCategoryEndpoints.cs \
  server/ExpenseTracker.Application/DependencyInjection.cs server/ExpenseTracker.Api/Program.cs \
  server/ExpenseTracker.Tests/Integration/IncomeCategoriesApiTests.cs
git commit -m "feat(server): GET /api/income-categories with seeded defaults"
```

---

### Task B3: Income CRUD endpoints

**Files:**
- Create: `server/ExpenseTracker.Application/Incomes/IncomeDtos.cs`, `server/ExpenseTracker.Application/Incomes/IncomeService.cs`, `server/ExpenseTracker.Api/Endpoints/IncomeEndpoints.cs`, `server/ExpenseTracker.Tests/Integration/IncomesApiTests.cs`
- Modify: `server/ExpenseTracker.Application/DependencyInjection.cs`, `server/ExpenseTracker.Api/Program.cs`

**Interfaces:**
- Consumes: `IApplicationDbContext.Incomes`/`IncomeCategories`, `OperationResult<T>`, `EndpointResults.ToHttp`, `ICurrentUser`.
- Produces: `IncomeInput(decimal Amount, string CurrencyCode, int IncomeCategoryId, DateOnly ReceivedOn, string? Note)`; `IncomeDto(int Id, decimal Amount, string CurrencyCode, int IncomeCategoryId, DateOnly ReceivedOn, string? Note)`; `/api/incomes` GET (`from?`,`to?`,`categoryId?`), POST, PUT/{id}, DELETE/{id}.

- [ ] **Step 1: Write the failing integration test**

`server/ExpenseTracker.Tests/Integration/IncomesApiTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.IncomeCategories;
using ExpenseTracker.Application.Incomes;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class IncomesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    async Task<int> FirstIncomeCategoryId(HttpClient client)
    {
        var cats = await client.GetFromJsonAsync<List<IncomeCategoryDto>>("/api/income-categories");
        return cats![0].Id;
    }

    [Fact]
    public async Task Create_then_list_returns_the_income()
    {
        var client = ClientFor(31001);
        var catId = await FirstIncomeCategoryId(client);
        var input = new IncomeInput(1500m, "USD", catId, new DateOnly(2026, 6, 1), "salary");

        var created = await client.PostAsJsonAsync("/api/incomes", input);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<List<IncomeDto>>("/api/incomes");
        list.Should().ContainSingle(i => i.Note == "salary" && i.Amount == 1500m);
    }

    [Fact]
    public async Task User_cannot_see_another_users_incomes()
    {
        var alice = ClientFor(31101);
        var aliceCat = await FirstIncomeCategoryId(alice);
        await alice.PostAsJsonAsync("/api/incomes",
            new IncomeInput(99m, "USD", aliceCat, new DateOnly(2026, 6, 1), "alice-secret"));

        var bobList = await ClientFor(31102).GetFromJsonAsync<List<IncomeDto>>("/api/incomes");
        bobList.Should().NotContain(i => i.Note == "alice-secret");
    }

    [Fact]
    public async Task Income_with_foreign_category_is_rejected()
    {
        var alice = ClientFor(31201);
        var aliceCat = await FirstIncomeCategoryId(alice);

        var bob = ClientFor(31202);
        var res = await bob.PostAsJsonAsync("/api/incomes",
            new IncomeInput(10m, "USD", aliceCat, new DateOnly(2026, 6, 1), "x"));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task User_cannot_delete_another_users_income()
    {
        var alice = ClientFor(31301);
        var aliceCat = await FirstIncomeCategoryId(alice);
        var created = await (await alice.PostAsJsonAsync("/api/incomes",
            new IncomeInput(5m, "USD", aliceCat, new DateOnly(2026, 6, 1), "x")))
            .Content.ReadFromJsonAsync<IncomeDto>();

        var res = await ClientFor(31302).DeleteAsync($"/api/incomes/{created!.Id}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test server/ExpenseTracker.sln --filter "FullyQualifiedName~IncomesApiTests"`
Expected: FAIL to compile (`ExpenseTracker.Application.Incomes` / `IncomeInput` / `IncomeDto` undefined).

- [ ] **Step 3: Create the DTOs**

`server/ExpenseTracker.Application/Incomes/IncomeDtos.cs`:
```csharp
namespace ExpenseTracker.Application.Incomes;

public record IncomeInput(decimal Amount, string CurrencyCode, int IncomeCategoryId, DateOnly ReceivedOn, string? Note);
public record IncomeDto(int Id, decimal Amount, string CurrencyCode, int IncomeCategoryId, DateOnly ReceivedOn, string? Note);
```

- [ ] **Step 4: Create the service**

`server/ExpenseTracker.Application/Incomes/IncomeService.cs`:
```csharp
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Incomes;

public class IncomeService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<IncomeDto>> ListAsync(int userId, DateOnly? from, DateOnly? to, int? categoryId)
    {
        var q = db.Incomes.Where(i => i.UserId == userId);
        if (from is not null) q = q.Where(i => i.ReceivedOn >= from);
        if (to is not null) q = q.Where(i => i.ReceivedOn <= to);
        if (categoryId is not null) q = q.Where(i => i.IncomeCategoryId == categoryId);
        return await q.OrderByDescending(i => i.ReceivedOn).ThenByDescending(i => i.Id)
            .Select(i => new IncomeDto(i.Id, i.Amount, i.CurrencyCode, i.IncomeCategoryId, i.ReceivedOn, i.Note))
            .ToListAsync();
    }

    public async Task<OperationResult<IncomeDto>> CreateAsync(int userId, IncomeInput input)
    {
        if (input.Amount <= 0) return OperationResult<IncomeDto>.Bad("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(input.CurrencyCode)) return OperationResult<IncomeDto>.Bad("Currency required.");
        var ownsCategory = await db.IncomeCategories.AnyAsync(c => c.Id == input.IncomeCategoryId && c.UserId == userId);
        if (!ownsCategory) return OperationResult<IncomeDto>.Bad("Unknown category.");
        var i = new Income
        {
            UserId = userId, Amount = input.Amount,
            CurrencyCode = input.CurrencyCode.ToUpperInvariant(),
            IncomeCategoryId = input.IncomeCategoryId, ReceivedOn = input.ReceivedOn,
            Note = input.Note, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Incomes.Add(i);
        await db.SaveChangesAsync();
        return OperationResult<IncomeDto>.Created(
            new IncomeDto(i.Id, i.Amount, i.CurrencyCode, i.IncomeCategoryId, i.ReceivedOn, i.Note));
    }

    public async Task<OperationResult<IncomeDto>> UpdateAsync(int userId, int id, IncomeInput input)
    {
        var i = await db.Incomes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (i is null) return OperationResult<IncomeDto>.NotFound();
        if (input.Amount <= 0) return OperationResult<IncomeDto>.Bad("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(input.CurrencyCode)) return OperationResult<IncomeDto>.Bad("Currency required.");
        var ownsCategory = await db.IncomeCategories.AnyAsync(c => c.Id == input.IncomeCategoryId && c.UserId == userId);
        if (!ownsCategory) return OperationResult<IncomeDto>.Bad("Unknown category.");
        i.Amount = input.Amount;
        i.CurrencyCode = input.CurrencyCode.ToUpperInvariant();
        i.IncomeCategoryId = input.IncomeCategoryId;
        i.ReceivedOn = input.ReceivedOn;
        i.Note = input.Note;
        await db.SaveChangesAsync();
        return OperationResult<IncomeDto>.Ok(
            new IncomeDto(i.Id, i.Amount, i.CurrencyCode, i.IncomeCategoryId, i.ReceivedOn, i.Note));
    }

    public async Task<OperationResult<IncomeDto>> DeleteAsync(int userId, int id)
    {
        var i = await db.Incomes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (i is null) return OperationResult<IncomeDto>.NotFound();
        db.Incomes.Remove(i);
        await db.SaveChangesAsync();
        return OperationResult<IncomeDto>.NoContent();
    }
}
```

- [ ] **Step 5: Create the endpoints**

`server/ExpenseTracker.Api/Endpoints/IncomeEndpoints.cs`:
```csharp
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Incomes;

namespace ExpenseTracker.Api.Endpoints;

public static class IncomeEndpoints
{
    public static IEndpointRouteBuilder MapIncomeEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/incomes");

        g.MapGet("", async (ICurrentUser cu, IncomeService svc,
            DateOnly? from, DateOnly? to, int? categoryId) =>
        {
            var user = await cu.GetOrCreateAsync();
            return Results.Ok(await svc.ListAsync(user.Id, from, to, categoryId));
        });

        g.MapPost("", async (ICurrentUser cu, IncomeService svc, IncomeInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.CreateAsync(user.Id, input);
            return EndpointResults.ToHttp(result, result.Value is not null ? $"/api/incomes/{result.Value.Id}" : null);
        });

        g.MapPut("/{id:int}", async (ICurrentUser cu, IncomeService svc, int id, IncomeInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            return EndpointResults.ToHttp(await svc.UpdateAsync(user.Id, id, input));
        });

        g.MapDelete("/{id:int}", async (ICurrentUser cu, IncomeService svc, int id) =>
        {
            var user = await cu.GetOrCreateAsync();
            return EndpointResults.ToHttp(await svc.DeleteAsync(user.Id, id));
        });

        return api;
    }
}
```

- [ ] **Step 6: Register the service and endpoints**

In `server/ExpenseTracker.Application/DependencyInjection.cs`: add `using ExpenseTracker.Application.Incomes;` and, after the `IncomeCategoryService` registration, add:
```csharp
        services.AddScoped<IncomeService>();
```
In `server/ExpenseTracker.Api/Program.cs`: after `api.MapIncomeCategoryEndpoints();` add:
```csharp
api.MapIncomeEndpoints();
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test server/ExpenseTracker.sln --filter "FullyQualifiedName~IncomesApiTests"`
Expected: PASS (4/4).

- [ ] **Step 8: Commit**

```bash
git add server/ExpenseTracker.Application/Incomes server/ExpenseTracker.Api/Endpoints/IncomeEndpoints.cs \
  server/ExpenseTracker.Application/DependencyInjection.cs server/ExpenseTracker.Api/Program.cs \
  server/ExpenseTracker.Tests/Integration/IncomesApiTests.cs
git commit -m "feat(server): /api/incomes CRUD"
```

---

### Task B4: Income report summary

**Files:**
- Create: `server/ExpenseTracker.Application/Reports/IncomeReportService.cs`, `server/ExpenseTracker.Api/Endpoints/IncomeReportEndpoints.cs`, `server/ExpenseTracker.Tests/Integration/IncomeReportsApiTests.cs`
- Modify: `server/ExpenseTracker.Application/DependencyInjection.cs`, `server/ExpenseTracker.Api/Program.cs`

**Interfaces:**
- Consumes: `IApplicationDbContext.Incomes`/`IncomeCategories`, `ExchangeRateService`, `CurrencyConverter`, the `ReportSummary`/`CategoryTotal`/`MonthTotal` records from `ExpenseTracker.Application.Reports`.
- Produces: `GET /api/reports/income-summary?from=&to= → ReportSummary` (income grouped by income category, by `ReceivedOn` month, converted to base currency; unquoted currencies skipped).

- [ ] **Step 1: Write the failing integration test**

`server/ExpenseTracker.Tests/Integration/IncomeReportsApiTests.cs` (the `StubRateSource` quotes USD=12000):
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.IncomeCategories;
using ExpenseTracker.Application.Incomes;
using ExpenseTracker.Application.Reports;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class IncomeReportsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    async Task<int> FirstIncomeCategoryId(HttpClient client)
    {
        var cats = await client.GetFromJsonAsync<List<IncomeCategoryDto>>("/api/income-categories");
        return cats![0].Id;
    }

    [Fact]
    public async Task Income_summary_sums_in_base_currency_by_category_and_month()
    {
        var client = ClientFor(41001);
        var catId = await FirstIncomeCategoryId(client);
        // Base UZS; USD converts at 12000 via the stub.
        await client.PostAsJsonAsync("/api/incomes",
            new IncomeInput(10m, "USD", catId, new DateOnly(2026, 6, 1), null));
        await client.PostAsJsonAsync("/api/incomes",
            new IncomeInput(15m, "USD", catId, new DateOnly(2026, 6, 2), null));

        var summary = await client.GetFromJsonAsync<ReportSummary>(
            "/api/reports/income-summary?from=2026-06-01&to=2026-06-30");

        summary!.BaseCurrency.Should().Be("UZS");
        summary.GrandTotal.Should().Be(300000m);
        summary.ByCategory.Should().ContainSingle(c => c.CategoryId == catId && c.Total == 300000m);
        summary.ByMonth.Should().Contain(m => m.Month == "2026-06" && m.Total == 300000m);
    }

    [Fact]
    public async Task Income_summary_excludes_expense_data()
    {
        // Income summary must be zero for a user who only has expenses.
        var client = ClientFor(41777);
        var summary = await client.GetFromJsonAsync<ReportSummary>(
            "/api/reports/income-summary?from=2026-06-01&to=2026-06-30");
        summary!.GrandTotal.Should().Be(0m);
        summary.ByCategory.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test server/ExpenseTracker.sln --filter "FullyQualifiedName~IncomeReportsApiTests"`
Expected: FAIL — `/api/reports/income-summary` returns 404 (route not mapped), so deserialization fails / assertions fail.

- [ ] **Step 3: Create the income report service**

`server/ExpenseTracker.Application/Reports/IncomeReportService.cs`:
```csharp
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.ExchangeRates;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Reports;

public class IncomeReportService(IApplicationDbContext db, ExchangeRateService rates, CurrencyConverter conv)
{
    public async Task<OperationResult<ReportSummary>> SummaryAsync(int userId, DateOnly? from, DateOnly? to)
    {
        var setting = await db.Settings.FirstAsync(s => s.UserId == userId);
        var baseCcy = setting.BaseCurrency;

        var q = db.Incomes.Where(i => i.UserId == userId);
        if (from is not null) q = q.Where(i => i.ReceivedOn >= from);
        if (to is not null) q = q.Where(i => i.ReceivedOn <= to);

        var rows = await q.Join(db.IncomeCategories, i => i.IncomeCategoryId, c => c.Id,
            (i, c) => new { i.Amount, i.CurrencyCode, i.ReceivedOn, CategoryId = i.IncomeCategoryId, CategoryName = c.Name })
            .ToListAsync();

        var byCategory = new Dictionary<int, (string Name, decimal Total)>();
        var byMonth = new Dictionary<string, decimal>();
        decimal grand = 0m;

        foreach (var r in rows)
        {
            decimal amount;
            try
            {
                var rate = await rates.GetRateAsync(r.CurrencyCode, baseCcy, r.ReceivedOn);
                amount = conv.Convert(r.Amount, rate);
            }
            catch (InvalidOperationException)
            {
                continue; // skip rows whose currency CBU does not quote for that date
            }
            grand += amount;

            var cat = byCategory.GetValueOrDefault(r.CategoryId, (r.CategoryName, 0m));
            byCategory[r.CategoryId] = (r.CategoryName, cat.Item2 + amount);

            var month = r.ReceivedOn.ToString("yyyy-MM");
            byMonth[month] = byMonth.GetValueOrDefault(month, 0m) + amount;
        }

        var summary = new ReportSummary(
            baseCcy, grand,
            byCategory.Select(kv => new CategoryTotal(kv.Key, kv.Value.Name, kv.Value.Total))
                .OrderByDescending(c => c.Total).ToList(),
            byMonth.Select(kv => new MonthTotal(kv.Key, kv.Value))
                .OrderBy(m => m.Month).ToList());
        return OperationResult<ReportSummary>.Ok(summary);
    }
}
```

- [ ] **Step 4: Create the endpoint**

`server/ExpenseTracker.Api/Endpoints/IncomeReportEndpoints.cs`:
```csharp
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Reports;

namespace ExpenseTracker.Api.Endpoints;

public static class IncomeReportEndpoints
{
    public static IEndpointRouteBuilder MapIncomeReportEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/reports/income-summary", async (ICurrentUser cu, IncomeReportService svc,
            DateOnly? from, DateOnly? to) =>
        {
            var user = await cu.GetOrCreateAsync();
            return EndpointResults.ToHttp(await svc.SummaryAsync(user.Id, from, to));
        });
        return api;
    }
}
```

- [ ] **Step 5: Register the service and endpoint**

In `server/ExpenseTracker.Application/DependencyInjection.cs`: after `services.AddScoped<ReportService>();` add:
```csharp
        services.AddScoped<IncomeReportService>();
```
In `server/ExpenseTracker.Api/Program.cs`: after `api.MapReportEndpoints();` add:
```csharp
api.MapIncomeReportEndpoints();
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test server/ExpenseTracker.sln --filter "FullyQualifiedName~IncomeReportsApiTests"`
Expected: PASS (2/2).

- [ ] **Step 7: Commit**

```bash
git add server/ExpenseTracker.Application/Reports/IncomeReportService.cs \
  server/ExpenseTracker.Api/Endpoints/IncomeReportEndpoints.cs \
  server/ExpenseTracker.Application/DependencyInjection.cs server/ExpenseTracker.Api/Program.cs \
  server/ExpenseTracker.Tests/Integration/IncomeReportsApiTests.cs
git commit -m "feat(server): GET /api/reports/income-summary"
```

---

### Task B5: Expose the income-tracking settings flag

**Files:**
- Modify: `server/ExpenseTracker.Application/Settings/SettingDtos.cs`, `server/ExpenseTracker.Application/Settings/SettingService.cs`, `server/ExpenseTracker.Tests/Integration/BudgetsAndSettingsApiTests.cs`

**Interfaces:**
- Produces: `SettingDto(string BaseCurrency, bool IncomeTrackingEnabled)`; `GET /api/settings` returns the flag; `PUT /api/settings` persists it.

- [ ] **Step 1: Update the existing settings test and add a flag round-trip test**

In `server/ExpenseTracker.Tests/Integration/BudgetsAndSettingsApiTests.cs`, replace the body of `Settings_default_is_uzs_and_can_be_changed` and add a new test. The existing `new SettingDto("EUR")` must become `new SettingDto("EUR", false)`:
```csharp
    [Fact]
    public async Task Settings_default_is_uzs_and_can_be_changed()
    {
        var client = ClientFor(12001);
        var initial = await client.GetFromJsonAsync<SettingDto>("/api/settings");
        initial!.BaseCurrency.Should().Be("UZS");
        initial.IncomeTrackingEnabled.Should().BeFalse();

        await client.PutAsJsonAsync("/api/settings", new SettingDto("EUR", false));
        var updated = await client.GetFromJsonAsync<SettingDto>("/api/settings");
        updated!.BaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Income_tracking_flag_round_trips()
    {
        var client = ClientFor(12050);
        await client.PutAsJsonAsync("/api/settings", new SettingDto("UZS", true));
        var updated = await client.GetFromJsonAsync<SettingDto>("/api/settings");
        updated!.IncomeTrackingEnabled.Should().BeTrue();
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test server/ExpenseTracker.sln --filter "FullyQualifiedName~BudgetsAndSettingsApiTests"`
Expected: FAIL to compile — `SettingDto` has one constructor parameter; `new SettingDto("EUR", false)` and `.IncomeTrackingEnabled` don't exist yet.

- [ ] **Step 3: Extend the DTO**

`server/ExpenseTracker.Application/Settings/SettingDtos.cs`:
```csharp
namespace ExpenseTracker.Application.Settings;

public record SettingDto(string BaseCurrency, bool IncomeTrackingEnabled);
```

- [ ] **Step 4: Update the service to read and persist the flag**

`server/ExpenseTracker.Application/Settings/SettingService.cs`:
```csharp
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Settings;

public class SettingService(IApplicationDbContext db)
{
    public async Task<OperationResult<SettingDto>> GetAsync(int userId)
    {
        var s = await db.Settings.FirstAsync(x => x.UserId == userId);
        return OperationResult<SettingDto>.Ok(new SettingDto(s.BaseCurrency, s.IncomeTrackingEnabled));
    }

    public async Task<OperationResult<SettingDto>> UpdateAsync(int userId, SettingDto input)
    {
        if (string.IsNullOrWhiteSpace(input.BaseCurrency)) return OperationResult<SettingDto>.Bad("Base currency required.");
        var s = await db.Settings.FirstAsync(x => x.UserId == userId);
        s.BaseCurrency = input.BaseCurrency.ToUpperInvariant();
        s.IncomeTrackingEnabled = input.IncomeTrackingEnabled;
        await db.SaveChangesAsync();
        return OperationResult<SettingDto>.Ok(new SettingDto(s.BaseCurrency, s.IncomeTrackingEnabled));
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test server/ExpenseTracker.sln --filter "FullyQualifiedName~BudgetsAndSettingsApiTests"`
Expected: PASS (all in the class).

- [ ] **Step 6: Full backend test sweep**

Run: `dotnet test server/ExpenseTracker.sln`
Expected: all tests PASS (existing + new income suites). This confirms the `SettingDto` change didn't break other callers.

- [ ] **Step 7: Commit**

```bash
git add server/ExpenseTracker.Application/Settings server/ExpenseTracker.Tests/Integration/BudgetsAndSettingsApiTests.cs
git commit -m "feat(server): expose IncomeTrackingEnabled in settings DTO"
```

---

## Self-Review

**Spec coverage:**
- `Income` entity / `Incomes` table → B1. ✓
- `IncomeCategory` entity / `IncomeCategories` table, defaults seeded on provisioning → B1; seed-on-read for pre-existing users → B2. ✓
- `Setting.IncomeTrackingEnabled` (default false) + migration → B1; DTO/endpoint exposure → B5. ✓
- Single `AddIncomeTracking` migration via design-time factory → B1. ✓
- `/api/incomes` GET(from/to/categoryId)/POST/PUT/DELETE → B3. ✓
- `/api/income-categories` GET → B2. ✓
- `/api/reports/income-summary` reusing `ReportSummary`, conversion, by-category/by-month → B4. ✓
- User scoping + foreign-category rejection mirrored → B3 tests. ✓
- Income separate from expenses (own tables; income summary excludes expenses) → B4 `Income_summary_excludes_expense_data`. ✓

**Placeholder scan:** none — every code step is complete; the only generated artifact is the EF migration (Step B1.10), which is produced by the named command, not hand-written.

**Type consistency:** `IncomeInput`/`IncomeDto` use `IncomeCategoryId` + `ReceivedOn` consistently across service, endpoints, and tests. `IncomeCategoryDto(Id,Name,Emoji)` consistent in B2 and consumed in B3/B4 tests. `IncomeReportService` reuses `ReportSummary`/`CategoryTotal`/`MonthTotal` from `ExpenseTracker.Application.Reports`. `SettingDto(BaseCurrency, IncomeTrackingEnabled)` updated in B5 with the one existing caller (`BudgetsAndSettingsApiTests`) fixed in the same task. DI registration names (`IncomeCategoryService`, `IncomeService`, `IncomeReportService`) and `Map*Endpoints` extension names are consistent between their defining task and `Program.cs`/`DependencyInjection.cs` edits.
