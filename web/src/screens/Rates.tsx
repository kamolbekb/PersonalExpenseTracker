import { useState } from "react";
import { useGold, useRates } from "../api/hooks";
import { localDateString } from "../lib/date";

const fmt = (n: number) =>
	n.toLocaleString("en-US", { maximumFractionDigits: 2 });

export default function Rates() {
	const [date, setDate] = useState(localDateString(new Date()));
	const { data: rates } = useRates(date);
	const { data: gold } = useGold(date);

	return (
		<div className="screen">
			<h2>Rates</h2>
			<input
				type="date"
				value={date}
				onChange={(e) => setDate(e.target.value)}
			/>

			<h3>Currencies (per source)</h3>
			{rates?.rates.length === 0 && <p>No rates for this date.</p>}
			<ul className="list">
				{rates?.rates.map((r) => (
					<li key={`${r.source}-${r.currency}`}>
						<strong>{r.source}</strong> · 1 {r.currency} = {fmt(r.ratePerUnit)}{" "}
						UZS
						<span className="hint">
							{" "}
							· 1 UZS = {r.unitPerUzs.toFixed(6)} {r.currency}
						</span>
					</li>
				))}
			</ul>

			<h3>Gold (CBU)</h3>
			{gold?.historyFrom && (
				<p className="hint">History from {gold.historyFrom}</p>
			)}
			<ul className="list">
				{gold?.items.map((g) => (
					<li key={g.item}>
						{g.item}: sell {g.sellPrice ? fmt(g.sellPrice) : "—"} / buy-back{" "}
						{g.buyBackPrice ? fmt(g.buyBackPrice) : "—"} UZS
					</li>
				))}
			</ul>
		</div>
	);
}
