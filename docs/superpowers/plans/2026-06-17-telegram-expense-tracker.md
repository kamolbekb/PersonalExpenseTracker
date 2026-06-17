# Telegram Expense Tracker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a multi-user Telegram Mini App expense tracker (log expenses, categories, budgets, reports, multi-currency) as an ASP.NET Core API serving a React/TypeScript frontend, backed by PostgreSQL.

**Architecture:** A single ASP.NET Core service exposes a REST API under `/api/*` and serves the built React app from `wwwroot` (same origin, no CORS). Identity is passwordless: the frontend forwards Telegram `initData`, which the backend validates by HMAC and resolves to an auto-provisioned `User`. Every domain query is scoped to that user. Multi-currency reports convert to a per-user base currency using ECB rates fetched lazily from Frankfurter and cached in the DB.

**Tech Stack:** ASP.NET Core (.NET 8) Web API, EF Core + Npgsql, PostgreSQL, xUnit + FluentAssertions + Testcontainers, React 18 + TypeScript + Vite, TanStack Query, React Router, Recharts, `@telegram-apps/sdk-react`, Docker.

## Global Constraints

- **Target framework:** .NET 8 (`net8.0`) for all C# projects.
- **Money:** always `decimal`, never `double`/`float`. Rates stored as `decimal(18,8)`, amounts as `decimal(18,2)`.
- **Per-user isolation:** every read/write of `Expense`, `Category`, `Budget`, `Setting` MUST be filtered by the authenticated `UserId`. This is a security invariant; every feature task includes an isolation test.
- **Auth on every `/api/*` endpoint:** requests without a valid `initData` get `401`. The only unauthenticated routes are static files and `GET /healthz`.
- **initData validation:** follow Telegram's documented algorithm — secret key = `HMAC_SHA256(bot_token, "WebAppData")`, then compare `HMAC_SHA256(data_check_string, secret_key)` to the `hash` field; reject if `auth_date` is older than 24h.
- **Config keys (env vars):** `BotToken`, `ConnectionStrings__Default`. Bind via `IOptions`/configuration; never hardcode the token.
- **FX source:** Frankfurter API (`https://api.frankfurter.app`), free, no key. Base/quote via ISO 4217 codes (e.g. `USD`, `EUR`).
- **Frontend → API auth:** every request carries header `Authorization: tma <initData>`.

---

## File Structure

```
PersonalExpenseTracker/
├── ExpenseTracker.sln
├── Dockerfile
├── .dockerignore
├── server/
│   ├── ExpenseTracker.Api/
│   │   ├── ExpenseTracker.Api.csproj
│   │   ├── Program.cs                     # composition root, pipeline, endpoint mapping
│   │   ├── appsettings.json
│   │   ├── Domain/                         # EF entities (one file each)
│   │   │   ├── User.cs  Category.cs  Expense.cs  Budget.cs  ExchangeRate.cs  Setting.cs
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── DefaultCategories.cs        # seed list
│   │   │   └── Migrations/                 # EF-generated
│   │   ├── Auth/
│   │   │   ├── TelegramInitDataValidator.cs    # pure validation logic (unit-tested)
│   │   │   ├── TelegramUser.cs                  # parsed payload
│   │   │   ├── TelegramAuthHandler.cs           # AuthenticationHandler<>
│   │   │   ├── CurrentUserAccessor.cs           # resolves/provisions User from claims
│   │   │   └── BotOptions.cs
│   │   ├── Currency/
│   │   │   ├── IExchangeRateProvider.cs
│   │   │   ├── FrankfurterRateProvider.cs       # HTTP client to Frankfurter
│   │   │   ├── ExchangeRateService.cs           # cache-through DB + provider
│   │   │   └── CurrencyConverter.cs             # pure math (unit-tested)
│   │   └── Features/                        # endpoint groups + DTOs (one folder each)
│   │       ├── Expenses/ Categories/ Budgets/ Reports/ Settings/
│   └── ExpenseTracker.Tests/
│       ├── ExpenseTracker.Tests.csproj
│       ├── Unit/  (TelegramInitDataValidatorTests.cs, CurrencyConverterTests.cs)
│       ├── Integration/ (ApiFactory.cs, ExpensesApiTests.cs, ...)
│       └── TestData/ (InitDataBuilder.cs)
└── web/
    ├── package.json  vite.config.ts  tsconfig.json  index.html
    └── src/
        ├── main.tsx  App.tsx  router.tsx
        ├── telegram/initData.ts  telegram/theme.ts
        ├── api/client.ts  api/types.ts  api/hooks.ts
        ├── screens/AddExpense.tsx  Expenses.tsx  Categories.tsx  Budgets.tsx  Reports.tsx  Settings.tsx
        └── components/ (shared UI)
```

---

# Phase 1 — Backend API (independently testable)

### Task 1: Solution & project scaffolding

**Files:**
- Create: `ExpenseTracker.sln`, `server/ExpenseTracker.Api/ExpenseTracker.Api.csproj`, `server/ExpenseTracker.Tests/ExpenseTracker.Tests.csproj`
- Create: `server/ExpenseTracker.Api/Program.cs`, `appsettings.json`

**Interfaces:**
- Produces: a buildable solution; `Program.cs` exposing `GET /healthz` returning `200 "ok"`; a `WebApplication` startup that integration tests can host.

- [ ] **Step 1: Create solution and projects**

```bash
cd /mnt/projects/data/kamol/dev/PersonalExpenseTracker
dotnet new sln -n ExpenseTracker
dotnet new webapi -n ExpenseTracker.Api -o server/ExpenseTracker.Api --framework net8.0 --use-minimal-apis
dotnet new xunit -n ExpenseTracker.Tests -o server/ExpenseTracker.Tests --framework net8.0
dotnet sln add server/ExpenseTracker.Api/ExpenseTracker.Api.csproj server/ExpenseTracker.Tests/ExpenseTracker.Tests.csproj
dotnet add server/ExpenseTracker.Tests reference server/ExpenseTracker.Api
```

- [ ] **Step 2: Add packages**

```bash
dotnet add server/ExpenseTracker.Api package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add server/ExpenseTracker.Api package Microsoft.EntityFrameworkCore.Design
dotnet add server/ExpenseTracker.Tests package FluentAssertions
dotnet add server/ExpenseTracker.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add server/ExpenseTracker.Tests package Testcontainers.PostgreSql
```

- [ ] **Step 3: Replace `Program.cs` with a minimal, testable startup**

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/healthz", () => Results.Text("ok"));

app.Run();

public partial class Program { } // exposes Program to the test host
```

- [ ] **Step 4: Build and run the default test**

Run: `dotnet build ExpenseTracker.sln`
Expected: `Build succeeded`. Then `dotnet test` — the template's placeholder test passes (we replace it in Task 3).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "chore: scaffold solution, api, and test projects"
```

---

### Task 2: Domain entities, DbContext, and initial migration

**Files:**
- Create: `server/ExpenseTracker.Api/Domain/{User,Category,Expense,Budget,ExchangeRate,Setting}.cs`
- Create: `server/ExpenseTracker.Api/Data/AppDbContext.cs`, `Data/DefaultCategories.cs`
- Modify: `server/ExpenseTracker.Api/Program.cs` (register `AppDbContext`)

**Interfaces:**
- Produces: entity types and `AppDbContext` with `DbSet`s `Users, Categories, Expenses, Budgets, ExchangeRates, Settings`; `DefaultCategories.All` (a `IReadOnlyList<(string Name, string Emoji)>`).

- [ ] **Step 1: Create entity classes**

`Domain/User.cs`:
```csharp
namespace ExpenseTracker.Api.Domain;

public class User
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }   // unique
    public string? FirstName { get; set; }
    public string? Username { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

`Domain/Category.cs`:
```csharp
namespace ExpenseTracker.Api.Domain;

public class Category
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
    public bool IsArchived { get; set; }
}
```

`Domain/Expense.cs`:
```csharp
namespace ExpenseTracker.Api.Domain;

