using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Application.ExchangeRates;

// Returns "to per 1 from" so amount_from * rate = amount_to (ReportService contract unchanged).
public class ExchangeRateService(ICbuRates rates)
{
    public async Task<decimal> GetRateAsync(string from, string to, DateOnly date)
    {
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();
        if (from == to) return 1m;
        var fromUzs = await UzsPerUnit(from, date);
        var toUzs = await UzsPerUnit(to, date);
        return fromUzs / toUzs;
    }

    async Task<decimal> UzsPerUnit(string ccy, DateOnly date)
    {
        if (ccy == "UZS") return 1m;
        return await rates.GetCbuRateAsync(ccy, date)
            ?? throw new InvalidOperationException($"No CBU rate for {ccy} on {date:yyyy-MM-dd}");
    }
}
