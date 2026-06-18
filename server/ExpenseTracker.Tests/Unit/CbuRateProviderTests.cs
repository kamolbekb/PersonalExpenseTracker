using ExpenseTracker.Infrastructure.ExchangeRates;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class CbuRateProviderTests
{
    const string Json = """
    [{"Ccy":"USD","Rate":"12052.05","Nominal":"1","Date":"18.06.2026"},
     {"Ccy":"RUB","Rate":"165.30","Nominal":"1","Date":"18.06.2026"},
     {"Ccy":"JPY","Rate":"8100.00","Nominal":"100","Date":"18.06.2026"}]
    """;

    [Fact]
    public void Parses_and_normalizes_by_nominal()
    {
        var rates = CbuRateProvider.Parse(Json);
        rates.Should().Contain(r => r.CurrencyCode == "USD" && r.Rate == 12052.05m);
        rates.Should().Contain(r => r.CurrencyCode == "JPY" && r.Rate == 81.00m); // 8100/100
        rates.Should().OnlyContain(r => r.Buy == null && r.Sell == null);
    }
}
