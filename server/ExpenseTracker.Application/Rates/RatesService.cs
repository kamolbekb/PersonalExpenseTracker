using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Rates;

public class RatesService(IApplicationDbContext db, IEnumerable<IRateSource> sources) : ICbuRates
{
    public async Task EnsureSourceForDateAsync(string sourceCode, DateOnly date, CancellationToken ct = default)
    {
        sourceCode = sourceCode.ToUpperInvariant();
        if (await db.CurrencyRates.AnyAsync(r => r.Source == sourceCode && r.AsOfDate == date, ct)) return;
        var source = sources.FirstOrDefault(s => s.SourceCode.ToUpperInvariant() == sourceCode);
        if (source is null) return;
        foreach (var f in await source.FetchAsync(date, ct))
            db.CurrencyRates.Add(new CurrencyRate
            {
                Source = sourceCode, CurrencyCode = f.CurrencyCode.ToUpperInvariant(),
                AsOfDate = date, Rate = f.Rate, Buy = f.Buy, Sell = f.Sell
            });
        await db.SaveChangesAsync(ct);
    }

    public async Task EnsureAllSourcesForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        foreach (var s in sources) await EnsureSourceForDateAsync(s.SourceCode, date, ct);
    }

    public async Task<decimal?> GetCbuRateAsync(string currency, DateOnly date, CancellationToken ct = default)
    {
        currency = currency.ToUpperInvariant();
        if (currency == "UZS") return 1m;
        await EnsureSourceForDateAsync("CBU", date, ct);
        var row = await db.CurrencyRates.FirstOrDefaultAsync(
            r => r.Source == "CBU" && r.CurrencyCode == currency && r.AsOfDate == date, ct);
        return row?.Rate;
    }
}
