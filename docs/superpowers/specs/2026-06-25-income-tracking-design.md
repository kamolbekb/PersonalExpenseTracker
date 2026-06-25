# Income tracking

**Date:** 2026-06-25
**Status:** Approved design, ready for implementation plan

## Goal

Add optional **income tracking** to the Personal Expense Tracker. A toggle in Settings (default **off**) enables it. When on, a new **Income** tab appears in the navbar, showing a Spending-style period view (date range, base-currency total, charts, and a list of incomes for the period) plus an **+ Add income** button. Income is tracked entirely **separately** from expenses — its own data, its own categories, its own report — and never mixes into expense totals.

This builds directly on the merged **Spending** screen ([web/src/screens/Spending.tsx](../../../web/src/screens/Spending.tsx)); the two period views are ~95% identical and will share one presentational component.

## Decisions (settled during brainstorming)

1. **Separate income categories.** Income has its own category set, distinct from expense categories — so the income charts can group by category just like Spending.
2. **Seed defaults only (no management UI this version).** Each user is auto-provisioned a fixed set of income categories. No add/edit/archive UI for them yet — a possible follow-up.
3. **Add income via a dedicated screen.** A prominent `+ Add income` button on the Income tab navigates to a full add-income form (mirror of the Add-expense screen). Keeps the dense Income page clean.
4. **Toggle persisted on the backend.** The enable flag lives in the user's `Setting` row (like base currency), so it syncs across devices. Turning it **off only hides** the tab — income data is never deleted.
5. **Shared component (option A).** Extract one presentational component that both the Spending and Income tabs render, refactoring the existing `Spending.tsx` to use it. Avoids duplicating ~250 lines.
6. **Income tab placement.** When enabled: `Add · Spending · Income · Budgets · Rates` (5 tabs on, 4 off).

## Architecture

### Backend (ASP.NET Core + EF Core + PostgreSQL, clean architecture)

Mirror the existing expense slice. Files follow the established structure (`ExpenseTracker.Domain/Entities`, `…Application/<Feature>`, `…Api/Endpoints`, `…Infrastructure/Persistence`).

**New entities** (`ExpenseTracker.Domain/Entities/`):
- `Income`: `int Id`, `int UserId`, `decimal Amount` (decimal(18,2)), `string CurrencyCode = "UZS"`, `int IncomeCategoryId`, `DateOnly ReceivedOn`, `string? Note`, `DateTimeOffset CreatedAt`. Mirrors `Expense` (with `ReceivedOn` in place of `SpentOn` and `IncomeCategoryId` in place of `CategoryId`).
- `IncomeCategory`: `int Id`, `int UserId`, `string Name = ""`, `string Emoji = ""`. A separate table from expense `Categories` (full isolation; no `Type` discriminator added to the existing `Category`, so expense flows are untouched). No `IsArchived` for v1 (no management UI).

**Settings change** (`ExpenseTracker.Domain/Entities/Setting.cs`):
- Add `bool IncomeTrackingEnabled` (default `false`).

**DbContext** (`ApplicationDbContext`): add `DbSet<Income> Incomes` and `DbSet<IncomeCategory> IncomeCategories`. Configure: `Income` indexed on `(UserId, ReceivedOn)`, FK `IncomeCategoryId → IncomeCategories.Id`; `IncomeCategory` indexed on `UserId`.

**Migration**: one new EF Core migration adding `Incomes` and `IncomeCategories` tables and the `Settings.IncomeTrackingEnabled` column (existing rows default to `false`).

**Provisioning** (`UserProvisioningService.GetOrCreateAsync`): on new-user creation (and as a backfill for existing users on first income-categories fetch — see endpoint note), seed default income categories: 💼 Salary, 💻 Freelance, 🎁 Gifts, 📈 Investments, 💰 Other. The existing default-`Setting` creation gains `IncomeTrackingEnabled = false`.

**DTOs** (`…Application/Incomes/`):
- `IncomeInput(decimal Amount, string CurrencyCode, int IncomeCategoryId, DateOnly ReceivedOn, string? Note)`
- `IncomeDto(int Id, decimal Amount, string CurrencyCode, int IncomeCategoryId, DateOnly ReceivedOn, string? Note)`
- `IncomeCategoryDto(int Id, string Name, string Emoji)`

