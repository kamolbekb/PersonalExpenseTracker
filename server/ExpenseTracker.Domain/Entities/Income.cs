namespace ExpenseTracker.Domain.Entities;

public class Income
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }            // in CurrencyCode
    public string CurrencyCode { get; set; } = "UZS";
    public int IncomeCategoryId { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
