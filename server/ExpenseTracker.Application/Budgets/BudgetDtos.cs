namespace ExpenseTracker.Application.Budgets;

public record BudgetInput(int? CategoryId, decimal LimitAmount, string CurrencyCode);
public record BudgetDto(int Id, int? CategoryId, decimal LimitAmount, string CurrencyCode);
