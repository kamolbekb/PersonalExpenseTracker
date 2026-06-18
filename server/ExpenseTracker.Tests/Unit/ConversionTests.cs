using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.ExchangeRates;
using FluentAssertions;
using Xunit;

namespace ExpenseTracker.Tests.Unit;

public class ConversionTests
{
    class FakeCbuRates : ICbuRates
    {
        public Task<decimal?> GetCbuRateAsync(string ccy, DateOnly date, CancellationToken ct = default)
            => Task.FromResult<decimal?>(ccy.ToUpperInvariant() switch
            { "USD" => 12000m, "RUB" => 160m, "EUR" => 13000m, _ => (decimal?)null });
    }
    readonly ExchangeRateService _sut = new(new FakeCbuRates());
    readonly DateOnly _d = new(2026, 6, 18);

    [Fact] public async Task Identity_is_one() => (await _sut.GetRateAsync("USD","USD",_d)).Should().Be(1m);
    [Fact] public async Task X_to_UZS_is_cbu_rate() => (await _sut.GetRateAsync("USD","UZS",_d)).Should().Be(12000m);
    [Fact] public async Task UZS_to_X_is_reciprocal() => (await _sut.GetRateAsync("UZS","USD",_d)).Should().Be(1m/12000m);
    [Fact] public async Task X_to_Y_cross_via_uzs() => (await _sut.GetRateAsync("USD","RUB",_d)).Should().Be(12000m/160m);
    [Fact] public async Task Missing_currency_throws() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GetRateAsync("GBP","UZS",_d));
}
