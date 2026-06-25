import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type {
	Budget,
	BudgetInput,
	Category,
	Expense,
	ExpenseInput,
	Income,
	IncomeInput,
	IncomeCategory,
	ReportSummary,
	Settings,
	RatesView,
	GoldView,
} from "./types";

export const useCategories = () =>
	useQuery({
		queryKey: ["categories"],
		queryFn: () => api<Category[]>("/categories"),
	});

export const useCreateCategory = () => {
	const qc = useQueryClient();
	return useMutation({
		mutationFn: (input: { name: string; emoji: string }) =>
			api<Category>("/categories", {
				method: "POST",
				body: JSON.stringify(input),
			}),
		onSuccess: () => qc.invalidateQueries({ queryKey: ["categories"] }),
	});
};

export const useExpenses = (
	filters: { from?: string; to?: string; categoryId?: number } = {},
) => {
	const qs = new URLSearchParams();
	if (filters.from) qs.set("from", filters.from);
	if (filters.to) qs.set("to", filters.to);
	if (filters.categoryId) qs.set("categoryId", String(filters.categoryId));
	return useQuery({
		queryKey: ["expenses", filters],
		queryFn: () => api<Expense[]>(`/expenses?${qs.toString()}`),
	});
};

export const useCreateExpense = () => {
	const qc = useQueryClient();
	return useMutation({
		mutationFn: (input: ExpenseInput) =>
			api<Expense>("/expenses", {
				method: "POST",
				body: JSON.stringify(input),
			}),
		onSuccess: () => {
			qc.invalidateQueries({ queryKey: ["expenses"] });
			qc.invalidateQueries({ queryKey: ["report"] });
		},
	});
};

export const useReport = (range: { from: string; to: string }) =>
	useQuery({
		queryKey: ["report", range],
		queryFn: () =>
			api<ReportSummary>(`/reports/summary?from=${range.from}&to=${range.to}`),
	});

export const useBudgets = () =>
	useQuery({ queryKey: ["budgets"], queryFn: () => api<Budget[]>("/budgets") });

export const useUpsertBudget = () => {
	const qc = useQueryClient();
	return useMutation({
		mutationFn: (input: BudgetInput) =>
			api<Budget>("/budgets", { method: "PUT", body: JSON.stringify(input) }),
		onSuccess: () => qc.invalidateQueries({ queryKey: ["budgets"] }),
	});
};

export const useSettings = () =>
	useQuery({
		queryKey: ["settings"],
		queryFn: () => api<Settings>("/settings"),
	});

export const useUpdateSettings = () => {
	const qc = useQueryClient();
	return useMutation({
		mutationFn: (input: Settings) =>
			api<Settings>("/settings", {
				method: "PUT",
				body: JSON.stringify(input),
			}),
		onSuccess: () => {
			qc.invalidateQueries({ queryKey: ["settings"] });
			qc.invalidateQueries({ queryKey: ["report"] });
			qc.invalidateQueries({ queryKey: ["income-report"] });
		},
	});
};

export const useDeleteExpense = () => {
	const qc = useQueryClient();
	return useMutation({
		mutationFn: (id: number) =>
			api<void>(`/expenses/${id}`, { method: "DELETE" }),
		onSuccess: () => {
			qc.invalidateQueries({ queryKey: ["expenses"] });
			qc.invalidateQueries({ queryKey: ["report"] });
		},
	});
};

export const useIncomeCategories = () =>
	useQuery({
		queryKey: ["income-categories"],
		queryFn: () => api<IncomeCategory[]>("/income-categories"),
	});

export const useIncomes = (
	filters: { from?: string; to?: string; categoryId?: number } = {},
) => {
	const qs = new URLSearchParams();
	if (filters.from) qs.set("from", filters.from);
	if (filters.to) qs.set("to", filters.to);
	if (filters.categoryId) qs.set("categoryId", String(filters.categoryId));
	return useQuery({
		queryKey: ["incomes", filters],
		queryFn: () => api<Income[]>(`/incomes?${qs.toString()}`),
	});
};

export const useCreateIncome = () => {
	const qc = useQueryClient();
	return useMutation({
		mutationFn: (input: IncomeInput) =>
			api<Income>("/incomes", {
				method: "POST",
				body: JSON.stringify(input),
			}),
		onSuccess: () => {
			qc.invalidateQueries({ queryKey: ["incomes"] });
			qc.invalidateQueries({ queryKey: ["income-report"] });
		},
	});
};

export const useDeleteIncome = () => {
	const qc = useQueryClient();
	return useMutation({
		mutationFn: (id: number) =>
			api<void>(`/incomes/${id}`, { method: "DELETE" }),
		onSuccess: () => {
			qc.invalidateQueries({ queryKey: ["incomes"] });
			qc.invalidateQueries({ queryKey: ["income-report"] });
		},
	});
};

export const useIncomeReport = (range: { from: string; to: string }) =>
	useQuery({
		queryKey: ["income-report", range],
		queryFn: () =>
			api<ReportSummary>(
				`/reports/income-summary?from=${range.from}&to=${range.to}`,
			),
	});

export const useRates = (date: string, currencies = "USD,RUB,KZT") =>
	useQuery({
		queryKey: ["rates", date, currencies],
		queryFn: () =>
			api<RatesView>(`/rates?date=${date}&currencies=${currencies}`),
	});

export const useGold = (date: string) =>
	useQuery({
		queryKey: ["gold", date],
		queryFn: () => api<GoldView>(`/gold?date=${date}`),
	});