public class Expense
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }            // in CurrencyCode
    public string CurrencyCode { get; set; } = "USD";
    public int CategoryId { get; set; }
    public DateOnly SpentOn { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

`Domain/Budget.cs`:
```csharp
namespace ExpenseTracker.Api.Domain;

public class Budget
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? CategoryId { get; set; }           // null = overall budget
    public decimal LimitAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    // Period is monthly-only for v1; no column needed yet.
}
```

`Domain/ExchangeRate.cs` (global/shared):
```csharp
namespace ExpenseTracker.Api.Domain;

public class ExchangeRate
{
    public int Id { get; set; }
    public string BaseCurrency { get; set; } = "";
    public string QuoteCurrency { get; set; } = "";
    public decimal Rate { get; set; }              // 1 Base = Rate Quote
    public DateOnly AsOfDate { get; set; }
}
```

`Domain/Setting.cs`:
```csharp
namespace ExpenseTracker.Api.Domain;

public class Setting
{
    public int Id { get; set; }
    public int UserId { get; set; }                // unique
    public string BaseCurrency { get; set; } = "USD";
}
```

- [ ] **Step 2: Create the default-categories seed**

`Data/DefaultCategories.cs`:
```csharp
namespace ExpenseTracker.Api.Data;

public static class DefaultCategories
{
    public static readonly IReadOnlyList<(string Name, string Emoji)> All = new[]
    {
        ("Food", "🍔"), ("Transport", "🚌"), ("Rent", "🏠"),
        ("Groceries", "🛒"), ("Entertainment", "🎬"), ("Health", "💊"),
        ("Shopping", "🛍️"), ("Other", "📦"),
    };
}
```

- [ ] **Step 3: Create `AppDbContext`**

`Data/AppDbContext.cs`:
```csharp
using ExpenseTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.TelegramUserId).IsUnique();
        b.Entity<Setting>().HasIndex(s => s.UserId).IsUnique();
        b.Entity<Expense>().HasIndex(e => new { e.UserId, e.SpentOn });
        b.Entity<Category>().HasIndex(c => c.UserId);
        b.Entity<Budget>().HasIndex(bg => bg.UserId);
        b.Entity<ExchangeRate>()
            .HasIndex(r => new { r.BaseCurrency, r.QuoteCurrency, r.AsOfDate }).IsUnique();

        b.Entity<Expense>().Property(e => e.Amount).HasColumnType("decimal(18,2)");
        b.Entity<Budget>().Property(e => e.LimitAmount).HasColumnType("decimal(18,2)");
        b.Entity<ExchangeRate>().Property(e => e.Rate).HasColumnType("decimal(18,8)");
    }
}
```

- [ ] **Step 4: Register the DbContext in `Program.cs`**

Add after `CreateBuilder`:
```csharp
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```
Add the using: `using ExpenseTracker.Api.Data;` and to `appsettings.json` a placeholder:
```json
{ "ConnectionStrings": { "Default": "Host=localhost;Database=expenses;Username=postgres;Password=postgres" } }
```

- [ ] **Step 5: Create the initial migration**

```bash
dotnet tool install --global dotnet-ef   # if not already installed
dotnet ef migrations add InitialCreate -p server/ExpenseTracker.Api -s server/ExpenseTracker.Api
```
Expected: a `Data/Migrations/*_InitialCreate.cs` file is created.

- [ ] **Step 6: Build & commit**

Run: `dotnet build` → succeeds.
```bash
git add -A && git commit -m "feat: add domain model, DbContext, and initial migration"
```

---

### Task 3: Telegram initData validator (security-critical, pure unit-tested)

**Files:**
- Create: `server/ExpenseTracker.Api/Auth/{TelegramInitDataValidator.cs,TelegramUser.cs}`
- Create: `server/ExpenseTracker.Tests/TestData/InitDataBuilder.cs`
- Create: `server/ExpenseTracker.Tests/Unit/TelegramInitDataValidatorTests.cs`
- Delete: the template's default test file (`server/ExpenseTracker.Tests/UnitTest1.cs`)

**Interfaces:**
- Produces:
  - `record TelegramUser(long Id, string? FirstName, string? Username)`
  - `class TelegramInitDataValidator` with `bool TryValidate(string initData, string botToken, DateTimeOffset now, out TelegramUser user)`. Returns `false` on bad signature, missing `hash`, missing `user`, or `auth_date` older than 24h.
  - Test helper `InitDataBuilder.Build(long userId, string botToken, DateTimeOffset authDate, string? firstName = null, string? username = null)` → a correctly-signed `initData` query string.

- [ ] **Step 1: Write the failing tests**

`server/ExpenseTracker.Tests/TestData/InitDataBuilder.cs`:
```csharp
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ExpenseTracker.Tests.TestData;

public static class InitDataBuilder
{
    public static string Build(long userId, string botToken, DateTimeOffset authDate,
        string? firstName = "Test", string? username = "tester")
    {
        var userJson = JsonSerializer.Serialize(new
        {
            id = userId, first_name = firstName, username
        });
        var fields = new SortedDictionary<string, string>
        {
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(),
            ["user"] = userJson,
        };
        var dataCheckString = string.Join("\n", fields.Select(kv => $"{kv.Key}={kv.Value}"));
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"),
                                         Encoding.UTF8.GetBytes(botToken));
        var hash = Convert.ToHexString(
            new HMACSHA256(secret).ComputeHash(Encoding.UTF8.GetBytes(dataCheckString)))
            .ToLowerInvariant();
        var parts = fields.Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}").ToList();
        parts.Add($"hash={hash}");
        return string.Join("&", parts);
    }
}
```

`server/ExpenseTracker.Tests/Unit/TelegramInitDataValidatorTests.cs`:
```csharp
using ExpenseTracker.Api.Auth;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class TelegramInitDataValidatorTests
{
    const string BotToken = "123456:TESTBOTTOKEN";
    readonly TelegramInitDataValidator _sut = new();
    readonly DateTimeOffset _now = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valid_initData_returns_true_and_parses_user()
    {
        var initData = InitDataBuilder.Build(42, BotToken, _now.AddMinutes(-1), "Alice", "alice");

        var ok = _sut.TryValidate(initData, BotToken, _now, out var user);

        ok.Should().BeTrue();
        user.Id.Should().Be(42);
        user.FirstName.Should().Be("Alice");
        user.Username.Should().Be("alice");
    }

    [Fact]
    public void Tampered_hash_returns_false()
    {
        var initData = InitDataBuilder.Build(42, BotToken, _now.AddMinutes(-1));
        var tampered = initData[..^4] + "0000";

        _sut.TryValidate(tampered, BotToken, _now, out _).Should().BeFalse();
    }

    [Fact]
    public void Wrong_bot_token_returns_false()
    {
        var initData = InitDataBuilder.Build(42, BotToken, _now.AddMinutes(-1));

        _sut.TryValidate(initData, "999:WRONG", _now, out _).Should().BeFalse();
    }

    [Fact]
    public void Expired_auth_date_returns_false()
    {
        var initData = InitDataBuilder.Build(42, BotToken, _now.AddHours(-25));

        _sut.TryValidate(initData, BotToken, _now, out _).Should().BeFalse();
    }

    [Fact]
    public void Missing_hash_returns_false()
    {
        _sut.TryValidate("user=%7B%22id%22%3A1%7D&auth_date=1", BotToken, _now, out _)
            .Should().BeFalse();
    }
}
```
Then delete `server/ExpenseTracker.Tests/UnitTest1.cs`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter TelegramInitDataValidatorTests`
Expected: FAIL — `TelegramInitDataValidator` / `TelegramUser` do not exist (compile error).

- [ ] **Step 3: Implement the validator**

`Auth/TelegramUser.cs`:
```csharp
namespace ExpenseTracker.Api.Auth;

public record TelegramUser(long Id, string? FirstName, string? Username);
```

`Auth/TelegramInitDataValidator.cs`:
```csharp
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ExpenseTracker.Api.Auth;

public class TelegramInitDataValidator
{
    static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    public bool TryValidate(string initData, string botToken, DateTimeOffset now, out TelegramUser user)
    {
        user = default!;
        if (string.IsNullOrEmpty(initData)) return false;

        var pairs = initData.Split('&')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]));

        if (!pairs.TryGetValue("hash", out var providedHash)) return false;
        if (!pairs.TryGetValue("auth_date", out var authRaw)
            || !long.TryParse(authRaw, out var authUnix)) return false;
        if (!pairs.TryGetValue("user", out var userJson)) return false;

        var authDate = DateTimeOffset.FromUnixTimeSeconds(authUnix);
        if (now - authDate > MaxAge) return false;

        var dataCheckString = string.Join("\n", pairs
            .Where(kv => kv.Key != "hash")
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"),
                                         Encoding.UTF8.GetBytes(botToken));
        var computed = Convert.ToHexString(
            new HMACSHA256(secret).ComputeHash(Encoding.UTF8.GetBytes(dataCheckString)))
            .ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(providedHash.ToLowerInvariant()))) return false;

        using var doc = JsonDocument.Parse(userJson);
        var root = doc.RootElement;
        user = new TelegramUser(
            root.GetProperty("id").GetInt64(),
            root.TryGetProperty("first_name", out var f) ? f.GetString() : null,
            root.TryGetProperty("username", out var u) ? u.GetString() : null);
        return true;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter TelegramInitDataValidatorTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: add Telegram initData HMAC validator with tests"
```

---

### Task 4: Auth handler, current-user provisioning, and category seeding

**Files:**
- Create: `server/ExpenseTracker.Api/Auth/{BotOptions.cs,TelegramAuthHandler.cs,CurrentUserAccessor.cs}`
- Modify: `server/ExpenseTracker.Api/Program.cs` (register validator, options, auth, current-user; require auth on `/api`)
- Create: `server/ExpenseTracker.Tests/Integration/ApiFactory.cs`
- Create: `server/ExpenseTracker.Tests/Integration/AuthTests.cs`

**Interfaces:**
- Consumes: `TelegramInitDataValidator` (Task 3), `AppDbContext` (Task 2), `DefaultCategories.All`.
- Produces:
  - `class BotOptions { public string Token { get; set; } }` bound from config key `BotToken`.
  - `TelegramAuthHandler` (scheme name `"tma"`): reads `Authorization: tma <initData>`, validates, sets a `Claim("tg_id", <id>)` plus name/username claims.
  - `interface ICurrentUser { Task<Domain.User> GetOrCreateAsync(); }` + `CurrentUserAccessor` implementation that finds-or-inserts a `User` by `TelegramUserId` and, on creation, seeds `DefaultCategories.All` and a `Setting{BaseCurrency="USD"}`.
  - `ApiFactory : WebApplicationFactory<Program>` for integration tests, wiring a Testcontainers Postgres and a known `BotToken`.

- [ ] **Step 1: Write the failing integration test**

`server/ExpenseTracker.Tests/Integration/ApiFactory.cs`:
```csharp
using ExpenseTracker.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string BotToken = "123456:TESTBOTTOKEN";
    readonly PostgreSqlContainer _db = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("BotToken", BotToken);
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync() => await _db.DisposeAsync();
}
```
(Add `using Microsoft.AspNetCore.Hosting;` if the analyzer requests it.)

`server/ExpenseTracker.Tests/Integration/AuthTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task No_auth_header_returns_401()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/categories");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Valid_initData_provisions_user_and_default_categories()
    {
        var client = factory.CreateClient();
        var initData = InitDataBuilder.Build(7001, ApiFactory.BotToken, DateTimeOffset.UtcNow);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma", initData);

        var res = await client.GetAsync("/api/categories");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.TelegramUserId == 7001);
        (await db.Categories.CountAsync(c => c.UserId == user.Id)).Should().Be(DefaultCategories.All.Count);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter AuthTests`
Expected: FAIL — `/api/categories` route and auth scheme `tma` don't exist yet (401-on-missing test may pass coincidentally if route 404s; the provisioning test will fail).

- [ ] **Step 3: Implement options, handler, and accessor**

`Auth/BotOptions.cs`:
```csharp
namespace ExpenseTracker.Api.Auth;
public class BotOptions { public string Token { get; set; } = ""; }
```

`Auth/TelegramAuthHandler.cs`:
```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Api.Auth;

