/* Stroke icon set — inherits currentColor, 24px grid. */
type P = { className?: string };
const base = {
	viewBox: "0 0 24 24",
	fill: "none",
	stroke: "currentColor",
	strokeWidth: 1.9,
	strokeLinecap: "round" as const,
	strokeLinejoin: "round" as const,
};

export const IconAdd = (p: P) => (
	<svg {...base} {...p}>
		<circle cx="12" cy="12" r="9" />
		<path d="M12 8.5v7M8.5 12h7" />
	</svg>
);
export const IconList = (p: P) => (
	<svg {...base} {...p}>
		<path d="M6 3.5h9l3.5 3.5v13.5H6z" />
		<path d="M9 9.5h6M9 13h6M9 16.5h3.5" />
	</svg>
);
export const IconReports = (p: P) => (
	<svg {...base} {...p}>
		<path d="M4.5 20h15" />
		<path d="M7.5 20v-7M12 20V6.5M16.5 20v-4.5" />
	</svg>
);
export const IconBudgets = (p: P) => (
	<svg {...base} {...p}>
		<circle cx="12" cy="12" r="8.5" />
		<circle cx="12" cy="12" r="3.4" />
		<path d="M12 3.5v3M12 17.5v3M20.5 12h-3M6.5 12h-3" />
	</svg>
);
export const IconRates = (p: P) => (
	<svg {...base} {...p}>
		<path d="M4.5 8.5h13l-3-3M19.5 15.5h-13l3 3" />
	</svg>
);
export const IconSettings = (p: P) => (
	<svg {...base} {...p}>
		<circle cx="12" cy="12" r="3" />
		<path d="M19.4 13.5a1.7 1.7 0 0 0 .3 1.9l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-2.9 1.2v.1a2 2 0 1 1-4 0v-.2a1.7 1.7 0 0 0-2.9-1.1l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0-1.1-2.9H4a2 2 0 1 1 0-4h.2a1.7 1.7 0 0 0 1.1-2.9l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.9.3h.1a1.7 1.7 0 0 0 1-1.5V4a2 2 0 1 1 4 0v.2a1.7 1.7 0 0 0 2.9 1.1l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.9v.1a1.7 1.7 0 0 0 1.5 1H20a2 2 0 1 1 0 4h-.2a1.7 1.7 0 0 0-1.5 1z" />
	</svg>
);
export const IconTrash = (p: P) => (
	<svg {...base} {...p}>
		<path d="M5 7h14M10 7V5.5h4V7M8 7l.6 11h6.8L16 7" />
	</svg>
);
export const IconTag = (p: P) => (
	<svg {...base} {...p}>
		<path d="M4 12.5V5.5A1.5 1.5 0 0 1 5.5 4h7l7.5 7.5a1.5 1.5 0 0 1 0 2.1l-5.4 5.4a1.5 1.5 0 0 1-2.1 0z" />
		<circle cx="8.5" cy="8.5" r="1.3" fill="currentColor" stroke="none" />
	</svg>
);
