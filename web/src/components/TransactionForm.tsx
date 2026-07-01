import { useEffect, useRef, useState } from "react";
import { localDateString } from "../lib/date";
import { formatAmount, toNumber, amountFontSize } from "../lib/amount";
import { CURRENCIES } from "../lib/currencies";

const today = () => localDateString(new Date());
const inTelegram = () => Boolean(window.Telegram?.WebApp?.initData);

export interface TransactionValues {
	amount: number;
	currency: string;
	categoryId: number;
	date: string;
	note: string | null;
}

interface FormCategory {
	id: number;
	name: string;
	emoji: string;
}

interface TransactionFormProps {
	categories?: FormCategory[];
	baseCurrency?: string;
	amountLabel: string;
	notePlaceholder: string;
	submitText: string;
	mode: "create" | "edit";
	/** Pre-filled values for edit mode. */
	initial?: TransactionValues;
	/** Runs the mutation; resolve on success, reject on failure. */
	onSubmit: (values: TransactionValues) => Promise<unknown>;
	/** Called after a successful edit save (e.g. navigate back). */
	onSaved?: () => void;
}

export default function TransactionForm({
	categories,
	baseCurrency,
	amountLabel,
	notePlaceholder,
	submitText,
	mode,
	initial,
	onSubmit,
	onSaved,
}: TransactionFormProps) {
	const [amount, setAmount] = useState(
		initial ? formatAmount(String(initial.amount)) : "",
	);
	// Currency/category default from initial/settings/first-category; user choices override.
	const [currencyOverride, setCurrencyOverride] = useState<string | null>(null);
	const [categoryOverride, setCategoryOverride] = useState<number | null>(null);
	const [date, setDate] = useState(initial?.date ?? today());
	const [note, setNote] = useState(initial?.note ?? "");
	const [justSaved, setJustSaved] = useState(false);

	const currency =
		currencyOverride ?? initial?.currency ?? baseCurrency ?? "UZS";
	const categoryId =
		categoryOverride ?? initial?.categoryId ?? categories?.[0]?.id ?? null;

	// Keep the latest submit logic in a ref so the Main Button handler is registered once.
	const submitRef = useRef<() => void>(() => {});
	useEffect(() => {
		submitRef.current = () => {
			const wa = window.Telegram?.WebApp;
			const value = toNumber(amount);
			if (!value || value <= 0 || categoryId === null) return;
			wa?.MainButton.showProgress();
			onSubmit({ amount: value, currency, categoryId, date, note: note || null })
				.then(() => {
					wa?.HapticFeedback.impactOccurred("medium");
					if (mode === "create") {
						setAmount("");
						setNote("");
						setJustSaved(true);
						setTimeout(() => setJustSaved(false), 1600);
					} else {
						onSaved?.();
					}
				})
				.catch(() => {})
				.finally(() => wa?.MainButton.hideProgress());
		};
	});

	useEffect(() => {
		const wa = window.Telegram?.WebApp;
		if (!wa) return;
		const mb = wa.MainButton;
		mb.setText(submitText);
		mb.show();
		const handler = () => submitRef.current();
		mb.onClick(handler);
		return () => {
			mb.offClick(handler);
			mb.hide();
		};
	}, [submitText]);

	const canSave = toNumber(amount) > 0 && categoryId !== null;

	return (
		<div className="screen">
			<section className="card hero">
				<p className="eyebrow">{amountLabel}</p>
				<div className="amount-field">
					<input
						inputMode="decimal"
						placeholder="0.00"
						value={amount}
						onChange={(e) => setAmount(formatAmount(e.target.value))}
						autoFocus
						style={{ fontSize: amountFontSize(amount || "0.00") }}
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
							value={date}
							onChange={(e) => setDate(e.target.value)}
						/>
					</div>
				</div>
				<div className="field" style={{ marginTop: 12 }}>
					<label>Note</label>
					<input
						placeholder={notePlaceholder}
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
					{justSaved ? "Saved ✓" : submitText}
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
