import { useMemo, useState } from "react";
import { useGold, useRates, useSettings } from "../api/hooks";
import { localDateString } from "../lib/date";

const flag: Record<string, string> = {
	UZS: "🇺🇿",
	USD: "🇺🇸",
	RUB: "🇷🇺",
	EUR: "🇪🇺",
	GBP: "🇬🇧",
	KZT: "🇰🇿",
};

const fmt = (n: number) =>
	n.toLocaleString("en-US", { maximumFractionDigits: 2 });

// Currency-aware: UZS rounds to whole sum, others to 2 dp.
const fmtCur = (n: number, cur: string) =>
	n.toLocaleString("en-US", {
		maximumFractionDigits: cur === "UZS" ? 0 : 2,
	});

export default function Rates() {
	const [date, setDate] = useState(localDateString(new Date()));
	const { data: rates } = useRates(date);
	const { data: gold } = useGold(date);
	const { data: settings } = useSettings();
	const base = settings?.baseCurrency ?? "UZS";

	// UZS per 1 unit of each currency (UZS itself = 1).
	const ratePerUnit = useMemo(() => {
		const map: Record<string, number> = { UZS: 1 };
		rates?.rates.forEach((r) => (map[r.currency] = r.ratePerUnit));
		return map;
	}, [rates]);
	const currencies = Object.keys(ratePerUnit); // e.g. ["UZS","USD","RUB","KZT"]

	const [amount, setAmount] = useState("100");
	// Default the "from" currency to the user's base currency until they pick one.
	const [from, setFrom] = useState<string | null>(null);
	const fromCur =
		from && currencies.includes(from)
			? from
			: currencies.includes(base)
				? base
				: (currencies[0] ?? "UZS");
	const value = parseFloat(amount) || 0;
	const inUzs = value * (ratePerUnit[fromCur] ?? 1);

	return (
		<div className="screen">
			<section className="card">
				<div className="field">
					<label>As of date</label>
					<input
						type="date"
						value={date}
						onChange={(e) => setDate(e.target.value)}
					/>
				</div>
			</section>

			{/* Converter */}
			<p className="eyebrow">Converter</p>
			<section className="card">
				<div className="row">
					<input
						className="grow"
						inputMode="decimal"
						placeholder="0"
						value={amount}
						onChange={(e) => setAmount(e.target.value)}
						aria-label="Amount"
					/>
					<select
						value={fromCur}
						onChange={(e) => setFrom(e.target.value)}
						style={{ width: 110, flex: "none" }}
						aria-label="From currency"
					>
						{currencies.map((c) => (
							<option key={c} value={c}>
								{c}
							</option>
						))}
					</select>
				</div>

				{currencies.length <= 1 ? (
					<p className="hint" style={{ marginTop: 12 }}>
						Rates unavailable for this date.
					</p>
				) : (
					<ul className="list" style={{ marginTop: 6 }}>
						{currencies
							.filter((c) => c !== fromCur)
							.map((c) => (
								<li key={c} className="item">
									<span className="avatar">{flag[c] ?? "💱"}</span>
									<div className="item__main">
										<div className="item__title">{c}</div>
									</div>
									<div className="item__amount">
										{fmtCur(inUzs / (ratePerUnit[c] ?? 1), c)}
										<span className="item__cur">{c}</span>
									</div>
								</li>
							))}
					</ul>
				)}
			</section>

			<p className="eyebrow">Exchange rates</p>
			{rates?.rates.length === 0 && (
				<div className="card empty">No rates published for this date.</div>
			)}
			{rates?.rates.map((r) => (
				<section className="card" key={`${r.source}-${r.currency}`}>
					<div className="row" style={{ alignItems: "center", gap: 12 }}>
						<span className="avatar">{flag[r.currency] ?? "💱"}</span>
						<div className="item__main">
							<div className="item__title">
								1 {r.currency} ={" "}
								<span className="num" style={{ fontWeight: 600 }}>
									{fmt(r.ratePerUnit)}
								</span>{" "}
								UZS
							</div>
							<div className="item__sub">
								1 UZS = {r.unitPerUzs.toFixed(6)} {r.currency}
							</div>
						</div>
						<span className="pill">{r.source}</span>
					</div>
				</section>
			))}

			<p className="eyebrow">Gold bars · CBU</p>
			{gold?.historyFrom && (
				<p className="hint" style={{ marginTop: -6, paddingLeft: 4 }}>
					History from {gold.historyFrom}
				</p>
			)}
			<section className="card card--flush">
				{gold?.items.length === 0 && (
					<div className="empty" style={{ padding: 28 }}>
						No gold prices for this date.
					</div>
				)}
				<ul className="list">
					{gold?.items.map((g) => (
						<li key={g.item} className="item">
							<span className="avatar">🪙</span>
							<div className="item__main">
								<div className="item__title">{g.item}</div>
								<div className="item__sub">
									buy-back {g.buyBackPrice ? fmt(g.buyBackPrice) : "—"} UZS
								</div>
							</div>
							<div className="item__amount">
								{g.sellPrice ? fmt(g.sellPrice) : "—"}
								<span className="item__cur">UZS</span>
							</div>
						</li>
					))}
				</ul>
			</section>
		</div>
	);
}
