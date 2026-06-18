# UZS + CBU Rates & Gold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the tracker UZS-centric and add a daily, historically-queryable CBU exchange-rate & gold dashboard, with a multi-source schema (CBU only now) and a daily morning scheduler — all within the existing Clean Architecture layers.

**Architecture:** A multi-source `CurrencyRate` store + `GoldPrice` store (Domain). CBU is an `IRateSource`/`IGoldSource` adapter (Infrastructure). `RatesService`/`GoldService` (Application) fetch-and-cache per day and serve the dashboard; `ExchangeRateService` (conversion, same contract) now computes UZS-based cross-rates from the CBU store, replacing Frankfurter. A `DailyRateFetchService` (BackgroundService) refreshes daily at a Tashkent morning hour. Thin Api endpoints + a React Rates screen.

**Tech Stack:** .NET 8, EF Core/Npgsql, `HtmlAgilityPack` (gold scrape), xUnit + FluentAssertions + Testcontainers, React + TS + TanStack Query.

## Global Constraints

- Target `net8.0`; preserve the Clean Architecture dependency rule — Domain depends on nothing; Application only on Domain + EF Core abstractions (no Npgsql/ASP.NET/HttpClient/HtmlAgilityPack); Infrastructure holds all adapters; Api is the composition root.
- Behavior-preservation safety net: after each task `dotnet build ExpenseTracker.sln` is clean (0 warnings) and `dotnet test` is green. The web tasks additionally require `cd web && npm run build` clean (strict TS).
- **No live CBU/network calls in tests.** Integration tests register stub `IRateSource`/`IGoldSource`; unit tests parse fixture JSON/HTML strings. CBU is hit live only by the scheduler at runtime.
- Currency defaults are **UZS** (`Setting.BaseCurrency`, `Expense.CurrencyCode`, `Budget.CurrencyCode`, seeded setting).
- CBU currency endpoint: `https://cbu.uz/uz/arkhiv-kursov-valyut/json/all/<yyyy-MM-dd>/` (array; fields `Ccy`, `Rate` string, `Nominal` string, `Date`). Rate normalized to per-1-unit = `Rate / Nominal`.
- CBU gold page: `https://cbu.uz/uz/banknotes-coins/gold-bars/prices/` (static HTML table: weight, selling price, buy-back price; numbers have thousands separators).
- Conversion semantics unchanged: `ExchangeRateService.GetRateAsync(from, to, date)` returns **`to` per 1 `from`** (so `amount_from * rate = amount_to`); `ReportService` is untouched.
- Daily fetch time config `Rates:DailyFetchHourTashkent` (default `9`), timezone `Asia/Tashkent`.
- Money is `decimal`; rates stored `decimal(18,6)`, gold/amounts `decimal(18,2)` as appropriate.

---

## File Structure (new/changed)

```
Domain/Entities/CurrencyRate.cs          (new)   GoldPrice.cs (new)   ExchangeRate.cs (DELETE)
Application/
  Common/Interfaces/IRateSource.cs (new) IGoldSource.cs (new) IClock.cs (new)   IExchangeRateProvider.cs (DELETE)
  Common/Interfaces/IApplicationDbContext.cs (modify: +CurrencyRates,+GoldPrices,-ExchangeRates)
  Rates/RatesService.cs (new)  Rates/RateDtos.cs (new)
  Gold/GoldService.cs (new)    Gold/GoldDtos.cs (new)
  ExchangeRates/ExchangeRateService.cs (rewrite)
  DependencyInjection.cs (modify: +RatesService,+GoldService)
Infrastructure/
  ExchangeRates/CbuRateProvider.cs (new : IRateSource)   FrankfurterRateProvider.cs (DELETE)
  Gold/CbuGoldScraper.cs (new : IGoldSource)
  Time/TashkentClock.cs (new : IClock)
  Scheduling/DailyRateFetchService.cs (new : BackgroundService)
  Persistence/ApplicationDbContext.cs (modify) + Migration AddRatesAndGold
  DependencyInjection.cs (modify)
Api/Endpoints/RatesEndpoints.cs (new)  GoldEndpoints.cs (new)   Program.cs (map them)
Tests/
  TestData/StubRateSource.cs (new) StubGoldSource.cs (new) CbuJsonFixtures.cs / gold-fixture.html
  Unit/CbuRateProviderTests.cs (new) ConversionTests.cs (new) CbuGoldScraperTests.cs (new) DailyFetchTests.cs (new)
  Integration/RatesApiTests.cs (new) GoldApiTests.cs (new) + ApiFactory.cs (modify: stubs, drop hosted service)
web/src/
  api/types.ts (modify) api/hooks.ts (+useRates,+useGold)
  screens/Rates.tsx (new)  router.tsx (+Rates tab)
```

---

### Task 1: FX core — rate store, CBU source, conversion swap (replaces Frankfurter)

**Files:** Create `Domain/Entities/{CurrencyRate,GoldPrice}.cs`; delete `Domain/Entities/ExchangeRate.cs`. Create Application `Common/Interfaces/{IRateSource,IGoldSource,IClock}.cs`, `Rates/RatesService.cs`, `Rates/RateDtos.cs`; delete `Common/Interfaces/IExchangeRateProvider.cs`; rewrite `ExchangeRates/ExchangeRateService.cs`; modify `Common/Interfaces/IApplicationDbContext.cs` + `DependencyInjection.cs`. Create Infrastructure `ExchangeRates/CbuRateProvider.cs`, `Time/TashkentClock.cs`; delete `ExchangeRates/FrankfurterRateProvider.cs`; modify `Persistence/ApplicationDbContext.cs` + add migration; modify `DependencyInjection.cs`. Create test doubles + unit tests; modify `Tests/Integration/ApiFactory.cs`.

