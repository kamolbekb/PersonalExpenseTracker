import { useCategories, useDeleteExpense, useExpenses } from "../api/hooks";
import { IconTrash } from "../components/icons";

const fmtAmount = (n: number) =>
	n.toLocaleString("en-US", {
		minimumFractionDigits: 2,
		maximumFractionDigits: 2,
	});

const fmtDate = (iso: string) => {
	const d = new Date(iso + "T00:00:00");
	return d.toLocaleDateString("en-US", { month: "short", day: "numeric" });
};

export default function Expenses() {
	const { data: expenses } = useExpenses();
	const { data: categories } = useCategories();
	const del = useDeleteExpense();

	const catFor = (id: number) => categories?.find((c) => c.id === id);

	if (expenses && expenses.length === 0) {
		return (
			<div className="screen">
				<div className="card empty">
					<span className="emoji">🧾</span>
					No expenses yet.
					<br />
					Add your first one from the Add tab.
				</div>
			</div>
		);
	}

	const total = expenses?.reduce((s, e) => s + e.amount, 0) ?? 0;

	return (
		<div className="screen">
			<p className="eyebrow">
				{expenses?.length ?? 0} {expenses?.length === 1 ? "entry" : "entries"}
			</p>
			<section className="card card--flush">
				<ul className="list">
					{expenses?.map((e) => {
						const cat = catFor(e.categoryId);
						return (
							<li key={e.id} className="item">
								<div className="avatar">{cat?.emoji ?? "💸"}</div>
								<div className="item__main">
									<div className="item__title">{cat?.name ?? "—"}</div>
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
			</section>
			{total > 0 && (
				<p className="hint" style={{ textAlign: "center" }}>
					Showing amounts in their original currencies.
				</p>
			)}
		</div>
	);
}
