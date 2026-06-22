namespace ExpenseTracker.Application.Common.Interfaces;

public record SourceGold(string Item, decimal? SellPrice, decimal? BuyBackPrice);

public interface IGoldSource
{
    Task<IReadOnlyList<SourceGold>> FetchAsync(DateOnly date, CancellationToken ct = default);
}
