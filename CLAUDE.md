# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A personal expense/income tracker delivered as a Telegram Mini App: ASP.NET Core minimal-API backend (`server/`) + React/Vite SPA (`web/`) served as static files from the same container. Auth is Telegram `initData` (HMAC-signed), not cookies/JWT — see `server/ExpenseTracker.Api/Auth/TelegramAuthHandler.cs`.

## Commands

### Backend (`server/`)
- Build: `dotnet build` (from repo root or `server/`)
- Run API locally: `dotnet run --project server/ExpenseTracker.Api` (needs local Postgres + `ConnectionStrings__Default` env var; see `docs/DEPLOYMENT.md`)
- Run all tests: `dotnet test` — integration tests spin up a real Postgres via Testcontainers, so **Docker must be running**
- Run a single test: `dotnet test --filter "FullyQualifiedName~ExpenseTracker.Tests.Integration.ExpensesApiTests"`
- EF Core migrations live in `server/ExpenseTracker.Infrastructure/Persistence/Migrations`; they run automatically via `Database.Migrate()` on app startup (`Program.cs`), not via a separate CLI step in normal dev.

### Frontend (`web/`)
- Install: `npm install` (in `web/`)
- Dev server: `npm run dev` — proxy `/api` to the backend via Vite `server.proxy` if running separately, or set `VITE_DEV_INIT_DATA` to a signed initData string (generate with the test `InitDataBuilder`) to exercise auth outside Telegram
- Build: `npm run build` (runs `tsc -b && vite build`)
- Lint: `npm run lint`

## Backend architecture

Clean Architecture, four projects with a strict dependency direction: `Domain` ← `Application` ← `Infrastructure`/`Api`. Application never references Infrastructure.

- **Domain** (`ExpenseTracker.Domain`): plain entities (`Entities/`) and seed data (`DefaultCategories.cs`, `DefaultIncomeCategories.cs`). No logic, no EF attributes beyond simple POCOs.
- **Application** (`ExpenseTracker.Application`): one folder per feature (`Expenses/`, `Budgets/`, `Rates/`, `Gold/`, etc.), each with a `*Service.cs` (business logic, talks only to `IApplicationDbContext`/other interfaces in `Common/Interfaces/`) and `*Dtos.cs`. Services return `OperationResult<T>` (`Common/OperationResult.cs`) rather than throwing for expected failure paths (not-found, validation) — endpoints map that to HTTP via `EndpointResults.ToHttp`.
- **Infrastructure** (`ExpenseTracker.Infrastructure`): EF Core `ApplicationDbContext` + migrations, Telegram initData validation/identity (`Identity/`), external HTTP sources for CBU exchange rates and gold prices (`ExchangeRates/`, `Gold/`), the Tashkent-timezone clock (`Time/TashkentClock.cs`), and the `DailyRateFetchService` background job.
- **Api** (`ExpenseTracker.Api`): minimal-API endpoint groups in `Endpoints/` (one `Map*Endpoints` extension per feature, wired up in `Program.cs`), plus the `TelegramAuthHandler`. Endpoints are thin: resolve `ICurrentUser`, call the Application service, map the result — no business logic here.

Key interfaces to know (`Application/Common/Interfaces/`): `ICurrentUser` (gets/creates the `User` row for the authenticated Telegram identity — implemented by `UserProvisioningService`), `IUserContext` (raw Telegram identity claims from the request, no DB), `IClock` (Tashkent-local "today", used everywhere instead of `DateTime.Now`), `ICbuRates`/`IRateSource`/`IGoldSource` (exchange rate and gold price providers).

Currency: all money is valued in UZS using the CBU rate on or before the transaction date (`CurrencyConverter`, `ExchangeRateService`) — rates are fetched daily and backfilled on demand for past dates that aren't cached yet.

Auth flow: `/api/*` routes require authorization via the Telegram scheme; the `Authorization: tma <initData>` header is validated by `TelegramInitDataValidator`, then `ICurrentUser.GetOrCreateAsync()` upserts the user. `/internal/refresh` uses a separate shared-secret header (`X-Refresh-Token`) instead, for the Cloud Scheduler cron call that drives the daily rate/gold fetch under scale-to-zero hosting.

## Frontend architecture

- `src/router.tsx` defines all routes and the persistent tab-bar `Layout`; screens live in `src/screens/` (one per route, roughly mirroring backend features).
- `src/api/client.ts` is the single fetch wrapper — it attaches the Telegram `initData` as `Authorization: tma <data>` and prefixes `/api`. `src/api/hooks.ts` wraps endpoints in TanStack Query hooks; `src/api/types.ts` holds the response/request shapes mirroring the backend DTOs.
- `src/telegram/` reads Telegram WebApp `initData` and theme params; `src/lib/appTheme.ts` maps Telegram theme into CSS.
- Feature toggles (e.g. income tracking) come from the `/api/settings` response and are read via `useSettings()`; routes/tabs for a disabled feature redirect rather than 404 (see `RequireIncome` in `router.tsx`).

## Notes

- Integration tests (`server/ExpenseTracker.Tests/Integration/`) boot the full `ApiFactory` (a `WebApplicationFactory<Program>`) against a throwaway Testcontainers Postgres instance — they are the primary way to verify endpoint + EF behavior end-to-end; unit tests (`Unit/`) cover pure logic (rate parsing, currency conversion, initData validation).
- Past design/planning docs for major features are in `docs/superpowers/plans/` and `docs/superpowers/specs/` if you need historical rationale for a feature's shape.
