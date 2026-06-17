using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Api.Features.Budgets;
using ExpenseTracker.Api.Features.Settings;
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
        initial!.BaseCurrency.Should().Be("USD");

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
}
