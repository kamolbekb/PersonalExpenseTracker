export interface Category {
	id: number;
	name: string;
	emoji: string;
	isArchived: boolean;
}

export interface Expense {
	id: number;
	amount: number;
	currencyCode: string;
	categoryId: number;
	spentOn: string;
	note: string | null;
}

export interface ExpenseInput {
	amount: number;
	currencyCode: string;
	categoryId: number;
	spentOn: string;
	note: string | null;
}

export interface Budget {
	id: number;
	categoryId: number | null;
	limitAmount: number;
	currencyCode: string;
}

export interface BudgetInput {
	categoryId: number | null;
	limitAmount: number;
	currencyCode: string;
}

export interface CategoryTotal {
	categoryId: number;
	categoryName: string;
	total: number;
}

export interface MonthTotal {
	month: string;
	total: number;
}

export interface ReportSummary {
	baseCurrency: string;
	grandTotal: number;
	byCategory: CategoryTotal[];
	byMonth: MonthTotal[];
}

export interface Settings {
	baseCurrency: string;
}

export interface RateRow {
	source: string;
	currency: string;
	ratePerUnit: number;
	unitPerUzs: number;
	buy: number | null;
	sell: number | null;
}

export interface RatesView {
	date: string;
	rates: RateRow[];
}

export interface GoldRow {
	item: string;
	sellPrice: number | null;
	buyBackPrice: number | null;
}

export interface GoldView {
	date: string;
	historyFrom: string | null;
	items: GoldRow[];
}
