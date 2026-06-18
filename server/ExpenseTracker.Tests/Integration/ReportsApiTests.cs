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
        // Base currency is USD by default; use USD to avoid network FX in tests.
        await client.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(10m, "USD", catId, new DateOnly(2026, 6, 1), null));
        await client.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(15m, "USD", catId, new DateOnly(2026, 6, 2), null));

        var summary = await client.GetFromJsonAsync<ReportSummary>(
            "/api/reports/summary?from=2026-06-01&to=2026-06-30");

        summary!.BaseCurrency.Should().Be("USD");
        summary.GrandTotal.Should().Be(25m);
        summary.ByCategory.Should().ContainSingle(c => c.CategoryId == catId && c.Total == 25m);
        summary.ByMonth.Should().Contain(m => m.Month == "2026-06" && m.Total == 25m);
    }
}
