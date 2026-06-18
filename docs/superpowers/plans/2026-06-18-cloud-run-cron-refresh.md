# Cloud Run Cron-Refresh Endpoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a secret-guarded `POST /internal/refresh` endpoint that triggers the existing daily rate/gold fetch, so it runs reliably on scale-to-zero Cloud Run via Cloud Scheduler; document the Cloud Run + Cloud Scheduler + Neon deploy.

**Architecture:** A new anonymous-to-Telegram minimal-API endpoint outside the `/api` group, guarded by a shared-secret header (`X-Refresh-Token` vs config `Rates:RefreshToken`, constant-time compare, closed-by-default), reusing the existing static `DailyRateFetchService.RunOnceAsync`. The in-process `DailyRateFetchService` is left untouched (harmless on serverless; works on always-on Oracle).

**Tech Stack:** .NET 8 minimal APIs, xUnit + FluentAssertions + Testcontainers; Google Cloud Run + Cloud Scheduler + Neon (docs).

## Global Constraints

- Target `net8.0`; preserve Clean Architecture (the endpoint lives in `Api/Program.cs`; it calls the existing `DailyRateFetchService.RunOnceAsync` in Infrastructure and `IClock` from Application — no new cross-layer leakage).
- The endpoint is registered **outside** the `/api` `RequireAuthorization()` group (Cloud Scheduler can't send Telegram `initData`).
- **Closed by default:** if `Rates:RefreshToken` is null/empty → `401`. Header `X-Refresh-Token` compared with `CryptographicOperations.FixedTimeEquals`. Mismatch/missing → `401`. Success → run the fetch for `IClock.TodayInTashkent()` → `200`.
- No change to rates/gold/conversion logic or the in-process scheduler.
- After each task: `dotnet build ExpenseTracker.sln` clean (0 warnings) + `dotnet test` green. No live CBU calls in tests (existing stub sources).

---

## File Structure

```
server/ExpenseTracker.Api/Program.cs                       (modify: map POST /internal/refresh)
server/ExpenseTracker.Tests/Integration/ApiFactory.cs      (modify: set Rates:RefreshToken)
server/ExpenseTracker.Tests/Integration/RefreshEndpointTests.cs  (new)
docs/DEPLOYMENT.md                                          (modify: Cloud Run + Scheduler + Neon section)
```

---

### Task 1: `/internal/refresh` endpoint + tests

**Files:**
- Modify: `server/ExpenseTracker.Api/Program.cs`
- Modify: `server/ExpenseTracker.Tests/Integration/ApiFactory.cs`
- Create: `server/ExpenseTracker.Tests/Integration/RefreshEndpointTests.cs`

**Interfaces:**
- Consumes: `DailyRateFetchService.RunOnceAsync(IServiceProvider sp, DateOnly date, ILogger logger, CancellationToken ct)` (Infrastructure.Scheduling); `IClock.TodayInTashkent()` (Application.Common.Interfaces); `ApplicationDbContext` (Infrastructure.Persistence). Stub sources return 3 CurrencyRate rows + 2 GoldPrice rows for any date.
- Produces: `ApiFactory.RefreshToken` const for tests.

- [ ] **Step 1: Write the failing tests**

`server/ExpenseTracker.Tests/Integration/RefreshEndpointTests.cs`:
```csharp
using System.Net;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class RefreshEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Correct_token_refreshes_and_stores_today()
    {
        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/refresh");
        req.Headers.Add("X-Refresh-Token", ApiFactory.RefreshToken);

        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayInTashkent();
        (await db.CurrencyRates.CountAsync(r => r.Source == "CBU" && r.AsOfDate == today)).Should().Be(3);
        (await db.GoldPrices.CountAsync(g => g.AsOfDate == today)).Should().Be(2);
    }

    [Fact]
    public async Task Wrong_token_is_401()
    {
        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/refresh");
        req.Headers.Add("X-Refresh-Token", "wrong-token");
        (await client.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Missing_token_is_401()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsync("/internal/refresh", null);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 2: Add the refresh token to ApiFactory**

In `server/ExpenseTracker.Tests/Integration/ApiFactory.cs`, add the constant near `BotToken`:
```csharp
    public const string RefreshToken = "test-refresh-token";
```
and in `ConfigureWebHost` (next to the existing `builder.UseSetting(...)` calls):
```csharp
        builder.UseSetting("Rates:RefreshToken", RefreshToken);
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test --filter RefreshEndpointTests`
Expected: FAIL — `/internal/refresh` returns 404 (route not mapped), so the 200 assertion fails (the two 401 tests would coincidentally pass on a 404≠401? No: 404≠401 so they also fail). All three fail.

- [ ] **Step 4: Map the endpoint in Program.cs**

In `server/ExpenseTracker.Api/Program.cs`, add usings:
```csharp
using System.Security.Cryptography;
using System.Text;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Infrastructure.Scheduling;
```
Then add the endpoint after the `app.MapGet("/healthz", ...)` line and before `var api = app.MapGroup("/api")...` (so it's NOT inside the authorized group):
```csharp
app.MapPost("/internal/refresh", async (
    HttpRequest request, IServiceProvider services, IClock clock,
    IConfiguration config, ILoggerFactory loggerFactory, CancellationToken ct) =>
{
    var configured = config["Rates:RefreshToken"];
    var provided = request.Headers["X-Refresh-Token"].ToString();
    if (string.IsNullOrEmpty(configured) ||
        !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(configured)))
    {
        return Results.Unauthorized();
    }

    var today = clock.TodayInTashkent();
    var logger = loggerFactory.CreateLogger("RefreshEndpoint");
    await DailyRateFetchService.RunOnceAsync(services, today, logger, ct);
    return Results.Ok(new { refreshed = today.ToString("yyyy-MM-dd") });
});
```
(`IConfiguration` is available from DI; `services` is the request scope passed straight to `RunOnceAsync`, which resolves the scoped `RatesService`/`GoldService` from it. `FixedTimeEquals` returns false for differing-length inputs, so a missing/short header → 401.)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter RefreshEndpointTests`
Expected: PASS (3 tests). Then `dotnet test` → full suite green; `dotnet build` 0 warnings.