**Interfaces (Produces):**
- `record SourceRate(string CurrencyCode, decimal Rate, decimal? Buy, decimal? Sell)`; `interface IRateSource { string SourceCode { get; } Task<IReadOnlyList<SourceRate>> FetchAsync(DateOnly date, CancellationToken ct = default); }`
- `record SourceGold(string Item, decimal? SellPrice, decimal? BuyBackPrice)`; `interface IGoldSource { Task<IReadOnlyList<SourceGold>> FetchAsync(DateOnly date, CancellationToken ct = default); }`
- `interface IClock { DateTimeOffset Now { get; } DateOnly TodayInTashkent(); }`
- `RatesService` with `EnsureSourceForDateAsync(string sourceCode, DateOnly date, CancellationToken ct=default)`, `EnsureAllSourcesForDateAsync(DateOnly date, CancellationToken ct=default)`, `Task<decimal?> GetCbuRateAsync(string currency, DateOnly date, CancellationToken ct=default)`.
- `ExchangeRateService.GetRateAsync(string from, string to, DateOnly date)` (unchanged signature; `ReportService` consumes it).

- [ ] **Step 1: Domain entities + delete ExchangeRate**

`Domain/Entities/CurrencyRate.cs`:
```csharp
namespace ExpenseTracker.Domain.Entities;

public class CurrencyRate
{
    public int Id { get; set; }
    public string Source { get; set; } = "";       // "CBU" (later "IPAKYULI"|"ASAKA")
    public string CurrencyCode { get; set; } = "";  // e.g. "USD","RUB"
    public DateOnly AsOfDate { get; set; }
    public decimal Rate { get; set; }               // official UZS per 1 unit
    public decimal? Buy { get; set; }               // null for CBU
    public decimal? Sell { get; set; }              // null for CBU
}
```
`Domain/Entities/GoldPrice.cs`:
```csharp
namespace ExpenseTracker.Domain.Entities;

public class GoldPrice
{
    public int Id { get; set; }
    public DateOnly AsOfDate { get; set; }
    public string Item { get; set; } = "";          // e.g. "5g","50g","100g"
    public decimal? SellPrice { get; set; }          // CBU selling price (UZS)
    public decimal? BuyBackPrice { get; set; }       // CBU buy-back price (UZS)
}
```
Delete `Domain/Entities/ExchangeRate.cs`.

- [ ] **Step 2: Application ports + DbContext interface**

Create `Common/Interfaces/IRateSource.cs`, `IGoldSource.cs`, `IClock.cs` with the record + interface definitions from the Interfaces block above (namespace `ExpenseTracker.Application.Common.Interfaces`). Delete `Common/Interfaces/IExchangeRateProvider.cs`.
Modify `Common/Interfaces/IApplicationDbContext.cs`: remove `DbSet<ExchangeRate> ExchangeRates`, add:
```csharp
    DbSet<CurrencyRate> CurrencyRates { get; }
    DbSet<GoldPrice> GoldPrices { get; }
```

- [ ] **Step 3: RatesService (write the failing unit test first)**

