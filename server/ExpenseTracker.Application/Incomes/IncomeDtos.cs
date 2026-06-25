namespace ExpenseTracker.Application.Incomes;

public record IncomeInput(decimal Amount, string CurrencyCode, int IncomeCategoryId, DateOnly ReceivedOn, string? Note);
public record IncomeDto(int Id, decimal Amount, string CurrencyCode, int IncomeCategoryId, DateOnly ReceivedOn, string? Note);
