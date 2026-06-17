# Telegram Mini App — Expense Tracker — Design

**Date:** 2026-06-17
**Status:** Approved (design)

## 1. Summary

A multi-user expense tracker delivered as a Telegram Mini App. Any Telegram
user can open the app, log expenses with categories, view reports and charts,
set monthly budgets, and track spending across multiple currencies. There are
no passwords: identity comes from Telegram itself.

The app ships as a **single deployable unit** — an ASP.NET Core service that
serves both the REST API and the built React frontend — running on a managed
PaaS with a managed PostgreSQL database.

## 2. Goals & Non-Goals

### Goals
- Log an expense quickly: amount, category, date, optional note.
- Reports & charts: spending by category and by month.
- Budgets: monthly limits per category (and an optional overall limit), with
  remaining/over indicators.
- Multi-currency: enter expenses in any currency; reports roll up to a chosen
  base currency.
- Multi-user: every Telegram user gets their own isolated data, auto-provisioned
  on first launch.

### Non-Goals (YAGNI for now)
- Admin dashboard / user-management UI.
- Billing, subscription tiers, or rate-limiting tiers.
- Shared/household budgets or expense splitting between users.
- Bank/account syncing or receipt OCR.
- Native mobile apps (the Telegram Mini App webview is the only client).

## 3. Stack