public class TelegramAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "tma";
    readonly TelegramInitDataValidator _validator;
    readonly BotOptions _bot;

    public TelegramAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder,
        TelegramInitDataValidator validator, IOptions<BotOptions> bot)
        : base(options, logger, encoder)
    {
        _validator = validator;
        _bot = bot.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith($"{Scheme} ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var initData = header[(Scheme.Length + 1)..];
        if (!_validator.TryValidate(initData, _bot.Token, DateTimeOffset.UtcNow, out var tgUser))
            return Task.FromResult(AuthenticateResult.Fail("Invalid initData"));

        var claims = new List<Claim> { new("tg_id", tgUser.Id.ToString()) };
        if (tgUser.FirstName is not null) claims.Add(new(ClaimTypes.GivenName, tgUser.FirstName));
        if (tgUser.Username is not null) claims.Add(new("tg_username", tgUser.Username));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme)));
    }
}
```

`Auth/CurrentUserAccessor.cs`:
```csharp
using System.Security.Claims;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Auth;

public interface ICurrentUser { Task<User> GetOrCreateAsync(); }

public class CurrentUserAccessor(IHttpContextAccessor http, AppDbContext db) : ICurrentUser
{
    public async Task<User> GetOrCreateAsync()
    {
        var principal = http.HttpContext!.User;
        var tgId = long.Parse(principal.FindFirstValue("tg_id")!);
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramUserId == tgId);
        if (user is not null) return user;

        user = new User
        {
            TelegramUserId = tgId,
            FirstName = principal.FindFirstValue(ClaimTypes.GivenName),
            Username = principal.FindFirstValue("tg_username"),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();   // assigns user.Id

        db.Categories.AddRange(DefaultCategories.All.Select(c =>
            new Category { UserId = user.Id, Name = c.Name, Emoji = c.Emoji }));
        db.Settings.Add(new Setting { UserId = user.Id, BaseCurrency = "USD" });
        await db.SaveChangesAsync();
        return user;
    }
}
```

- [ ] **Step 4: Wire it all in `Program.cs`**

Add registrations after the DbContext line and replace the pipeline section:
```csharp
builder.Services.Configure<BotOptions>(o => o.Token = builder.Configuration["BotToken"] ?? "");
builder.Services.AddSingleton<TelegramInitDataValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddAuthentication(TelegramAuthHandler.Scheme)
    .AddScheme<AuthenticationSchemeOptions, TelegramAuthHandler>(TelegramAuthHandler.Scheme, _ => { });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Text("ok"));

var api = app.MapGroup("/api").RequireAuthorization();
// Placeholder so the auth test has a route; replaced in Task 5+.
api.MapGet("/categories", async (ICurrentUser cu, AppDbContext db) =>
{
    var user = await cu.GetOrCreateAsync();
    return Results.Ok(await db.Categories.Where(c => c.UserId == user.Id).ToListAsync());
});

