using ExpenseTracker.Api.Currency;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Reports;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Features.Reports;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/reports/summary", async (ICurrentUser cu, AppDbContext db,
            ExchangeRateService rates, CurrencyConverter conv, DateOnly? from, DateOnly? to) =>
        {
            var user = await cu.GetOrCreateAsync();
            var setting = await db.Settings.FirstAsync(s => s.UserId == user.Id);
            var baseCcy = setting.BaseCurrency;

            var q = db.Expenses.Where(e => e.UserId == user.Id);
            if (from is not null) q = q.Where(e => e.SpentOn >= from);
            if (to is not null) q = q.Where(e => e.SpentOn <= to);

            var rows = await q.Join(db.Categories, e => e.CategoryId, c => c.Id,
                (e, c) => new { e.Amount, e.CurrencyCode, e.SpentOn, e.CategoryId, CategoryName = c.Name })
                .ToListAsync();

            var byCategory = new Dictionary<int, (string Name, decimal Total)>();
            var byMonth = new Dictionary<string, decimal>();
            decimal grand = 0m;

            foreach (var r in rows)
            {
                var rate = await rates.GetRateAsync(r.CurrencyCode, baseCcy, r.SpentOn);
                var amount = conv.Convert(r.Amount, rate);
                grand += amount;

                var cat = byCategory.GetValueOrDefault(r.CategoryId, (r.CategoryName, 0m));
                byCategory[r.CategoryId] = (r.CategoryName, cat.Item2 + amount);

                var month = r.SpentOn.ToString("yyyy-MM");
                byMonth[month] = byMonth.GetValueOrDefault(month, 0m) + amount;
            }

            var summary = new ReportSummary(
                baseCcy, grand,
                byCategory.Select(kv => new CategoryTotal(kv.Key, kv.Value.Name, kv.Value.Total))
                    .OrderByDescending(c => c.Total).ToList(),
                byMonth.Select(kv => new MonthTotal(kv.Key, kv.Value))
                    .OrderBy(m => m.Month).ToList());
            return Results.Ok(summary);
        });

        return api;
    }
}
