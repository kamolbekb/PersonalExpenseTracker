import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type {
	Budget,
	BudgetInput,
	Category,
	Expense,
	ExpenseInput,
	ReportSummary,
	Settings,
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
		},
	});
};
