using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.IncomeCategories;
using ExpenseTracker.Application.Incomes;
using ExpenseTracker.Application.Reports;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class IncomeReportsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    async Task<int> FirstIncomeCategoryId(HttpClient client)
    {
        var cats = await client.GetFromJsonAsync<List<IncomeCategoryDto>>("/api/income-categories");
        return cats![0].Id;
    }

    [Fact]
    public async Task Income_summary_sums_in_base_currency_by_category_and_month()
    {
        var client = ClientFor(41001);
        var catId = await FirstIncomeCategoryId(client);
        // Base UZS; USD converts at 12000 via the stub.
        await client.PostAsJsonAsync("/api/incomes",
            new IncomeInput(10m, "USD", catId, new DateOnly(2026, 6, 1), null));
        await client.PostAsJsonAsync("/api/incomes",
            new IncomeInput(15m, "USD", catId, new DateOnly(2026, 6, 2), null));

        var summary = await client.GetFromJsonAsync<ReportSummary>(
            "/api/reports/income-summary?from=2026-06-01&to=2026-06-30");

        summary!.BaseCurrency.Should().Be("UZS");
        summary.GrandTotal.Should().Be(300000m);
        summary.ByCategory.Should().ContainSingle(c => c.CategoryId == catId && c.Total == 300000m);
        summary.ByMonth.Should().Contain(m => m.Month == "2026-06" && m.Total == 300000m);
    }

    [Fact]
    public async Task Income_summary_excludes_expense_data()
    {
        // Income summary must be zero for a user who only has expenses.
        var client = ClientFor(41777);
        var summary = await client.GetFromJsonAsync<ReportSummary>(
            "/api/reports/income-summary?from=2026-06-01&to=2026-06-30");
        summary!.GrandTotal.Should().Be(0m);
        summary.ByCategory.Should().BeEmpty();
    }
}
