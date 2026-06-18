using ExpenseTracker.Application.Common;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class CurrencyConverterTests
{
    readonly CurrencyConverter _sut = new();

    [Fact]
    public void Converts_and_rounds_to_two_decimals()
        => _sut.Convert(10m, 0.875m).Should().Be(8.75m);

    [Fact]
    public void Uses_bankers_rounding()
        => _sut.Convert(1m, 1.005m).Should().Be(1.00m); // 1.005 -> 1.00 (round half to even)

    [Fact]
    public void Identity_rate_returns_same_amount()
        => _sut.Convert(42.42m, 1m).Should().Be(42.42m);
}
