# Income Tracking — Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Income tab UI — a shared `PeriodLedger` component (refactored out of `Spending.tsx`), an `Income` screen and `AddIncome` screen, income types/hooks, a Settings "Track income" toggle, and a conditional navbar tab.

**Architecture:** Extract the Spending page's view into one presentational `PeriodLedger` component that both Spending and Income render (parent owns range state + data fetching; the component owns ephemeral view/drill-down state). Income consumes the income API mirrors. The navbar shows the Income tab only when `settings.incomeTrackingEnabled`.

**Tech Stack:** React 19 + TypeScript, Vite, React Router v7, TanStack Query, Recharts.

**Depends on:** the income backend endpoints (`/api/incomes`, `/api/income-categories`, `/api/reports/income-summary`, and `incomeTrackingEnabled` in `/api/settings`) from the backend plan. Frontend builds and lints without the backend running; runtime/visual verification uses a mock dev server (see Verification).

## Global Constraints

- **Indentation: hard tabs.** Match the existing web code exactly.
- **No new CSS.** Reuse existing classes only: `.screen`, `.card`, `.card--flush`, `.hero`, `.eyebrow`, `.segmented`, `.list`, `.item`, `.item__main/__title/__sub/__amount/__cur`, `.avatar`, `.dot`, `.empty`, `.hint`, `.chip`, `.chip--active`, `.icon-btn`, `.btn`, `.btn--primary`, `.btn--block`, `.row`, `.field`, `.grow`, `.amount-field`, `.amount-display`, `.chip-grid`, `.cur-select`.
- **Income rows show original currency only** (amount + `currencyCode`), like expenses.
- **Drill-down filters the list only**; total/pie/breakdown stay whole-period. Changing the range resets the drill-down.
- **No test framework** in `web/`. Per-task gates: `npm run build` (`tsc -b` + vite build) and `npm run lint`, run from `web/`. Do not add a test harness. node_modules is installed.
- **The `PeriodLedger` refactor must preserve current Spending behavior exactly** (range default this month→today, total, pie + by-category breakdown, by-month bar for multi-month, drill-down, delete, original-currency rows).
- There are 4 pre-existing eslint errors in `AddExpense.tsx` and `Settings.tsx` (react-hooks rules) unrelated to this work; do not attribute them to these tasks, but do not add new lint errors.

## File Structure

- Create: `web/src/components/PeriodLedger.tsx`, `web/src/screens/Income.tsx`, `web/src/screens/AddIncome.tsx`.
- Modify: `web/src/screens/Spending.tsx` (refactor to use `PeriodLedger`), `web/src/api/types.ts` (+income types, extend `Settings`), `web/src/api/hooks.ts` (+income hooks), `web/src/screens/Settings.tsx` (+toggle), `web/src/router.tsx` (+routes/tab), `web/src/components/icons.tsx` (+`IconIncome`).

---

### Task F1: Extract `PeriodLedger` and refactor `Spending`

**Files:**
- Create: `web/src/components/PeriodLedger.tsx`
- Modify: `web/src/screens/Spending.tsx`

**Interfaces:**
- Produces: `PeriodLedger` (default export) with props `{ from: string; to: string; onFromChange(v): void; onToChange(v): void; report?: ReportSummary; rows?: LedgerRow[]; categories?: LedgerCategory[]; onDelete(id: number): void; headerAction?: ReactNode; emptyVerb?: string; emptyCategoryText?: string }`; named exports `LedgerRow` (`{ id: number; amount: number; currencyCode: string; categoryId: number; date: string; note: string | null }`) and `LedgerCategory` (`{ id: number; name: string; emoji: string }`).
- Consumes: `ReportSummary` from `../api/types`; `IconTrash` from `./icons`.

- [ ] **Step 1: Create `web/src/components/PeriodLedger.tsx`**

