using ExpenseTracker.Api.Data;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Currency;

public class ExchangeRateService(AppDbContext db, IExchangeRateProvider provider)
{
    public async Task<decimal> GetRateAsync(string from, string to, DateOnly date)
    {
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();
        if (from == to) return 1m;

        var cached = await db.ExchangeRates.FirstOrDefaultAsync(r =>
            r.BaseCurrency == from && r.QuoteCurrency == to && r.AsOfDate == date);
        if (cached is not null) return cached.Rate;

        var rate = await provider.GetRateAsync(from, to, date);
        db.ExchangeRates.Add(new ExchangeRate
        {
            BaseCurrency = from, QuoteCurrency = to, Rate = rate, AsOfDate = date
        });
        await db.SaveChangesAsync();
        return rate;
    }
}
