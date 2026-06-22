/**
 * Bridges the app's own "warm editorial" design system to the Telegram client:
 * - expands the viewport,
 * - mirrors the user's light/dark choice onto <html data-theme>,
 * - paints Telegram's native header/background to match our --bg so the chrome
 *   blends seamlessly (no white bar above a warm-paper app).
 *
 * It does NOT overwrite our design tokens — the palette lives in index.css.
 */
function bgFor(scheme: "light" | "dark"): string {
	return scheme === "dark" ? "#000000" : "#f2f2f7";
}

export function applyTelegramTheme(): void {
	const root = document.documentElement;
	const wa = window.Telegram?.WebApp;

	const sync = () => {
		const scheme: "light" | "dark" =
			wa?.colorScheme ??
			(window.matchMedia?.("(prefers-color-scheme: dark)").matches
				? "dark"
				: "light");
		root.setAttribute("data-theme", scheme);
		const bg = bgFor(scheme);
		wa?.setHeaderColor?.(bg);
		wa?.setBackgroundColor?.(bg);
	};

	if (!wa) {
		sync();
		window
			.matchMedia?.("(prefers-color-scheme: dark)")
			.addEventListener?.("change", sync);
		return;
	}

	wa.ready();
	wa.expand();
	sync();
	wa.onEvent?.("themeChanged", sync);
}
