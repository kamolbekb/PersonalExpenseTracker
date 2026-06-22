import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useSettings, useUpdateSettings } from "../api/hooks";
import { IconTag } from "../components/icons";

export default function Settings() {
	const { data: settings } = useSettings();
	const update = useUpdateSettings();
	const [base, setBase] = useState("UZS");
	const [saved, setSaved] = useState(false);

	useEffect(() => {
		if (settings) setBase(settings.baseCurrency);
	}, [settings]);

	const save = () =>
		update.mutate(
			{ baseCurrency: base },
			{
				onSuccess: () => {
					setSaved(true);
					setTimeout(() => setSaved(false), 1500);
				},
			},
		);

	return (
		<div className="screen">
			<section className="card">
				<h3>Base currency</h3>
				<div className="row">
					<input
						className="grow"
						value={base}
						maxLength={3}
						onChange={(e) => setBase(e.target.value.toUpperCase())}
						style={{ textTransform: "uppercase", letterSpacing: "0.08em" }}
					/>
					<button className="btn btn--primary" onClick={save}>
						{saved ? "Saved ✓" : "Save"}
					</button>
				</div>
				<p className="hint" style={{ marginTop: 12 }}>
					Reports and budgets convert all expenses to this currency using the
					CBU rate as of each expense's date.
				</p>
			</section>

			<Link
				to="/categories"
				className="card row"
				style={{ alignItems: "center", gap: 13 }}
			>
				<span className="avatar">
					<IconTag className="" />
				</span>
				<div className="item__main">
					<div className="item__title" style={{ color: "var(--ink)" }}>
						Manage categories
					</div>
					<div className="item__sub">
						Add or review your spending categories
					</div>
				</div>
				<span style={{ color: "var(--muted)", fontSize: 22 }}>›</span>
			</Link>
		</div>
	);
}