```tsx
import { useEffect, useState, type ReactNode } from "react";
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
import { IconTrash } from "./icons";
import type { ReportSummary } from "../api/types";

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

export interface LedgerRow {
	id: number;
	amount: number;
	currencyCode: string;
	categoryId: number;
	date: string;
	note: string | null;
}

export interface LedgerCategory {
	id: number;
	name: string;
	emoji: string;
}

interface PeriodLedgerProps {
	from: string;
	to: string;
	onFromChange: (v: string) => void;
	onToChange: (v: string) => void;
	report?: ReportSummary;
	rows?: LedgerRow[];
	categories?: LedgerCategory[];
	onDelete: (id: number) => void;
	headerAction?: ReactNode;
	emptyVerb?: string;
	emptyCategoryText?: string;
}

export default function PeriodLedger({
	from,
	to,
	onFromChange,
	onToChange,
	report,
	rows,
	categories,
	onDelete,
	headerAction,
	emptyVerb = "spent",
	emptyCategoryText = "No expenses in this category.",
}: PeriodLedgerProps) {
	const [view, setView] = useState<"category" | "month">("category");
	const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(
		null,
	);

	// Reset the drill-down whenever the date range changes.
	useEffect(() => setSelectedCategoryId(null), [from, to]);

	const title = `${from} → ${to}`;
	const isRange = from.slice(0, 7) !== to.slice(0, 7);
	const hasData = (report?.byCategory.length ?? 0) > 0;

	const catFor = (id: number) => categories?.find((c) => c.id === id);
	const toggleCategory = (categoryId: number) =>
		setSelectedCategoryId((cur) => (cur === categoryId ? null : categoryId));

	const visibleRows = (rows ?? []).filter(
		(r) => selectedCategoryId === null || r.categoryId === selectedCategoryId,
	);
	const selectedCat =
		selectedCategoryId !== null ? catFor(selectedCategoryId) : null;

	return (
		<div className="screen">
			{headerAction}

			<section className="card">
				<div className="row" style={{ gap: 10 }}>
					<div className="field grow">
						<label>From</label>
						<input
							type="date"
							value={from}
							max={to}
							onChange={(e) => onFromChange(e.target.value)}
						/>
					</div>
					<div className="field grow">
						<label>To</label>
						<input
							type="date"
							value={to}
							min={from}
							onChange={(e) => onToChange(e.target.value)}
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
					Nothing {emptyVerb} in {title}.
				</div>
			)}

			{hasData && report && (
				<>
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

					{rows && (
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
									{visibleRows.length}{" "}
									{visibleRows.length === 1 ? "entry" : "entries"}
								</p>
							)}

							<section className="card card--flush">
								{visibleRows.length === 0 ? (
									<div className="empty" style={{ padding: 16 }}>
										{emptyCategoryText}
									</div>
								) : (
									<ul className="list">
										{visibleRows.map((r) => {
											const cat = catFor(r.categoryId);
											return (
												<li key={r.id} className="item">
													<div className="avatar">{cat?.emoji ?? "💸"}</div>
													<div className="item__main">
														<div className="item__title">
															{cat?.name ?? "—"}
														</div>
														<div className="item__sub">
															{fmtDate(r.date)}
															{r.note ? ` · ${r.note}` : ""}
														</div>
													</div>
													<div className="item__amount">
														{fmtAmount(r.amount)}
														<span className="item__cur">{r.currencyCode}</span>
													</div>
													<button
														className="icon-btn"
														aria-label="Delete"
														onClick={() => onDelete(r.id)}
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

- [ ] **Step 2: Replace `web/src/screens/Spending.tsx` with the thin parent**

```tsx
import { useState } from "react";
import {
	useCategories,
	useDeleteExpense,
	useExpenses,
	useReport,
} from "../api/hooks";
import PeriodLedger, { type LedgerRow } from "../components/PeriodLedger";
import { localDateString } from "../lib/date";

// First day of the current month (the default "from").
const monthStart = () => {
	const d = new Date();
	return localDateString(new Date(d.getFullYear(), d.getMonth(), 1));
};

