import { useEffect, useState } from "react";
import { useSettings, useUpdateSettings } from "../api/hooks";

export default function Settings() {
	const { data: settings } = useSettings();
	const update = useUpdateSettings();
	const [base, setBase] = useState("USD");

	useEffect(() => {
		if (settings) setBase(settings.baseCurrency);
	}, [settings]);

	return (
		<div className="screen">
			<h2>Settings</h2>
			<label>Base currency</label>
			<input
				value={base}
				maxLength={3}
				onChange={(e) => setBase(e.target.value.toUpperCase())}
			/>
			<button onClick={() => update.mutate({ baseCurrency: base })}>
				Save
			</button>
			<p className="hint">Reports convert all expenses to this currency.</p>
		</div>
	);
}
