import { useState } from "react";
import { useCategories, useCreateCategory } from "../api/hooks";

export default function Categories() {
	const { data: categories } = useCategories();
	const create = useCreateCategory();
	const [name, setName] = useState("");
	const [emoji, setEmoji] = useState("🏷️");

	const submit = () => {
		if (!name.trim()) return;
		create.mutate(
			{ name: name.trim(), emoji },
			{ onSuccess: () => setName("") },
		);
	};

	return (
		<div className="screen">
			<p className="eyebrow">{categories?.length ?? 0} categories</p>

			<section className="card">
				<div className="chip-grid">
					{categories?.map((c) => (
						<span key={c.id} className="chip" style={{ cursor: "default" }}>
							<span className="emoji">{c.emoji}</span>
							{c.name}
						</span>
					))}
				</div>
			</section>

			<section className="card">
				<h3>New category</h3>
				<div className="row">
					<input
						value={emoji}
						onChange={(e) => setEmoji(e.target.value)}
						style={{ width: 60, textAlign: "center", flex: "none" }}
						aria-label="Emoji"
					/>
					<input
						className="grow"
						placeholder="Name — e.g. Coffee"
						value={name}
						onChange={(e) => setName(e.target.value)}
					/>
					<button className="btn btn--primary" onClick={submit}>
						Add
					</button>
				</div>
			</section>
		</div>
	);
}
