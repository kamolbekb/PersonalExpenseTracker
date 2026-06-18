# UZS Currency + CBU Multi-Source Rates & Gold — Design

**Date:** 2026-06-18
**Status:** Approved (design)

## 1. Goal

Make the expense tracker UZS-centric and add a daily, historically-queryable
exchange-rate & gold dashboard sourced from the Central Bank of Uzbekistan (CBU):

1. Expenses and budgets default to **UZS (sum)**; the base currency is UZS.
2. Report conversion of foreign-currency expenses → UZS uses **CBU** (replacing
   Frankfurter, which has no UZS rates).
3. A **multi-source** rate store (schema ready for several banks) holding daily
   UZS rates per source; **CBU is the only source implemented now** (Ipak Yo'li,
   Asaka plug in later with no schema change).
4. A **daily morning scheduled fetch** stores each day's CBU rates + gold prices;
   users can pick a **prior date** and see that day's course.
5. **CBU gold-bar prices** displayed, with history accruing from first capture.

Built entirely within the existing Clean Architecture layers
(Domain ← Application ← Infrastructure ← Api).

## 2. Data sources (researched)

- **CBU currency JSON API** — `https://cbu.uz/uz/arkhiv-kursov-valyut/json/all/<yyyy-MM-dd>/`
  returns all ~75 currencies for that date. Fields per item: `Ccy` (e.g. "USD"),
  `Code`, `Rate` (string UZS per `Nominal` units), `Nominal` (string, usually "1";
  e.g. "100" for some), `Diff`, `Date` ("dd.MM.yyyy"). **Supports historical dates.**
- **CBU gold-bar prices** — `https://cbu.uz/uz/banknotes-coins/gold-bars/prices/`
  is an HTML page (no JSON): bar weights (e.g. 5g/10g/20g/50g/100g) with a
  selling price and a buy-back price in UZS. **Current-day only** (no history API).
- **Ipak Yo'li / Asaka** — no public API; **deferred** (schema supports them).

## 3. Domain changes

**Currency defaults → UZS:**
- `Setting.BaseCurrency` default `"UZS"`; `UserProvisioningService` seeds
  `BaseCurrency = "UZS"`; `Expense.CurrencyCode` and `Budget.CurrencyCode`
  defaults `"UZS"`.

**New entity `CurrencyRate`** (multi-source daily rate store):
```
CurrencyRate {
  int Id;
  string Source;        // "CBU" (later "IPAKYULI" | "ASAKA")
  string CurrencyCode;  // ISO, e.g. "USD","RUB","EUR"
  DateOnly AsOfDate;    // CBU quote date
  decimal Rate;         // official UZS per 1 unit (Rate/Nominal)
  decimal? Buy;         // null for CBU; commercial banks later
  decimal? Sell;        // null for CBU; commercial banks later
}
// unique index (Source, CurrencyCode, AsOfDate); index (AsOfDate)
```

**New entity `GoldPrice`** (CBU gold bars):
```
GoldPrice {
  int Id;
  DateOnly AsOfDate;
  string Item;          // bar label, e.g. "5g","10g","20g","50g","100g"
  decimal? SellPrice;   // CBU selling price (UZS)
  decimal? BuyBackPrice;// CBU buy-back price (UZS)
}
// unique index (AsOfDate, Item)
```

**Removed:** the Frankfurter-specific `ExchangeRate` entity (its caching role is
replaced by `CurrencyRate`). No production data exists, so the EF migration drops
the `ExchangeRates` table and creates `CurrencyRates` + `GoldPrices`.

## 4. Application layer

**Ports (Application.Common.Interfaces):**
- `IRateSource` — one per data source:
  - `string SourceCode { get; }` (e.g. "CBU")
  - `Task<IReadOnlyList<SourceRate>> FetchAsync(DateOnly date, CancellationToken ct)`
    where `SourceRate(string CurrencyCode, decimal Rate, decimal? Buy, decimal? Sell)`
    (Rate already normalized to per-1-unit).
