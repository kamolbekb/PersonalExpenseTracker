namespace ExpenseTracker.Application.Common.Interfaces;

public interface IExchangeRateProvider
{
    Task<decimal> GetRateAsync(string from, string to, DateOnly date);
}
