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
			<h2>Categories</h2>
			<ul className="list">
				{categories?.map((c) => (
					<li key={c.id}>
						{c.emoji} {c.name}
					</li>
				))}
			</ul>
			<div className="row">
				<input
					value={emoji}
					onChange={(e) => setEmoji(e.target.value)}
					style={{ width: 48 }}
				/>
				<input
					placeholder="New category"
					value={name}
					onChange={(e) => setName(e.target.value)}
				/>
				<button onClick={submit}>Add</button>
			</div>
		</div>
	);
}
