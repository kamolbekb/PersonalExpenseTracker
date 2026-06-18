namespace ExpenseTracker.Application.Common.Interfaces;

public record SourceRate(string CurrencyCode, decimal Rate, decimal? Buy, decimal? Sell);

public interface IRateSource
{
    string SourceCode { get; }
    Task<IReadOnlyList<SourceRate>> FetchAsync(DateOnly date, CancellationToken ct = default);
}
