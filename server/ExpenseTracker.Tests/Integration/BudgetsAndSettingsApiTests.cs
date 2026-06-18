using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.Budgets;
using ExpenseTracker.Application.Settings;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class BudgetsAndSettingsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    [Fact]
    public async Task Settings_default_is_usd_and_can_be_changed()
    {
        var client = ClientFor(12001);
        var initial = await client.GetFromJsonAsync<SettingDto>("/api/settings");
        initial!.BaseCurrency.Should().Be("UZS");

        await client.PutAsJsonAsync("/api/settings", new SettingDto("EUR"));
        var updated = await client.GetFromJsonAsync<SettingDto>("/api/settings");
        updated!.BaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Budget_upsert_replaces_existing_for_same_category()
    {
        var client = ClientFor(12002);
        await client.PutAsJsonAsync("/api/budgets", new BudgetInput(null, 500m, "USD"));
        await client.PutAsJsonAsync("/api/budgets", new BudgetInput(null, 600m, "USD"));

        var list = await client.GetFromJsonAsync<List<BudgetDto>>("/api/budgets");
        list!.Should().ContainSingle(b => b.CategoryId == null && b.LimitAmount == 600m);
    }

    [Fact]
    public async Task Budget_with_foreign_category_is_rejected()
    {
        // Provision "alice" (12301) and get one of her category ids
        var alice = ClientFor(12301);
        var aliceCats = await alice.GetFromJsonAsync<List<CatRow>>("/api/categories");
        var aliceCatId = aliceCats![0].Id;

        // "bob" (12302) tries to budget against alice's category id
        var bob = ClientFor(12302);
        var res = await bob.PutAsJsonAsync("/api/budgets",
            new BudgetInput(aliceCatId, 100m, "USD"));
        res.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    private record CatRow(int Id, string Name);
}
