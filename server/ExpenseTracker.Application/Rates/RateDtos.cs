namespace ExpenseTracker.Application.Rates;

public record RateRow(string Source, string Currency, decimal RatePerUnit, decimal UnitPerUzs, decimal? Buy, decimal? Sell);
public record RatesView(string Date, IReadOnlyList<RateRow> Rates);
