import { useState, type ReactNode } from "react";
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
	// Store the range key alongside the selection so the drill-down resets automatically
	// when the date range changes, without needing an effect.
	const rangeKey = `${from}|${to}`;
	const [selection, setSelection] = useState<{
		rangeKey: string;
		categoryId: number | null;
	}>({ rangeKey, categoryId: null });

	const selectedCategoryId =
		selection.rangeKey === rangeKey ? selection.categoryId : null;
	const setSelectedCategoryId = (
		id: number | null | ((prev: number | null) => number | null),
	) => {
		setSelection((prev) => {
			const prevId = prev.rangeKey === rangeKey ? prev.categoryId : null;
			const nextId = typeof id === "function" ? id(prevId) : id;
			return { rangeKey, categoryId: nextId };
		});
	};

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