export default function Spending() {
	const [from, setFrom] = useState(monthStart);
	const [to, setTo] = useState(() => localDateString(new Date()));

	const range = { from, to };
	const { data: report } = useReport(range);
	const { data: expenses } = useExpenses(range);
	const { data: categories } = useCategories();
	const del = useDeleteExpense();

	const rows: LedgerRow[] | undefined = expenses?.map((e) => ({
		id: e.id,
		amount: e.amount,
		currencyCode: e.currencyCode,
		categoryId: e.categoryId,
		date: e.spentOn,
		note: e.note,
	}));

	return (
		<PeriodLedger
			from={from}
			to={to}
			onFromChange={setFrom}
			onToChange={setTo}
			report={report}
			rows={rows}
			categories={categories}
			onDelete={(id) => del.mutate(id)}
		/>
	);
}
```

- [ ] **Step 3: Build and lint**

Run (from `web/`): `npm run build` then `npm run lint`
Expected: both PASS. No new eslint errors in `PeriodLedger.tsx` or `Spending.tsx`.

- [ ] **Step 4: Commit**

```bash
git add web/src/components/PeriodLedger.tsx web/src/screens/Spending.tsx
git commit -m "refactor(web): extract PeriodLedger shared by Spending (and Income)"
```

---

### Task F2: Income types and hooks

**Files:**
- Modify: `web/src/api/types.ts`, `web/src/api/hooks.ts`

**Interfaces:**
- Produces types: `Income { id: number; amount: number; currencyCode: string; incomeCategoryId: number; receivedOn: string; note: string | null }`; `IncomeInput { amount: number; currencyCode: string; incomeCategoryId: number; receivedOn: string; note: string | null }`; `IncomeCategory { id: number; name: string; emoji: string }`; `Settings { baseCurrency: string; incomeTrackingEnabled: boolean }`.
- Produces hooks: `useIncomes({from?,to?,categoryId?}) → Income[]`; `useCreateIncome()` (mutate `IncomeInput`); `useDeleteIncome()` (mutate `number`); `useIncomeCategories() → IncomeCategory[]`; `useIncomeReport({from,to}) → ReportSummary`.

- [ ] **Step 1: Add income types and extend `Settings`**

In `web/src/api/types.ts`, add after the `Expense`/`ExpenseInput` records:
```ts
export interface Income {
	id: number;
	amount: number;
	currencyCode: string;
	incomeCategoryId: number;
	receivedOn: string;
	note: string | null;
}

export interface IncomeInput {
	amount: number;
	currencyCode: string;
	incomeCategoryId: number;
	receivedOn: string;
	note: string | null;
}

export interface IncomeCategory {
	id: number;
	name: string;
	emoji: string;
}
```
And change the `Settings` interface to:
```ts
export interface Settings {
	baseCurrency: string;
	incomeTrackingEnabled: boolean;
}
```

- [ ] **Step 2: Add income hooks**

In `web/src/api/hooks.ts`, extend the type import to include the new types:
```ts
import type {
	Budget,
	BudgetInput,
	Category,
	Expense,
	ExpenseInput,
	Income,
	IncomeInput,
	IncomeCategory,
	ReportSummary,
	Settings,
	RatesView,
	GoldView,
} from "./types";
```
Then add these hooks (place them after `useDeleteExpense`):
```ts
export const useIncomeCategories = () =>
	useQuery({
		queryKey: ["income-categories"],
		queryFn: () => api<IncomeCategory[]>("/income-categories"),
	});

export const useIncomes = (
	filters: { from?: string; to?: string; categoryId?: number } = {},
) => {
	const qs = new URLSearchParams();
	if (filters.from) qs.set("from", filters.from);
	if (filters.to) qs.set("to", filters.to);
	if (filters.categoryId) qs.set("categoryId", String(filters.categoryId));
	return useQuery({
		queryKey: ["incomes", filters],
		queryFn: () => api<Income[]>(`/incomes?${qs.toString()}`),
	});
};

