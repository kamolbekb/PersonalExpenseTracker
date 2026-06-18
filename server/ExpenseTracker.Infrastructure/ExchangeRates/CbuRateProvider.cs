using System.Globalization;
using System.Text.Json;
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Infrastructure.ExchangeRates;

public class CbuRateProvider(HttpClient http) : IRateSource
{
    public string SourceCode => "CBU";

    public async Task<IReadOnlyList<SourceRate>> FetchAsync(DateOnly date, CancellationToken ct = default)
    {
        var url = $"uz/arkhiv-kursov-valyut/json/all/{date:yyyy-MM-dd}/";
        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return Parse(json);
    }

    public static IReadOnlyList<SourceRate> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<SourceRate>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var ccy = el.GetProperty("Ccy").GetString();
            if (string.IsNullOrEmpty(ccy)) continue;
            var rate = decimal.Parse(el.GetProperty("Rate").GetString()!, CultureInfo.InvariantCulture);
            var nominal = el.TryGetProperty("Nominal", out var n) && decimal.TryParse(
                n.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var nm) && nm != 0 ? nm : 1m;
            list.Add(new SourceRate(ccy.ToUpperInvariant(), rate / nominal, null, null));
        }
        return list;
    }
}
