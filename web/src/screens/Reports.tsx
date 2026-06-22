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
import { useReport } from "../api/hooks";
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

// First day of the current month (the default "from").
const monthStart = () => {
	const d = new Date();
	return localDateString(new Date(d.getFullYear(), d.getMonth(), 1));
};

export default function Reports() {
	// Always-visible date range, defaulting to this month → today.
	const [from, setFrom] = useState(monthStart);
	const [to, setTo] = useState(() => localDateString(new Date()));

	const range = { from, to };
	const title = `${from} → ${to}`;
	// spans >1 month when the from/to year-months differ → offer By-month
	const isRange = from.slice(0, 7) !== to.slice(0, 7);

	const [view, setView] = useState<"category" | "month">("category");
	const { data: report } = useReport(range);

	const hasData = (report?.byCategory.length ?? 0) > 0;

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
							onChange={(e) => setFrom(e.target.value)}
						/>
					</div>
					<div className="field grow">
						<label>To</label>
						<input
							type="date"
							value={to}
							min={from}
							onChange={(e) => setTo(e.target.value)}
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
					{/* By category is always available; By month only for the 6-month range */}
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
										{report.byCategory.map((_, i) => (
											<Cell key={i} fill={COLORS[i % COLORS.length]} />
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
									return (
										<li key={c.categoryId} className="item">
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
				</>
			)}
		</div>
	);
}