export const useCreateIncome = () => {
	const qc = useQueryClient();
	return useMutation({
		mutationFn: (input: IncomeInput) =>
			api<Income>("/incomes", {
				method: "POST",
				body: JSON.stringify(input),
			}),
		onSuccess: () => {
			qc.invalidateQueries({ queryKey: ["incomes"] });
			qc.invalidateQueries({ queryKey: ["income-report"] });
		},
	});
};

export const useDeleteIncome = () => {
	const qc = useQueryClient();
	return useMutation({
		mutationFn: (id: number) =>
			api<void>(`/incomes/${id}`, { method: "DELETE" }),
		onSuccess: () => {
			qc.invalidateQueries({ queryKey: ["incomes"] });
			qc.invalidateQueries({ queryKey: ["income-report"] });
		},
	});
};

export const useIncomeReport = (range: { from: string; to: string }) =>
	useQuery({
		queryKey: ["income-report", range],
		queryFn: () =>
			api<ReportSummary>(
				`/reports/income-summary?from=${range.from}&to=${range.to}`,
			),
	});
```
Also, in the existing `useUpdateSettings.onSuccess`, add an income-report invalidation so a base-currency change refreshes income too — after the `["report"]` line add:
```ts
				qc.invalidateQueries({ queryKey: ["income-report"] });
```

- [ ] **Step 3: Build and lint**

Run (from `web/`): `npm run build` then `npm run lint`
Expected: both PASS. (The new `Settings.incomeTrackingEnabled` field may make `Settings.tsx` fail type-check because its `update.mutate({ baseCurrency: base })` is now missing a field — if `npm run build` reports that error, it is expected and fixed in Task F4. To keep this task green, this step's build is allowed to surface only that single `Settings.tsx` error; everything else must compile. If you prefer a fully-green build here, you may apply F4's Settings.tsx change now — but the canonical sequence fixes it in F4.)

> Note to implementer: To keep each task independently green, make the minimal `Settings.tsx` edit from Task F4 Step 2 (the `save`/`toggleIncome` payloads) as part of this commit if the build fails on it. Otherwise proceed; F4 fixes it.

- [ ] **Step 4: Commit**

```bash
git add web/src/api/types.ts web/src/api/hooks.ts
git commit -m "feat(web): income types and query hooks"
```

---

### Task F3: Income and AddIncome screens

**Files:**
- Create: `web/src/screens/Income.tsx`, `web/src/screens/AddIncome.tsx`

**Interfaces:**
- Consumes: `PeriodLedger` + `LedgerRow` (F1); `useIncomes`, `useIncomeReport`, `useIncomeCategories`, `useDeleteIncome`, `useCreateIncome`, `useSettings` (F2/existing); `localDateString`, `CURRENCIES`.
- Produces: default exports `Income` and `AddIncome` (consumed by F4 routing).

- [ ] **Step 1: Create `web/src/screens/Income.tsx`**

```tsx
import { useState } from "react";
import { Link } from "react-router-dom";
import {
	useDeleteIncome,
	useIncomeCategories,
	useIncomeReport,
	useIncomes,
} from "../api/hooks";
import PeriodLedger, { type LedgerRow } from "../components/PeriodLedger";
import { localDateString } from "../lib/date";

const monthStart = () => {
	const d = new Date();
	return localDateString(new Date(d.getFullYear(), d.getMonth(), 1));
};

