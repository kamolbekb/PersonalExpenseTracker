using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Features.Expenses;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class ExpensesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        var initData = InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma", initData);
        return client;
    }

    async Task<int> FirstCategoryId(long tgId)
    {
        var cats = await ClientFor(tgId).GetFromJsonAsync<List<CategoryRow>>("/api/categories");
        return cats![0].Id;
    }
    record CategoryRow(int Id, string Name);

    [Fact]
    public async Task Create_then_list_returns_the_expense()
    {
        var client = ClientFor(8001);
        var catId = await FirstCategoryId(8001);
        var input = new ExpenseInput(12.50m, "USD", catId, new DateOnly(2026, 6, 1), "lunch");

        var created = await client.PostAsJsonAsync("/api/expenses", input);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses");
        list.Should().ContainSingle(e => e.Note == "lunch" && e.Amount == 12.50m);
    }

    [Fact]
    public async Task User_cannot_see_another_users_expenses()
    {
        var alice = ClientFor(8101);
        var catId = await FirstCategoryId(8101);
        await alice.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(99m, "USD", catId, new DateOnly(2026, 6, 1), "alice-secret"));

        var bob = ClientFor(8102);
        var bobList = await bob.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses");
        bobList.Should().NotContain(e => e.Note == "alice-secret");
    }

    [Fact]
    public async Task User_cannot_delete_another_users_expense()
    {
        var alice = ClientFor(8201);
        var catId = await FirstCategoryId(8201);
        var created = await (await alice.PostAsJsonAsync("/api/expenses",
            new ExpenseInput(5m, "USD", catId, new DateOnly(2026, 6, 1), "x")))
            .Content.ReadFromJsonAsync<ExpenseDto>();

        var bob = ClientFor(8202);
        var res = await bob.DeleteAsync($"/api/expenses/{created!.Id}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
