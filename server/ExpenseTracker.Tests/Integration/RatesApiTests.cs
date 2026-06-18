using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Application.Rates;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class RatesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient Client(long tg)
    {
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tg, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return c;
    }

    [Fact]
    public async Task Rates_returns_usd_and_rub_both_directions()
    {
        var view = await Client(40001).GetFromJsonAsync<RatesView>("/api/rates?date=2026-06-12&currencies=USD,RUB");
        view!.Rates.Should().Contain(r => r.Source == "CBU" && r.Currency == "USD" && r.RatePerUnit == 12000m);
        view.Rates.Should().Contain(r => r.Currency == "RUB" && r.RatePerUnit == 160m);
    }
}
