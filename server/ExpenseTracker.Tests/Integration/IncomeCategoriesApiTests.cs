using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.IncomeCategories;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class IncomeCategoriesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    [Fact]
    public async Task New_user_gets_seeded_income_categories()
    {
        var client = ClientFor(21001);
        var cats = await client.GetFromJsonAsync<List<IncomeCategoryDto>>("/api/income-categories");
        cats!.Should().NotBeEmpty();
        cats.Should().Contain(c => c.Name == "Salary");
    }

    [Fact]
    public async Task Income_categories_are_user_scoped()
    {
        var aliceCats = await ClientFor(21101).GetFromJsonAsync<List<IncomeCategoryDto>>("/api/income-categories");
        var bobCats = await ClientFor(21102).GetFromJsonAsync<List<IncomeCategoryDto>>("/api/income-categories");
        aliceCats!.Select(c => c.Id).Should().NotIntersectWith(bobCats!.Select(c => c.Id));
    }
}
