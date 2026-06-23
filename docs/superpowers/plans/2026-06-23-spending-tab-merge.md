# Spending Tab Merge — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Merge the separate "Expenses" and "Reports" navbar tabs into a single "Spending" tab that shows the period's analytics (range, total, charts, category breakdown) with the period's actual expense list below it, plus tap-a-category drill-down on the list.

**Architecture:** Create one new screen `Spending.tsx` that combines today's `Reports.tsx` (range picker, total hero, pie + by-category breakdown, by-month bar chart) with the date-filtered expense list from `Expenses.tsx`. Both halves are driven by the same `{ from, to }` range — `useReport(range)` for aggregates, `useExpenses({ from, to })` for the rows. Category drill-down is client-side state filtering the already-fetched rows. Then cut the router over to `/spending`, redirect the old paths, and delete the two old screens.

**Tech Stack:** React 19 + TypeScript, Vite, React Router v7, TanStack Query, Recharts.

## Global Constraints

- **No backend changes.** `useExpenses({ from, to })` already supports date filtering ([web/src/api/hooks.ts:33](../../../web/src/api/hooks.ts#L33)); the report endpoint is unchanged.
- **Indentation: hard tabs.** The web codebase indents with tabs — match it exactly in all new/edited code.
- **Expense rows show original currency only** (amount + `currencyCode`, like today's Expenses list). No conversion.
- **Drill-down filters the expense list only** — the total, pie, and by-category breakdown always reflect the whole period.
- **No new CSS.** Reuse existing classes: `.card`, `.card--flush`, `.hero`, `.eyebrow`, `.segmented`, `.list`, `.item`, `.item__main/__title/__sub/__amount/__cur`, `.avatar`, `.dot`, `.empty`, `.hint`, `.chip`, `.chip--active`, `.icon-btn`.
- **No test framework exists** in `web/` (no vitest/testing-library). Verification per task is: `npm run build` (type-check via `tsc -b` + vite build), `npm run lint` (eslint), and manual checks in `npm run dev`. Do **not** add a test harness — out of scope.
- All `npm` commands run from the `web/` directory.

## File Structure

- **Create** `web/src/screens/Spending.tsx` — the merged screen (range + total + charts + breakdown + date-filtered expense list + category drill-down). One screen, one responsibility: "view a period's spending."
- **Modify** `web/src/router.tsx` — replace the two tab/route entries with one `/spending` entry; redirect `/expenses` and `/reports` to `/spending`; update title map and imports.
- **Delete** `web/src/screens/Reports.tsx` and `web/src/screens/Expenses.tsx` — their logic now lives in `Spending.tsx`.

---

### Task 1: Create the merged Spending screen

**Files:**
- Create: `web/src/screens/Spending.tsx`

**Interfaces:**
- Consumes (all already exist):
  - `useReport({ from, to }) → { data?: ReportSummary }` where `ReportSummary = { baseCurrency: string; grandTotal: number; byCategory: { categoryId: number; categoryName: string; total: number }[]; byMonth: { month: string; total: number }[] }` ([web/src/api/types.ts:49](../../../web/src/api/types.ts#L49)).
  - `useExpenses({ from, to }) → { data?: Expense[] }` where `Expense = { id: number; amount: number; currencyCode: string; categoryId: number; spentOn: string; note: string | null }` ([web/src/api/types.ts:8](../../../web/src/api/types.ts#L8)).
  - `useCategories() → { data?: Category[] }` where `Category = { id: number; name: string; emoji: string; isArchived: boolean }`.
  - `useDeleteExpense() → { mutate(id: number) }` (invalidates `expenses` and `report` queries).
  - `localDateString(d: Date): string` from `../lib/date`; `IconTrash` from `../components/icons`.
- Produces: a default-exported `Spending` React component (consumed by Task 2's router).

- [ ] **Step 1: Create `web/src/screens/Spending.tsx` with the full component**

```tsx
import { useState } from "react";
import {
	Bar,
	BarChart,
	Cell,
	Pie,
	PieChart,
	ResponsiveContainer,
	Tooltip,
	XAxis,
} from "recharts";
import {
	useCategories,
	useDeleteExpense,
	useExpenses,
	useReport,
} from "../api/hooks";
import { IconTrash } from "../components/icons";
import { localDateString } from "../lib/date";

// iOS system colors.
const COLORS = [
	"#007aff",
	"#34c759",
	"#ff9500",
	"#ff2d55",
	"#5856d6",
	"#5ac8fa",
	"#af52de",
	"#ffcc00",
];

const fmt = (n: number) =>
	n.toLocaleString("en-US", { maximumFractionDigits: 0 });

const fmtAmount = (n: number) =>
	n.toLocaleString("en-US", {
		minimumFractionDigits: 2,
		maximumFractionDigits: 2,
	});

const fmtDate = (iso: string) => {
	const d = new Date(iso + "T00:00:00");
	return d.toLocaleDateString("en-US", { month: "short", day: "numeric" });
};

// First day of the current month (the default "from").
const monthStart = () => {
	const d = new Date();
	return localDateString(new Date(d.getFullYear(), d.getMonth(), 1));
};

export default function Spending() {
	// Always-visible date range, defaulting to this month → today.
	const [from, setFrom] = useState(monthStart);
	const [to, setTo] = useState(() => localDateString(new Date()));

	// Drill-down: which category the expense list is filtered to (null = all).
	const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(
		null,
	);

	const range = { from, to };
	const title = `${from} → ${to}`;
	// spans >1 month when the from/to year-months differ → offer By-month
	const isRange = from.slice(0, 7) !== to.slice(0, 7);

	const [view, setView] = useState<"category" | "month">("category");

	const { data: report } = useReport(range);
	const { data: expenses } = useExpenses(range);
	const { data: categories } = useCategories();
	const del = useDeleteExpense();

	const catFor = (id: number) => categories?.find((c) => c.id === id);

	const hasData = (report?.byCategory.length ?? 0) > 0;

	// Changing the range clears any active drill-down.
	const changeFrom = (v: string) => {
		setFrom(v);
		setSelectedCategoryId(null);
	};
	const changeTo = (v: string) => {
		setTo(v);
		setSelectedCategoryId(null);
	};

	const toggleCategory = (categoryId: number) =>
		setSelectedCategoryId((cur) => (cur === categoryId ? null : categoryId));

	const visibleExpenses = (expenses ?? []).filter(
		(e) => selectedCategoryId === null || e.categoryId === selectedCategoryId,
	);
	const selectedCat =
		selectedCategoryId !== null ? catFor(selectedCategoryId) : null;

	return (
		<div className="screen">
			<section className="card">
				<div className="row" style={{ gap: 10 }}>
					<div className="field grow">
						<label>From</label>
						<input
							type="date"
							value={from}
							max={to}
							onChange={(e) => changeFrom(e.target.value)}
						/>
					</div>
					<div className="field grow">
						<label>To</label>
						<input
							type="date"
							value={to}
							min={from}
							onChange={(e) => changeTo(e.target.value)}
						/>
					</div>
				</div>
			</section>

			<section className="card hero">
				<p className="eyebrow">Total · {title}</p>
				<div className="amount-display" style={{ fontSize: 46, marginTop: 4 }}>
					{fmt(report?.grandTotal ?? 0)}
					<span
						style={{
							fontSize: 17,
							fontWeight: 600,
							marginLeft: 8,
							color: "var(--label-2)",
						}}
					>
						{report?.baseCurrency ?? "UZS"}
					</span>
				</div>
			</section>

			{!hasData && (
				<div className="card empty">
					<span className="emoji">📊</span>
					Nothing spent in {title}.
				</div>
			)}

			{hasData && report && (
				<>
					{/* By category is always available; By month only for a multi-month range */}
					{isRange && (
						<div className="segmented" role="tablist">
							<button
								role="tab"
								aria-selected={view === "category"}
								onClick={() => setView("category")}
							>
								By category
							</button>
							<button
								role="tab"
								aria-selected={view === "month"}
								onClick={() => setView("month")}
							>
								By month
							</button>
						</div>
					)}

					{(!isRange || view === "category") && (
						<section className="card">
							<ResponsiveContainer width="100%" height={210}>
								<PieChart>
									<Pie
										data={report.byCategory}
										dataKey="total"
										nameKey="categoryName"
										innerRadius={52}
										outerRadius={92}
										paddingAngle={2}
										stroke="none"
										isAnimationActive={false}
									>
										{report.byCategory.map((c, i) => (
											<Cell
												key={c.categoryId}
												fill={COLORS[i % COLORS.length]}
												style={{ cursor: "pointer" }}
												opacity={
													selectedCategoryId === null ||
													selectedCategoryId === c.categoryId
														? 1
														: 0.35
												}
												onClick={() => toggleCategory(c.categoryId)}
											/>
										))}
									</Pie>
									<Tooltip
										formatter={(v: unknown) =>
											`${fmt(Number(v))} ${report.baseCurrency}`
										}
									/>
								</PieChart>
							</ResponsiveContainer>
							<ul className="list" style={{ marginTop: 6 }}>
								{report.byCategory.map((c, i) => {
									const pct = report.grandTotal
										? Math.round((c.total / report.grandTotal) * 100)
										: 0;
									const active = selectedCategoryId === c.categoryId;
									return (
										<li
											key={c.categoryId}
											className="item"
											aria-selected={active}
											onClick={() => toggleCategory(c.categoryId)}
											style={{
												cursor: "pointer",
												opacity:
													selectedCategoryId === null || active ? 1 : 0.5,
											}}
										>
											<span
												className="dot"
												style={{ background: COLORS[i % COLORS.length] }}
											/>
											<div className="item__main">
												<div className="item__title">{c.categoryName}</div>
												<div className="item__sub">{pct}% of total</div>
											</div>
											<div className="item__amount">
												{fmt(c.total)}
												<span className="item__cur">{report.baseCurrency}</span>
											</div>
										</li>
									);
								})}
							</ul>
						</section>
					)}

					{isRange && view === "month" && (
						<section className="card">
							<ResponsiveContainer width="100%" height={240}>
								<BarChart data={report.byMonth} barCategoryGap="28%">
									<XAxis
										dataKey="month"
										tickLine={false}
										axisLine={false}
										tick={{ fontSize: 11 }}
									/>
									<Tooltip
										cursor={{ fill: "#78788014" }}
										formatter={(v: unknown) =>
											`${fmt(Number(v))} ${report.baseCurrency}`
										}
									/>
									<Bar
										dataKey="total"
										fill="#007aff"
										radius={[6, 6, 0, 0]}
										isAnimationActive={false}
									/>
								</BarChart>
							</ResponsiveContainer>
						</section>
					)}

					{/* Period's expense list (original currencies), with optional category filter */}
					{expenses && (
						<>
							{selectedCat ? (
								<button
									className="chip chip--active"
									style={{ alignSelf: "flex-start" }}
									onClick={() => setSelectedCategoryId(null)}
								>
									<span className="emoji">{selectedCat.emoji}</span>
									{selectedCat.name}
									<span style={{ marginLeft: 2 }}>✕</span>
								</button>
							) : (
								<p className="eyebrow">
									{visibleExpenses.length}{" "}
									{visibleExpenses.length === 1 ? "entry" : "entries"}
								</p>
							)}

							<section className="card card--flush">
								{visibleExpenses.length === 0 ? (
									<div className="empty" style={{ padding: 16 }}>
										No expenses in this category.
									</div>
								) : (
									<ul className="list">
										{visibleExpenses.map((e) => {
											const cat = catFor(e.categoryId);
											return (
												<li key={e.id} className="item">
													<div className="avatar">{cat?.emoji ?? "💸"}</div>
													<div className="item__main">
														<div className="item__title">
															{cat?.name ?? "—"}
														</div>
														<div className="item__sub">
															{fmtDate(e.spentOn)}
															{e.note ? ` · ${e.note}` : ""}
														</div>
													</div>
													<div className="item__amount">
														{fmtAmount(e.amount)}
														<span className="item__cur">{e.currencyCode}</span>
													</div>
													<button
														className="icon-btn"
														aria-label="Delete"
														onClick={() => del.mutate(e.id)}
													>
														<IconTrash />
													</button>
												</li>
											);
										})}
									</ul>
								)}
							</section>

							<p className="hint" style={{ textAlign: "center" }}>
								Showing amounts in their original currencies.
							</p>
						</>
					)}
				</>
			)}
		</div>
	);
}
```

- [ ] **Step 2: Type-check and build**

Run (from `web/`): `npm run build`
Expected: PASS — `tsc -b` reports no errors and vite build completes. (No unused imports/vars; every imported symbol is used.)

- [ ] **Step 3: Lint**

Run (from `web/`): `npm run lint`
Expected: PASS — no eslint errors for `src/screens/Spending.tsx`.

- [ ] **Step 4: Commit**

```bash
git add web/src/screens/Spending.tsx
git commit -m "feat(web): add merged Spending screen (reports + period expense list + drill-down)"
```

---

### Task 2: Route the Spending tab and retire Expenses/Reports

**Files:**
- Modify: `web/src/router.tsx`
- Delete: `web/src/screens/Reports.tsx`, `web/src/screens/Expenses.tsx`

**Interfaces:**
- Consumes: default-exported `Spending` from Task 1 (`./screens/Spending`).
- Produces: navbar with a single `Spending` tab at `/spending`; `/expenses` and `/reports` redirect to `/spending`.

- [ ] **Step 1: Replace `web/src/router.tsx` with the updated version**

Replace the entire file contents with:

```tsx
import {
	createBrowserRouter,
	RouterProvider,
	NavLink,
	Navigate,
	Outlet,
	useLocation,
	useNavigate,
} from "react-router-dom";
import AddExpense from "./screens/AddExpense";
import Categories from "./screens/Categories";
import Budgets from "./screens/Budgets";
import Spending from "./screens/Spending";
import Settings from "./screens/Settings";
import Rates from "./screens/Rates";
import {
	IconAdd,
	IconReports,
	IconBudgets,
	IconRates,
	IconSettings,
} from "./components/icons";

const TABS = [
	{ to: "/", label: "Add", Icon: IconAdd, end: true },
	{ to: "/spending", label: "Spending", Icon: IconReports },
	{ to: "/budgets", label: "Budgets", Icon: IconBudgets },
	{ to: "/rates", label: "Rates", Icon: IconRates },
];

const TITLES: Record<string, string> = {
	"/": "Add expense",
	"/spending": "Spending",
	"/budgets": "Budgets",
	"/rates": "Rates & gold",
	"/settings": "Settings",
	"/categories": "Categories",
};

function Layout() {
	const { pathname } = useLocation();
	const navigate = useNavigate();
	const title = TITLES[pathname] ?? "Spending";

	return (
		<div className="app">
			<header className="app__header">
				<h1 className="app__title">{title}</h1>
				<button
					className="icon-btn"
					aria-label="Settings"
					onClick={() => navigate("/settings")}
				>
					<IconSettings />
				</button>
			</header>

			<main className="app__main">
				<Outlet />
			</main>

			<nav className="tabbar">
				<div className="tabbar__inner">
					{TABS.map(({ to, label, Icon, end }) => (
						<NavLink key={to} to={to} end={end}>
							<Icon />
							{label}
						</NavLink>
					))}
				</div>
			</nav>
		</div>
	);
}

const router = createBrowserRouter([
	{
		path: "/",
		element: <Layout />,
		children: [
			{ index: true, element: <AddExpense /> },
			{ path: "spending", element: <Spending /> },
			{ path: "expenses", element: <Navigate to="/spending" replace /> },
			{ path: "reports", element: <Navigate to="/spending" replace /> },
			{ path: "categories", element: <Categories /> },
			{ path: "budgets", element: <Budgets /> },
			{ path: "settings", element: <Settings /> },
			{ path: "rates", element: <Rates /> },
		],
	},
]);

export const AppRouter = () => <RouterProvider router={router} />;
```

- [ ] **Step 2: Delete the two old screens**

```bash
git rm web/src/screens/Reports.tsx web/src/screens/Expenses.tsx
```

- [ ] **Step 3: Type-check and build**

Run (from `web/`): `npm run build`
Expected: PASS — no dangling imports of `Reports`/`Expenses`, `IconList` no longer imported, no unused-symbol errors.

- [ ] **Step 4: Lint**

Run (from `web/`): `npm run lint`
Expected: PASS — no eslint errors.

- [ ] **Step 5: Manual verification in the dev app**

Run (from `web/`): `npm run dev`, open the app, and confirm:
1. Navbar shows `Add · Spending · Budgets · Rates` (no separate Expenses/Reports tabs); header reads "Spending".
2. Spending tab shows: from/to range (defaulting to this month → today), base-currency total, pie + by-category breakdown, and below it the period's expense list (original currencies) with delete buttons.
3. Changing From/To updates the total, charts, and the list together; the drill-down resets.
4. With a multi-month range, the "By category / By month" toggle and bar chart appear.
5. Tapping a category row (and a pie slice) filters the list to that category and shows the `emoji name ✕` chip; the non-selected breakdown rows/slices dim; tapping the chip (or the active category again) clears the filter. The total/pie/breakdown stay whole-period.
6. Deleting an expense from the list updates both the list and the charts.
7. Navigating to `/expenses` and `/reports` (e.g. via address bar) redirects to `/spending`.

- [ ] **Step 6: Commit**

```bash
git add web/src/router.tsx
git commit -m "feat(web): route Spending tab, redirect old expenses/reports, remove old screens"
```

---

## Self-Review

**Spec coverage:**
- Merge into one "Spending" tab → Task 2 (TABS, route, redirects, deletions). ✓
- Keep range/total/charts/breakdown unchanged → Task 1 carries the Reports JSX verbatim. ✓
- Date-filtered expense list below the breakdown → Task 1 (`useExpenses(range)` + list). ✓
- Row amounts in original currency only → Task 1 (`fmtAmount(e.amount)` + `e.currencyCode`). ✓
- Category drill-down filtering list only → Task 1 (`selectedCategoryId`, breakdown/pie `onClick`, chip, `visibleExpenses`); total/pie/breakdown untouched by the filter. ✓
- `/expenses` & `/reports` redirect; header reads "Spending" → Task 2. ✓
- Empty states (nothing spent; filtered-empty) → Task 1 ("Nothing spent" card; "No expenses in this category"). ✓
- No backend change; reuse existing CSS → satisfied (only `useExpenses` filters + existing classes). ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete code; commands have expected outcomes. ✓

**Type consistency:** `toggleCategory`, `selectedCategoryId`, `visibleExpenses`, `selectedCat`, `changeFrom`/`changeTo` are defined and used consistently within Task 1. `Spending` default export consumed by Task 2's `import Spending from "./screens/Spending"`. `IconList` removed from imports since its only use (the Expenses tab) is gone; `IconReports` retained for the Spending tab. `Navigate` added to the react-router-dom import for the redirects. ✓