export default function Income() {
	const [from, setFrom] = useState(monthStart);
	const [to, setTo] = useState(() => localDateString(new Date()));

	const range = { from, to };
	const { data: report } = useIncomeReport(range);
	const { data: incomes } = useIncomes(range);
	const { data: categories } = useIncomeCategories();
	const del = useDeleteIncome();

	const rows: LedgerRow[] | undefined = incomes?.map((i) => ({
		id: i.id,
		amount: i.amount,
		currencyCode: i.currencyCode,
		categoryId: i.incomeCategoryId,
		date: i.receivedOn,
		note: i.note,
	}));

	return (
		<PeriodLedger
			from={from}
			to={to}
			onFromChange={setFrom}
			onToChange={setTo}
			report={report}
			rows={rows}
			categories={categories}
			onDelete={(id) => del.mutate(id)}
			emptyVerb="earned"
			emptyCategoryText="No income in this category."
			headerAction={
				<Link className="btn btn--primary btn--block" to="/income/add">
					+ Add income
				</Link>
			}
		/>
	);
}
```

- [ ] **Step 2: Create `web/src/screens/AddIncome.tsx`** (mirror of `AddExpense.tsx`)

```tsx
import { useEffect, useRef, useState } from "react";
import { useCreateIncome, useIncomeCategories, useSettings } from "../api/hooks";
import { localDateString } from "../lib/date";
import { CURRENCIES } from "../lib/currencies";

const today = () => localDateString(new Date());
const inTelegram = () => Boolean(window.Telegram?.WebApp?.initData);

// Format a typed amount with thousands separators, keeping up to 2 decimals.
function formatAmount(raw: string): string {
	let s = raw.replace(/[^\d.]/g, "");
	const dot = s.indexOf(".");
	if (dot !== -1)
		s =
			s.slice(0, dot + 1) +
			s
				.slice(dot + 1)
				.replace(/\./g, "")
				.slice(0, 2);
	const [int, dec] = s.split(".");
	const intFmt = int ? Number(int).toLocaleString("en-US") : "";
	return dec !== undefined ? `${intFmt}.${dec}` : intFmt;
}
const toNumber = (s: string) => parseFloat(s.replace(/,/g, ""));

