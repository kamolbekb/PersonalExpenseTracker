# Cloud Run Cron-Refresh Endpoint — Design

**Date:** 2026-06-18
**Status:** Approved (design)

## 1. Goal

Make the daily rate/gold fetch reliable on a **scale-to-zero serverless host** (Google Cloud Run), where the in-process `DailyRateFetchService` timer cannot fire. Add a secret-guarded HTTP endpoint that an external scheduler (Google Cloud Scheduler) calls once daily to trigger the existing fetch. Document the Cloud Run + Cloud Scheduler + Neon deployment.

The in-process scheduler is **kept unchanged** — it's harmless on Cloud Run (won't tick under scale-to-zero, still does startup catch-up) and resumes working as-is on an always-on host (Oracle) later. The cron endpoint is simply the reliable trigger on serverless.

## 2. The endpoint

`POST /internal/refresh` — registered in `Program.cs` **outside** the `/api` group, so it does NOT require Telegram `initData` (Cloud Scheduler cannot supply that). Guarded instead by a shared secret:

- Reads config `Rates:RefreshToken` (env `Rates__RefreshToken`).
- **Closed by default:** if `Rates:RefreshToken` is null/empty → always `401` (a misconfigured deploy must not leave the endpoint open).
- Compares the request header `X-Refresh-Token` to the configured token using `CryptographicOperations.FixedTimeEquals`. Mismatch → `401`.
- On success: runs `DailyRateFetchService.RunOnceAsync(httpContext.RequestServices, clock.TodayInTashkent(), logger, ct)` (the existing failure-isolated rates+gold fetch) and returns `200` (`Results.Ok`).

Handler dependencies via minimal-API injection: `HttpRequest`, `IServiceProvider` (request scope, passed to `RunOnceAsync`), `IClock`, `IConfiguration`, `ILoggerFactory`, `CancellationToken`. No new service classes — reuses `RunOnceAsync`.

## 3. Deployment (Cloud Run + Cloud Scheduler + Neon)

Documented in `docs/DEPLOYMENT.md` (new "Cloud Run (free, serverless)" section):

1. **Neon**: create a free Postgres project; copy the connection string (Npgsql form, `SSL Mode=Require;Trust Server Certificate=true`).
2. **Cloud Run**: deploy the Dockerfile (`gcloud run deploy --source .` or container build), allow unauthenticated invocations (the app does its own auth), set env vars: `ConnectionStrings__Default` (Neon), `BotToken`, `Rates__RefreshToken` (a long random secret). Scale-to-zero (min instances 0) keeps it free.
3. **Cloud Scheduler**: one HTTP job, `--schedule="0 9 * * *" --time-zone="Asia/Tashkent" --http-method=POST --uri="https://<service-url>/internal/refresh" --headers="X-Refresh-Token=<the secret>"`. (3 scheduler jobs are free.)
4. **BotFather**: set the Mini App URL to the Cloud Run HTTPS URL.
5. Migrations run automatically on startup; the first Cloud Scheduler call (or any visit) populates rates/gold via the existing catch-up/on-demand paths.

Operational note: the user-facing app still cold-starts (~seconds) after idle on scale-to-zero; the scheduled fetch itself is unaffected because Cloud Scheduler's request wakes a container to handle it.

## 4. Testing

Integration test (`Tests/Integration/RefreshEndpointTests.cs`, Testcontainers + stub sources, with `Rates:RefreshToken` set in `ApiFactory`):
- `POST /internal/refresh` with the correct `X-Refresh-Token` → `200`, and CurrencyRate (3 stub rows) + GoldPrice (2 stub rows) are stored for today.
- Wrong header → `401`; missing header → `401`.

`ApiFactory` sets `Rates:RefreshToken` via `UseSetting` so the endpoint is testable; it continues to stub `IRateSource`/`IGoldSource` and remove hosted services (no live calls, no timer).

## 5. Out of scope

- Google OIDC auth (chosen shared-secret header instead — simpler, host-agnostic).
- Removing/altering the in-process `DailyRateFetchService` (kept for Oracle/always-on).
- Any change to the rates/gold/conversion logic.