app.Run();
```
Add usings: `using ExpenseTracker.Api.Auth; using Microsoft.AspNetCore.Authentication; using Microsoft.EntityFrameworkCore;`

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test --filter AuthTests`
Expected: PASS (2 tests). Requires Docker running for Testcontainers.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: Telegram auth handler with user provisioning and category seeding"
```

---

### Task 5: Expenses CRUD endpoints (with per-user isolation tests)

**Files:**
- Create: `server/ExpenseTracker.Api/Features/Expenses/ExpenseEndpoints.cs`, `Features/Expenses/ExpenseDtos.cs`
- Modify: `server/ExpenseTracker.Api/Program.cs` (call `app.MapExpenseEndpoints()`)
- Create: `server/ExpenseTracker.Tests/Integration/ExpensesApiTests.cs`

**Interfaces:**
- Consumes: `ICurrentUser`, `AppDbContext`.
- Produces:
  - DTOs: `record ExpenseInput(decimal Amount, string CurrencyCode, int CategoryId, DateOnly SpentOn, string? Note)`, `record ExpenseDto(int Id, decimal Amount, string CurrencyCode, int CategoryId, DateOnly SpentOn, string? Note)`.
  - `static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder api)` mapping `GET/POST /api/expenses`, `PUT/DELETE /api/expenses/{id}` with filters `from`, `to`, `categoryId`.

- [ ] **Step 1: Write the failing tests**

`server/ExpenseTracker.Tests/Integration/ExpensesApiTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Features.Expenses;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class ExpensesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        var initData = InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma", initData);
        return client;
    }

    async Task<int> FirstCategoryId(long tgId)
    {
        var cats = await ClientFor(tgId).GetFromJsonAsync<List<CategoryRow>>("/api/categories");
        return cats![0].Id;
    }
    record CategoryRow(int Id, string Name);

    [Fact]
    public async Task Create_then_list_returns_the_expense()
    {
        var client = ClientFor(8001);
        var catId = await FirstCategoryId(8001);
        var input = new ExpenseInput(12.50m, "USD", catId, new DateOnly(2026, 6, 1), "lunch");

        var created = await client.PostAsJsonAsync("/api/expenses", input);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses");
        list.Should().ContainSingle(e => e.Note == "lunch" && e.Amount == 12.50m);
    }

    [Fact]
    public async Task User_cannot_see_another_users_expenses()
    {
        var alice = ClientFor(8101);
        var catId = await FirstCategoryId(8101);
        await alice.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(99m, "USD", catId, new DateOnly(2026, 6, 1), "alice-secret"));

        var bob = ClientFor(8102);
        var bobList = await bob.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses");
        bobList.Should().NotContain(e => e.Note == "alice-secret");
    }

    [Fact]
    public async Task User_cannot_delete_another_users_expense()
    {
        var alice = ClientFor(8201);
        var catId = await FirstCategoryId(8201);
        var created = await (await alice.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(5m, "USD", catId, new DateOnly(2026, 6, 1), "x")))
            .Content.ReadFromJsonAsync<ExpenseDto>();

        var bob = ClientFor(8202);
        var res = await bob.DeleteAsync($"/api/expenses/{created!.Id}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter ExpensesApiTests`
Expected: FAIL — `ExpenseInput`/`ExpenseDto` and `/api/expenses` routes don't exist.

- [ ] **Step 3: Implement DTOs and endpoints**

`Features/Expenses/ExpenseDtos.cs`:
```csharp
namespace ExpenseTracker.Api.Features.Expenses;

public record ExpenseInput(decimal Amount, string CurrencyCode, int CategoryId, DateOnly SpentOn, string? Note);
public record ExpenseDto(int Id, decimal Amount, string CurrencyCode, int CategoryId, DateOnly SpentOn, string? Note);
```

`Features/Expenses/ExpenseEndpoints.cs`:
```csharp
using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Features.Expenses;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/expenses");

        g.MapGet("", async (ICurrentUser cu, AppDbContext db,
            DateOnly? from, DateOnly? to, int? categoryId) =>
        {
            var user = await cu.GetOrCreateAsync();
            var q = db.Expenses.Where(e => e.UserId == user.Id);
            if (from is not null) q = q.Where(e => e.SpentOn >= from);
            if (to is not null) q = q.Where(e => e.SpentOn <= to);
            if (categoryId is not null) q = q.Where(e => e.CategoryId == categoryId);
            var items = await q.OrderByDescending(e => e.SpentOn).ThenByDescending(e => e.Id)
                .Select(e => new ExpenseDto(e.Id, e.Amount, e.CurrencyCode, e.CategoryId, e.SpentOn, e.Note))
                .ToListAsync();
            return Results.Ok(items);
        });

        g.MapPost("", async (ICurrentUser cu, AppDbContext db, ExpenseInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            if (input.Amount <= 0) return Results.BadRequest("Amount must be positive.");
            var ownsCategory = await db.Categories.AnyAsync(c => c.Id == input.CategoryId && c.UserId == user.Id);
            if (!ownsCategory) return Results.BadRequest("Unknown category.");
            var e = new Expense
            {
                UserId = user.Id, Amount = input.Amount,
                CurrencyCode = input.CurrencyCode.ToUpperInvariant(),
                CategoryId = input.CategoryId, SpentOn = input.SpentOn,
                Note = input.Note, CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Expenses.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/expenses/{e.Id}",
                new ExpenseDto(e.Id, e.Amount, e.CurrencyCode, e.CategoryId, e.SpentOn, e.Note));
        });

        g.MapPut("/{id:int}", async (ICurrentUser cu, AppDbContext db, int id, ExpenseInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);
            if (e is null) return Results.NotFound();
            e.Amount = input.Amount;
            e.CurrencyCode = input.CurrencyCode.ToUpperInvariant();
            e.CategoryId = input.CategoryId;
            e.SpentOn = input.SpentOn;
            e.Note = input.Note;
            await db.SaveChangesAsync();
            return Results.Ok(new ExpenseDto(e.Id, e.Amount, e.CurrencyCode, e.CategoryId, e.SpentOn, e.Note));
        });

        g.MapDelete("/{id:int}", async (ICurrentUser cu, AppDbContext db, int id) =>
        {
            var user = await cu.GetOrCreateAsync();
            var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);
            if (e is null) return Results.NotFound();
            db.Expenses.Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return api;
    }
}
```

- [ ] **Step 4: Map it in `Program.cs`**

Replace the placeholder `/categories` mapping's neighbor area by adding before `app.Run();`:
```csharp
api.MapExpenseEndpoints();
```
Add `using ExpenseTracker.Api.Features.Expenses;`.

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test --filter ExpensesApiTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: expenses CRUD endpoints with per-user isolation"
```

---

### Task 6: Categories endpoints

**Files:**
- Create: `server/ExpenseTracker.Api/Features/Categories/CategoryEndpoints.cs`, `Features/Categories/CategoryDtos.cs`
- Modify: `server/ExpenseTracker.Api/Program.cs` (replace the placeholder `/categories` with `MapCategoryEndpoints()`)
- Create: `server/ExpenseTracker.Tests/Integration/CategoriesApiTests.cs`

**Interfaces:**
- Consumes: `ICurrentUser`, `AppDbContext`.
- Produces: `record CategoryInput(string Name, string Emoji)`, `record CategoryDto(int Id, string Name, string Emoji, bool IsArchived)`, and `static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder api)` mapping `GET /api/categories`, `POST /api/categories`, `PUT /api/categories/{id}`.

- [ ] **Step 1: Write the failing test**

`server/ExpenseTracker.Tests/Integration/CategoriesApiTests.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Api.Features.Categories;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class CategoriesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    [Fact]
    public async Task New_user_has_default_categories()
    {
        var list = await ClientFor(9001).GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        list!.Should().Contain(c => c.Name == "Food");
    }

    [Fact]
    public async Task Can_add_a_custom_category()
    {
        var client = ClientFor(9002);
        await client.PostAsJsonAsync("/api/categories", new CategoryInput("Coffee", "☕"));
        var list = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        list!.Should().Contain(c => c.Name == "Coffee" && c.Emoji == "☕");
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter CategoriesApiTests`
Expected: FAIL — `CategoryDto`/`CategoryInput` don't exist.

- [ ] **Step 3: Implement DTOs and endpoints**

`Features/Categories/CategoryDtos.cs`:
```csharp
namespace ExpenseTracker.Api.Features.Categories;

public record CategoryInput(string Name, string Emoji);
public record CategoryDto(int Id, string Name, string Emoji, bool IsArchived);
```

`Features/Categories/CategoryEndpoints.cs`:
```csharp
using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Features.Categories;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/categories");

        g.MapGet("", async (ICurrentUser cu, AppDbContext db) =>
        {
            var user = await cu.GetOrCreateAsync();
            return Results.Ok(await db.Categories
                .Where(c => c.UserId == user.Id && !c.IsArchived)
                .OrderBy(c => c.Name)
                .Select(c => new CategoryDto(c.Id, c.Name, c.Emoji, c.IsArchived))
                .ToListAsync());
        });

        g.MapPost("", async (ICurrentUser cu, AppDbContext db, CategoryInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            if (string.IsNullOrWhiteSpace(input.Name)) return Results.BadRequest("Name required.");
            var c = new Category { UserId = user.Id, Name = input.Name.Trim(), Emoji = input.Emoji };
            db.Categories.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/categories/{c.Id}",
                new CategoryDto(c.Id, c.Name, c.Emoji, c.IsArchived));
        });

        g.MapPut("/{id:int}", async (ICurrentUser cu, AppDbContext db, int id, CategoryInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);
            if (c is null) return Results.NotFound();
            c.Name = input.Name.Trim();
            c.Emoji = input.Emoji;
            await db.SaveChangesAsync();
            return Results.Ok(new CategoryDto(c.Id, c.Name, c.Emoji, c.IsArchived));
        });

        return api;
    }
}
```

- [ ] **Step 4: Replace the placeholder in `Program.cs`**

Delete the inline `api.MapGet("/categories", ...)` placeholder from Task 4 and add:
```csharp
api.MapCategoryEndpoints();
```
Add `using ExpenseTracker.Api.Features.Categories;`.

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test --filter CategoriesApiTests`
Expected: PASS. Also re-run `dotnet test --filter AuthTests` — still green (route still exists).

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: categories endpoints"
```

---

### Task 7: Currency conversion (pure math, unit-tested) + rate service

**Files:**
- Create: `server/ExpenseTracker.Api/Currency/{IExchangeRateProvider.cs,FrankfurterRateProvider.cs,ExchangeRateService.cs,CurrencyConverter.cs}`
- Modify: `server/ExpenseTracker.Api/Program.cs` (register `HttpClient` + services)
- Create: `server/ExpenseTracker.Tests/Unit/CurrencyConverterTests.cs`

**Interfaces:**
- Produces:
  - `class CurrencyConverter` with `decimal Convert(decimal amount, decimal rate)` returning `Math.Round(amount * rate, 2, MidpointRounding.ToEven)`.
  - `interface IExchangeRateProvider { Task<decimal> GetRateAsync(string from, string to, DateOnly date); }` (1 unit `from` = N `to`).
  - `class FrankfurterRateProvider : IExchangeRateProvider` (HTTP).
  - `class ExchangeRateService` with `Task<decimal> GetRateAsync(string from, string to, DateOnly date)` that returns `1m` when `from==to`, else reads `ExchangeRate` cache, and on miss calls the provider and stores the row.

- [ ] **Step 1: Write the failing unit test**

`server/ExpenseTracker.Tests/Unit/CurrencyConverterTests.cs`:
```csharp
using ExpenseTracker.Api.Currency;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class CurrencyConverterTests
{
    readonly CurrencyConverter _sut = new();

    [Fact]
    public void Converts_and_rounds_to_two_decimals()
        => _sut.Convert(10m, 0.875m).Should().Be(8.75m);

    [Fact]
    public void Uses_bankers_rounding()
        => _sut.Convert(1m, 1.005m).Should().Be(1.00m); // 1.005 -> 1.00 (round half to even)

    [Fact]
    public void Identity_rate_returns_same_amount()
        => _sut.Convert(42.42m, 1m).Should().Be(42.42m);
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter CurrencyConverterTests`
Expected: FAIL — `CurrencyConverter` does not exist.

- [ ] **Step 3: Implement converter and rate plumbing**

`Currency/CurrencyConverter.cs`:
```csharp
namespace ExpenseTracker.Api.Currency;

public class CurrencyConverter
{
    public decimal Convert(decimal amount, decimal rate)
        => Math.Round(amount * rate, 2, MidpointRounding.ToEven);
}
```

`Currency/IExchangeRateProvider.cs`:
```csharp
namespace ExpenseTracker.Api.Currency;

public interface IExchangeRateProvider
{
    Task<decimal> GetRateAsync(string from, string to, DateOnly date);
}
```

`Currency/FrankfurterRateProvider.cs`:
```csharp
using System.Text.Json;

namespace ExpenseTracker.Api.Currency;

public class FrankfurterRateProvider(HttpClient http) : IExchangeRateProvider
{
    public async Task<decimal> GetRateAsync(string from, string to, DateOnly date)
    {
        // https://api.frankfurter.app/2026-06-01?from=USD&to=EUR
        var url = $"{date:yyyy-MM-dd}?from={from}&to={to}";
        using var stream = await http.GetStreamAsync(url);
        using var doc = await JsonDocument.ParseAsync(stream);
        var rate = doc.RootElement.GetProperty("rates").GetProperty(to).GetDecimal();
        return rate;
    }
}
```

`Currency/ExchangeRateService.cs`:
```csharp
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Currency;

public class ExchangeRateService(AppDbContext db, IExchangeRateProvider provider)
{
    public async Task<decimal> GetRateAsync(string from, string to, DateOnly date)
    {
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();
        if (from == to) return 1m;

        var cached = await db.ExchangeRates.FirstOrDefaultAsync(r =>
            r.BaseCurrency == from && r.QuoteCurrency == to && r.AsOfDate == date);
        if (cached is not null) return cached.Rate;

        var rate = await provider.GetRateAsync(from, to, date);
        db.ExchangeRates.Add(new ExchangeRate
        {
            BaseCurrency = from, QuoteCurrency = to, Rate = rate, AsOfDate = date
        });
        await db.SaveChangesAsync();
        return rate;
    }
}
```

- [ ] **Step 4: Register services in `Program.cs`**

```csharp
builder.Services.AddSingleton<CurrencyConverter>();
builder.Services.AddHttpClient<IExchangeRateProvider, FrankfurterRateProvider>(c =>
    c.BaseAddress = new Uri("https://api.frankfurter.app/"));
builder.Services.AddScoped<ExchangeRateService>();
```
Add `using ExpenseTracker.Api.Currency;`.

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test --filter CurrencyConverterTests`
Expected: PASS (3 tests). Run `dotnet build` to confirm the service classes compile.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: currency converter and lazy exchange-rate service"
```

---

### Task 8: Reports summary endpoint (converts to base currency)

**Files:**
- Create: `server/ExpenseTracker.Api/Features/Reports/ReportEndpoints.cs`, `Features/Reports/ReportDtos.cs`
- Modify: `server/ExpenseTracker.Api/Program.cs` (`api.MapReportEndpoints()`)
- Create: `server/ExpenseTracker.Tests/Integration/ReportsApiTests.cs`

**Interfaces:**
- Consumes: `ICurrentUser`, `AppDbContext`, `ExchangeRateService`, `CurrencyConverter`; settings' `BaseCurrency`.
- Produces:
  - `record CategoryTotal(int CategoryId, string CategoryName, decimal Total)`
  - `record MonthTotal(string Month, decimal Total)` (Month = `yyyy-MM`)
  - `record ReportSummary(string BaseCurrency, decimal GrandTotal, IReadOnlyList<CategoryTotal> ByCategory, IReadOnlyList<MonthTotal> ByMonth)`
  - `static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder api)` mapping `GET /api/reports/summary?from=&to=`.

- [ ] **Step 1: Write the failing test**

`server/ExpenseTracker.Tests/Integration/ReportsApiTests.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Api.Features.Expenses;
using ExpenseTracker.Api.Features.Reports;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class ReportsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    record CategoryRow(int Id, string Name);

    [Fact]
    public async Task Summary_sums_same_currency_expenses_in_base()
    {
        var client = ClientFor(11001);
        var cats = await client.GetFromJsonAsync<List<CategoryRow>>("/api/categories");
        var catId = cats![0].Id;
        // Base currency is USD by default; use USD to avoid network FX in tests.
        await client.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(10m, "USD", catId, new DateOnly(2026, 6, 1), null));
        await client.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(15m, "USD", catId, new DateOnly(2026, 6, 2), null));

        var summary = await client.GetFromJsonAsync<ReportSummary>(
            "/api/reports/summary?from=2026-06-01&to=2026-06-30");

        summary!.BaseCurrency.Should().Be("USD");
        summary.GrandTotal.Should().Be(25m);
        summary.ByCategory.Should().ContainSingle(c => c.CategoryId == catId && c.Total == 25m);
        summary.ByMonth.Should().Contain(m => m.Month == "2026-06" && m.Total == 25m);
    }
}
```
(Note: the test deliberately uses only USD so it needs no outbound FX call. Cross-currency conversion is exercised by the unit tests in Task 7.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter ReportsApiTests`
Expected: FAIL — report types/route missing.