**Endpoints** (`…Api/Endpoints/`):
- `IncomeEndpoints` under `/api/incomes`: `GET` (query `from?`, `to?`, `categoryId?`; ordered by `ReceivedOn` desc, `Id` desc), `POST`, `PUT/{id}`, `DELETE/{id}` — same validation/ownership rules as `ExpenseEndpoints` (Amount > 0, CurrencyCode not empty, `IncomeCategoryId` owned by user).
- `IncomeCategoryEndpoints`: `GET /api/income-categories` → `List<IncomeCategoryDto>` for the current user. If the user has no income categories yet (existing users created before this feature), seed the defaults on read so the list is never empty.
- `IncomeReportEndpoints`: `GET /api/reports/income-summary?from=&to=` → the **same** `ReportSummary` shape `{ BaseCurrency, GrandTotal, ByCategory[], ByMonth[] }`. Reuse the conversion (`IExchangeRateService` + `CurrencyConverter`) and aggregation logic from `ReportService` — factor the shared aggregation so both expense and income summaries use it, or a parallel `IncomeReportService` mirroring `ReportService`. `ByCategory` groups by income category.

**Settings endpoint** (`SettingEndpoints` / `SettingDto`): extend `SettingDto` to `record SettingDto(string BaseCurrency, bool IncomeTrackingEnabled)`. `GET` returns the flag; `PUT` accepts and persists it (alongside base currency).

### Frontend (React 19 + TS + Vite + TanStack Query + Recharts)

**Types** ([web/src/api/types.ts](../../../web/src/api/types.ts)):
- `Income { id; amount; currencyCode; categoryId: number /* incomeCategoryId, named categoryId in the client for the shared component */; receivedOn: string; note: string | null }` — to keep the shared component generic, the client normalizes the row to a common shape (see below).
- `IncomeInput { amount; currencyCode; incomeCategoryId; receivedOn; note }`.
- `IncomeCategory { id; name; emoji }`.
- Extend `Settings` to `{ baseCurrency: string; incomeTrackingEnabled: boolean }`.
- Income report reuses the existing `ReportSummary` / `CategoryTotal` / `MonthTotal` types (identical shape).

**Hooks** ([web/src/api/hooks.ts](../../../web/src/api/hooks.ts)) — mirror the expense hooks:
- `useIncomes({ from?, to?, categoryId? })` → `Income[]`
- `useCreateIncome()` (invalidates `["incomes"]` and `["income-report"]`)
- `useDeleteIncome()` (same invalidations)
- `useIncomeCategories()` → `IncomeCategory[]`
- `useIncomeReport({ from, to })` → `ReportSummary` (queries `/reports/income-summary`)
- `useUpdateSettings` already exists; it now round-trips `incomeTrackingEnabled` too.

**Shared component** `web/src/components/PeriodLedger.tsx` — extracted from the current `Spending.tsx`. **Purely presentational**; the parent owns range state + data fetching. Props:
- `from: string; to: string; onFromChange(v): void; onToChange(v): void`
- `report?: ReportSummary`
- `rows?: { id: number; amount: number; currencyCode: string; categoryId: number; date: string; note: string | null }[]` (parent normalizes `Expense.spentOn`/`Income.receivedOn` → `date`)
- `categories?: { id: number; name: string; emoji: string }[]`
- `onDelete(id: number): void`
- `headerAction?: React.ReactNode` (the `+ Add income` button for Income; omitted for Spending)

The component owns only ephemeral UI state — `view` (category/month) and `selectedCategoryId` (drill-down) — and renders the range picker, total hero, segmented toggle, pie + by-category breakdown, by-month bar chart, the row list (original-currency amounts + delete), and the drill-down chip, exactly as Spending does today. Changing `from`/`to` resets the drill-down. The category drill-down filters the row list only.

