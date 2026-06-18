using System.Net;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class RefreshEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Correct_token_refreshes_and_stores_today()
    {
        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/refresh");
        req.Headers.Add("X-Refresh-Token", ApiFactory.RefreshToken);

        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().TodayInTashkent();
        (await db.CurrencyRates.CountAsync(r => r.Source == "CBU" && r.AsOfDate == today)).Should().Be(3);
        (await db.GoldPrices.CountAsync(g => g.AsOfDate == today)).Should().Be(2);
    }

    [Fact]
    public async Task Wrong_token_is_401()
    {
        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/internal/refresh");
        req.Headers.Add("X-Refresh-Token", "wrong-token");
        (await client.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Missing_token_is_401()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsync("/internal/refresh", null);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