`Tests/TestData/StubRateSource.cs`:
```csharp
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Tests.TestData;

public class StubRateSource : IRateSource
{
    public string SourceCode => "CBU";
    // deterministic test rates (UZS per 1 unit)
    public Task<IReadOnlyList<SourceRate>> FetchAsync(DateOnly date, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SourceRate>>(new List<SourceRate>
        {
            new("USD", 12000m, null, null),
            new("RUB", 160m, null, null),
            new("EUR", 13000m, null, null),
        });
}
```
`Tests/Unit/ConversionTests.cs` (uses an in-memory `IApplicationDbContext` fake is heavy; instead test `ExchangeRateService` against a `RatesService` backed by a tiny in-memory `IApplicationDbContext`. To avoid an EF in-memory dependency, inject a hand-written fake `IApplicationDbContext` exposing `DbSet`s. Simpler: test via the real Sqlite/in-memory is overkill — use the integration path in Task 2 for end-to-end, and unit-test the pure cross-rate math here by stubbing `RatesService` is not possible since it's concrete). **Chosen approach:** make `ExchangeRateService` depend on an interface `ICbuRates { Task<decimal?> GetCbuRateAsync(string ccy, DateOnly date, CancellationToken ct=default); }` that `RatesService` implements, so conversion is unit-testable with a trivial stub:
```csharp
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.ExchangeRates;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class ConversionTests
{
    class FakeCbuRates : ICbuRates
    {
        public Task<decimal?> GetCbuRateAsync(string ccy, DateOnly date, CancellationToken ct = default)
            => Task.FromResult<decimal?>(ccy.ToUpperInvariant() switch
            { "USD" => 12000m, "RUB" => 160m, "EUR" => 13000m, _ => (decimal?)null });
    }
    readonly ExchangeRateService _sut = new(new FakeCbuRates());
    readonly DateOnly _d = new(2026, 6, 18);

    [Fact] public async Task Identity_is_one() => (await _sut.GetRateAsync("USD","USD",_d)).Should().Be(1m);
    [Fact] public async Task X_to_UZS_is_cbu_rate() => (await _sut.GetRateAsync("USD","UZS",_d)).Should().Be(12000m);
    [Fact] public async Task UZS_to_X_is_reciprocal() => (await _sut.GetRateAsync("UZS","USD",_d)).Should().Be(1m/12000m);
    [Fact] public async Task X_to_Y_cross_via_uzs() => (await _sut.GetRateAsync("USD","RUB",_d)).Should().Be(12000m/160m);
    [Fact] public async Task Missing_currency_throws() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GetRateAsync("GBP","UZS",_d));
}
```

- [ ] **Step 4: Run the conversion test → FAIL** (`ICbuRates`, `ExchangeRateService` ctor not defined yet).
Run: `dotnet test --filter ConversionTests` → FAIL (compile).

- [ ] **Step 5: Implement `ICbuRates`, `RatesService`, and rewrite `ExchangeRateService`**

`Common/Interfaces/ICbuRates.cs`:
```csharp
namespace ExpenseTracker.Application.Common.Interfaces;

public interface ICbuRates
{
    Task<decimal?> GetCbuRateAsync(string currency, DateOnly date, CancellationToken ct = default);
}
```
`Rates/RatesService.cs`:
```csharp
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Rates;

public class RatesService(IApplicationDbContext db, IEnumerable<IRateSource> sources) : ICbuRates
{
    public async Task EnsureSourceForDateAsync(string sourceCode, DateOnly date, CancellationToken ct = default)
    {
        sourceCode = sourceCode.ToUpperInvariant();
        if (await db.CurrencyRates.AnyAsync(r => r.Source == sourceCode && r.AsOfDate == date, ct)) return;
        var source = sources.FirstOrDefault(s => s.SourceCode.ToUpperInvariant() == sourceCode);
        if (source is null) return;
        foreach (var f in await source.FetchAsync(date, ct))
            db.CurrencyRates.Add(new CurrencyRate
            {
                Source = sourceCode, CurrencyCode = f.CurrencyCode.ToUpperInvariant(),
                AsOfDate = date, Rate = f.Rate, Buy = f.Buy, Sell = f.Sell
            });
        await db.SaveChangesAsync(ct);
    }

    public async Task EnsureAllSourcesForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        foreach (var s in sources) await EnsureSourceForDateAsync(s.SourceCode, date, ct);
    }

    public async Task<decimal?> GetCbuRateAsync(string currency, DateOnly date, CancellationToken ct = default)
    {
        currency = currency.ToUpperInvariant();
        if (currency == "UZS") return 1m;
        await EnsureSourceForDateAsync("CBU", date, ct);
        var row = await db.CurrencyRates.FirstOrDefaultAsync(
            r => r.Source == "CBU" && r.CurrencyCode == currency && r.AsOfDate == date, ct);
        return row?.Rate;
    }
}
```
`ExchangeRates/ExchangeRateService.cs` (rewrite):
```csharp
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Application.ExchangeRates;

// Returns "to per 1 from" so amount_from * rate = amount_to (ReportService contract unchanged).
public class ExchangeRateService(ICbuRates rates)
{
    public async Task<decimal> GetRateAsync(string from, string to, DateOnly date)
    {
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();
        if (from == to) return 1m;
        var fromUzs = await UzsPerUnit(from, date);
        var toUzs = await UzsPerUnit(to, date);
        return fromUzs / toUzs;
    }

    async Task<decimal> UzsPerUnit(string ccy, DateOnly date)
    {
        if (ccy == "UZS") return 1m;
        return await rates.GetCbuRateAsync(ccy, date)
            ?? throw new InvalidOperationException($"No CBU rate for {ccy} on {date:yyyy-MM-dd}");
    }
}
```

- [ ] **Step 6: Run the conversion test → PASS** (`dotnet test --filter ConversionTests` → 5 passing).

- [ ] **Step 7: CBU provider (write fixture parse test first)**

`Tests/Unit/CbuRateProviderTests.cs` — test the pure parse helper. Refactor `CbuRateProvider` to expose `static IReadOnlyList<SourceRate> Parse(string json)`:
```csharp
using ExpenseTracker.Infrastructure.ExchangeRates;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class CbuRateProviderTests
{
    const string Json = """
    [{"Ccy":"USD","Rate":"12052.05","Nominal":"1","Date":"18.06.2026"},
     {"Ccy":"RUB","Rate":"165.30","Nominal":"1","Date":"18.06.2026"},
     {"Ccy":"JPY","Rate":"8100.00","Nominal":"100","Date":"18.06.2026"}]
    """;

    [Fact]
    public void Parses_and_normalizes_by_nominal()
    {
        var rates = CbuRateProvider.Parse(Json);
        rates.Should().Contain(r => r.CurrencyCode == "USD" && r.Rate == 12052.05m);
        rates.Should().Contain(r => r.CurrencyCode == "JPY" && r.Rate == 81.00m); // 8100/100
        rates.Should().OnlyContain(r => r.Buy == null && r.Sell == null);
    }
}
```

- [ ] **Step 8: Run → FAIL, then implement `CbuRateProvider`**

Run: `dotnet test --filter CbuRateProviderTests` → FAIL.
`Infrastructure/ExchangeRates/CbuRateProvider.cs`:
```csharp
using System.Globalization;
using System.Text.Json;
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Infrastructure.ExchangeRates;

public class CbuRateProvider(HttpClient http) : IRateSource
{
    public string SourceCode => "CBU";

    public async Task<IReadOnlyList<SourceRate>> FetchAsync(DateOnly date, CancellationToken ct = default)
    {
        var url = $"uz/arkhiv-kursov-valyut/json/all/{date:yyyy-MM-dd}/";
        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return Parse(json);
    }

    public static IReadOnlyList<SourceRate> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<SourceRate>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var ccy = el.GetProperty("Ccy").GetString();
            if (string.IsNullOrEmpty(ccy)) continue;
            var rate = decimal.Parse(el.GetProperty("Rate").GetString()!, CultureInfo.InvariantCulture);
            var nominal = el.TryGetProperty("Nominal", out var n) && decimal.TryParse(
                n.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var nm) && nm != 0 ? nm : 1m;
            list.Add(new SourceRate(ccy.ToUpperInvariant(), rate / nominal, null, null));
        }
        return list;
    }
}
```

- [ ] **Step 9: Run → PASS** (`dotnet test --filter CbuRateProviderTests`).

- [ ] **Step 10: Infrastructure wiring — DbContext, migration, DI, clock; remove Frankfurter**

`Time/TashkentClock.cs`:
```csharp
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Infrastructure.Time;

public class TashkentClock : IClock
{
    static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
    public DateOnly TodayInTashkent() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Tz).DateTime);
}
```
`Persistence/ApplicationDbContext.cs`: replace the `DbSet<ExchangeRate> ExchangeRates` with `DbSet<CurrencyRate> CurrencyRates` and `DbSet<GoldPrice> GoldPrices`; in `OnModelCreating` remove the `ExchangeRate` config and add:
```csharp
        b.Entity<CurrencyRate>().HasIndex(r => new { r.Source, r.CurrencyCode, r.AsOfDate }).IsUnique();
        b.Entity<CurrencyRate>().HasIndex(r => r.AsOfDate);
        b.Entity<CurrencyRate>().Property(r => r.Rate).HasColumnType("decimal(18,6)");
        b.Entity<CurrencyRate>().Property(r => r.Buy).HasColumnType("decimal(18,6)");
        b.Entity<CurrencyRate>().Property(r => r.Sell).HasColumnType("decimal(18,6)");
        b.Entity<GoldPrice>().HasIndex(g => new { g.AsOfDate, g.Item }).IsUnique();
        b.Entity<GoldPrice>().Property(g => g.SellPrice).HasColumnType("decimal(18,2)");
        b.Entity<GoldPrice>().Property(g => g.BuyBackPrice).HasColumnType("decimal(18,2)");
```
Delete `Infrastructure/ExchangeRates/FrankfurterRateProvider.cs`.
`Application/DependencyInjection.cs`: register `services.AddScoped<RatesService>();` and `services.AddScoped<ICbuRates>(sp => sp.GetRequiredService<RatesService>());`; keep `ExchangeRateService` registration.
`Infrastructure/DependencyInjection.cs`: remove the Frankfurter `AddHttpClient<IExchangeRateProvider,...>`; add:
```csharp
        services.AddSingleton<IClock, TashkentClock>();
        services.AddHttpClient<IRateSource, CbuRateProvider>(c => c.BaseAddress = new Uri("https://cbu.uz/"));
```
Generate the migration:
```
DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 ConnectionStrings__Default="Host=localhost;Database=x;Username=x;Password=x" \
  dotnet ef migrations add AddRatesAndGold -p server/ExpenseTracker.Infrastructure -s server/ExpenseTracker.Api -o Persistence/Migrations
```
Confirm it drops `ExchangeRates` and creates `CurrencyRates` + `GoldPrices` with the indexes.

- [ ] **Step 11: Make integration tests hermetic — stub the source in ApiFactory**

In `Tests/Integration/ApiFactory.cs` `ConfigureWebHost`, after the existing settings, replace the real CBU source with the stub so no test hits the network:
```csharp
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRateSource>();
            services.AddSingleton<IRateSource, ExpenseTracker.Tests.TestData.StubRateSource>();
        });
```
(Add `using Microsoft.Extensions.DependencyInjection.Extensions;` and `using ExpenseTracker.Application.Common.Interfaces;`.)

- [ ] **Step 12: Full build + suite**

Run: `dotnet build ExpenseTracker.sln` → 0 warnings. `dotnet test` → all green (existing 26 + new unit tests; existing ReportsApiTests still uses USD base + USD expense → identity, unaffected).

- [ ] **Step 13: Commit**
```bash
git add -A && git commit -m "feat: CBU multi-source rate store and conversion, replacing Frankfurter"
```

---

### Task 2: UZS defaults

**Files:** Modify `Domain/Entities/{Setting,Expense,Budget}.cs`; `Application/Users/UserProvisioningService.cs`; tests `Integration/BudgetsAndSettingsApiTests.cs` and `Integration/ReportsApiTests.cs`.

**Interfaces:** Consumes Task 1's `StubRateSource` (USD=12000 UZS) for report conversion in tests.

- [ ] **Step 1: Change defaults to UZS**
- `Setting.cs`: `public string BaseCurrency { get; set; } = "UZS";`
- `Expense.cs`: `public string CurrencyCode { get; set; } = "UZS";`
- `Budget.cs`: `public string CurrencyCode { get; set; } = "UZS";`
- `UserProvisioningService.cs`: change the seeded setting to `new Setting { UserId = user.Id, BaseCurrency = "UZS" }`.

- [ ] **Step 2: Update the settings-default test**
In `BudgetsAndSettingsApiTests.cs`, the test asserting the default base is `"USD"` → change the expectation to `"UZS"`:
```csharp
        initial!.BaseCurrency.Should().Be("UZS");
```

- [ ] **Step 3: Update ReportsApiTests to exercise UZS conversion**
The provisioned user's base is now UZS; a USD expense converts via the stub (USD=12000 UZS). Replace the report assertions so the seeded `10 USD + 15 USD = 25 USD` becomes `300000 UZS`:
```csharp
        summary!.BaseCurrency.Should().Be("UZS");
        summary.GrandTotal.Should().Be(300000m);
        summary.ByCategory.Should().ContainSingle(c => c.CategoryId == catId && c.Total == 300000m);
        summary.ByMonth.Should().Contain(m => m.Month == "2026-06" && m.Total == 300000m);
```
(The expenses are created in `"USD"`; the stub provides USD=12000. No live CBU.)

- [ ] **Step 4: Build + suite**
Run: `dotnet build ExpenseTracker.sln` (0 warnings); `dotnet test` → all green.

- [ ] **Step 5: Commit**
```bash
git add -A && git commit -m "feat: default currency to UZS (sum) for settings, expenses, budgets"
```

---

### Task 3: CBU gold source + GoldService

**Files:** Create `Infrastructure/Gold/CbuGoldScraper.cs`; `Application/Gold/GoldService.cs`, `Application/Gold/GoldDtos.cs`; modify `Application/DependencyInjection.cs` (register GoldService) and `Infrastructure/DependencyInjection.cs` (register gold source + HtmlAgilityPack). Tests: `Tests/TestData/StubGoldSource.cs`, `Tests/Unit/CbuGoldScraperTests.cs`.

**Interfaces (Produces):** `GoldService` with `EnsureForDateAsync(DateOnly date, CancellationToken ct=default)`; `CbuGoldScraper : IGoldSource` with `static IReadOnlyList<SourceGold> Parse(string html)`.

- [ ] **Step 1: Add HtmlAgilityPack to Infrastructure**
```bash
dotnet add server/ExpenseTracker.Infrastructure package HtmlAgilityPack
```

- [ ] **Step 2: Gold parse test (fixture HTML)**
`Tests/Unit/CbuGoldScraperTests.cs`:
```csharp
using ExpenseTracker.Infrastructure.Gold;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class CbuGoldScraperTests
{
    const string Html = """
    <table class="prices"><tbody>
      <tr><th>Og'irligi</th><th>Sotish narxi</th><th>Qaytarib sotib olish narxi</th></tr>
      <tr><td>5 g</td><td>9 243 000</td><td>8 281 000</td></tr>
      <tr><td>50 g</td><td>92 431 000</td><td>82 811 000</td></tr>
      <tr><td>100 g</td><td>184 861 000</td><td>165 622 000</td></tr>
    </tbody></table>
    """;

    [Fact]
    public void Parses_weight_and_prices_stripping_separators()
    {
        var rows = CbuGoldScraper.Parse(Html);
        rows.Should().Contain(g => g.Item == "5 g" && g.SellPrice == 9243000m && g.BuyBackPrice == 8281000m);
        rows.Should().Contain(g => g.Item == "100 g" && g.SellPrice == 184861000m);
        rows.Should().HaveCount(3);
    }
}
```

- [ ] **Step 3: Run → FAIL, implement `CbuGoldScraper`**
Run: `dotnet test --filter CbuGoldScraperTests` → FAIL.
`Infrastructure/Gold/CbuGoldScraper.cs`:
```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using ExpenseTracker.Application.Common.Interfaces;
using HtmlAgilityPack;

namespace ExpenseTracker.Infrastructure.Gold;

public class CbuGoldScraper(HttpClient http) : IGoldSource
{
    public async Task<IReadOnlyList<SourceGold>> FetchAsync(DateOnly date, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync("uz/banknotes-coins/gold-bars/prices/", ct);
        resp.EnsureSuccessStatusCode();
        return Parse(await resp.Content.ReadAsStringAsync(ct));
    }

    public static IReadOnlyList<SourceGold> Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var rows = new List<SourceGold>();
        // Each data row: first cell weight (contains a digit + 'g'), next numeric cells are prices.
        foreach (var tr in doc.DocumentNode.SelectNodes("//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var cells = tr.SelectNodes("./td");
            if (cells is null || cells.Count < 2) continue;
            var item = HtmlEntity.DeEntitize(cells[0].InnerText).Trim();
            if (!Regex.IsMatch(item, @"\d")) continue;                 // skip header/non-data rows
            decimal? Money(int i) => i < cells.Count
                && decimal.TryParse(Regex.Replace(HtmlEntity.DeEntitize(cells[i].InnerText), @"[^\d]", ""),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
            rows.Add(new SourceGold(item, Money(1), Money(2)));
        }
        return rows;
    }
}
```
**Implementer note:** the fixture verifies the parse logic. Before relying on it live, fetch `https://cbu.uz/uz/banknotes-coins/gold-bars/prices/` and confirm the real table matches `//tr`/`./td` shape; adjust the selector if CBU wraps cells differently. Parsing failure must not throw out of `FetchAsync` callers (the scheduler wraps it) — but `Parse` itself returning `[]` on no matches is acceptable.

- [ ] **Step 4: Run → PASS** (`dotnet test --filter CbuGoldScraperTests`).

- [ ] **Step 5: GoldService + DI + stub**
`Application/Gold/GoldService.cs`:
```csharp
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Gold;

public class GoldService(IApplicationDbContext db, IGoldSource source)
{
    public async Task EnsureForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        if (await db.GoldPrices.AnyAsync(g => g.AsOfDate == date, ct)) return;
        foreach (var g in await source.FetchAsync(date, ct))
            db.GoldPrices.Add(new GoldPrice
            { AsOfDate = date, Item = g.Item, SellPrice = g.SellPrice, BuyBackPrice = g.BuyBackPrice });
        await db.SaveChangesAsync(ct);
    }
}
```
`Application/DependencyInjection.cs`: `services.AddScoped<GoldService>();`
`Infrastructure/DependencyInjection.cs`: `services.AddHttpClient<IGoldSource, CbuGoldScraper>(c => c.BaseAddress = new Uri("https://cbu.uz/"));`
`Tests/TestData/StubGoldSource.cs`:
```csharp
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Tests.TestData;

public class StubGoldSource : IGoldSource
{
    public Task<IReadOnlyList<SourceGold>> FetchAsync(DateOnly date, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SourceGold>>(new List<SourceGold>
        { new("5 g", 9243000m, 8281000m), new("100 g", 184861000m, 165622000m) });
}
```
Register it in `ApiFactory.ConfigureTestServices` alongside the rate stub:
```csharp
            services.RemoveAll<IGoldSource>();
            services.AddSingleton<IGoldSource, ExpenseTracker.Tests.TestData.StubGoldSource>();
```

- [ ] **Step 6: Build + suite + commit**
Run: `dotnet build` (0 warnings); `dotnet test` → green.
```bash
git add -A && git commit -m "feat: CBU gold-price scraper and GoldService"
```

---

### Task 4: Daily morning scheduler

**Files:** Create `Infrastructure/Scheduling/DailyRateFetchService.cs`; modify `Infrastructure/DependencyInjection.cs` (AddHostedService); modify `Tests/Integration/ApiFactory.cs` (remove the hosted service in tests); create `Tests/Unit/DailyFetchTests.cs`.

**Interfaces (Produces):** `DailyRateFetchService : BackgroundService` with `static async Task RunOnceAsync(IServiceProvider scopedSp, DateOnly date, ILogger logger, CancellationToken ct)` (the testable fetch-and-store core, source-isolated).

- [ ] **Step 1: Failing test for the fetch-and-store core (idempotent, source-isolated)**
`Tests/Unit/DailyFetchTests.cs` — verify `RunOnceAsync` calls `RatesService.EnsureAllSourcesForDateAsync` + `GoldService.EnsureForDateAsync` and that a throwing gold source does not prevent rates from being stored. Use the integration `ApiFactory` scope (real DB) OR a focused fake. **Chosen:** put this as an integration-style test in `Tests/Integration/SchedulerTests.cs` using the stubbed sources + a scope:
```csharp
using ExpenseTracker.Application.Rates;
using ExpenseTracker.Application.Gold;
using ExpenseTracker.Infrastructure.Scheduling;
using ExpenseTracker.Infrastructure.Persistence;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class SchedulerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task RunOnce_stores_rates_and_gold_and_is_idempotent()
    {
        var date = new DateOnly(2026, 6, 10);
        using (var scope = factory.Services.CreateScope())
            await DailyRateFetchService.RunOnceAsync(scope.ServiceProvider, date, NullLogger.Instance, default);
        using (var scope = factory.Services.CreateScope())
            await DailyRateFetchService.RunOnceAsync(scope.ServiceProvider, date, NullLogger.Instance, default); // again

        using var s = factory.Services.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CurrencyRates.CountAsync(r => r.Source == "CBU" && r.AsOfDate == date)).Should().Be(3); // stub has 3
        (await db.GoldPrices.CountAsync(g => g.AsOfDate == date)).Should().Be(2);                          // stub has 2
    }
}
```

- [ ] **Step 2: Run → FAIL, implement the service**
Run: `dotnet test --filter SchedulerTests` → FAIL.
`Infrastructure/Scheduling/DailyRateFetchService.cs`:
```csharp
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Gold;
using ExpenseTracker.Application.Rates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Infrastructure.Scheduling;

public class DailyRateFetchService(
    IServiceScopeFactory scopeFactory, IClock clock, ILogger<DailyRateFetchService> logger,
    Microsoft.Extensions.Configuration.IConfiguration config) : BackgroundService
{
    static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tashkent");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await FetchTodayAsync(stoppingToken);                       // startup catch-up
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun();
            try { await Task.Delay(delay, stoppingToken); } catch (TaskCanceledException) { break; }
            await FetchTodayAsync(stoppingToken);
        }
    }

    TimeSpan TimeUntilNextRun()
    {
        var hour = config.GetValue("Rates:DailyFetchHourTashkent", 9);
        var nowTz = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Tz);
        var next = new DateTimeOffset(nowTz.Year, nowTz.Month, nowTz.Day, hour, 0, 0, nowTz.Offset);
        if (next <= nowTz) next = next.AddDays(1);
        return next - nowTz;
    }

    async Task FetchTodayAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        await RunOnceAsync(scope.ServiceProvider, clock.TodayInTashkent(), logger, ct);
    }

    public static async Task RunOnceAsync(IServiceProvider sp, DateOnly date, ILogger logger, CancellationToken ct)
    {
        var rates = sp.GetRequiredService<RatesService>();
        var gold = sp.GetRequiredService<GoldService>();
        try { await rates.EnsureAllSourcesForDateAsync(date, ct); }
        catch (Exception ex) { logger.LogError(ex, "Rate fetch failed for {Date}", date); }
        try { await gold.EnsureForDateAsync(date, ct); }
        catch (Exception ex) { logger.LogError(ex, "Gold fetch failed for {Date}", date); }
    }
}
```
`Infrastructure/DependencyInjection.cs`: `services.AddHostedService<DailyRateFetchService>();`

- [ ] **Step 3: Keep tests deterministic — remove the hosted service in ApiFactory**
In `ApiFactory.ConfigureTestServices`, add (so the timer/startup-catch-up doesn't run during integration tests; the SchedulerTests call `RunOnceAsync` explicitly):
```csharp
            services.RemoveAll<IHostedService>();
```
(Add `using Microsoft.Extensions.Hosting;`.) This removes `DailyRateFetchService` (and any other hosted service) for tests only.

- [ ] **Step 4: Run → PASS, full suite**
Run: `dotnet test --filter SchedulerTests` → PASS; then `dotnet test` → all green; `dotnet build` 0 warnings.

- [ ] **Step 5: Commit**
```bash
git add -A && git commit -m "feat: daily Tashkent-morning rate/gold fetch background service"
```

---

### Task 5: Rates & Gold API endpoints

**Files:** Create `Application/Rates/RateDtos.cs` (view DTOs), add `RatesService.GetRatesAsync`; `Application/Gold/GoldDtos.cs`, add `GoldService.GetGoldAsync`; create `Api/Endpoints/RatesEndpoints.cs`, `Api/Endpoints/GoldEndpoints.cs`; modify `Program.cs`. Tests: `Tests/Integration/RatesApiTests.cs`, `GoldApiTests.cs`.

**Interfaces (Produces):**
- `record RateRow(string Source, string Currency, decimal RatePerUnit, decimal UnitPerUzs, decimal? Buy, decimal? Sell)`; `record RatesView(string Date, IReadOnlyList<RateRow> Rates)`.
- `record GoldRow(string Item, decimal? SellPrice, decimal? BuyBackPrice)`; `record GoldView(string Date, string? HistoryFrom, IReadOnlyList<GoldRow> Items)`.
- `RatesService.GetRatesAsync(DateOnly? date, IReadOnlyList<string> currencies, CancellationToken ct=default) : Task<RatesView>`
- `GoldService.GetGoldAsync(DateOnly? date, CancellationToken ct=default) : Task<GoldView>`

- [ ] **Step 1: Failing integration tests**
`Tests/Integration/RatesApiTests.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.Rates;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class RatesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient Client(long tg)
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tg, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return c;
    }

    [Fact]
    public async Task Rates_returns_usd_and_rub_both_directions()
    {
        var view = await Client(40001).GetFromJsonAsync<RatesView>("/api/rates?date=2026-06-12&currencies=USD,RUB");
        view!.Rates.Should().Contain(r => r.Source == "CBU" && r.Currency == "USD" && r.RatePerUnit == 12000m);
        view.Rates.Should().Contain(r => r.Currency == "RUB" && r.RatePerUnit == 160m);
    }
}
```
`Tests/Integration/GoldApiTests.cs`:
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.Gold;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class GoldApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Gold_returns_bars_for_date()
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(40002, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        var view = await c.GetFromJsonAsync<GoldView>("/api/gold?date=2026-06-12");
        view!.Items.Should().Contain(g => g.Item == "5 g" && g.SellPrice == 9243000m);
    }
}
```

- [ ] **Step 2: Run → FAIL, implement DTOs + service methods + endpoints**
Run: `dotnet test --filter "RatesApiTests|GoldApiTests"` → FAIL.
`Application/Rates/RateDtos.cs`:
```csharp
namespace ExpenseTracker.Application.Rates;

public record RateRow(string Source, string Currency, decimal RatePerUnit, decimal UnitPerUzs, decimal? Buy, decimal? Sell);
public record RatesView(string Date, IReadOnlyList<RateRow> Rates);
```
Add to `RatesService`:
```csharp
    public async Task<RatesView> GetRatesAsync(DateOnly? date, IReadOnlyList<string> currencies, CancellationToken ct = default)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        await EnsureAllSourcesForDateAsync(d, ct);
        var wanted = currencies.Select(c => c.ToUpperInvariant()).ToHashSet();
        var rows = await db.CurrencyRates
            .Where(r => r.AsOfDate == d && wanted.Contains(r.CurrencyCode))
            .OrderBy(r => r.Source).ThenBy(r => r.CurrencyCode)
            .Select(r => new RateRow(r.Source, r.CurrencyCode, r.Rate,
                r.Rate == 0 ? 0 : 1m / r.Rate, r.Buy, r.Sell))
            .ToListAsync(ct);
        return new RatesView(d.ToString("yyyy-MM-dd"), rows);
    }
