namespace ExpenseTracker.Api.Features.Budgets;

public record BudgetInput(int? CategoryId, decimal LimitAmount, string CurrencyCode);
public record BudgetDto(int Id, int? CategoryId, decimal LimitAmount, string CurrencyCode);
