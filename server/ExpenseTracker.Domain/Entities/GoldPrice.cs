namespace ExpenseTracker.Domain.Entities;

public class GoldPrice
{
    public int Id { get; set; }
    public DateOnly AsOfDate { get; set; }
    public string Item { get; set; } = "";          // e.g. "5g","50g","100g"
    public decimal? SellPrice { get; set; }          // CBU selling price (UZS)
    public decimal? BuyBackPrice { get; set; }       // CBU buy-back price (UZS)
}
