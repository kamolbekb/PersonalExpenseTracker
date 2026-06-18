namespace ExpenseTracker.Application.Common.Interfaces;

public interface ICbuRates
{
    Task<decimal?> GetCbuRateAsync(string currency, DateOnly date, CancellationToken ct = default);
}