- `IGoldSource`:
  - `Task<IReadOnlyList<SourceGold>> FetchAsync(DateOnly date, CancellationToken ct)`
    where `SourceGold(string Item, decimal? SellPrice, decimal? BuyBackPrice)`.
- `IClock` — `DateOnly TodayInTashkent()` / `DateTimeOffset Now` (so scheduling +
  "today" are testable; implemented in Infrastructure using `Asia/Tashkent`).

The daily job and on-demand lookups iterate **all registered `IRateSource`s**;
today only the CBU implementation is registered.

**`RatesService`** (Application.Rates):
- `Task EnsureSourceForDateAsync(string sourceCode, DateOnly date, CancellationToken ct)`
  — if that source has no `CurrencyRate` rows for `date`, fetch via the matching
  `IRateSource` and upsert. Used by conversion (CBU) and the daily job (all sources).
- `Task<RatesView> GetRatesAsync(DateOnly? date, IReadOnlyList<string> currencies, CancellationToken ct)`
  — resolves the date (default: latest stored ≤ today, ensuring today is fetched
  first), returns per-source rates for the requested currencies (default USD, RUB),
  each with both directions (UZS-per-unit and the reciprocal unit-per-UZS).

**`GoldService`** (Application.Gold):
- `Task EnsureForDateAsync(DateOnly date, CancellationToken ct)` (fetch+store if absent).
- `Task<GoldView> GetGoldAsync(DateOnly? date, CancellationToken ct)` (resolved date
  → stored bars; notes the earliest captured date for the "history from" hint).

**Conversion — `ExchangeRateService` reimplemented (same public contract):**
- Keeps `Task<decimal> GetRateAsync(string from, string to, DateOnly date)` so
  `ReportService` is unchanged.
- New logic (CBU-backed via the store): `from==to → 1`. Ensure CBU rates for `date`
  (`RatesService.EnsureSourceForDateAsync("CBU", date)`), then:
  - `X → UZS` = CBU UZS-per-X.
  - `UZS → X` = 1 / (CBU UZS-per-X).
  - `X → Y` (neither UZS) = (UZS-per-X) / (UZS-per-Y) cross-rate.
  - If a needed currency is absent from CBU's set for that date → throw a clear
    `InvalidOperationException` (caller surfaces it; reports already tolerate per-row).
- `CurrencyConverter` (rounding) is unchanged.
- The old single-rate `IExchangeRateProvider` port **and** `FrankfurterRateProvider`
  are **removed**; `ExchangeRateService` now depends on `RatesService` + the
  `CurrencyRate` store (which in turn use the `IRateSource`s), not on a single-rate
  provider. `ReportService`'s dependency on `ExchangeRateService.GetRateAsync` is
  unchanged.

**DTOs:** `RatesView`/`RateRow` (source, currency, ratePerUnit, unitPerUZS, buy?, sell?),
`GoldView`/`GoldRow` (item, sellPrice?, buyBackPrice?, historyFrom). camelCase JSON.

## 5. Infrastructure layer

- **`CbuRateProvider : IRateSource`** (`SourceCode="CBU"`): GET the dated JSON
  endpoint, parse `Ccy`/`Rate`/`Nominal`, normalize `Rate/Nominal` to per-1-unit,
  `EnsureSuccessStatusCode`, tolerate CBU returning the nearest published date.
  Registered as a keyed/enumerable `IRateSource`.
- **`CbuGoldScraper : IGoldSource`**: GET the gold-bars HTML page, parse the price
  table (bar label + selling + buy-back). Defensive parsing; failure logs + returns
  empty (does not crash the job).