- [ ] **Step 3: Implement DTOs and endpoint**

`Features/Reports/ReportDtos.cs`:
```csharp
namespace ExpenseTracker.Api.Features.Reports;

public record CategoryTotal(int CategoryId, string CategoryName, decimal Total);
public record MonthTotal(string Month, decimal Total);
public record ReportSummary(string BaseCurrency, decimal GrandTotal,
    IReadOnlyList<CategoryTotal> ByCategory, IReadOnlyList<MonthTotal> ByMonth);
```

`Features/Reports/ReportEndpoints.cs`:
```csharp
using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Currency;
using ExpenseTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Features.Reports;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/reports/summary", async (ICurrentUser cu, AppDbContext db,
            ExchangeRateService rates, CurrencyConverter conv, DateOnly? from, DateOnly? to) =>
        {
            var user = await cu.GetOrCreateAsync();
            var setting = await db.Settings.FirstAsync(s => s.UserId == user.Id);
            var baseCcy = setting.BaseCurrency;

            var q = db.Expenses.Where(e => e.UserId == user.Id);
            if (from is not null) q = q.Where(e => e.SpentOn >= from);
            if (to is not null) q = q.Where(e => e.SpentOn <= to);

            var rows = await q.Join(db.Categories, e => e.CategoryId, c => c.Id,
                (e, c) => new { e.Amount, e.CurrencyCode, e.SpentOn, e.CategoryId, CategoryName = c.Name })
                .ToListAsync();

            var byCategory = new Dictionary<int, (string Name, decimal Total)>();
            var byMonth = new Dictionary<string, decimal>();
            decimal grand = 0m;

            foreach (var r in rows)
            {
                var rate = await rates.GetRateAsync(r.CurrencyCode, baseCcy, r.SpentOn);
                var amount = conv.Convert(r.Amount, rate);
                grand += amount;

                var cat = byCategory.GetValueOrDefault(r.CategoryId, (r.CategoryName, 0m));
                byCategory[r.CategoryId] = (r.CategoryName, cat.Total + amount);

                var month = r.SpentOn.ToString("yyyy-MM");
                byMonth[month] = byMonth.GetValueOrDefault(month, 0m) + amount;
            }

            var summary = new ReportSummary(
                baseCcy, grand,
                byCategory.Select(kv => new CategoryTotal(kv.Key, kv.Value.Name, kv.Value.Total))
                    .OrderByDescending(c => c.Total).ToList(),
                byMonth.Select(kv => new MonthTotal(kv.Key, kv.Value))
                    .OrderBy(m => m.Month).ToList());
            return Results.Ok(summary);
        });

        return api;
    }
}
```

- [ ] **Step 4: Map in `Program.cs`**

```csharp
api.MapReportEndpoints();
```
Add `using ExpenseTracker.Api.Features.Reports;`.

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test --filter ReportsApiTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: reports summary endpoint with base-currency conversion"
```

---

### Task 9: Budgets & Settings endpoints

**Files:**
- Create: `server/ExpenseTracker.Api/Features/Budgets/BudgetEndpoints.cs`, `Features/Budgets/BudgetDtos.cs`
- Create: `server/ExpenseTracker.Api/Features/Settings/SettingEndpoints.cs`, `Features/Settings/SettingDtos.cs`
- Modify: `server/ExpenseTracker.Api/Program.cs` (`api.MapBudgetEndpoints(); api.MapSettingEndpoints();`)
- Create: `server/ExpenseTracker.Tests/Integration/BudgetsAndSettingsApiTests.cs`

**Interfaces:**
- Produces:
  - `record BudgetInput(int? CategoryId, decimal LimitAmount, string CurrencyCode)`, `record BudgetDto(int Id, int? CategoryId, decimal LimitAmount, string CurrencyCode)`, `MapBudgetEndpoints` → `GET /api/budgets`, `PUT /api/budgets` (upsert by `CategoryId`).
  - `record SettingDto(string BaseCurrency)`, `MapSettingEndpoints` → `GET /api/settings`, `PUT /api/settings`.

- [ ] **Step 1: Write the failing test**

`server/ExpenseTracker.Tests/Integration/BudgetsAndSettingsApiTests.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Api.Features.Budgets;
using ExpenseTracker.Api.Features.Settings;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class BudgetsAndSettingsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    [Fact]
    public async Task Settings_default_is_usd_and_can_be_changed()
    {
        var client = ClientFor(12001);
        var initial = await client.GetFromJsonAsync<SettingDto>("/api/settings");
        initial!.BaseCurrency.Should().Be("USD");

        await client.PutAsJsonAsync("/api/settings", new SettingDto("EUR"));
        var updated = await client.GetFromJsonAsync<SettingDto>("/api/settings");
        updated!.BaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Budget_upsert_replaces_existing_for_same_category()
    {
        var client = ClientFor(12002);
        await client.PutAsJsonAsync("/api/budgets", new BudgetInput(null, 500m, "USD"));
        await client.PutAsJsonAsync("/api/budgets", new BudgetInput(null, 600m, "USD"));

        var list = await client.GetFromJsonAsync<List<BudgetDto>>("/api/budgets");
        list!.Should().ContainSingle(b => b.CategoryId == null && b.LimitAmount == 600m);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter BudgetsAndSettingsApiTests`
Expected: FAIL — types/routes missing.

- [ ] **Step 3: Implement Budgets**

`Features/Budgets/BudgetDtos.cs`:
```csharp
namespace ExpenseTracker.Api.Features.Budgets;

public record BudgetInput(int? CategoryId, decimal LimitAmount, string CurrencyCode);
public record BudgetDto(int Id, int? CategoryId, decimal LimitAmount, string CurrencyCode);
```

`Features/Budgets/BudgetEndpoints.cs`:
```csharp
using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Features.Budgets;

public static class BudgetEndpoints
{
    public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/budgets");

        g.MapGet("", async (ICurrentUser cu, AppDbContext db) =>
        {
            var user = await cu.GetOrCreateAsync();
            return Results.Ok(await db.Budgets.Where(b => b.UserId == user.Id)
                .Select(b => new BudgetDto(b.Id, b.CategoryId, b.LimitAmount, b.CurrencyCode))
                .ToListAsync());
        });

        g.MapPut("", async (ICurrentUser cu, AppDbContext db, BudgetInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            if (input.LimitAmount <= 0) return Results.BadRequest("Limit must be positive.");
            var existing = await db.Budgets.FirstOrDefaultAsync(b =>
                b.UserId == user.Id && b.CategoryId == input.CategoryId);
            if (existing is null)
            {
                existing = new Budget { UserId = user.Id, CategoryId = input.CategoryId };
                db.Budgets.Add(existing);
            }
            existing.LimitAmount = input.LimitAmount;
            existing.CurrencyCode = input.CurrencyCode.ToUpperInvariant();
            await db.SaveChangesAsync();
            return Results.Ok(new BudgetDto(existing.Id, existing.CategoryId,
                existing.LimitAmount, existing.CurrencyCode));
        });

        return api;
    }
}
```

- [ ] **Step 4: Implement Settings**

`Features/Settings/SettingDtos.cs`:
```csharp
namespace ExpenseTracker.Api.Features.Settings;

public record SettingDto(string BaseCurrency);
```

`Features/Settings/SettingEndpoints.cs`:
```csharp
using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Features.Settings;

public static class SettingEndpoints
{
    public static IEndpointRouteBuilder MapSettingEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/settings");

        g.MapGet("", async (ICurrentUser cu, AppDbContext db) =>
        {
            var user = await cu.GetOrCreateAsync();
            var s = await db.Settings.FirstAsync(x => x.UserId == user.Id);
            return Results.Ok(new SettingDto(s.BaseCurrency));
        });

        g.MapPut("", async (ICurrentUser cu, AppDbContext db, SettingDto input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var s = await db.Settings.FirstAsync(x => x.UserId == user.Id);
            s.BaseCurrency = input.BaseCurrency.ToUpperInvariant();
            await db.SaveChangesAsync();
            return Results.Ok(new SettingDto(s.BaseCurrency));
        });

        return api;
    }
}
```

- [ ] **Step 5: Map both in `Program.cs` and run tests**

```csharp
api.MapBudgetEndpoints();
api.MapSettingEndpoints();
```
Add usings for both feature namespaces.
Run: `dotnet test --filter BudgetsAndSettingsApiTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: budgets and settings endpoints"
```

---

### Task 10: Apply migrations on startup + full backend test pass

**Files:**
- Modify: `server/ExpenseTracker.Api/Program.cs` (run `db.Database.Migrate()` on startup)

**Interfaces:**
- Produces: a backend that, on boot, applies pending migrations so a fresh PaaS database is ready without a manual step.

- [ ] **Step 1: Add startup migration**

In `Program.cs`, after `var app = builder.Build();` and before mapping endpoints:
```csharp
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}
```

- [ ] **Step 2: Run the full backend test suite**

Run: `dotnet test`
Expected: ALL tests PASS (unit + integration). Docker must be running for Testcontainers.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: apply EF migrations on startup"
```

