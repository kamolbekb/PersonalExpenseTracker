import { useEffect, useRef, useState } from "react";
import { useCategories, useCreateExpense, useSettings } from "../api/hooks";

const today = () => new Date().toISOString().slice(0, 10);

export default function AddExpense() {
	const { data: categories } = useCategories();
	const { data: settings } = useSettings();
	const createExpense = useCreateExpense();

	const [amount, setAmount] = useState("");
	const [currency, setCurrency] = useState("USD");
	const [categoryId, setCategoryId] = useState<number | null>(null);
	const [spentOn, setSpentOn] = useState(today());
	const [note, setNote] = useState("");

	useEffect(() => {
		if (settings) setCurrency(settings.baseCurrency);
	}, [settings]);
	useEffect(() => {
		if (categories?.length && categoryId === null)
			setCategoryId(categories[0].id);
	}, [categories, categoryId]);

	// Keep the latest submit logic in a ref so the Main Button handler is registered once.
	const submitRef = useRef<() => void>(() => {});
	submitRef.current = () => {
		const wa = window.Telegram?.WebApp;
		const value = parseFloat(amount);
		if (!value || value <= 0 || categoryId === null) return;
		wa?.MainButton.showProgress();
		createExpense.mutate(
			{
				amount: value,
				currencyCode: currency,
				categoryId,
				spentOn,
				note: note || null,
			},
			{
				onSuccess: () => {
					wa?.HapticFeedback.impactOccurred("medium");
					setAmount("");
					setNote("");
					wa?.MainButton.hideProgress();
				},
				onError: () => wa?.MainButton.hideProgress(),
			},
		);
	};

	useEffect(() => {
		const wa = window.Telegram?.WebApp;
		if (!wa) return;
		const mb = wa.MainButton;
		mb.setText("Save expense");
		mb.show();
		const handler = () => submitRef.current();
		mb.onClick(handler);
		return () => {
			mb.offClick(handler);
			mb.hide();
		};
	}, []);

	return (
		<div className="screen">
			<h2>Add expense</h2>
			<input
				inputMode="decimal"
				placeholder="0.00"
				value={amount}
				onChange={(e) => setAmount(e.target.value)}
			/>
			<input
				value={currency}
				onChange={(e) => setCurrency(e.target.value.toUpperCase())}
				maxLength={3}
			/>
			<select
				value={categoryId ?? ""}
				onChange={(e) => setCategoryId(Number(e.target.value))}
			>
				{categories?.map((c) => (
					<option key={c.id} value={c.id}>
						{c.emoji} {c.name}
					</option>
				))}
			</select>
			<input
				type="date"
				value={spentOn}
				onChange={(e) => setSpentOn(e.target.value)}
			/>
			<input
				placeholder="Note (optional)"
				value={note}
				onChange={(e) => setNote(e.target.value)}
			/>
		</div>
	);
}
