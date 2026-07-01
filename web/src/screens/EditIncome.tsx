import { useNavigate, useParams } from "react-router-dom";
import {
	useIncome,
	useIncomeCategories,
	useSettings,
	useUpdateIncome,
} from "../api/hooks";
import TransactionForm, {
	type TransactionValues,
} from "../components/TransactionForm";

export default function EditIncome() {
	const { id } = useParams();
	const incomeId = Number(id);
	const navigate = useNavigate();
	const { data: categories } = useIncomeCategories();
	const { data: settings } = useSettings();
	const { data: income, isLoading, isError } = useIncome(incomeId);
	const updateIncome = useUpdateIncome();

	const submit = (v: TransactionValues) =>
		updateIncome.mutateAsync({
			id: incomeId,
			amount: v.amount,
			currencyCode: v.currency,
			incomeCategoryId: v.categoryId,
			receivedOn: v.date,
			note: v.note,
		});

	if (isLoading)
		return (
			<div className="screen">
				<div className="card empty">Loading…</div>
			</div>
		);
	if (isError || !income)
		return (
			<div className="screen">
				<div className="card empty">Income not found.</div>
			</div>
		);

	return (
		<TransactionForm
			categories={categories}
			baseCurrency={settings?.baseCurrency}
			amountLabel="Amount received"
			notePlaceholder="Optional — e.g. June salary"
			submitText="Save changes"
			mode="edit"
			initial={{
				amount: income.amount,
				currency: income.currencyCode,
				categoryId: income.incomeCategoryId,
				date: income.receivedOn,
				note: income.note,
			}}
			onSubmit={submit}
			onSaved={() => navigate(-1)}
		/>
	);
}
