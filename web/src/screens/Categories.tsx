import { useState } from "react";
import {
	useCategories,
	useCreateCategory,
	useUpdateCategory,
} from "../api/hooks";

export default function Categories() {
	const { data: categories } = useCategories();
	const create = useCreateCategory();
	const update = useUpdateCategory();
	const [editingId, setEditingId] = useState<number | null>(null);
	const [name, setName] = useState("");
	const [emoji, setEmoji] = useState("🏷️");

	const reset = () => {
		setEditingId(null);
		setName("");
		setEmoji("🏷️");
	};

	const startEdit = (id: number, curName: string, curEmoji: string) => {
		setEditingId(id);
		setName(curName);
		setEmoji(curEmoji);
	};

	const submit = () => {
		if (!name.trim()) return;
		if (editingId !== null) {
			update.mutate(
				{ id: editingId, name: name.trim(), emoji },
				{ onSuccess: reset },
			);
		} else {
			create.mutate({ name: name.trim(), emoji }, { onSuccess: () => setName("") });
		}
	};

	return (
		<div className="screen">
			<p className="eyebrow">{categories?.length ?? 0} categories</p>

			<section className="card">
				<div className="chip-grid">
					{categories?.map((c) => (
						<button
							key={c.id}
							className={`chip${editingId === c.id ? " chip--active" : ""}`}
							onClick={() => startEdit(c.id, c.name, c.emoji)}
						>
							<span className="emoji">{c.emoji}</span>
							{c.name}
						</button>
					))}
				</div>
			</section>

			<section className="card">
				<h3>{editingId !== null ? "Edit category" : "New category"}</h3>
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
						{editingId !== null ? "Save" : "Add"}
					</button>
				</div>
				{editingId !== null && (
					<button
						className="btn btn--block"
						style={{ marginTop: 10 }}
						onClick={reset}
					>
						Cancel
					</button>
				)}
			</section>
		</div>
	);
}
