import { useState } from "react";
import {
	useBudgets,
	useCategories,
	useReport,
	useSettings,
	useUpsertBudget,
} from "../api/hooks";
import { localDateString } from "../lib/date";

const monthRange = () => {
	const now = new Date();
	const from = localDateString(new Date(now.getFullYear(), now.getMonth(), 1));
	const to = localDateString(
		new Date(now.getFullYear(), now.getMonth() + 1, 0),
	);
	return { from, to };
};

const fmt = (n: number) =>
	n.toLocaleString("en-US", { maximumFractionDigits: 0 });

export default function Budgets() {
	const { data: budgets } = useBudgets();
	const { data: categories } = useCategories();
	const { data: settings } = useSettings();
	const { data: report } = useReport(monthRange());
	const upsert = useUpsertBudget();

	const [categoryId, setCategoryId] = useState<string>("");
	const [limit, setLimit] = useState("");

	const cur = settings?.baseCurrency ?? "UZS";

	const spentFor = (catId: number | null) =>
		catId === null
			? (report?.grandTotal ?? 0)
			: (report?.byCategory.find((c) => c.categoryId === catId)?.total ?? 0);

	const submit = () => {
		const value = parseFloat(limit);
		if (!value || value <= 0) return;
		upsert.mutate(
			{
				categoryId: categoryId === "" ? null : Number(categoryId),
				limitAmount: value,
				currencyCode: cur,
			},
			{ onSuccess: () => setLimit("") },
		);
	};

	return (
		<div className="screen">
			<p className="eyebrow">This month · {cur}</p>

			{budgets && budgets.length === 0 && (
				<div className="card empty">
					<span className="emoji">🎯</span>
					No budgets yet. Set a monthly limit below.
				</div>
			)}

			{budgets?.map((b) => {
				const spent = spentFor(b.categoryId);
				const label =
					b.categoryId === null
						? "Overall"
						: (categories?.find((c) => c.id === b.categoryId)?.name ?? "—");
				const ratio = b.limitAmount ? spent / b.limitAmount : 0;
				const pct = Math.min(ratio * 100, 100);
				const state = ratio > 1 ? "over" : ratio > 0.8 ? "warn" : "ok";
				const remaining = b.limitAmount - spent;
				return (
					<section className="card" key={b.id}>
						<div
							className="row"
							style={{
								justifyContent: "space-between",
								alignItems: "baseline",
							}}
						>
							<span style={{ fontWeight: 700 }}>{label}</span>
							<span className="num" style={{ fontWeight: 600 }}>
								{fmt(spent)}{" "}
								<span style={{ color: "var(--muted)" }}>
									/ {fmt(b.limitAmount)}
								</span>
							</span>
						</div>
						<div className="progress" style={{ margin: "12px 0 10px" }}>
							<div
								className={`progress__bar progress__bar--${state}`}
								style={{ width: `${pct}%` }}
							/>
						</div>
						<span
							className={`pill ${remaining < 0 ? "pill--neg" : "pill--pos"}`}
						>
							{remaining < 0
								? `${fmt(-remaining)} ${cur} over`
								: `${fmt(remaining)} ${cur} left`}
						</span>
					</section>
				);
			})}

			<section className="card">
				<h3>Set a limit</h3>
				<div className="field">
					<label>Category</label>
					<select
						value={categoryId}
						onChange={(e) => setCategoryId(e.target.value)}
					>
						<option value="">Overall (all spending)</option>
						{categories?.map((c) => (
							<option key={c.id} value={c.id}>
								{c.emoji} {c.name}
							</option>
						))}
					</select>
				</div>
				<div className="row" style={{ marginTop: 12 }}>
					<input
						className="grow"
						inputMode="decimal"
						placeholder={`Monthly limit (${cur})`}
						value={limit}
						onChange={(e) => setLimit(e.target.value)}
					/>
					<button className="btn btn--primary" onClick={submit}>
						Save
					</button>
				</div>
			</section>
		</div>
	);
}
