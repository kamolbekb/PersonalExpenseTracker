import { useCategories, useDeleteExpense, useExpenses } from "../api/hooks";

export default function Expenses() {
	const { data: expenses } = useExpenses();
	const { data: categories } = useCategories();
	const del = useDeleteExpense();
	const nameFor = (id: number) =>
		categories?.find((c) => c.id === id)?.name ?? "—";

	return (
		<div className="screen">
			<h2>Expenses</h2>
			{expenses?.length === 0 && <p>No expenses yet.</p>}
			<ul className="list">
				{expenses?.map((e) => (
					<li key={e.id}>
						<span>{e.spentOn}</span>
						<span>{nameFor(e.categoryId)}</span>
						<span>
							{e.amount.toFixed(2)} {e.currencyCode}
						</span>
						<span>{e.note}</span>
						<button onClick={() => del.mutate(e.id)}>✕</button>
					</li>
				))}
			</ul>
		</div>
	);
}
