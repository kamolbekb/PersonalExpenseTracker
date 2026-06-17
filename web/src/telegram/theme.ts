export function applyTelegramTheme(): void {
	const wa = window.Telegram?.WebApp;
	if (!wa) return;
	wa.ready();
	wa.expand();
	const p = wa.themeParams ?? {};
	const root = document.documentElement.style;
	root.setProperty("--bg", p.bg_color ?? "#ffffff");
	root.setProperty("--text", p.text_color ?? "#000000");
	root.setProperty("--hint", p.hint_color ?? "#888888");
	root.setProperty("--button", p.button_color ?? "#2481cc");
	root.setProperty("--button-text", p.button_text_color ?? "#ffffff");
}
