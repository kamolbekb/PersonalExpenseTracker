using System.Globalization;
using System.Text.RegularExpressions;
using ExpenseTracker.Application.Common.Interfaces;
using HtmlAgilityPack;

namespace ExpenseTracker.Infrastructure.Gold;

public class CbuGoldScraper(HttpClient http) : IGoldSource
{
    public async Task<IReadOnlyList<SourceGold>> FetchAsync(DateOnly date, CancellationToken ct = default)
    {
        using var resp = await http.GetAsync("uz/banknotes-coins/gold-bars/prices/", ct);
        resp.EnsureSuccessStatusCode();
        return Parse(await resp.Content.ReadAsStringAsync(ct));
    }

    public static IReadOnlyList<SourceGold> Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var rows = new List<SourceGold>();
        // CBU gold-bar row layout (cells): [0] weight, [1] selling price,
        // [2] buy-back price with INTACT packaging, [3] buy-back price when packaging is
        // damaged / not technically compliant (a lower price). We deliberately take the
        // SELL price and the INTACT buy-back, and ignore the damaged-bar column.
        foreach (var tr in doc.DocumentNode.SelectNodes("//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var cells = tr.SelectNodes("./td");
            if (cells is null || cells.Count < 2) continue;
            var item = HtmlEntity.DeEntitize(cells[0].InnerText).Trim();
            if (!Regex.IsMatch(item, @"\d")) continue;                 // skip header/non-data rows
            decimal? Money(int i) => i < cells.Count
                && decimal.TryParse(Regex.Replace(HtmlEntity.DeEntitize(cells[i].InnerText), @"[^\d]", ""),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : null;
            rows.Add(new SourceGold(item, Money(1), Money(2)));   // sell, intact buy-back
        }
        return rows;
    }
}
