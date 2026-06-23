# Merge Expenses + Reports into one "Spending" tab

**Date:** 2026-06-23
**Status:** Approved design, ready for implementation plan

## Goal

The web app currently has two separate navbar tabs:

- **Expenses** ([web/src/screens/Expenses.tsx](../../../web/src/screens/Expenses.tsx)) — a flat, unfiltered list of all expenses.
- **Reports** ([web/src/screens/Reports.tsx](../../../web/src/screens/Reports.tsx)) — a from/to date range (default: this month → today), a base-currency grand total, a pie chart + "by category" breakdown, and a by-month bar chart when the range spans more than one month.

Merge them into a single **Spending** tab. The analytics (range, total, charts, breakdown) stay exactly as they are today, and the period's actual expense transactions are shown as a list **below** the breakdown — all bound to the same date range. Tapping a category drills the list down to that category.

This makes the Spending tab a complete "what happened this period" view: analyze at the top, browse the underlying transactions below.

## Decisions (settled during brainstorming)

1. **Merge, not duplicate.** The separate Expenses tab is removed. Spending is the single place to both analyze and browse a period's expenses. No "all-time unfiltered list" is retained — the list is always scoped to the selected range.
2. **Tab name: "Spending".**
3. **Row amounts: original currency only.** Each expense row shows the amount as entered, in its own currency code (exactly like today's Expenses list). No conversion, no backend change. The list rows are not expected to visibly sum to the converted total above — the charts/total provide the base-currency picture, the list is the faithful transaction record.
4. **Category drill-down: tap to filter.** Tapping a category filters the expense list to just that category. Filtering affects the **list only** — the total, pie, and breakdown above continue to show the whole period.

## Architecture

### Navigation & routing

Files affected: `web/src/router.tsx`.

- The `TABS` array collapses its two entries (`/expenses` "Expenses", `/reports` "Reports") into one:
  `{ to: "/spending", label: "Spending", Icon: IconReports }`.
  - The existing Reports/chart icon (`IconReports`) is reused. (Trivially swappable to `IconList` if preferred later.)
- A single route `/spending` renders the new merged screen.
- `/expenses` and `/reports` remain as **redirects** to `/spending` so existing links/bookmarks don't break.
- The header-title mapping (route → title in the Layout component) is updated so `/spending` reads "Spending."

### Screens

- New `web/src/screens/Spending.tsx` — the merged screen (described below).
- `web/src/screens/Reports.tsx` and `web/src/screens/Expenses.tsx` are deleted; their logic moves into `Spending.tsx`.

### Screen layout (top → bottom)

All sections are driven by **one** from/to range object, defaulting to this month → today (identical to today's Reports behavior).

1. **Range picker** — unchanged from Reports: two native `<input type="date">` fields (From with `max={to}`, To with `min={from}`).
2. **Total hero** — unchanged: base-currency grand total from `useReport(range)`.
3. **Charts + breakdown** — unchanged from Reports:
   - Pie chart + "by category" list, always shown when there is data.
   - "By category / By month" segmented toggle and the by-month bar chart, shown only when the range spans more than one month (`from.slice(0,7) !== to.slice(0,7)`).
4. **Expense list (new)** — shown below the breakdown. Reuses the existing Expenses row markup: emoji avatar, category name, `date · note` subline, **original-currency** amount, and a per-row delete button. Sorted newest-first.

### Category drill-down

- A `selectedCategoryId: number | null` state lives on the `Spending` screen.
- Tapping a category **in the breakdown list** — and clicking its **pie slice** — sets `selectedCategoryId`. The active breakdown row / pie slice gets a selected visual treatment.
- When a category is active, a **filter chip** (`Food ✕`) appears just above the expense list. The list narrows to expenses in that category.
- Clearing: tapping the chip's ✕, or tapping the already-active category again, resets `selectedCategoryId` to `null`.
- **Scope:** the filter affects the **expense list only**. The total, pie, and "by category" breakdown continue to reflect the entire period regardless of the selected category.

### Data flow

- The screen calls both existing hooks with the same range:
  - `useReport(range)` — for total, pie, and breakdowns (unchanged).
  - `useExpenses({ from, to })` — for the period's transactions. This hook already supports `from`/`to`/`categoryId` filters ([web/src/api/hooks.ts:33](../../../web/src/api/hooks.ts#L33)); only `from`/`to` are used.
- **No backend change.** Category drill-down is applied **client-side** by filtering the already-fetched expense rows on `categoryId`. Changing the active category does not trigger a refetch (the period's expense set is small).
- Deletion reuses `useDeleteExpense()`, which already invalidates both the `expenses` and `report` queries, keeping the charts and list in sync after a delete.

### Empty / edge states

- **Nothing spent in the period:** the existing "Nothing spent in {range}" card (shown when `report.byCategory` is empty) covers this; the expense list simply renders nothing in that case.
- **Drill-down category with no rows in view:** the list shows a small "No expenses in this category" line. (In practice a category only appears in the breakdown if it has expenses in the period, so this is a defensive case.)

## Out of scope (YAGNI)

- No converted/base-currency amounts on individual rows; no new or modified backend endpoints.
- The charts and total do **not** react to the category filter (list-only drill-down).
- No additional sorting, grouping, or filtering controls on the list beyond the category drill-down.

## Testing

- Manual verification in the running web app (the app uses Telegram Mini App auth; existing manual flows apply):
  - Spending tab shows range, total, charts, breakdown, and the period's expense list together.
  - Changing from/to updates total, charts, and the list consistently.
  - Tapping a category in the breakdown (and on the pie) filters the list; the chip clears it; charts/total stay whole-period.
  - Deleting an expense from the list updates both the list and the charts.
  - `/expenses` and `/reports` redirect to `/spending`; header title reads "Spending."
- If the project has component/unit tests for these screens, port/replace any Reports/Expenses-specific tests to target `Spending.tsx`.
