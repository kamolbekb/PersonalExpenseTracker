import { useNavigate, useParams } from "react-router-dom";
import {
	useCategories,
	useExpense,
	useSettings,
	useUpdateExpense,
} from "../api/hooks";
import TransactionForm, {
	type TransactionValues,
} from "../components/TransactionForm";

export default function EditExpense() {
	const { id } = useParams();
	const expenseId = Number(id);
	const navigate = useNavigate();
	const { data: categories } = useCategories();
	const { data: settings } = useSettings();
	const { data: expense, isLoading, isError } = useExpense(expenseId);
	const updateExpense = useUpdateExpense();

	const submit = (v: TransactionValues) =>
		updateExpense.mutateAsync({
			id: expenseId,
			amount: v.amount,
			currencyCode: v.currency,
			categoryId: v.categoryId,
			spentOn: v.date,
			note: v.note,
		});

	if (isLoading)
		return (
			<div className="screen">
				<div className="card empty">Loading…</div>
			</div>
		);
	if (isError || !expense)
		return (
			<div className="screen">
				<div className="card empty">Expense not found.</div>
			</div>
		);

	return (
		<TransactionForm
			categories={categories}
			baseCurrency={settings?.baseCurrency}
			amountLabel="Amount spent"
			notePlaceholder="Optional — e.g. lunch with team"
			submitText="Save changes"
			mode="edit"
			initial={{
				amount: expense.amount,
				currency: expense.currencyCode,
				categoryId: expense.categoryId,
				date: expense.spentOn,
				note: expense.note,
			}}
			onSubmit={submit}
			onSaved={() => navigate(-1)}
		/>
	);
}
