import {
	Bar,
	BarChart,
	Cell,
	Pie,
	PieChart,
	ResponsiveContainer,
	Tooltip,
	XAxis,
	YAxis,
} from "recharts";
import { useReport } from "../api/hooks";
import { localDateString } from "../lib/date";

const monthRange = () => {
	const now = new Date();
	const from = localDateString(
		new Date(now.getFullYear(), now.getMonth() - 5, 1),
	);
	const to = localDateString(
		new Date(now.getFullYear(), now.getMonth() + 1, 0),
	);
	return { from, to };
};

const COLORS = [
	"#2481cc",
	"#e74c3c",
	"#2ecc71",
	"#f39c12",
	"#9b59b6",
	"#1abc9c",
	"#e67e22",
	"#34495e",
];

export default function Reports() {
	const { data: report } = useReport(monthRange());
	if (!report) return <div className="screen">Loading…</div>;

	return (
		<div className="screen">
			<h2>Reports</h2>
			<p>
				Total: {report.grandTotal.toFixed(2)} {report.baseCurrency}
			</p>

			<h3>By category</h3>
			<ResponsiveContainer width="100%" height={240}>
				<PieChart>
					<Pie
						data={report.byCategory}
						dataKey="total"
						nameKey="categoryName"
						outerRadius={90}
						label
					>
						{report.byCategory.map((_, i) => (
							<Cell key={i} fill={COLORS[i % COLORS.length]} />
						))}
					</Pie>
					<Tooltip />
				</PieChart>
			</ResponsiveContainer>

			<h3>By month</h3>
			<ResponsiveContainer width="100%" height={240}>
				<BarChart data={report.byMonth}>
					<XAxis dataKey="month" />
					<YAxis />
					<Tooltip />
					<Bar dataKey="total" fill="#2481cc" />
				</BarChart>
			</ResponsiveContainer>
		</div>
	);
}
