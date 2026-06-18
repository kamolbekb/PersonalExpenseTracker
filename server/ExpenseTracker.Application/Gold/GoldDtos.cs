namespace ExpenseTracker.Application.Gold;

public record GoldRow(string Item, decimal? SellPrice, decimal? BuyBackPrice);
public record GoldView(string Date, string? HistoryFrom, IReadOnlyList<GoldRow> Items);
