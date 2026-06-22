declare global {
	interface Window {
		Telegram?: {
			WebApp: {
				initData: string;
				ready: () => void;
				expand: () => void;
				themeParams: Record<string, string>;
				colorScheme?: "light" | "dark";
				setHeaderColor?: (color: string) => void;
				setBackgroundColor?: (color: string) => void;
				onEvent?: (event: string, cb: () => void) => void;
				MainButton: {
					setText: (t: string) => void;
					show: () => void;
					hide: () => void;
					onClick: (cb: () => void) => void;
					offClick: (cb: () => void) => void;
					showProgress: () => void;
					hideProgress: () => void;
				};
				HapticFeedback: { impactOccurred: (s: string) => void };
			};
		};
	}
}

export function getInitData(): string {
	const fromTg = window.Telegram?.WebApp?.initData;
	if (fromTg) return fromTg;
	return import.meta.env.VITE_DEV_INIT_DATA ?? ""; // dev-only fallback
}
