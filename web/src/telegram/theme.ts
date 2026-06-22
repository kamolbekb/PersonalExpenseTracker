import { applyTheme } from "../lib/appTheme";

/**
 * Bridges the app to the Telegram client: expands the viewport and applies the
 * user's theme preference (System / Light / Dark — see lib/appTheme). When the
 * preference is "system", changes in the Telegram or OS theme re-apply live.
 */
export function applyTelegramTheme(): void {
	const wa = window.Telegram?.WebApp;

	if (wa) {
		wa.ready();
		wa.expand();
	}
	applyTheme();

	wa?.onEvent?.("themeChanged", applyTheme);
	window
		.matchMedia?.("(prefers-color-scheme: dark)")
		.addEventListener?.("change", applyTheme);
}
