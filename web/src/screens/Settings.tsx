import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useSettings, useUpdateSettings } from "../api/hooks";
import { IconTag } from "../components/icons";
import { CURRENCIES } from "../lib/currencies";
import { getThemePref, setThemePref, type ThemePref } from "../lib/appTheme";

const THEMES: { key: ThemePref; label: string }[] = [
	{ key: "system", label: "System" },
	{ key: "light", label: "Light" },
	{ key: "dark", label: "Dark" },
];

export default function Settings() {
	const { data: settings } = useSettings();
	const update = useUpdateSettings();
	const [base, setBase] = useState("UZS");
	const [saved, setSaved] = useState(false);
	const [theme, setTheme] = useState<ThemePref>(getThemePref());

	useEffect(() => {
		if (settings) setBase(settings.baseCurrency);
	}, [settings]);

	const options = CURRENCIES.includes(base as (typeof CURRENCIES)[number])
		? CURRENCIES
		: [base, ...CURRENCIES];

	const save = () =>
		update.mutate(
			{
				baseCurrency: base,
				incomeTrackingEnabled: settings?.incomeTrackingEnabled ?? false,
			},
			{
				onSuccess: () => {
					setSaved(true);
					setTimeout(() => setSaved(false), 1500);
				},
			},
		);

	const pickTheme = (t: ThemePref) => {
		setTheme(t);
		setThemePref(t);
	};

	return (
		<div className="screen">
			<p className="eyebrow">Appearance</p>
			<section className="card">
				<div className="segmented" role="tablist">
					{THEMES.map((t) => (
						<button
							key={t.key}
							role="tab"
							aria-selected={theme === t.key}
							onClick={() => pickTheme(t.key)}
						>
							{t.label}
						</button>
					))}
				</div>
			</section>

			<p className="eyebrow">Base currency</p>
			<section className="card">
				<div className="row">
					<select
						className="grow"
						value={base}
						onChange={(e) => setBase(e.target.value)}
					>
						{options.map((c) => (
							<option key={c} value={c}>
								{c}
							</option>
						))}
					</select>
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
					<div className="item__title" style={{ color: "var(--label)" }}>
						Manage categories
					</div>
					<div className="item__sub">
						Add or review your spending categories
					</div>
				</div>
				<span style={{ color: "var(--label-2)", fontSize: 22 }}>›</span>
			</Link>
		</div>
	);
}