```
`Application/Gold/GoldDtos.cs`:
```csharp
namespace ExpenseTracker.Application.Gold;

public record GoldRow(string Item, decimal? SellPrice, decimal? BuyBackPrice);
public record GoldView(string Date, string? HistoryFrom, IReadOnlyList<GoldRow> Items);
```
Add to `GoldService`:
```csharp
    public async Task<GoldView> GetGoldAsync(DateOnly? date, CancellationToken ct = default)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        await EnsureForDateAsync(d, ct);
        var items = await db.GoldPrices.Where(g => g.AsOfDate == d).OrderBy(g => g.Id)
            .Select(g => new GoldRow(g.Item, g.SellPrice, g.BuyBackPrice)).ToListAsync(ct);
        var earliest = await db.GoldPrices.OrderBy(g => g.AsOfDate).Select(g => (DateOnly?)g.AsOfDate).FirstOrDefaultAsync(ct);
        return new GoldView(d.ToString("yyyy-MM-dd"), earliest?.ToString("yyyy-MM-dd"), items);
    }
```
`Api/Endpoints/RatesEndpoints.cs`:
```csharp
using ExpenseTracker.Application.Rates;

namespace ExpenseTracker.Api.Endpoints;

public static class RatesEndpoints
{
    public static IEndpointRouteBuilder MapRatesEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/rates", async (RatesService svc, string? date, string? currencies, CancellationToken ct) =>
        {
            DateOnly? d = DateOnly.TryParse(date, out var parsed) ? parsed : null;
            var ccys = string.IsNullOrWhiteSpace(currencies)
                ? new[] { "USD", "RUB" }
                : currencies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return Results.Ok(await svc.GetRatesAsync(d, ccys, ct));
        });
        return api;
    }
}
```
`Api/Endpoints/GoldEndpoints.cs`:
```csharp
using ExpenseTracker.Application.Gold;

