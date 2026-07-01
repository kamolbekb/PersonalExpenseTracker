import { useCreateIncome, useIncomeCategories, useSettings } from "../api/hooks";
import TransactionForm, {
	type TransactionValues,
} from "../components/TransactionForm";

export default function AddIncome() {
	const { data: categories } = useIncomeCategories();
	const { data: settings } = useSettings();
	const createIncome = useCreateIncome();

	const submit = (v: TransactionValues) =>
		createIncome.mutateAsync({
			amount: v.amount,
			currencyCode: v.currency,
			incomeCategoryId: v.categoryId,
			receivedOn: v.date,
			note: v.note,
		});

	return (
		<TransactionForm
			categories={categories}
			baseCurrency={settings?.baseCurrency}
			amountLabel="Amount received"
			notePlaceholder="Optional — e.g. June salary"
			submitText="Save income"
			mode="create"
			onSubmit={submit}
		/>
	);
}
