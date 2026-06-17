import { useState } from "react";
import {
	useBudgets,
	useCategories,
	useReport,
	useSettings,
	useUpsertBudget,
} from "../api/hooks";

const monthRange = () => {
	const now = new Date();
	const from = new Date(now.getFullYear(), now.getMonth(), 1)
		.toISOString()
		.slice(0, 10);
	const to = new Date(now.getFullYear(), now.getMonth() + 1, 0)
		.toISOString()
		.slice(0, 10);
	return { from, to };
};

export default function Budgets() {
	const { data: budgets } = useBudgets();
	const { data: categories } = useCategories();
	const { data: settings } = useSettings();
	const { data: report } = useReport(monthRange());
	const upsert = useUpsertBudget();

	const [categoryId, setCategoryId] = useState<string>("");
	const [limit, setLimit] = useState("");

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
				currencyCode: settings?.baseCurrency ?? "USD",
			},
			{ onSuccess: () => setLimit("") },
		);
	};

	return (
		<div className="screen">
			<h2>Budgets ({settings?.baseCurrency})</h2>
			<ul className="list">
				{budgets?.map((b) => {
					const spent = spentFor(b.categoryId);
					const label =
						b.categoryId === null
							? "Overall"
							: (categories?.find((c) => c.id === b.categoryId)?.name ?? "—");
					const over = spent > b.limitAmount;
					return (
						<li key={b.id} style={{ color: over ? "crimson" : undefined }}>
							{label}: {spent.toFixed(2)} / {b.limitAmount.toFixed(2)}
						</li>
					);
				})}
			</ul>
			<div className="row">
				<select
					value={categoryId}
					onChange={(e) => setCategoryId(e.target.value)}
				>
					<option value="">Overall</option>
					{categories?.map((c) => (
						<option key={c.id} value={c.id}>
							{c.name}
						</option>
					))}
				</select>
				<input
					inputMode="decimal"
					placeholder="Limit"
					value={limit}
					onChange={(e) => setLimit(e.target.value)}
				/>
				<button onClick={submit}>Save</button>
			</div>
		</div>
	);
}
