/** Amount input helpers shared by the transaction forms. */

/** Format a typed amount with thousands separators, keeping up to 2 decimals. */
export function formatAmount(raw: string): string {
	let s = raw.replace(/[^\d.]/g, "");
	const dot = s.indexOf(".");
	if (dot !== -1)
		s =
			s.slice(0, dot + 1) +
			s
				.slice(dot + 1)
				.replace(/\./g, "")
				.slice(0, 2);
	const [int, dec] = s.split(".");
	const intFmt = int ? Number(int).toLocaleString("en-US") : "";
	return dec !== undefined ? `${intFmt}.${dec}` : intFmt;
}

/** Parse a formatted amount string back to a number. */
export const toNumber = (s: string) => parseFloat(s.replace(/,/g, ""));

/**
 * Shrink the amount input's font as the formatted value grows, so large
 * totals (1,000,000+) stay inside the field instead of clipping.
 */
export function amountFontSize(value: string): number {
	const len = value.length;
	return len <= 9 ? 54 : Math.max(28, 54 - (len - 9) * 4);
}
