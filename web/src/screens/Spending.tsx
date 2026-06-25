import { useState } from "react";
import {
	useCategories,
	useDeleteExpense,
	useExpenses,
	useReport,
} from "../api/hooks";
import PeriodLedger, { type LedgerRow } from "../components/PeriodLedger";
import { localDateString } from "../lib/date";

// First day of the current month (the default "from").
const monthStart = () => {
	const d = new Date();
	return localDateString(new Date(d.getFullYear(), d.getMonth(), 1));
};

export default function Spending() {
	const [from, setFrom] = useState(monthStart);
	const [to, setTo] = useState(() => localDateString(new Date()));

	const range = { from, to };
	const { data: report } = useReport(range);
	const { data: expenses } = useExpenses(range);
	const { data: categories } = useCategories();
	const del = useDeleteExpense();

	const rows: LedgerRow[] | undefined = expenses?.map((e) => ({
		id: e.id,
		amount: e.amount,
		currencyCode: e.currencyCode,
		categoryId: e.categoryId,
		date: e.spentOn,
		note: e.note,
	}));

	return (
		<PeriodLedger
			from={from}
			to={to}
			onFromChange={setFrom}
			onToChange={setTo}
			report={report}
			rows={rows}
			categories={categories}
			onDelete={(id) => del.mutate(id)}
		/>
	);
}
