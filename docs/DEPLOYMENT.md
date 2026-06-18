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