- [ ] **Step 6: Commit**
```bash
git add -A && git commit -m "feat: secret-guarded /internal/refresh endpoint for scheduled fetch"
```

---

### Task 2: Cloud Run + Cloud Scheduler + Neon deploy runbook

**Files:**
- Modify: `docs/DEPLOYMENT.md`

**Interfaces:** none (docs only). Documents the env vars the app reads: `ConnectionStrings__Default`, `BotToken`, `Rates__RefreshToken`; and the endpoint `POST /internal/refresh` with header `X-Refresh-Token`.

- [ ] **Step 1: Append a Cloud Run section to `docs/DEPLOYMENT.md`**

Add this section verbatim:
````markdown
## Deploy on Google Cloud Run (free, serverless) + Cloud Scheduler + Neon

This is the recommended free path. The app scales to zero; a daily Cloud Scheduler
call drives the rate/gold fetch (the in-process timer doesn't run under scale-to-zero).

### 1. Postgres — Neon (free)
- Create a free project at neon.tech; copy the connection string in Npgsql form, e.g.
  `Host=<host>;Database=<db>;Username=<user>;Password=<pw>;SSL Mode=Require;Trust Server Certificate=true`

### 2. Deploy the container to Cloud Run
From the repo root (Cloud Build builds the Dockerfile):
```bash
gcloud run deploy expense-tracker \
  --source . \
  --region europe-west1 \
  --allow-unauthenticated \
  --min-instances 0 \
  --set-env-vars 'ConnectionStrings__Default=<neon-connection-string>' \
  --set-env-vars 'BotToken=<bot-token>' \
  --set-env-vars 'Rates__RefreshToken=<long-random-secret>'
```
- `--allow-unauthenticated` is correct: the app does its own auth (Telegram `initData` for `/api/*`, the refresh secret for `/internal/refresh`).
- Note the service URL it prints (e.g. `https://expense-tracker-xxxx.run.app`). Confirm `…/healthz` returns `ok`. Migrations run automatically on startup.

### 3. Daily fetch — Cloud Scheduler (3 jobs free)
```bash
gcloud scheduler jobs create http daily-rate-fetch \
  --location europe-west1 \
  --schedule '0 9 * * *' \
  --time-zone 'Asia/Tashkent' \
  --uri 'https://<service-url>/internal/refresh' \
  --http-method POST \
  --headers 'X-Refresh-Token=<the-same-long-random-secret>'
```
This POSTs to `/internal/refresh` at 09:00 Tashkent daily; the request wakes a
container which fetches+stores that day's CBU rates and gold.

### 4. Register the Mini App
- @BotFather → Bot Settings → Menu Button → Web App URL = the Cloud Run HTTPS URL.

### Notes
- The user-facing app cold-starts (~seconds) after idle on scale-to-zero; the scheduled
  fetch is unaffected. Past currency dates also backfill on demand from CBU when viewed.
- To move to an always-on host later (e.g. Oracle), no code change is needed — the
  in-process `DailyRateFetchService` resumes firing at 09:00; the Cloud Scheduler job
  can then be deleted.
````

- [ ] **Step 2: Commit**
```bash
git add docs/DEPLOYMENT.md && git commit -m "docs: Cloud Run + Cloud Scheduler + Neon deployment runbook"
```

---

## Self-Review

**Spec coverage:** §2 endpoint (outside /api, shared secret, closed-by-default, FixedTimeEquals, RunOnceAsync, 200/401) → Task 1; §3 deploy (Neon, Cloud Run env vars, Cloud Scheduler 09:00 Tashkent, BotFather) → Task 2; §4 testing (correct→200+rows, wrong→401, missing→401, ApiFactory token, stubs) → Task 1. In-process scheduler untouched (§1) — no task modifies it. ✓

**Placeholder scan:** Deploy commands use `<...>` for user-supplied secrets/URLs (runtime values, not code placeholders) — correct for a runbook. No "TBD"/vague-logic gaps.

**Type consistency:** `DailyRateFetchService.RunOnceAsync(IServiceProvider, DateOnly, ILogger, CancellationToken)`, `IClock.TodayInTashkent()`, `ApplicationDbContext.CurrencyRates/GoldPrices`, and `ApiFactory.RefreshToken` / config key `Rates:RefreshToken` (env `Rates__RefreshToken`) are consistent across the endpoint, the test, ApiFactory, and the runbook. Stub counts (3 rates, 2 gold) match the existing stubs used by other integration tests.
