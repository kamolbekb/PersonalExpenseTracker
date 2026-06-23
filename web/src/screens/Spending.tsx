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
