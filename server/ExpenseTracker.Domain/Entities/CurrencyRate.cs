namespace ExpenseTracker.Domain.Entities;

public class CurrencyRate
{
    public int Id { get; set; }
    public string Source { get; set; } = "";       // "CBU" (later "IPAKYULI"|"ASAKA")
    public string CurrencyCode { get; set; } = "";  // e.g. "USD","RUB"
    public DateOnly AsOfDate { get; set; }
    public decimal Rate { get; set; }               // official UZS per 1 unit
    public decimal? Buy { get; set; }               // null for CBU
    public decimal? Sell { get; set; }              // null for CBU
}
