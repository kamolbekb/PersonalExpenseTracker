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
}
