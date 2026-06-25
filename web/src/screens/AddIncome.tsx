import { useEffect, useRef, useState } from "react";
import {
	useCreateIncome,
	useIncomeCategories,
	useSettings,
} from "../api/hooks";
import { localDateString } from "../lib/date";
import { CURRENCIES } from "../lib/currencies";

const today = () => localDateString(new Date());
const inTelegram = () => Boolean(window.Telegram?.WebApp?.initData);

// Format a typed amount with thousands separators, keeping up to 2 decimals.
function formatAmount(raw: string): string {
	let s = raw.replace(/[^\d.]/g, "");
	const dot = s.indexOf(".");
	if (dot !== -1)
		s =
			s.slice(0, dot + 1) +
			s
				.slice(dot + 1)
				.replace(/\./g, "")
				.slice(0, 2);
	const [int, dec] = s.split(".");
	const intFmt = int ? Number(int).toLocaleString("en-US") : "";
	return dec !== undefined ? `${intFmt}.${dec}` : intFmt;
}
const toNumber = (s: string) => parseFloat(s.replace(/,/g, ""));

export default function AddIncome() {
	const { data: categories } = useIncomeCategories();
	const { data: settings } = useSettings();
	const createIncome = useCreateIncome();

	const [amount, setAmount] = useState("");
	// Currency/category default from settings/first-category; user choices override.
	const [currencyOverride, setCurrencyOverride] = useState<string | null>(null);
	const [categoryOverride, setCategoryOverride] = useState<number | null>(null);
	const [receivedOn, setReceivedOn] = useState(today());
	const [note, setNote] = useState("");
	const [justSaved, setJustSaved] = useState(false);

	const currency = currencyOverride ?? settings?.baseCurrency ?? "UZS";
	const categoryId = categoryOverride ?? categories?.[0]?.id ?? null;

	// Keep the latest submit logic in a ref so the Main Button handler is registered once.
	const submitRef = useRef<() => void>(() => {});
	useEffect(() => {
		submitRef.current = () => {
			const wa = window.Telegram?.WebApp;
			const value = toNumber(amount);
			if (!value || value <= 0 || categoryId === null) return;
			wa?.MainButton.showProgress();
			createIncome.mutate(
				{
					amount: value,
					currencyCode: currency,
					incomeCategoryId: categoryId,
					receivedOn,
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
	});

	useEffect(() => {
		const wa = window.Telegram?.WebApp;
		if (!wa) return;
		const mb = wa.MainButton;
		mb.setText("Save income");
		mb.show();
		const handler = () => submitRef.current();
		mb.onClick(handler);
		return () => {
			mb.offClick(handler);
			mb.hide();
		};
	}, []);

	const canSave = toNumber(amount) > 0 && categoryId !== null;

	return (
		<div className="screen">
			<section className="card hero">
				<p className="eyebrow">Amount received</p>
				<div className="amount-field">
					<input
						inputMode="decimal"
						placeholder="0.00"
						value={amount}
						onChange={(e) => setAmount(formatAmount(e.target.value))}
						autoFocus
					/>
					<select
						className="cur-select"
						value={currency}
						onChange={(e) => setCurrencyOverride(e.target.value)}
						aria-label="Currency"
					>
						{(CURRENCIES.includes(currency as (typeof CURRENCIES)[number])
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
						onClick={() => setCategoryOverride(c.id)}
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
							value={receivedOn}
							onChange={(e) => setReceivedOn(e.target.value)}
						/>
					</div>
				</div>
				<div className="field" style={{ marginTop: 12 }}>
					<label>Note</label>
					<input
						placeholder="Optional — e.g. June salary"
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
					{justSaved ? "Saved ✓" : "Save income"}
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
