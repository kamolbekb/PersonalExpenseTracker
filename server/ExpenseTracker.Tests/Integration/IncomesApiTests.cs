using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.IncomeCategories;
using ExpenseTracker.Application.Incomes;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class IncomesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    public async Task Create_then_list_returns_the_income()
    {
        var client = ClientFor(31001);
        var catId = await FirstIncomeCategoryId(client);
        var input = new IncomeInput(1500m, "USD", catId, new DateOnly(2026, 6, 1), "salary");

        var created = await client.PostAsJsonAsync("/api/incomes", input);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<List<IncomeDto>>("/api/incomes");
        list.Should().ContainSingle(i => i.Note == "salary" && i.Amount == 1500m);
    }

    [Fact]
    public async Task User_cannot_see_another_users_incomes()
    {
        var alice = ClientFor(31101);
        var aliceCat = await FirstIncomeCategoryId(alice);
        await alice.PostAsJsonAsync("/api/incomes",
            new IncomeInput(99m, "USD", aliceCat, new DateOnly(2026, 6, 1), "alice-secret"));

        var bobList = await ClientFor(31102).GetFromJsonAsync<List<IncomeDto>>("/api/incomes");
        bobList.Should().NotContain(i => i.Note == "alice-secret");
    }

    [Fact]
    public async Task Income_with_foreign_category_is_rejected()
    {
        var alice = ClientFor(31201);
        var aliceCat = await FirstIncomeCategoryId(alice);

        var bob = ClientFor(31202);
        var res = await bob.PostAsJsonAsync("/api/incomes",
            new IncomeInput(10m, "USD", aliceCat, new DateOnly(2026, 6, 1), "x"));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task User_cannot_delete_another_users_income()
    {
        var alice = ClientFor(31301);
        var aliceCat = await FirstIncomeCategoryId(alice);
        var created = await (await alice.PostAsJsonAsync("/api/incomes",
            new IncomeInput(5m, "USD", aliceCat, new DateOnly(2026, 6, 1), "x")))
            .Content.ReadFromJsonAsync<IncomeDto>();

        var res = await ClientFor(31302).DeleteAsync($"/api/incomes/{created!.Id}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
