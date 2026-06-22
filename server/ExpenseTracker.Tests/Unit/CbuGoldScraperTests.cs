using ExpenseTracker.Infrastructure.Gold;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class CbuGoldScraperTests
{
    const string Html = """
    <table class="prices"><tbody>
      <tr><th>Og'irligi</th><th>Sotish narxi</th><th>Qaytarib sotib olish narxi</th></tr>
      <tr><td>5 g</td><td>9 243 000</td><td>8 281 000</td></tr>
      <tr><td>50 g</td><td>92 431 000</td><td>82 811 000</td></tr>
      <tr><td>100 g</td><td>184 861 000</td><td>165 622 000</td></tr>
    </tbody></table>
    """;

    [Fact]
    public void Parses_weight_and_prices_stripping_separators()
    {
        var rows = CbuGoldScraper.Parse(Html);
        rows.Should().Contain(g => g.Item == "5 g" && g.SellPrice == 9243000m && g.BuyBackPrice == 8281000m);
        rows.Should().Contain(g => g.Item == "100 g" && g.SellPrice == 184861000m);
        rows.Should().HaveCount(3);
    }
}
