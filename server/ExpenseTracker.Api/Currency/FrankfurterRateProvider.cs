using System.Text.Json;

namespace ExpenseTracker.Api.Currency;

public class FrankfurterRateProvider(HttpClient http) : IExchangeRateProvider
{
    public async Task<decimal> GetRateAsync(string from, string to, DateOnly date)
    {
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();
        // https://api.frankfurter.app/2026-06-01?from=USD&to=EUR
        var url = $"{date:yyyy-MM-dd}?from={from}&to={to}";
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var rate = doc.RootElement.GetProperty("rates").GetProperty(to).GetDecimal();
        return rate;
    }
}