---

# Phase 2 — React Mini App frontend

### Task 11: Vite scaffold + Telegram SDK init + theme

**Files:**
- Create: `web/` (Vite React-TS template), `web/src/telegram/initData.ts`, `web/src/telegram/theme.ts`
- Modify: `web/src/main.tsx`, `web/index.html`

**Interfaces:**
- Produces:
  - `web/` app that runs with `npm run dev`.
  - `getInitData(): string` — returns `window.Telegram.WebApp.initData` (or a dev fallback from `VITE_DEV_INIT_DATA`).
  - `applyTelegramTheme(): void` — calls `WebApp.ready()`, `WebApp.expand()`, and maps Telegram theme params to CSS variables.

- [ ] **Step 1: Scaffold the app and install deps**

```bash
cd /mnt/projects/data/kamol/dev/PersonalExpenseTracker
npm create vite@latest web -- --template react-ts
cd web
npm install
npm install @tanstack/react-query react-router-dom recharts @telegram-apps/sdk
npm install -D @types/node
```

- [ ] **Step 2: Add the Telegram SDK script to `index.html`**

In `web/index.html`, inside `<head>`:
```html
<script src="https://telegram.org/js/telegram-web-app.js"></script>
```

- [ ] **Step 3: Implement initData accessor**

`web/src/telegram/initData.ts`:
```ts
declare global {
  interface Window {
    Telegram?: { WebApp: { initData: string; ready: () => void; expand: () => void;
      themeParams: Record<string, string>;
      MainButton: { setText: (t: string) => void; show: () => void; hide: () => void;
        onClick: (cb: () => void) => void; offClick: (cb: () => void) => void;
        showProgress: () => void; hideProgress: () => void; };
      HapticFeedback: { impactOccurred: (s: string) => void };
    } };
  }
}

export function getInitData(): string {
  const fromTg = window.Telegram?.WebApp?.initData;
  if (fromTg) return fromTg;
  return import.meta.env.VITE_DEV_INIT_DATA ?? ""; // dev-only fallback
}
```

- [ ] **Step 4: Implement theme mapping**

`web/src/telegram/theme.ts`:
```ts
export function applyTelegramTheme(): void {
  const wa = window.Telegram?.WebApp;
  if (!wa) return;
  wa.ready();
  wa.expand();
  const p = wa.themeParams ?? {};
  const root = document.documentElement.style;
  root.setProperty("--bg", p.bg_color ?? "#ffffff");
  root.setProperty("--text", p.text_color ?? "#000000");
  root.setProperty("--hint", p.hint_color ?? "#888888");
  root.setProperty("--button", p.button_color ?? "#2481cc");
  root.setProperty("--button-text", p.button_text_color ?? "#ffffff");
}
```

- [ ] **Step 5: Call theme init in `main.tsx`**

`web/src/main.tsx`:
```tsx
import React from "react";
import ReactDOM from "react-dom/client";
import { applyTelegramTheme } from "./telegram/theme";
import App from "./App";
import "./index.css";

applyTelegramTheme();

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
```

- [ ] **Step 6: Verify build and commit**

Run: `cd web && npm run build`
Expected: build succeeds, `web/dist` produced.
```bash
git add -A && git commit -m "feat(web): scaffold Vite app with Telegram SDK init and theme"
```

---

### Task 12: API client, query provider, and router

**Files:**
- Create: `web/src/api/client.ts`, `web/src/api/types.ts`, `web/src/api/hooks.ts`, `web/src/router.tsx`
- Modify: `web/src/App.tsx`

**Interfaces:**
- Consumes: `getInitData()`.
- Produces:
  - `api<T>(path: string, init?: RequestInit): Promise<T>` — prefixes `/api`, attaches `Authorization: tma <initData>`, throws on non-2xx.
  - Types mirroring backend DTOs (`Category`, `Expense`, `ExpenseInput`, `Budget`, `ReportSummary`, `Settings`).
  - Hooks: `useCategories()`, `useExpenses(filters)`, `useCreateExpense()`, `useReport(range)`, `useBudgets()`, `useUpsertBudget()`, `useSettings()`, `useUpdateSettings()`, `useCreateCategory()`.
  - `<AppRouter/>` with routes for all screens.

- [ ] **Step 1: Implement the fetch client**

`web/src/api/client.ts`:
```ts
import { getInitData } from "../telegram/initData";

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await fetch(`/api${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      Authorization: `tma ${getInitData()}`,
      ...(init.headers ?? {}),
    },
  });
  if (!res.ok) throw new Error(`API ${res.status}: ${await res.text()}`);
  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}
```

- [ ] **Step 2: Define types**

`web/src/api/types.ts`:
```ts
export interface Category { id: number; name: string; emoji: string; isArchived: boolean; }
export interface Expense { id: number; amount: number; currencyCode: string; categoryId: number; spentOn: string; note: string | null; }
export interface ExpenseInput { amount: number; currencyCode: string; categoryId: number; spentOn: string; note: string | null; }
export interface Budget { id: number; categoryId: number | null; limitAmount: number; currencyCode: string; }
export interface BudgetInput { categoryId: number | null; limitAmount: number; currencyCode: string; }
export interface CategoryTotal { categoryId: number; categoryName: string; total: number; }
export interface MonthTotal { month: string; total: number; }
export interface ReportSummary { baseCurrency: string; grandTotal: number; byCategory: CategoryTotal[]; byMonth: MonthTotal[]; }
export interface Settings { baseCurrency: string; }
```

- [ ] **Step 3: Define hooks**

`web/src/api/hooks.ts`:
```ts
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type { Budget, BudgetInput, Category, Expense, ExpenseInput, ReportSummary, Settings } from "./types";

export const useCategories = () =>
  useQuery({ queryKey: ["categories"], queryFn: () => api<Category[]>("/categories") });

export const useCreateCategory = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { name: string; emoji: string }) =>
      api<Category>("/categories", { method: "POST", body: JSON.stringify(input) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["categories"] }),
  });
};

export const useExpenses = (filters: { from?: string; to?: string; categoryId?: number } = {}) => {
  const qs = new URLSearchParams();
  if (filters.from) qs.set("from", filters.from);
  if (filters.to) qs.set("to", filters.to);
  if (filters.categoryId) qs.set("categoryId", String(filters.categoryId));
  return useQuery({
    queryKey: ["expenses", filters],
    queryFn: () => api<Expense[]>(`/expenses?${qs.toString()}`),
  });
};

export const useCreateExpense = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: ExpenseInput) =>
      api<Expense>("/expenses", { method: "POST", body: JSON.stringify(input) }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["expenses"] });
      qc.invalidateQueries({ queryKey: ["report"] });
    },
  });
};

export const useReport = (range: { from: string; to: string }) =>
  useQuery({
    queryKey: ["report", range],
    queryFn: () => api<ReportSummary>(`/reports/summary?from=${range.from}&to=${range.to}`),
  });

export const useBudgets = () =>
  useQuery({ queryKey: ["budgets"], queryFn: () => api<Budget[]>("/budgets") });

export const useUpsertBudget = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: BudgetInput) =>
      api<Budget>("/budgets", { method: "PUT", body: JSON.stringify(input) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["budgets"] }),
  });
};

export const useSettings = () =>
  useQuery({ queryKey: ["settings"], queryFn: () => api<Settings>("/settings") });

export const useUpdateSettings = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: Settings) =>
      api<Settings>("/settings", { method: "PUT", body: JSON.stringify(input) }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["settings"] });
      qc.invalidateQueries({ queryKey: ["report"] });
    },
  });
};
```

- [ ] **Step 4: Define the router**

`web/src/router.tsx`:
```tsx
import { createBrowserRouter, RouterProvider, NavLink, Outlet } from "react-router-dom";
import AddExpense from "./screens/AddExpense";
import Expenses from "./screens/Expenses";
import Categories from "./screens/Categories";
import Budgets from "./screens/Budgets";
import Reports from "./screens/Reports";
import Settings from "./screens/Settings";

function Layout() {
  return (
    <div className="app">
      <main><Outlet /></main>
      <nav className="tabbar">
        <NavLink to="/">Add</NavLink>
        <NavLink to="/expenses">List</NavLink>
        <NavLink to="/reports">Reports</NavLink>
        <NavLink to="/budgets">Budgets</NavLink>
        <NavLink to="/settings">⚙</NavLink>
      </nav>
    </div>
  );
}

