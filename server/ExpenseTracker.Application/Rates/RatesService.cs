using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Rates;

public class RatesService(IApplicationDbContext db, IEnumerable<IRateSource> sources, IClock clock) : ICbuRates
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

    public async Task<RatesView> GetRatesAsync(DateOnly? date, IReadOnlyList<string> currencies, CancellationToken ct = default)
    {
        var requested = date ?? clock.TodayInTashkent();
        await EnsureAllSourcesForDateAsync(requested, ct);
        // fall back to the latest stored date <= requested (CBU may not have published `requested` yet)
        var d = await db.CurrencyRates.Where(r => r.AsOfDate <= requested)
            .MaxAsync(r => (DateOnly?)r.AsOfDate, ct) ?? requested;
        var wanted = currencies.Select(c => c.ToUpperInvariant()).ToHashSet();
        var rows = await db.CurrencyRates
            .Where(r => r.AsOfDate == d && wanted.Contains(r.CurrencyCode))
            .OrderBy(r => r.Source).ThenBy(r => r.CurrencyCode)
            .Select(r => new RateRow(r.Source, r.CurrencyCode, r.Rate,
                r.Rate == 0 ? 0 : 1m / r.Rate, r.Buy, r.Sell))
            .ToListAsync(ct);
        return new RatesView(d.ToString("yyyy-MM-dd"), rows);
    }
}