| Layer       | Choice                                              |
|-------------|-----------------------------------------------------|
| Frontend    | React + TypeScript (Vite), TanStack Query, React Router, Recharts, `@telegram-apps/sdk-react` |
| Backend     | ASP.NET Core Web API (C#), EF Core + Npgsql         |
| Database    | PostgreSQL (managed PaaS add-on)                    |
| Bot         | Thin Telegram bot (launcher only)                   |
| FX rates    | Frankfurter API (free, no key, ECB-based)           |
| Hosting     | Managed PaaS (Railway / Fly.io / Render / Azure), single container |
| Packaging   | Multi-stage Docker image                            |

## 4. Architecture

```
┌─────────────┐      opens Mini App      ┌──────────────────────────┐
│  Telegram   │ ───────────────────────► │  PaaS service (1 container)
│   client    │                          │                          │
│ (phone/web) │  ◄── static React app ── │  ASP.NET Core            │
└─────────────┘      + /api/* calls      │   ├─ serves wwwroot/     │ ← built React (Vite)
       │                                  │   ├─ /api/* endpoints    │
       │ /start → "Open app" button       │   └─ validates initData  │
       ▼                                  │                          │
┌─────────────┐                           │  PostgreSQL (PaaS add-on)│
│  Bot (thin) │                           └──────────┬───────────────┘
│  launcher   │                                      │ daily fetch
└─────────────┘                           ┌──────────▼───────────────┐
                                           │ Frankfurter FX API (free)│
                                           └──────────────────────────┘
```

**Why one container:** serving the React build from the backend's `wwwroot`
means same-origin requests (no CORS), one URL to register with BotFather, and
one thing to deploy and monitor. Best fit for a small project on a PaaS.

### Components
1. **Telegram Bot (thin launcher).** Created via BotFather. Handles `/start`
   and exposes a menu button that opens the Mini App URL. No business logic.
2. **React + TS frontend.** Built by Vite into static assets served from
   `wwwroot`. Reads `initData`, applies Telegram's theme, uses the native Main
   Button and haptics.
3. **ASP.NET Core Web API.** REST endpoints under `/api/*`. Validates Telegram
   `initData`, enforces per-user data isolation, talks to PostgreSQL, fetches
   and caches FX rates.
4. **PostgreSQL.** Managed add-on. Holds all user and domain data.
5. **Frankfurter FX API.** Daily exchange rates, cached locally.

## 5. Authentication & Authorization

Passwordless, based on Telegram's `initData` mechanism:

1. When the Mini App launches, Telegram provides a signed `initData` string.
2. The frontend sends `initData` on every API request (e.g. an
   `Authorization: tma <initData>` header).
3. The backend validates the HMAC-SHA256 signature using the bot token
   (per Telegram's documented algorithm) and rejects requests that fail or
   whose `auth_date` is too old.
4. From the validated payload the backend extracts the Telegram user ID. On
   first sight, it auto-provisions a `User` row (and seeds default categories).
5. **Per-user isolation:** every query for expenses/categories/budgets/settings
   is filtered by the authenticated `UserId`. This is the core security
   invariant and is covered by explicit tests.

Implemented as ASP.NET Core middleware / an authentication handler that turns a
valid `initData` into the current `User`, so endpoints can rely on it.

## 6. Data Model (EF Core + PostgreSQL)

- **User** — `Id, TelegramUserId (unique), FirstName, Username, CreatedAt`.
- **Category** — `Id, UserId (FK), Name, Emoji, IsArchived`. Default set seeded
  per user on first login (e.g. Food, Transport, Rent, Entertainment, Health).
- **Expense** — `Id, UserId (FK), Amount (decimal), CurrencyCode, CategoryId
  (FK), SpentOn (date), Note, CreatedAt`. Amount is stored in the **original**
  currency the user entered.
- **Budget** — `Id, UserId (FK), CategoryId (FK, nullable = overall budget),
  Period (Monthly), LimitAmount (decimal), CurrencyCode`. At most one active
  budget per category per user.
- **ExchangeRate** — `Id, BaseCurrency, QuoteCurrency, Rate (decimal),
  AsOfDate`. **Global/shared** across users.
- **Setting** — `UserId (FK, unique), BaseCurrency`, plus future per-user prefs.

Money is stored as `decimal` (never floating point). Per-user tables are
indexed on `UserId` (and `UserId + SpentOn` for the expense list/reports).

## 7. API Surface

All endpoints are under `/api` and require a valid `initData` (resolved to the
current user). All reads/writes are implicitly scoped to that user.

- `GET /expenses?from=&to=&categoryId=` — list with filters
- `POST /expenses` — create
- `PUT /expenses/{id}` — update
- `DELETE /expenses/{id}` — delete
- `GET /categories` — list (incl. defaults)
- `POST /categories`, `PUT /categories/{id}` — create/update
- `GET /budgets`, `PUT /budgets` — read and upsert monthly limits
- `GET /reports/summary?from=&to=` — totals by category and by month, all
  converted to the user's base currency
- `GET /settings`, `PUT /settings` — base currency and prefs

## 8. Multi-Currency

- The user picks a **base currency** in settings (default e.g. USD).
- Each expense keeps its **original currency** — the real amount spent is never
  lost or rewritten.
- A **daily job** (and/or lazy fetch-on-demand) pulls rates from Frankfurter and
  caches them in `ExchangeRate`, so reports work even if the API is briefly down
  and we don't hammer it.
- **Reports convert** each expense to the base currency using the rate **as of
  the expense's date** (historical accuracy), not today's rate. Budgets compare
  in base currency too.

## 9. Frontend Detail

- **Build:** Vite + React + TypeScript. Output served from backend `wwwroot`.
- **Data layer:** TanStack Query (caching, optimistic updates), a typed API
  client that attaches `initData`.
- **Routing:** React Router. Screens: **Add Expense** (the fast path),
  **Expenses** (list/edit), **Reports** (charts), **Budgets**, **Settings**.
- **Telegram integration:** `@telegram-apps/sdk-react` for init data, theme
  params (auto light/dark to match the client), the native Main Button for
  "Save expense", and haptic feedback.
- **Charts:** Recharts — pie (spend by category) and bar (spend by month).

## 10. Deployment

- **Dockerfile** (multi-stage): build React → publish .NET → final runtime image
  that serves API + static files.
- **PaaS:** one service + a PostgreSQL add-on. Configuration via environment
  variables:
  - `BotToken` — Telegram bot token (used for initData validation).
  - `ConnectionStrings__Default` — PostgreSQL connection string.
  - (Frankfurter needs no key.)
- **BotFather setup:** register the Mini App with the PaaS HTTPS URL; configure
  the bot's menu button to launch it.
- **Migrations:** EF Core migrations applied on startup or via a release step.

## 11. Testing

- **Backend (xUnit):**
  - `initData` HMAC validation — valid, tampered, expired, missing
    (security-critical).
  - Per-user isolation — user A cannot read/modify user B's data.
  - Currency conversion math — historical-rate selection, rounding.
  - API integration tests against a test PostgreSQL (or Testcontainers).
- **Frontend:** kept thin; a few component tests for critical forms, plus
  manual verification inside Telegram. (Expandable later if the UI grows.)

## 12. Open Questions / Future

- Optional: quick-add an expense by sending the bot a chat message (e.g.
  "12.50 lunch") — nice future enhancement, not in scope now.
- Optional: export to CSV.
- Choice of specific PaaS (Railway vs Fly.io vs Render vs Azure) deferred to
  implementation; all satisfy the requirements above.
