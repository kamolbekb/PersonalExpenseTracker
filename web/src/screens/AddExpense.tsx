import { useEffect, useRef, useState } from "react";
import { useCategories, useCreateExpense, useSettings } from "../api/hooks";
import { localDateString } from "../lib/date";

const today = () => localDateString(new Date());
const inTelegram = () => Boolean(window.Telegram?.WebApp?.initData);

// Common currencies; the chosen base currency is always included.
const CURRENCIES = [
	"UZS",
	"USD",
	"RUB",
	"EUR",
	"GBP",
	"KZT",
	"TRY",
	"KRW",
	"JPY",
	"CNY",
	"AED",
];

export default function AddExpense() {
	const { data: categories } = useCategories();
	const { data: settings } = useSettings();
	const createExpense = useCreateExpense();

	const [amount, setAmount] = useState("");
	const [currency, setCurrency] = useState("UZS");
	const [categoryId, setCategoryId] = useState<number | null>(null);
	const [spentOn, setSpentOn] = useState(today());
	const [note, setNote] = useState("");
	const [justSaved, setJustSaved] = useState(false);

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
					setJustSaved(true);
					setTimeout(() => setJustSaved(false), 1600);
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

	const canSave = parseFloat(amount) > 0 && categoryId !== null;

	return (
		<div className="screen">
			<section className="card hero">
				<p className="eyebrow">Amount spent</p>
				<div className="amount-field">
					<input
						inputMode="decimal"
						placeholder="0.00"
						value={amount}
						onChange={(e) => setAmount(e.target.value)}
						autoFocus
					/>
					<select
						className="cur-select"
						value={currency}
						onChange={(e) => setCurrency(e.target.value)}
						aria-label="Currency"
					>
						{(CURRENCIES.includes(currency)
							? CURRENCIES
							: [currency, ...CURRENCIES]
						).map((c) => (
							<option key={c} value={c}>
								{c}
							</option>
						))}
					</select>
				</div>
			</section>

			<p className="eyebrow">Category</p>
			<div className="chip-grid">
				{categories?.map((c) => (
					<button
						key={c.id}
						className={`chip${categoryId === c.id ? " chip--active" : ""}`}
						onClick={() => setCategoryId(c.id)}
					>
						<span className="emoji">{c.emoji}</span>
						{c.name}
					</button>
				))}
			</div>

			<section className="card">
				<div className="row" style={{ gap: 12 }}>
					<div className="field grow">
						<label>Date</label>
						<input
							type="date"
							value={spentOn}
							onChange={(e) => setSpentOn(e.target.value)}
						/>
					</div>
				</div>
				<div className="field" style={{ marginTop: 12 }}>
					<label>Note</label>
					<input
						placeholder="Optional — e.g. lunch with team"
						value={note}
						onChange={(e) => setNote(e.target.value)}
					/>
				</div>
			</section>

			{!inTelegram() && (
				<button
					className="btn btn--primary btn--block"
					disabled={!canSave}
					style={{ opacity: canSave ? 1 : 0.5 }}
					onClick={() => submitRef.current()}
				>
					{justSaved ? "Saved ✓" : "Save expense"}
				</button>
			)}
			{justSaved && inTelegram() && (
				<p className="hint" style={{ textAlign: "center" }}>
					Saved ✓ — add another
				</p>
			)}
		</div>
	);
}
