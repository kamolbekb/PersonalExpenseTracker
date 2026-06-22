// The currencies offered in every picker (Add, Settings) and fetched for
// rates / the converter. CBU quotes all of these.
export const CURRENCIES = ["UZS", "USD", "RUB", "KZT"] as const;

// Comma-separated form for the /api/rates `currencies` query (excludes the UZS base).
export const RATE_CURRENCIES = CURRENCIES.filter((c) => c !== "UZS").join(",");