export default function AddIncome() {
	const { data: categories } = useIncomeCategories();
	const { data: settings } = useSettings();
	const createIncome = useCreateIncome();

	const [amount, setAmount] = useState("");
	const [currency, setCurrency] = useState("UZS");
	const [categoryId, setCategoryId] = useState<number | null>(null);
	const [receivedOn, setReceivedOn] = useState(today());
	const [note, setNote] = useState("");
	const [justSaved, setJustSaved] = useState(false);

	useEffect(() => {
		if (settings) setCurrency(settings.baseCurrency);
	}, [settings]);
	useEffect(() => {
		if (categories?.length && categoryId === null)
			setCategoryId(categories[0].id);
	}, [categories, categoryId]);

	// Keep the latest submit logic in a ref so the Main Button handler is registered once.
	const submitRef = useRef<() => void>(() => {});
	submitRef.current = () => {
		const wa = window.Telegram?.WebApp;
		const value = toNumber(amount);
		if (!value || value <= 0 || categoryId === null) return;
		wa?.MainButton.showProgress();
		createIncome.mutate(
			{
				amount: value,
				currencyCode: currency,
				incomeCategoryId: categoryId,
				receivedOn,
				note: note || null,
			},
			{
				onSuccess: () => {
					wa?.HapticFeedback.impactOccurred("medium");
					setAmount("");
					setNote("");
					setJustSaved(true);
					setTimeout(() => setJustSaved(false), 1600);
					wa?.MainButton.hideProgress();
				},
				onError: () => wa?.MainButton.hideProgress(),
			},
		);
	};

	useEffect(() => {
		const wa = window.Telegram?.WebApp;
		if (!wa) return;
		const mb = wa.MainButton;
		mb.setText("Save income");
		mb.show();
		const handler = () => submitRef.current();
		mb.onClick(handler);
		return () => {
			mb.offClick(handler);
			mb.hide();
		};
	}, []);

	const canSave = toNumber(amount) > 0 && categoryId !== null;

	return (
		<div className="screen">
			<section className="card hero">
				<p className="eyebrow">Amount received</p>
				<div className="amount-field">
					<input
						inputMode="decimal"
						placeholder="0.00"
						value={amount}
						onChange={(e) => setAmount(formatAmount(e.target.value))}
						autoFocus
					/>
					<select
						className="cur-select"
						value={currency}
						onChange={(e) => setCurrency(e.target.value)}
						aria-label="Currency"
					>
						{(CURRENCIES.includes(currency as (typeof CURRENCIES)[number])
							? CURRENCIES
							: [currency, ...CURRENCIES]
						).map((c) => (
							<option key={c} value={c}>
								{c}
							</option>
						))}
					</select>
				</div>
			</section>

			<p className="eyebrow">Category</p>
			<div className="chip-grid">
				{categories?.map((c) => (
					<button
						key={c.id}
						className={`chip${categoryId === c.id ? " chip--active" : ""}`}
						onClick={() => setCategoryId(c.id)}
					>
						<span className="emoji">{c.emoji}</span>
						{c.name}
					</button>
				))}
			</div>

			<section className="card">
				<div className="row" style={{ gap: 12 }}>
					<div className="field grow">
						<label>Date</label>
						<input
							type="date"
							value={receivedOn}
							onChange={(e) => setReceivedOn(e.target.value)}
						/>
					</div>
				</div>
				<div className="field" style={{ marginTop: 12 }}>
					<label>Note</label>
					<input
						placeholder="Optional — e.g. June salary"
						value={note}
						onChange={(e) => setNote(e.target.value)}
					/>
				</div>
			</section>

			{!inTelegram() && (
				<button
					className="btn btn--primary btn--block"
					disabled={!canSave}
					style={{ opacity: canSave ? 1 : 0.5 }}
					onClick={() => submitRef.current()}
				>
					{justSaved ? "Saved ✓" : "Save income"}
				</button>
			)}
			{justSaved && inTelegram() && (
				<p className="hint" style={{ textAlign: "center" }}>
					Saved ✓ — add another
				</p>
			)}
		</div>
	);
}
```

- [ ] **Step 3: Build and lint**

Run (from `web/`): `npm run build` then `npm run lint`
Expected: both PASS (assuming F4's `Settings.tsx` fix is applied; if not yet, only the known `Settings.tsx` payload error from F2 may remain). No new errors in `Income.tsx`/`AddIncome.tsx`.

- [ ] **Step 4: Commit**

```bash
git add web/src/screens/Income.tsx web/src/screens/AddIncome.tsx
git commit -m "feat(web): Income tab and Add-income screens"
```

---

### Task F4: Settings toggle, navbar tab, and routes

**Files:**
- Modify: `web/src/components/icons.tsx`, `web/src/screens/Settings.tsx`, `web/src/router.tsx`

**Interfaces:**
- Produces: `IconIncome` icon; a "Track income" toggle in Settings writing `incomeTrackingEnabled`; `/income` and `/income/add` routes; a conditional Income navbar tab.

- [ ] **Step 1: Add the `IconIncome` icon**

In `web/src/components/icons.tsx`, add after `IconReports`:
```tsx
export const IconIncome = (p: P) => (
	<svg {...base} {...p}>
		<path d="M12 3.5v9M8.5 9l3.5 3.5L15.5 9" />
		<path d="M4.5 14.5v4a1.5 1.5 0 0 0 1.5 1.5h12a1.5 1.5 0 0 0 1.5-1.5v-4" />
	</svg>
);
```

- [ ] **Step 2: Add the Settings toggle**

Replace `web/src/screens/Settings.tsx` with (adds `incomeOn` state, an On/Off toggle that saves immediately, and sends `incomeTrackingEnabled` in every settings write):
```tsx
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useSettings, useUpdateSettings } from "../api/hooks";
import { IconTag } from "../components/icons";
import { CURRENCIES } from "../lib/currencies";
import { getThemePref, setThemePref, type ThemePref } from "../lib/appTheme";

const THEMES: { key: ThemePref; label: string }[] = [
	{ key: "system", label: "System" },
	{ key: "light", label: "Light" },
	{ key: "dark", label: "Dark" },
];