const router = createBrowserRouter([
  { path: "/", element: <Layout />, children: [
    { index: true, element: <AddExpense /> },
    { path: "expenses", element: <Expenses /> },
    { path: "categories", element: <Categories /> },
    { path: "budgets", element: <Budgets /> },
    { path: "reports", element: <Reports /> },
    { path: "settings", element: <Settings /> },
  ]},
]);

export const AppRouter = () => <RouterProvider router={router} />;
```

- [ ] **Step 5: Wire QueryClient in `App.tsx`**

`web/src/App.tsx`:
```tsx
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { AppRouter } from "./router";

const queryClient = new QueryClient();

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AppRouter />
    </QueryClientProvider>
  );
}
```

- [ ] **Step 6: Create placeholder screens so it compiles, then commit**

Create minimal `web/src/screens/{AddExpense,Expenses,Categories,Budgets,Reports,Settings}.tsx`, each:
```tsx
export default function Screen() { return <div>TODO</div>; }
```
(named per file; these are replaced in Tasks 13–18.)
Run: `cd web && npm run build` → succeeds.
```bash
git add -A && git commit -m "feat(web): api client, query hooks, and router scaffold"
```

---

### Task 13: Add Expense screen (the fast path) + Main Button

**Files:**
- Modify: `web/src/screens/AddExpense.tsx`

**Interfaces:**
- Consumes: `useCategories`, `useCreateExpense`, `useSettings`, Telegram `MainButton`/`HapticFeedback`.
- Produces: a form (amount, currency defaulting to base, category picker, date defaulting today, note) that creates an expense via the native Telegram Main Button.

- [ ] **Step 1: Implement the screen**

`web/src/screens/AddExpense.tsx`:
```tsx
import { useEffect, useState } from "react";
import { useCategories, useCreateExpense, useSettings } from "../api/hooks";

const today = () => new Date().toISOString().slice(0, 10);

export default function AddExpense() {
  const { data: categories } = useCategories();
  const { data: settings } = useSettings();
  const createExpense = useCreateExpense();

  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState("USD");
  const [categoryId, setCategoryId] = useState<number | null>(null);
  const [spentOn, setSpentOn] = useState(today());
  const [note, setNote] = useState("");

  useEffect(() => { if (settings) setCurrency(settings.baseCurrency); }, [settings]);
  useEffect(() => { if (categories?.length && categoryId === null) setCategoryId(categories[0].id); }, [categories, categoryId]);

  useEffect(() => {
    const wa = window.Telegram?.WebApp;
    if (!wa) return;
    const mb = wa.MainButton;
    mb.setText("Save expense");
    mb.show();
    const onClick = () => {
      const value = parseFloat(amount);
      if (!value || value <= 0 || categoryId === null) return;
      mb.showProgress();
      createExpense.mutate(
        { amount: value, currencyCode: currency, categoryId, spentOn, note: note || null },
        {
          onSuccess: () => { wa.HapticFeedback.impactOccurred("medium"); setAmount(""); setNote(""); mb.hideProgress(); },
          onError: () => mb.hideProgress(),
        }
      );
    };
    mb.onClick(onClick);
    return () => { mb.offClick(onClick); mb.hide(); };
  }, [amount, currency, categoryId, spentOn, note, createExpense]);

  return (
    <div className="screen">
      <h2>Add expense</h2>
      <input inputMode="decimal" placeholder="0.00" value={amount}
        onChange={(e) => setAmount(e.target.value)} />
      <input value={currency} onChange={(e) => setCurrency(e.target.value.toUpperCase())} maxLength={3} />
      <select value={categoryId ?? ""} onChange={(e) => setCategoryId(Number(e.target.value))}>
        {categories?.map((c) => <option key={c.id} value={c.id}>{c.emoji} {c.name}</option>)}
      </select>
      <input type="date" value={spentOn} onChange={(e) => setSpentOn(e.target.value)} />
      <input placeholder="Note (optional)" value={note} onChange={(e) => setNote(e.target.value)} />
    </div>
  );
}
```

- [ ] **Step 2: Build to verify and commit**

Run: `cd web && npm run build` → succeeds.
```bash
git add -A && git commit -m "feat(web): add-expense screen with Telegram Main Button"
```

---

### Task 14: Expenses list & delete

**Files:**
- Modify: `web/src/screens/Expenses.tsx`
- Modify: `web/src/api/hooks.ts` (add `useDeleteExpense`)

**Interfaces:**
- Consumes: `useExpenses`, `useCategories`.
- Produces: `useDeleteExpense()` (DELETE `/expenses/{id}`, invalidates `expenses` + `report`); a list grouped newest-first with delete buttons.

- [ ] **Step 1: Add the delete hook**

Append to `web/src/api/hooks.ts`:
```ts
export const useDeleteExpense = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api<void>(`/expenses/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["expenses"] });
      qc.invalidateQueries({ queryKey: ["report"] });
    },
  });
};
```

- [ ] **Step 2: Implement the screen**

`web/src/screens/Expenses.tsx`:
```tsx
import { useCategories, useDeleteExpense, useExpenses } from "../api/hooks";

export default function Expenses() {
  const { data: expenses } = useExpenses();
  const { data: categories } = useCategories();
  const del = useDeleteExpense();
  const nameFor = (id: number) => categories?.find((c) => c.id === id)?.name ?? "—";

  return (
    <div className="screen">
      <h2>Expenses</h2>
      {expenses?.length === 0 && <p>No expenses yet.</p>}
      <ul className="list">
        {expenses?.map((e) => (
          <li key={e.id}>
            <span>{e.spentOn}</span>
            <span>{nameFor(e.categoryId)}</span>
            <span>{e.amount.toFixed(2)} {e.currencyCode}</span>
            <span>{e.note}</span>
            <button onClick={() => del.mutate(e.id)}>✕</button>
          </li>
        ))}
      </ul>
    </div>
  );
}
```

- [ ] **Step 3: Build and commit**

Run: `cd web && npm run build` → succeeds.
```bash
git add -A && git commit -m "feat(web): expenses list with delete"
```

---

### Task 15: Categories management screen

**Files:**
- Modify: `web/src/screens/Categories.tsx`

**Interfaces:**
- Consumes: `useCategories`, `useCreateCategory`.
- Produces: list of categories + a small add form (name, emoji).

- [ ] **Step 1: Implement the screen**

`web/src/screens/Categories.tsx`:
```tsx
import { useState } from "react";
import { useCategories, useCreateCategory } from "../api/hooks";

export default function Categories() {
  const { data: categories } = useCategories();
  const create = useCreateCategory();
  const [name, setName] = useState("");
  const [emoji, setEmoji] = useState("🏷️");

  const submit = () => {
    if (!name.trim()) return;
    create.mutate({ name: name.trim(), emoji }, { onSuccess: () => setName("") });
  };

  return (
    <div className="screen">
      <h2>Categories</h2>
      <ul className="list">
        {categories?.map((c) => <li key={c.id}>{c.emoji} {c.name}</li>)}
      </ul>
      <div className="row">
        <input value={emoji} onChange={(e) => setEmoji(e.target.value)} style={{ width: 48 }} />
        <input placeholder="New category" value={name} onChange={(e) => setName(e.target.value)} />
        <button onClick={submit}>Add</button>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Build and commit**

Run: `cd web && npm run build` → succeeds.
```bash
git add -A && git commit -m "feat(web): categories management screen"
```

---

### Task 16: Budgets screen (with spend-vs-limit from report)

**Files:**
- Modify: `web/src/screens/Budgets.tsx`

**Interfaces:**
- Consumes: `useBudgets`, `useUpsertBudget`, `useCategories`, `useReport` (current month), `useSettings`.
- Produces: per-category budget rows showing limit and current-month spend in base currency, plus an upsert form.

- [ ] **Step 1: Implement the screen**

`web/src/screens/Budgets.tsx`:
```tsx
import { useState } from "react";
import { useBudgets, useCategories, useReport, useSettings, useUpsertBudget } from "../api/hooks";

const monthRange = () => {
  const now = new Date();
  const from = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10);
  const to = new Date(now.getFullYear(), now.getMonth() + 1, 0).toISOString().slice(0, 10);
  return { from, to };
};

