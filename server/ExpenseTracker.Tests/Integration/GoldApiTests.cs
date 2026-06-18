using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.Gold;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class GoldApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Gold_returns_bars_for_date()
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(40002, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        var view = await c.GetFromJsonAsync<GoldView>("/api/gold?date=2026-06-12");
        view!.Items.Should().Contain(g => g.Item == "5 g" && g.SellPrice == 9243000m);
    }
}
