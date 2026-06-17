using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseTracker.Api.Features.Categories;
using ExpenseTracker.Tests.TestData;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Integration;

public class CategoriesApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    HttpClient ClientFor(long tgId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            InitDataBuilder.Build(tgId, ApiFactory.BotToken, DateTimeOffset.UtcNow));
        return client;
    }

    [Fact]
    public async Task New_user_has_default_categories()
    {
        var list = await ClientFor(9001).GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        list!.Should().Contain(c => c.Name == "Food");
    }

    [Fact]
    public async Task Can_add_a_custom_category()
    {
        var client = ClientFor(9002);
        var postResp = await client.PostAsJsonAsync("/api/categories", new CategoryInput("Coffee", "☕"));
        postResp.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
        var list = await client.GetFromJsonAsync<List<CategoryDto>>("/api/categories");
        list!.Should().Contain(c => c.Name == "Coffee" && c.Emoji == "☕");
    }
}
