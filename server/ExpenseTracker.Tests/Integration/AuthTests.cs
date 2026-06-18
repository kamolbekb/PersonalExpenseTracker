using System.Net;
using System.Net.Http.Headers;
using ExpenseTracker.Infrastructure.Persistence;
using ExpenseTracker.Domain;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task No_auth_header_returns_401()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/categories");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Valid_initData_provisions_user_and_default_categories()
    {
        var client = factory.CreateClient();
        var initData = InitDataBuilder.Build(7001, ApiFactory.BotToken, DateTimeOffset.UtcNow);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma", initData);

        var res = await client.GetAsync("/api/categories");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.TelegramUserId == 7001);
        (await db.Categories.CountAsync(c => c.UserId == user.Id)).Should().Be(DefaultCategories.All.Count);
    }

    [Fact]
    public async Task Invalid_initData_returns_401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma", "bogus-not-signed");
        var res = await client.GetAsync("/api/categories");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