namespace ExpenseTracker.Api.Endpoints;

public static class GoldEndpoints
{
    public static IEndpointRouteBuilder MapGoldEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/gold", async (GoldService svc, string? date, CancellationToken ct) =>
        {
            DateOnly? d = DateOnly.TryParse(date, out var parsed) ? parsed : null;
            return Results.Ok(await svc.GetGoldAsync(d, ct));
        });
        return api;
    }
}
```
`Program.cs`: after the existing `api.Map...Endpoints();` calls add `api.MapRatesEndpoints(); api.MapGoldEndpoints();` and the usings.

- [ ] **Step 3: Run → PASS, full suite + build**
Run: `dotnet test --filter "RatesApiTests|GoldApiTests"` → PASS; `dotnet test` → all green; `dotnet build` 0 warnings.

- [ ] **Step 4: Commit**
```bash
git add -A && git commit -m "feat: /api/rates and /api/gold endpoints"
```

---

### Task 6: Frontend — Rates & Gold screen

**Files:** Modify `web/src/api/types.ts`, `web/src/api/hooks.ts`, `web/src/router.tsx`; create `web/src/screens/Rates.tsx`.

**Interfaces:** Consumes `/api/rates` (`RatesView`) and `/api/gold` (`GoldView`).

- [ ] **Step 1: Types**
Append to `web/src/api/types.ts`:
```ts
export interface RateRow { source: string; currency: string; ratePerUnit: number; unitPerUzs: number; buy: number | null; sell: number | null; }
export interface RatesView { date: string; rates: RateRow[]; }
export interface GoldRow { item: string; sellPrice: number | null; buyBackPrice: number | null; }
export interface GoldView { date: string; historyFrom: string | null; items: GoldRow[]; }
```

- [ ] **Step 2: Hooks**
Append to `web/src/api/hooks.ts`:
```ts
export const useRates = (date: string, currencies = "USD,RUB") =>
  useQuery({ queryKey: ["rates", date, currencies],
    queryFn: () => api<import("./types").RatesView>(`/rates?date=${date}&currencies=${currencies}`) });

