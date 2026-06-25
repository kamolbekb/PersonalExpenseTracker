import { useState } from "react";
import { Link } from "react-router-dom";
import {
	useDeleteIncome,
	useIncomeCategories,
	useIncomeReport,
	useIncomes,
} from "../api/hooks";
import PeriodLedger, { type LedgerRow } from "../components/PeriodLedger";
import { localDateString } from "../lib/date";

const monthStart = () => {
	const d = new Date();
	return localDateString(new Date(d.getFullYear(), d.getMonth(), 1));
};

export default function Income() {
	const [from, setFrom] = useState(monthStart);
	const [to, setTo] = useState(() => localDateString(new Date()));

	const range = { from, to };
	const { data: report } = useIncomeReport(range);
	const { data: incomes } = useIncomes(range);
	const { data: categories } = useIncomeCategories();
	const del = useDeleteIncome();

	const rows: LedgerRow[] | undefined = incomes?.map((i) => ({
		id: i.id,
		amount: i.amount,
		currencyCode: i.currencyCode,
		categoryId: i.incomeCategoryId,
		date: i.receivedOn,
		note: i.note,
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
			emptyVerb="earned"
			emptyCategoryText="No income in this category."
			headerAction={
				<Link className="btn btn--primary btn--block" to="/income/add">
					+ Add income
				</Link>
			}
		/>
	);
}