- **`TashkentClock : IClock`** using `TimeZoneInfo` `Asia/Tashkent`.
- **`DailyRateFetchService : BackgroundService`**:
  - On startup, run a catch-up for `IClock.TodayInTashkent()` (rates for every
    `IRateSource` + gold), each wrapped in try/catch + logging so one failure is
    isolated.
  - Then loop: compute the next occurrence of the configured hour (config
    `Rates:DailyFetchHourTashkent`, default `9`) in Asia/Tashkent, delay until then,
    run the fetch, repeat. Idempotent (skip a source/date already stored).
  - Resolves scoped services (`RatesService`, `GoldService`) via `IServiceScopeFactory`.
- **EF migration** `AddRatesAndGold`: drop `ExchangeRates`; create `CurrencyRates`
  (+ unique & date indexes) and `GoldPrices` (+ unique index).
- **DI (`AddInfrastructure`)**: register `IClock`, `CbuRateProvider` as `IRateSource`
  (+ its `HttpClient` with `BaseAddress https://cbu.uz/`), `CbuGoldScraper` as
  `IGoldSource`, and `AddHostedService<DailyRateFetchService>()`. Remove the
  Frankfurter registration/provider.

## 6. Api layer (thin endpoints, behind auth, not user-scoped)

- `GET /api/rates?date=<yyyy-MM-dd>&currencies=USD,RUB` → `RatesView` for the date
  (default latest). Resolves user via auth but the data is global (no `UserId` filter).
- `GET /api/gold?date=<yyyy-MM-dd>` → `GoldView` for the date (default latest).
- Mapped under the existing `/api` group with `RequireAuthorization()`, via new
  `RatesEndpoints`/`GoldEndpoints` returning `OperationResult`-mapped results.

## 7. Frontend (React) — new "Rates" tab

- Tab bar gains **Rates** (and the gold view lives within it).
- **Rates screen:** a date picker (default today, local date via the existing
  `localDateString` helper) → fetches `/api/rates?date=`. Shows USD and RUB as cards
  per source (CBU now): "1 USD = N UZS" and "1 UZS = … USD" (both directions), with
  room for Buy/Sell columns when banks arrive.
- **Gold section:** `/api/gold?date=` → a table of bar weights with sell / buy-back
  prices, plus a "history from <date>" note.
- **Add Expense:** currency field now defaults to UZS (base currency).
- TanStack Query hooks `useRates(date, currencies)` and `useGold(date)`; types mirror
  the DTOs.

## 8. Testing

- **Unit (Application/Infrastructure):**
  - CBU JSON parsing: `Nominal` normalization (e.g. Nominal=100), missing currency,
    date echo; from a fixture JSON string (no network).
  - Cross-rate conversion: X→UZS, UZS→X reciprocal, X→Y via UZS, identity, missing-currency throw.
  - Gold HTML parsing from a fixture HTML snippet.
  - Daily fetch core: calling the service's fetch-and-store method directly stores
    rows and is idempotent (re-run doesn't duplicate) — using a **stub `IRateSource`/
    `IGoldSource`**, not live CBU and not the timer.
- **Integration (Testcontainers):** `/api/rates` and `/api/gold` return stored data
  for a date, using a **stub source** registered in the test host (no live CBU).
  Existing expense/report tests keep passing with a stub CBU converter.
- No test performs a live CBU/network call. CBU is hit live only by the scheduler at
  runtime.

## 9. Scheduling / operational notes

- Default fetch time 09:00 Asia/Tashkent (config `Rates:DailyFetchHourTashkent`).
- Historical currency lookups backfill on demand from CBU (history-capable); gold
  history only accrues from the first captured day (CBU page is current-only) — the
  UI shows a "history from <date>" hint.
- The scheduler runs in-process in the single PaaS container (one instance assumed).

## 10. Out of scope (now)

- Ipak Yo'li / Asaka scrapers (schema-ready; add when a stable source exists).
- Buy/sell spreads (CBU has none; columns exist for future banks).
- Gold historical backfill (no CBU source).
- Multi-instance scheduler coordination (single container).
