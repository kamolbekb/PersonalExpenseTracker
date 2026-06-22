using ExpenseTracker.Infrastructure.Gold;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class CbuGoldScraperTests
{
    // Two-price layout (older/simple): weight, sell, buy-back.
    const string Html = """
    <table class="prices"><tbody>
      <tr><th>Og'irligi</th><th>Sotish narxi</th><th>Qaytarib sotib olish narxi</th></tr>
      <tr><td>5 g</td><td>9 243 000</td><td>8 281 000</td></tr>
      <tr><td>50 g</td><td>92 431 000</td><td>82 811 000</td></tr>
      <tr><td>100 g</td><td>184 861 000</td><td>165 622 000</td></tr>
    </tbody></table>
    """;

    // Real CBU layout has THREE price columns: sell, buy-back (intact packaging),
    // and buy-back (damaged / not technically compliant — a lower price).
    const string HtmlThreePrices = """
    <table class="prices"><tbody>
      <tr>
        <th>Og'irligi</th>
        <th>Sotish narxi</th>
        <th>qadog'i but holatda</th>
        <th>qadog'i buzilgan yoki texnik talablarga mos kelmaydigan holatda</th>
      </tr>
      <tr><td>5 gramm</td><td>9 240 000 so'm</td><td>8 010 000 so'm</td><td>7 929 000 so'm</td></tr>
      <tr><td>100 gramm</td><td>184 796 000 so'm</td><td>160 199 000 so'm</td><td>158 581 000 so'm</td></tr>
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

    [Fact]
    public void Takes_sell_and_intact_buyback_ignoring_the_damaged_column()
    {
        var rows = CbuGoldScraper.Parse(HtmlThreePrices);

        // 5g: sell = 9 240 000, intact buy-back = 8 010 000 (NOT the 7 929 000 damaged price).
        rows.Should().Contain(g =>
            g.Item == "5 gramm" && g.SellPrice == 9240000m && g.BuyBackPrice == 8010000m);
        // Never picks up the lower, damaged-packaging buy-back price.
        rows.Should().NotContain(g => g.BuyBackPrice == 7929000m || g.BuyBackPrice == 158581000m);
        rows.Should().HaveCount(2);
    }
}
