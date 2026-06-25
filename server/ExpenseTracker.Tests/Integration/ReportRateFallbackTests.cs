using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Expenses;
using ExpenseTracker.Application.Reports;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Infrastructure.Persistence;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class ReportRateFallbackTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Simulates CBU: publishes nothing for `gap` (a weekend/holiday), quotes USD otherwise.
    sealed class WeekendGapRateSource(DateOnly gap) : IRateSource
    {
        public string SourceCode => "CBU";
        public Task<IReadOnlyList<SourceRate>> FetchAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SourceRate>>(
                date == gap
                    ? new List<SourceRate>()
                    : new List<SourceRate> { new("USD", 12000m, null, null) });
    }

    private record CatRow(int Id, string Name);

    [Fact]
    public async Task Report_values_a_date_without_its_own_rate_at_the_latest_prior_rate()
    {
        var gap = new DateOnly(2026, 6, 20); // CBU publishes no rate for this date
        var prior = new DateOnly(2026, 6, 19); // last published business day

        var gapFactory = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IRateSource>();
            s.AddSingleton<IRateSource>(new WeekendGapRateSource(gap));
        }));

        // Seed the prior business day's USD rate, as the daily scheduler would have.
        using (var scope = gapFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.CurrencyRates.Add(new CurrencyRate
            {
                Source = "CBU",
                CurrencyCode = "USD",
                AsOfDate = prior,
                Rate = 12000m,
            });
            await db.SaveChangesAsync();
        }

        var client = gapFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(61001, ApiFactory.BotToken, DateTimeOffset.UtcNow));

        var cats = await client.GetFromJsonAsync<List<CatRow>>("/api/categories");
        var catId = cats![0].Id;

        // A USD expense dated on the gap day — CBU has no rate for that exact date.
        await client.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(5m, "USD", catId, gap, null));

        var summary = await client.GetFromJsonAsync<ReportSummary>(
            $"/api/reports/summary?from={gap:yyyy-MM-dd}&to={gap:yyyy-MM-dd}");

        // Valued at the prior day's rate (5 × 12000) rather than silently dropped.
        summary!.GrandTotal.Should().Be(60000m);
    }
}
