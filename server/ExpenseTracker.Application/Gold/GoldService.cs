using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Gold;

public class GoldService(IApplicationDbContext db, IGoldSource source)
{
    public async Task EnsureForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        if (await db.GoldPrices.AnyAsync(g => g.AsOfDate == date, ct)) return;
        foreach (var g in await source.FetchAsync(date, ct))
            db.GoldPrices.Add(new GoldPrice
            { AsOfDate = date, Item = g.Item, SellPrice = g.SellPrice, BuyBackPrice = g.BuyBackPrice });
        await db.SaveChangesAsync(ct);
    }

    public async Task<GoldView> GetGoldAsync(DateOnly? date, CancellationToken ct = default)
    {
        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        await EnsureForDateAsync(d, ct);
        var items = await db.GoldPrices.Where(g => g.AsOfDate == d).OrderBy(g => g.Id)
            .Select(g => new GoldRow(g.Item, g.SellPrice, g.BuyBackPrice)).ToListAsync(ct);
        var earliest = await db.GoldPrices.OrderBy(g => g.AsOfDate).Select(g => (DateOnly?)g.AsOfDate).FirstOrDefaultAsync(ct);
        return new GoldView(d.ToString("yyyy-MM-dd"), earliest?.ToString("yyyy-MM-dd"), items);
    }
}
