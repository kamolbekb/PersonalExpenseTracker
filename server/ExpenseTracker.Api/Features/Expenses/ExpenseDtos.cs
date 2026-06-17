namespace ExpenseTracker.Api.Features.Expenses;

public record ExpenseInput(decimal Amount, string CurrencyCode, int CategoryId, DateOnly SpentOn, string? Note);
public record ExpenseDto(int Id, decimal Amount, string CurrencyCode, int CategoryId, DateOnly SpentOn, string? Note);
