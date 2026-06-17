namespace ExpenseTracker.Api.Currency;

public interface IExchangeRateProvider
{
    Task<decimal> GetRateAsync(string from, string to, DateOnly date);
}
