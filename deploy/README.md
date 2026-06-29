# Deploying the Expense Tracker on a Linux box (Cloudflare Tunnel)

Works the same on a home server or a cloud VM (e.g. Hetzner): the app runs on your
box, the **database is hosted on Supabase**, and a Cloudflare Tunnel exposes the app
on a public HTTPS URL — outbound-only, so **no port-forwarding, no static IP**.

```
Internet ─https─▶ Cloudflare edge ─tunnel─▶ cloudflared ─http─▶ expense-api:8080
```

This is an **independent stack** from UPro: its own tunnel, its own hostname
(`expense.shermatov.uz`), its own database (Supabase). Multiple apps run on the
same box at once — they listen on `:8080` *inside their own containers*, which
never clash because `cloudflared` reaches each by service name.

> **Why a second tunnel and not just a second hostname on the UPro tunnel?**
> Either works — one tunnel can serve many hostnames. A second, self-contained
> tunnel keeps this repo independent (deploy/update it without touching UPro).
> If you'd rather reuse the UPro tunnel, skip step 3 below, delete the
> `cloudflared` service from `docker-compose.yml`, put `expense-api` on the same
> Docker network as UPro's `cloudflared`, and just add the public hostname (step 4)
> to the existing tunnel.

---

## 1. Get a Telegram bot token (@BotFather)

The app has **no passwords** — it authenticates users from Telegram's signed
`initData`, validated with this bot's token. So the token here **must** be the
bot that opens the Mini App.

1. In Telegram, open a chat with **@BotFather** (the verified one, blue check).
2. Send `/newbot`.
3. Give it a **display name** (e.g. `Shermatov Expenses`).
4. Give it a **username** ending in `bot` (e.g. `shermatov_expenses_bot`).
5. BotFather replies with a line like:
   `Use this token to access the HTTP API:` followed by
   `123456789:AAH...` — **that is your token.** Copy it.
6. Paste it into `deploy/.env` as `BotToken=`.

(Already have a bot? Reuse it: `/mybots → <bot> → API Token`.)

You'll come back to BotFather in **step 6** to point the bot at your URL — but you
need the deployed URL first.

## 2. Prepare env

```bash
cp deploy/.env.example deploy/.env
nano deploy/.env
```
Fill `ConnectionStrings__Default` (your Supabase connection string — dashboard →
**Connect → Session pooler**, port `5432`, converted to Npgsql `key=value` form;
see the example file) and `BotToken` (step 1). Leave `CLOUDFLARE_TUNNEL_TOKEN` for
step 3 and `Rates__RefreshToken` blank.

## 3. Create the Cloudflare Tunnel

1. Go to **Zero Trust** (one.dash.cloudflare.com) **→ Networks → Tunnels →
   Create a tunnel**.
2. Choose **Cloudflared**, name it (e.g. `expense`), **Save**.
3. On the "Install connector" screen, copy the **tunnel token** — the long string
   after `--token` in the shown command. You do **not** need to run that command;
   our `cloudflared` container uses the token.
4. Paste it into `deploy/.env` as `CLOUDFLARE_TUNNEL_TOKEN=`.

## 4. Add the public hostname

Still in the tunnel config → **Public Hostnames → Add a public hostname**:

- **Subdomain:** `expense`
- **Domain:** `shermatov.uz`
- **Type:** `HTTP`
- **URL:** `expense-api:8080`

Save. Cloudflare **auto-creates the DNS record** for `expense` (a `Tunnel`-type
entry, exactly like your existing `api → LocalServer` row). Don't add an A record
by hand.

## 5. Bring it up

```bash
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
```

Check it:
```bash
docker compose -f deploy/docker-compose.yml logs -f expense-api      # EF migrations + startup
curl -s https://expense.shermatov.uz/healthz                          # -> ok
```
`/api/*` returns 401 without Telegram `initData` — that's correct (auth works).
EF Core migrations run automatically on startup; no manual DB setup.

## 6. Point the bot at your URL (@BotFather)

Now that `https://expense.shermatov.uz` is live, register it as the Mini App:

- `/mybots → <your bot> → Bot Settings → Menu Button → Edit menu button URL`
  → enter `https://expense.shermatov.uz`.
- (Optional, for a `t.me/<bot>/app` deep link: `/newapp`, pick the bot, and give
  the same URL.)

Open the bot in Telegram, tap the menu button — the Mini App loads, auto-creates
your user, and seeds default categories.

## Rates / gold scheduler — nothing extra needed

The app fetches CBU exchange rates + gold prices daily at **09:00 Asia/Tashkent**
via an in-process background service, plus a catch-up on every startup. Because
your home server is always-on (unlike serverless), this just works — you do **not**
need Cloudflare/Cloud Scheduler or the `/internal/refresh` endpoint. Leave
`Rates__RefreshToken` blank.

## Day-2 operations

```bash
# update to latest code
git pull && docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build

# logs / restart
docker compose -f deploy/docker-compose.yml logs -f
docker compose -f deploy/docker-compose.yml restart expense-api
```

Database backups are handled by **Supabase** (automatic daily backups on the
dashboard). For an extra off-site copy you can still `pg_dump` against Supabase:

```bash
docker run --rm -e PGPASSWORD='<supabase-db-password>' postgres:16-alpine \
  pg_dump -h <session-pooler-host> -p 5432 -U postgres.<project-ref> -d postgres \
  -Fc --no-owner --no-acl | cat > ~/backups/expense-$(date +%F).dump
```

`restart: unless-stopped` + `systemctl enable docker` bring the stack back after a
reboot or power blip.

## Keep secrets out of git

`deploy/.env` holds the bot token, DB password, and tunnel token — it's
git-ignored (`deploy/.gitignore`). Keep it that way; never commit real values.
