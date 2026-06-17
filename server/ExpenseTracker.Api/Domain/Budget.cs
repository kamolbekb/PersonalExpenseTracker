namespace ExpenseTracker.Api.Domain;

public class Budget
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? CategoryId { get; set; }           // null = overall budget
    public decimal LimitAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    // Period is monthly-only for v1; no column needed yet.
}
