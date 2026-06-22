using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.Expenses;
using ExpenseTracker.Application.Reports;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class ReportsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    record CategoryRow(int Id, string Name);

    [Fact]
    public async Task Summary_sums_same_currency_expenses_in_base()
    {
        var client = ClientFor(11001);
        var cats = await client.GetFromJsonAsync<List<CategoryRow>>("/api/categories");
        var catId = cats![0].Id;
        // Base currency is UZS by default; use USD to exercise conversion via stub (USD = 12000 UZS).
        await client.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(10m, "USD", catId, new DateOnly(2026, 6, 1), null));
        await client.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(15m, "USD", catId, new DateOnly(2026, 6, 2), null));

        var summary = await client.GetFromJsonAsync<ReportSummary>(
            "/api/reports/summary?from=2026-06-01&to=2026-06-30");

        summary!.BaseCurrency.Should().Be("UZS");
        summary.GrandTotal.Should().Be(300000m);
        summary.ByCategory.Should().ContainSingle(c => c.CategoryId == catId && c.Total == 300000m);
        summary.ByMonth.Should().Contain(m => m.Month == "2026-06" && m.Total == 300000m);
    }

    [Fact]
    public async Task Report_skips_expenses_in_unquoted_currency()
    {
        var client = ClientFor(11777);
        var cats = await client.GetFromJsonAsync<List<CategoryRow>>("/api/categories");
        var catId = cats![0].Id;
        // StubRateSource only quotes USD (12000), RUB (160), EUR (13000) — not GBP.
        // Create one convertible (USD 10 = 120000 UZS) and one unquotable (GBP 5).
        // The unquotable should be skipped, not cause a 500 error.
        await client.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(10m, "USD", catId, new DateOnly(2026, 6, 15), null));
        await client.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(5m, "GBP", catId, new DateOnly(2026, 6, 15), null));

        var summary = await client.GetFromJsonAsync<ReportSummary>(
            "/api/reports/summary?from=2026-06-01&to=2026-06-30");

        summary!.BaseCurrency.Should().Be("UZS");
        // Only USD 10 × 12000 = 120000; GBP is skipped.
        summary.GrandTotal.Should().Be(120000m);
        summary.ByCategory.Should().ContainSingle(c => c.CategoryId == catId && c.Total == 120000m);
    }
}
