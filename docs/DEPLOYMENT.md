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
