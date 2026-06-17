namespace ExpenseTracker.Api.Domain;

public class Expense
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }            // in CurrencyCode
    public string CurrencyCode { get; set; } = "USD";
    public int CategoryId { get; set; }
    public DateOnly SpentOn { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
