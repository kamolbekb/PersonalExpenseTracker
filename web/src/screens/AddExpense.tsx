import { useCategories, useCreateExpense, useSettings } from "../api/hooks";
import TransactionForm, {
	type TransactionValues,
} from "../components/TransactionForm";

export default function AddExpense() {
	const { data: categories } = useCategories();
	const { data: settings } = useSettings();
	const createExpense = useCreateExpense();

	const submit = (v: TransactionValues) =>
		createExpense.mutateAsync({
			amount: v.amount,
			currencyCode: v.currency,
			categoryId: v.categoryId,
			spentOn: v.date,
			note: v.note,
		});

	return (
		<TransactionForm
			categories={categories}
			baseCurrency={settings?.baseCurrency}
			amountLabel="Amount spent"
			notePlaceholder="Optional — e.g. lunch with team"
			submitText="Save expense"
			mode="create"
			onSubmit={submit}
		/>
	);
}