export default function Budgets() {
  const { data: budgets } = useBudgets();
  const { data: categories } = useCategories();
  const { data: settings } = useSettings();
  const { data: report } = useReport(monthRange());
  const upsert = useUpsertBudget();

  const [categoryId, setCategoryId] = useState<string>("");
  const [limit, setLimit] = useState("");

  const spentFor = (catId: number | null) =>
    catId === null
      ? report?.grandTotal ?? 0
      : report?.byCategory.find((c) => c.categoryId === catId)?.total ?? 0;

  const submit = () => {
    const value = parseFloat(limit);
    if (!value || value <= 0) return;
    upsert.mutate(
      { categoryId: categoryId === "" ? null : Number(categoryId),
        limitAmount: value, currencyCode: settings?.baseCurrency ?? "USD" },
      { onSuccess: () => setLimit("") }
    );
  };

  return (
    <div className="screen">
      <h2>Budgets ({settings?.baseCurrency})</h2>
      <ul className="list">
        {budgets?.map((b) => {
          const spent = spentFor(b.categoryId);
          const label = b.categoryId === null
            ? "Overall"
            : categories?.find((c) => c.id === b.categoryId)?.name ?? "—";
          const over = spent > b.limitAmount;
          return (
            <li key={b.id} style={{ color: over ? "crimson" : undefined }}>
              {label}: {spent.toFixed(2)} / {b.limitAmount.toFixed(2)}
            </li>
          );
        })}
      </ul>
      <div className="row">
        <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
          <option value="">Overall</option>
          {categories?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <input inputMode="decimal" placeholder="Limit" value={limit}
          onChange={(e) => setLimit(e.target.value)} />
        <button onClick={submit}>Save</button>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Build and commit**

Run: `cd web && npm run build` → succeeds.
```bash
git add -A && git commit -m "feat(web): budgets screen with spend vs limit"
```

---

### Task 17: Reports screen with charts

**Files:**
- Modify: `web/src/screens/Reports.tsx`

**Interfaces:**
- Consumes: `useReport` (current month), Recharts.
- Produces: a pie chart of spend by category and a bar chart of spend by month, plus the grand total.

- [ ] **Step 1: Implement the screen**

`web/src/screens/Reports.tsx`:
```tsx
import { Bar, BarChart, Cell, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useReport } from "../api/hooks";

const monthRange = () => {
  const now = new Date();
  const from = new Date(now.getFullYear(), now.getMonth() - 5, 1).toISOString().slice(0, 10);
  const to = new Date(now.getFullYear(), now.getMonth() + 1, 0).toISOString().slice(0, 10);
  return { from, to };
};

const COLORS = ["#2481cc", "#e74c3c", "#2ecc71", "#f39c12", "#9b59b6", "#1abc9c", "#e67e22", "#34495e"];

export default function Reports() {
  const { data: report } = useReport(monthRange());
  if (!report) return <div className="screen">Loading…</div>;

  return (
    <div className="screen">
      <h2>Reports</h2>
      <p>Total: {report.grandTotal.toFixed(2)} {report.baseCurrency}</p>

      <h3>By category</h3>
      <ResponsiveContainer width="100%" height={240}>
        <PieChart>
          <Pie data={report.byCategory} dataKey="total" nameKey="categoryName" outerRadius={90} label>
            {report.byCategory.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
          </Pie>
          <Tooltip />
        </PieChart>
      </ResponsiveContainer>

      <h3>By month</h3>
      <ResponsiveContainer width="100%" height={240}>
        <BarChart data={report.byMonth}>
          <XAxis dataKey="month" />
          <YAxis />
          <Tooltip />
          <Bar dataKey="total" fill="#2481cc" />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
```

- [ ] **Step 2: Build and commit**

Run: `cd web && npm run build` → succeeds.
```bash
git add -A && git commit -m "feat(web): reports screen with pie and bar charts"
```

---

### Task 18: Settings screen

**Files:**
- Modify: `web/src/screens/Settings.tsx`

**Interfaces:**
- Consumes: `useSettings`, `useUpdateSettings`.
- Produces: base-currency editor.

- [ ] **Step 1: Implement the screen**

`web/src/screens/Settings.tsx`:
```tsx
import { useEffect, useState } from "react";
import { useSettings, useUpdateSettings } from "../api/hooks";

export default function Settings() {
  const { data: settings } = useSettings();
  const update = useUpdateSettings();
  const [base, setBase] = useState("USD");

  useEffect(() => { if (settings) setBase(settings.baseCurrency); }, [settings]);

  return (
    <div className="screen">
      <h2>Settings</h2>
      <label>Base currency</label>
      <input value={base} maxLength={3} onChange={(e) => setBase(e.target.value.toUpperCase())} />
      <button onClick={() => update.mutate({ baseCurrency: base })}>Save</button>
      <p className="hint">Reports convert all expenses to this currency.</p>
    </div>
  );
}
```

- [ ] **Step 2: Build and commit**

Run: `cd web && npm run build` → succeeds.
```bash
git add -A && git commit -m "feat(web): settings screen for base currency"
```

---

# Phase 3 — Integration & deployment

### Task 19: Serve the React build from the backend (single origin)

**Files:**
- Modify: `server/ExpenseTracker.Api/Program.cs` (static files + SPA fallback)
- Modify: `server/ExpenseTracker.Api/ExpenseTracker.Api.csproj` (optional: copy note)

**Interfaces:**
- Produces: backend serving `wwwroot/index.html` for non-API routes so the SPA's client-side routing works.

- [ ] **Step 1: Add static-file serving and SPA fallback in `Program.cs`**

After `app.UseAuthorization();` add:
```csharp
app.UseDefaultFiles();
app.UseStaticFiles();
```
After all `api.Map...()` calls and before `app.Run();` add:
```csharp
app.MapFallbackToFile("index.html");
```

- [ ] **Step 2: Produce a build into wwwroot and smoke-test locally**

```bash
cd web && npm run build
rm -rf ../server/ExpenseTracker.Api/wwwroot && cp -r dist ../server/ExpenseTracker.Api/wwwroot
cd ../server/ExpenseTracker.Api && dotnet run &
sleep 5 && curl -s localhost:5000/healthz && curl -s -o /dev/null -w "%{http_code}\n" localhost:5000/
```
Expected: `healthz` → `ok`; `/` → `200` (serves index.html). Stop the server afterward. (Requires a local Postgres reachable via the connection string, or set `ConnectionStrings__Default` to a running instance.)

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: serve SPA from backend wwwroot with fallback"
```

---

### Task 20: Multi-stage Dockerfile

**Files:**
- Create: `Dockerfile`, `.dockerignore`

**Interfaces:**
- Produces: a single image that builds the React app, publishes the API, places the build in `wwwroot`, and runs the API.

- [ ] **Step 1: Write `.dockerignore`**

```
**/bin
**/obj
**/node_modules
web/dist
server/ExpenseTracker.Api/wwwroot
```

- [ ] **Step 2: Write the `Dockerfile`**

```dockerfile
# --- Frontend build ---
FROM node:20-alpine AS web
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# --- Backend build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY server/ ./server/
COPY ExpenseTracker.sln ./
RUN dotnet restore server/ExpenseTracker.Api/ExpenseTracker.Api.csproj
RUN dotnet publish server/ExpenseTracker.Api/ExpenseTracker.Api.csproj -c Release -o /app
COPY --from=web /web/dist /app/wwwroot

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ExpenseTracker.Api.dll"]
```

- [ ] **Step 3: Build the image**

Run: `docker build -t expense-tracker .`
Expected: image builds successfully.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "build: multi-stage Dockerfile bundling web + api"
```

---

### Task 21: Bot launcher + deployment runbook

**Files:**
- Create: `docs/DEPLOYMENT.md`

**Interfaces:**
- Produces: documented steps to deploy on a PaaS and register the Mini App with BotFather. No new app code — the bot is configured entirely through BotFather (menu button → Web App URL), so a separate bot process is not required for v1.

- [ ] **Step 1: Write `docs/DEPLOYMENT.md`**

````markdown
# Deployment

## 1. Create the bot
1. Talk to @BotFather → `/newbot` → save the token.
2. `/setdomain` (or via Bot Settings) is not needed; instead use the Web App menu button below.

## 2. Deploy the container (example: Railway)
1. Create a project, add a **PostgreSQL** plugin.
2. Add a service from this repo (Railway detects the `Dockerfile`).
3. Set env vars on the service:
   - `BotToken` = the BotFather token
   - `ConnectionStrings__Default` = the Postgres connection string
     (Npgsql format: `Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true`)
4. Deploy. Confirm `https://<your-app>/healthz` returns `ok`.
   Migrations run automatically on startup.

## 3. Register the Mini App
1. @BotFather → your bot → **Bot Settings → Menu Button → Edit → Web App URL** = `https://<your-app>/`.
2. (Optional) @BotFather → **/newapp** to attach a richer Mini App entry.
3. Open the bot, tap the menu button — the Mini App loads and auto-provisions your account.

## Local development
- Backend: `dotnet run` in `server/ExpenseTracker.Api` (needs a local Postgres + `ConnectionStrings__Default`).
- Frontend: `cd web && npm run dev`. Set `VITE_DEV_INIT_DATA` to a signed initData string
  (generate one with the test `InitDataBuilder`) to exercise auth outside Telegram, and proxy `/api`
  to the backend via Vite `server.proxy` if running them separately.
````

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "docs: deployment runbook and BotFather setup"
```

---

## Self-Review

**Spec coverage check (spec §→task):**
- §3 stack → Tasks 1, 11, 12 (frameworks, libraries) ✓
- §4 architecture / single container → Tasks 19, 20 ✓
- §5 auth (initData HMAC, provisioning, isolation) → Tasks 3, 4; isolation tested in 5 ✓
- §6 data model (all 6 entities, indexes, decimal) → Task 2 ✓
- §7 API surface (expenses, categories, budgets, reports, settings) → Tasks 5, 6, 8, 9 ✓
- §8 multi-currency (base currency, original currency kept, lazy fetch + cache, historical rate by date) → Tasks 7 (service/converter), 8 (per-date conversion in reports) ✓
- §9 frontend (screens, Telegram SDK, theme, charts) → Tasks 11–18 ✓
- §10 deployment (Dockerfile, PaaS env vars, BotFather, migrations on startup) → Tasks 10, 20, 21 ✓
- §11 testing (HMAC valid/tampered/expired/missing, isolation, conversion math, API integration) → Tasks 3, 5, 7, 8 ✓

**Placeholder scan:** The only `TODO` strings are the intentional throwaway screen stubs in Task 12 Step 6, each explicitly replaced in Tasks 13–18. No "add error handling"/"write tests"-style gaps; all code steps contain full code.

**Type consistency:** Backend DTO names/types match their frontend `types.ts` mirror (camelCase JSON: `categoryId`, `spentOn`, `limitAmount`, `baseCurrency`, `byCategory`, `byMonth`, `grandTotal`). `MapXEndpoints` extension names are consistent between definition and `Program.cs` mapping. `ICurrentUser.GetOrCreateAsync()` used identically across Tasks 4–9.
