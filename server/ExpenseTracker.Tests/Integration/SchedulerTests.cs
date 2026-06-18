using ExpenseTracker.Application.Rates;
using ExpenseTracker.Application.Gold;
using ExpenseTracker.Infrastructure.Scheduling;
using ExpenseTracker.Infrastructure.Persistence;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class SchedulerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task RunOnce_stores_rates_and_gold_and_is_idempotent()
    {
        var date = new DateOnly(2026, 6, 10);
        using (var scope = factory.Services.CreateScope())
            await DailyRateFetchService.RunOnceAsync(scope.ServiceProvider, date, NullLogger.Instance, default);
        using (var scope = factory.Services.CreateScope())
            await DailyRateFetchService.RunOnceAsync(scope.ServiceProvider, date, NullLogger.Instance, default); // again

        using var s = factory.Services.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CurrencyRates.CountAsync(r => r.Source == "CBU" && r.AsOfDate == date)).Should().Be(3); // stub has 3
        (await db.GoldPrices.CountAsync(g => g.AsOfDate == date)).Should().Be(2);                          // stub has 2
    }
}
