// Theme preference: "system" follows Telegram / the OS; "light"/"dark" force it.
// Persisted in localStorage and applied to <html data-theme>.

export type ThemePref = "system" | "light" | "dark";
const KEY = "themePref";

export function getThemePref(): ThemePref {
	const v = localStorage.getItem(KEY);
	return v === "light" || v === "dark" ? v : "system";
}

function systemScheme(): "light" | "dark" {
	const wa = window.Telegram?.WebApp;
	if (wa?.colorScheme) return wa.colorScheme;
	return window.matchMedia?.("(prefers-color-scheme: dark)").matches
		? "dark"
		: "light";
}

export function effectiveScheme(): "light" | "dark" {
	const pref = getThemePref();
	return pref === "system" ? systemScheme() : pref;
}

/** Apply the current preference to the document + Telegram chrome. */
export function applyTheme(): void {
	const scheme = effectiveScheme();
	document.documentElement.setAttribute("data-theme", scheme);
	const bg = scheme === "dark" ? "#000000" : "#f2f2f7";
	const wa = window.Telegram?.WebApp;
	wa?.setHeaderColor?.(bg);
	wa?.setBackgroundColor?.(bg);
}

export function setThemePref(pref: ThemePref): void {
	if (pref === "system") localStorage.removeItem(KEY);
	else localStorage.setItem(KEY, pref);
	applyTheme();
}
