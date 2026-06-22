import { useMemo, useState } from "react";
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

type Period = {
	key: string;
	chip: string; // short label on the selector
	title: string; // shown under the total
	from: string;
	to: string;
	multiMonth: boolean; // spans >1 month → offer the By-month breakdown
};

// Selector options: All time · each recent year · last 6 months · last 12 months.
function buildPeriods(): Period[] {
	const now = new Date();
	const y = now.getFullYear();
	const m = now.getMonth();
	const today = localDateString(now);

	const allTime: Period = {
		key: "all",
		chip: "All time",
		title: "all time",
		from: "2000-01-01",
		to: today,
		multiMonth: true,
	};

	const years: Period[] = [];
	for (let i = 0; i < 3; i++) {
		const yr = y - i;
		years.push({
			key: `y${yr}`,
			chip: String(yr),
			title: String(yr),
			from: `${yr}-01-01`,
			to: i === 0 ? today : `${yr}-12-31`,
			multiMonth: true,
		});
	}

	const six: Period = {
		key: "6m",
		chip: "6 months",
		title: "last 6 months",
		from: localDateString(new Date(y, m - 5, 1)),
		to: localDateString(new Date(y, m + 1, 0)),
		multiMonth: true,
	};

	const months: Period[] = [];
	for (let i = 0; i < 12; i++) {
		const d = new Date(y, m - i, 1);
		const sameYear = d.getFullYear() === y;
		months.push({
			key: `${d.getFullYear()}-${d.getMonth()}`,
			chip: d.toLocaleDateString("en-US", { month: "short" }),
			title: d.toLocaleDateString("en-US", {
				month: "long",
				...(sameYear ? {} : { year: "numeric" }),
			}),
			from: localDateString(new Date(d.getFullYear(), d.getMonth(), 1)),
			to: localDateString(new Date(d.getFullYear(), d.getMonth() + 1, 0)),
			multiMonth: false,
		});
	}
	return [allTime, ...years, six, ...months];
}

export default function Reports() {
	const periods = useMemo(buildPeriods, []);
	const [periodKey, setPeriodKey] = useState("6m");
	const period = periods.find((p) => p.key === periodKey) ?? periods[0];
	const isRange = period.multiMonth;

	const [view, setView] = useState<"category" | "month">("category");
	const { data: report } = useReport({ from: period.from, to: period.to });

	const hasData = (report?.byCategory.length ?? 0) > 0;

	return (
		<div className="screen">
			{/* month / range selector */}
			<div className="chips">
				{periods.map((p) => (
					<button
						key={p.key}
						className={`chip${p.key === periodKey ? " chip--active" : ""}`}
						onClick={() => setPeriodKey(p.key)}
					>
						{p.chip}
					</button>
				))}
			</div>

			<section className="card hero">
				<p className="eyebrow">Total · {period.title}</p>
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
					Nothing spent in {period.title}.
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
