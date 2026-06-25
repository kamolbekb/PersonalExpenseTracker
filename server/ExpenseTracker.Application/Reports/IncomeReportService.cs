using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.ExchangeRates;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Reports;

public class IncomeReportService(IApplicationDbContext db, ExchangeRateService rates, CurrencyConverter conv)
{
    public async Task<OperationResult<ReportSummary>> SummaryAsync(int userId, DateOnly? from, DateOnly? to)
    {
        var setting = await db.Settings.FirstAsync(s => s.UserId == userId);
        var baseCcy = setting.BaseCurrency;

        var q = db.Incomes.Where(i => i.UserId == userId);
        if (from is not null) q = q.Where(i => i.ReceivedOn >= from);
        if (to is not null) q = q.Where(i => i.ReceivedOn <= to);

        var rows = await q.Join(db.IncomeCategories, i => i.IncomeCategoryId, c => c.Id,
            (i, c) => new { i.Amount, i.CurrencyCode, i.ReceivedOn, CategoryId = i.IncomeCategoryId, CategoryName = c.Name })
            .ToListAsync();

        var byCategory = new Dictionary<int, (string Name, decimal Total)>();
        var byMonth = new Dictionary<string, decimal>();
        decimal grand = 0m;

        foreach (var r in rows)
        {
            decimal amount;
            try
            {
                var rate = await rates.GetRateAsync(r.CurrencyCode, baseCcy, r.ReceivedOn);
                amount = conv.Convert(r.Amount, rate);
            }
            catch (InvalidOperationException)
            {
                continue; // skip rows whose currency CBU does not quote for that date
            }
            grand += amount;

            var cat = byCategory.GetValueOrDefault(r.CategoryId, (r.CategoryName, 0m));
            byCategory[r.CategoryId] = (r.CategoryName, cat.Item2 + amount);

            var month = r.ReceivedOn.ToString("yyyy-MM");
            byMonth[month] = byMonth.GetValueOrDefault(month, 0m) + amount;
        }

        var summary = new ReportSummary(
            baseCcy, grand,
            byCategory.Select(kv => new CategoryTotal(kv.Key, kv.Value.Name, kv.Value.Total))
                .OrderByDescending(c => c.Total).ToList(),
            byMonth.Select(kv => new MonthTotal(kv.Key, kv.Value))
                .OrderBy(m => m.Month).ToList());
        return OperationResult<ReportSummary>.Ok(summary);
    }
}