**Screens:**
- `Spending.tsx` (refactor): holds `[from,to]` state, calls `useReport`, `useExpenses`, `useCategories`, `useDeleteExpense`; normalizes rows; renders `<PeriodLedger … headerAction={undefined} />`.
- `Income.tsx` (new): same shape but `useIncomeReport`, `useIncomes`, `useIncomeCategories`, `useDeleteIncome`; renders `<PeriodLedger … headerAction={<Link className="btn btn--primary btn--block" to="/income/add">+ Add income</Link>} />` placed at the top.
- `AddIncome.tsx` (new): mirror of [AddExpense.tsx](../../../web/src/screens/AddExpense.tsx) — amount + currency, income-category chips (`useIncomeCategories`), date (`receivedOn`), note, save via `useCreateIncome`; Telegram MainButton labeled "Save income" (block button fallback outside Telegram). On success, clears the form (same UX as Add-expense).

**Routing & navbar** ([web/src/router.tsx](../../../web/src/router.tsx)):
- Import `Income`, `AddIncome`. Add routes `{ path: "income", element: <Income /> }` and `{ path: "income/add", element: <AddIncome /> }`.
- `TITLES`: `/income` → "Income", `/income/add` → "Add income".
- Navbar: `Layout` calls `useSettings()`; builds the tab list as `Add · Spending · [Income if settings.incomeTrackingEnabled] · Budgets · Rates`. Income uses a suitable existing icon (e.g. an income/wallet glyph — reuse an existing icon or add one small SVG to `components/icons.tsx`).

**Settings toggle** ([web/src/screens/Settings.tsx](../../../web/src/screens/Settings.tsx)): a "Track income" on/off control (styled like the existing `.segmented` On/Off, or a checkbox row) bound to `settings.incomeTrackingEnabled`, saved through `useUpdateSettings` (sent together with base currency). A short hint explains income appears as its own tab and is tracked separately.

### Data flow

- Income mirrors expense end-to-end: client → `/api/incomes` & `/api/reports/income-summary` (Telegram `tma` auth, `UserId`-scoped) → EF Core. Report conversion uses the same CBU-rate service as expenses; income rows display in their **original currencies**, the total/charts in base currency (same convention as Spending).
- The navbar reads the settings flag; toggling it in Settings flips tab visibility after the settings query refetches/invalidates.

### Error / edge handling

- Add-income validation mirrors add-expense (amount > 0, category required).
- Empty states reuse the Spending patterns: "Nothing earned in {range}" (income wording) when the income report is empty; "No income in this category" when a drill-down category is empty.
- Existing users (provisioned before this feature) get default income categories seeded on first `/api/income-categories` read, so the add form is never empty and `IncomeTrackingEnabled` defaults to off.

## Sequencing & dependencies

- This work **depends on the merged Spending screen** (option A refactors `Spending.tsx`). It is being developed on `feature/income-tracking`, branched from the Spending work. The Spending branch should be merged to `main` around the same time; income carries the same Spending commits and merges cleanly.
- Backend and frontend can be built in order: (1) backend entities + migration + endpoints + provisioning + settings flag, (2) shared `PeriodLedger` extraction + `Spending.tsx` refactor, (3) `Income.tsx` + `AddIncome.tsx` + hooks/types, (4) settings toggle + navbar wiring.

## Out of scope (YAGNI)

- Income category management UI (add/edit/archive); editing existing incomes via UI (backend `PUT` may exist for parity, but no edit screen).
- Converting/normalizing row amounts to base currency on the list (original currency only, like expenses).
- Any combined "net = income − expenses" view, budgets-for-income, or income in the existing expense reports.
- A `Type` discriminator on the existing `Category` table (income uses its own table).

## Testing

- **Backend**: mirror the existing `ExpenseTracker.Tests` integration tests for the income slice — income CRUD + ownership scoping, `/api/reports/income-summary` aggregation and currency conversion, the `IncomeTrackingEnabled` settings round-trip, and provisioning seeding default income categories.
- **Frontend**: no test framework exists; verification is `npm run build` (`tsc -b` + vite build) + `npm run lint`, plus manual/visual verification driving the app in a browser with mocked `/api` endpoints (the Vite mock-config + Playwright approach used to verify the Spending screen) — screenshots of: Income tab (default month, charts + list), category drill-down, the Add-income screen, and the Settings toggle hiding/showing the tab.
