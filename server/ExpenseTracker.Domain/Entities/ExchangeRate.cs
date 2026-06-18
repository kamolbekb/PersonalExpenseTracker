namespace ExpenseTracker.Domain.Entities;

public class ExchangeRate
{
    public int Id { get; set; }
    public string BaseCurrency { get; set; } = "";
    public string QuoteCurrency { get; set; } = "";
    public decimal Rate { get; set; }              // 1 Base = Rate Quote
    public DateOnly AsOfDate { get; set; }
}