export default function Settings() {
	const { data: settings } = useSettings();
	const update = useUpdateSettings();
	const [base, setBase] = useState("UZS");
	const [incomeOn, setIncomeOn] = useState(false);
	const [saved, setSaved] = useState(false);
	const [theme, setTheme] = useState<ThemePref>(getThemePref());

	useEffect(() => {
		if (settings) {
			setBase(settings.baseCurrency);
			setIncomeOn(settings.incomeTrackingEnabled);
		}
	}, [settings]);

	const options = CURRENCIES.includes(base as (typeof CURRENCIES)[number])
		? CURRENCIES
		: [base, ...CURRENCIES];

	const save = () =>
		update.mutate(
			{ baseCurrency: base, incomeTrackingEnabled: incomeOn },
			{
				onSuccess: () => {
					setSaved(true);
					setTimeout(() => setSaved(false), 1500);
				},
			},
		);

	const toggleIncome = (on: boolean) => {
		setIncomeOn(on);
		update.mutate({ baseCurrency: base, incomeTrackingEnabled: on });
	};

	const pickTheme = (t: ThemePref) => {
		setTheme(t);
		setThemePref(t);
	};

	return (
		<div className="screen">
			<p className="eyebrow">Appearance</p>
			<section className="card">
				<div className="segmented" role="tablist">
					{THEMES.map((t) => (
						<button
							key={t.key}
							role="tab"
							aria-selected={theme === t.key}
							onClick={() => pickTheme(t.key)}
						>
							{t.label}
						</button>
					))}
				</div>
			</section>

			<p className="eyebrow">Base currency</p>
			<section className="card">
				<div className="row">
					<select
						className="grow"
						value={base}
						onChange={(e) => setBase(e.target.value)}
					>
						{options.map((c) => (
							<option key={c} value={c}>
								{c}
							</option>
						))}
					</select>
					<button className="btn btn--primary" onClick={save}>
						{saved ? "Saved ✓" : "Save"}
					</button>
				</div>
				<p className="hint" style={{ marginTop: 12 }}>
					Reports and budgets convert all expenses to this currency using the
					CBU rate as of each expense's date.
				</p>
			</section>

			<p className="eyebrow">Income tracking</p>
			<section className="card">
				<div className="segmented" role="tablist">
					<button
						role="tab"
						aria-selected={!incomeOn}
						onClick={() => toggleIncome(false)}
					>
						Off
					</button>
					<button
						role="tab"
						aria-selected={incomeOn}
						onClick={() => toggleIncome(true)}
					>
						On
					</button>
				</div>
				<p className="hint" style={{ marginTop: 12 }}>
					Adds an Income tab to record money coming in, tracked separately from
					your expenses.
				</p>
			</section>

			<Link
				to="/categories"
				className="card row"
				style={{ alignItems: "center", gap: 13 }}
			>
				<span className="avatar">
					<IconTag className="" />
				</span>
				<div className="item__main">
					<div className="item__title" style={{ color: "var(--label)" }}>
						Manage categories
					</div>
					<div className="item__sub">
						Add or review your spending categories
					</div>
				</div>
				<span style={{ color: "var(--label-2)", fontSize: 22 }}>›</span>
			</Link>
		</div>
	);
}
```

- [ ] **Step 3: Wire routes and the conditional navbar tab**

Replace `web/src/router.tsx` with:
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
import Income from "./screens/Income";
import AddIncome from "./screens/AddIncome";
import Settings from "./screens/Settings";
import Rates from "./screens/Rates";
import { useSettings } from "./api/hooks";
import {
	IconAdd,
	IconReports,
	IconIncome,
	IconBudgets,
	IconRates,
	IconSettings,
} from "./components/icons";

const TITLES: Record<string, string> = {
	"/": "Add expense",
	"/spending": "Spending",
	"/income": "Income",
	"/income/add": "Add income",
	"/budgets": "Budgets",
	"/rates": "Rates & gold",
	"/settings": "Settings",
	"/categories": "Categories",
};

function Layout() {
	const { pathname } = useLocation();
	const navigate = useNavigate();
	const { data: settings } = useSettings();
	const title = TITLES[pathname] ?? "Spending";

	const tabs = [
		{ to: "/", label: "Add", Icon: IconAdd, end: true },
		{ to: "/spending", label: "Spending", Icon: IconReports, end: false },
		...(settings?.incomeTrackingEnabled
			? [{ to: "/income", label: "Income", Icon: IconIncome, end: false }]
			: []),
		{ to: "/budgets", label: "Budgets", Icon: IconBudgets, end: false },
		{ to: "/rates", label: "Rates", Icon: IconRates, end: false },
	];

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
					{tabs.map(({ to, label, Icon, end }) => (
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
			{ path: "income", element: <Income /> },
			{ path: "income/add", element: <AddIncome /> },
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

- [ ] **Step 4: Build and lint**

Run (from `web/`): `npm run build` then `npm run lint`
Expected: both PASS with no new eslint errors (only the 4 pre-existing `AddExpense.tsx`/`Settings.tsx` react-hooks errors remain; confirm no *new* ones were introduced).

- [ ] **Step 5: Commit**

```bash
git add web/src/components/icons.tsx web/src/screens/Settings.tsx web/src/router.tsx
git commit -m "feat(web): income settings toggle, conditional navbar tab, routes"
```

---

## Verification (visual, after F4)

Reuse the mock-dev-server approach used to verify the Spending screen: a `web/vite.mock.config.mts` whose middleware serves `/api/settings` (with `incomeTrackingEnabled: true`), `/api/income-categories`, `/api/incomes?from&to`, and `/api/reports/income-summary` with representative data; run `npx vite --config vite.mock.config.mts`, drive with Playwright at a 390×844 viewport, and screenshot:
1. Settings → "Income tracking" On → the **Income** tab appears in the navbar.
2. Income tab: range default month, base-currency total, pie + by-category breakdown over income categories, income list (original currencies), and the **+ Add income** button at top.
3. Tap a category → list filters, chip shows, charts stay whole-period.
4. **+ Add income** → the Add-income form (amount + currency, income category chips, date, note).
5. Settings → Off → the Income tab disappears.
Delete `vite.mock.config.mts` and screenshots before merging (untracked artifacts).

## Self-Review

**Spec coverage:**
- Shared `PeriodLedger` + Spending refactor (option A) → F1. ✓
- Income types + hooks → F2. ✓
- Income tab (Spending-style, income data, drill-down, original currency) + `+ Add income` button → F3 (`Income.tsx`). ✓
- Add-income dedicated screen mirroring Add-expense → F3 (`AddIncome.tsx`). ✓
- Settings "Track income" toggle persisted to backend → F4 (`Settings.tsx`). ✓
- Conditional navbar Income tab + `/income`, `/income/add` routes + titles → F4 (`router.tsx`). ✓
- Income-category icon → F4 (`IconIncome`). ✓
- Empty-state wording ("Nothing earned", "No income in this category") → F1 props, set in F3. ✓

**Placeholder scan:** none — every code step is complete. The only conditional note is F2 Step 3's known transient `Settings.tsx` type error, explicitly resolved in F4 Step 2 (with an inline option to fix early). No TODO/TBD.

**Type consistency:** `LedgerRow`/`LedgerCategory` defined in F1 and consumed in F1/F3 with matching field names (`categoryId`, `date`). `Income.incomeCategoryId`/`receivedOn` (F2) normalized to `categoryId`/`date` in `Income.tsx` (F3) — mirrors how `Spending.tsx` maps `spentOn`. `IncomeInput` shape used by `useCreateIncome` (F2) matches the payload built in `AddIncome.tsx` (F3): `{ amount, currencyCode, incomeCategoryId, receivedOn, note }`. `Settings` gains `incomeTrackingEnabled` (F2) and every `update.mutate(...)` call in `Settings.tsx` (F4) includes it. `IconIncome` defined in F4 Step 1 and imported in F4 Step 3. Query keys `["incomes"]`/`["income-report"]`/`["income-categories"]` consistent across hooks and invalidations.