export const useGold = (date: string) =>
  useQuery({ queryKey: ["gold", date],
    queryFn: () => api<import("./types").GoldView>(`/gold?date=${date}`) });
```

- [ ] **Step 3: Rates screen**
`web/src/screens/Rates.tsx`:
```tsx
import { useState } from "react";
import { useGold, useRates } from "../api/hooks";
import { localDateString } from "../lib/date";

const fmt = (n: number) => n.toLocaleString("en-US", { maximumFractionDigits: 2 });

export default function Rates() {
  const [date, setDate] = useState(localDateString(new Date()));
  const { data: rates } = useRates(date);
  const { data: gold } = useGold(date);

  return (
    <div className="screen">
      <h2>Rates</h2>
      <input type="date" value={date} onChange={(e) => setDate(e.target.value)} />

      <h3>Currencies (per source)</h3>
      {rates?.rates.length === 0 && <p>No rates for this date.</p>}
      <ul className="list">
        {rates?.rates.map((r) => (
          <li key={`${r.source}-${r.currency}`}>
            <strong>{r.source}</strong> · 1 {r.currency} = {fmt(r.ratePerUnit)} UZS
            <span className="hint"> · 1 UZS = {r.unitPerUzs.toFixed(6)} {r.currency}</span>
          </li>
        ))}
      </ul>

      <h3>Gold (CBU)</h3>
      {gold?.historyFrom && <p className="hint">History from {gold.historyFrom}</p>}
      <ul className="list">
        {gold?.items.map((g) => (
          <li key={g.item}>
            {g.item}: sell {g.sellPrice ? fmt(g.sellPrice) : "—"} / buy-back {g.buyBackPrice ? fmt(g.buyBackPrice) : "—"} UZS
          </li>
        ))}
      </ul>
    </div>
  );
}
```

- [ ] **Step 4: Route + tab link**
In `web/src/router.tsx`: import `Rates`, add `{ path: "rates", element: <Rates /> }` to the children, and add `<NavLink to="/rates">💱</NavLink>` to the tab bar.

- [ ] **Step 5: Build + commit**
Run: `cd web && npm run build` → succeeds (strict TS).
```bash
git add -A && git commit -m "feat(web): rates & gold screen with date picker"
```

---

## Self-Review

**Spec coverage (spec §→task):** §3 UZS defaults → Task 2; `CurrencyRate`/`GoldPrice` entities + remove `ExchangeRate` → Task 1 (gold entity) ; §4 ports/RatesService/ExchangeRateService rewrite/remove IExchangeRateProvider → Task 1; GoldService → Task 3; §5 CbuRateProvider → Task 1, CbuGoldScraper → Task 3, TashkentClock → Task 1, DailyRateFetchService + migration + DI → Tasks 1/3/4; §6 endpoints → Task 5; §7 frontend → Task 6 (Add-Expense UZS default is automatic — the screen already binds currency to `settings.baseCurrency`); §8 testing (CBU parse, conversion, gold parse, daily-fetch idempotent, rates/gold integration, no live calls) → Tasks 1,3,4,5; §9 scheduling → Task 4. ✓

**Placeholder scan:** The CbuGoldScraper carries an explicit implementer note to verify selectors against the live page — the fixture test pins the parse logic; this is a deliberate best-effort scrape (per spec §2/§10), not a placeholder. No "TBD"/"add validation" gaps.

**Type consistency:** `IRateSource`/`SourceRate`, `IGoldSource`/`SourceGold`, `IClock`, `ICbuRates.GetCbuRateAsync`, `RatesService.{EnsureSourceForDateAsync,EnsureAllSourcesForDateAsync,GetCbuRateAsync,GetRatesAsync}`, `GoldService.{EnsureForDateAsync,GetGoldAsync}`, `ExchangeRateService.GetRateAsync` (unchanged), `DailyRateFetchService.RunOnceAsync`, the DTOs (`RatesView/RateRow/GoldView/GoldRow`) and their camelCase TS mirrors are referenced consistently across tasks. `CurrencyRate`/`GoldPrice` field names match entity ↔ migration ↔ services. The `decimal(18,6)` rate precision and `Asia/Tashkent` tz are consistent.

**Note on coupling:** Task 1 is the large coupled core (entity/port/provider/converter/migration swap) that can only be green as a unit — mirrors the proven approach from the architecture restructure. Tasks 2–6 are smaller and independently reviewable.
