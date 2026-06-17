namespace ExpenseTracker.Api.Domain;

public class Setting
{
    public int Id { get; set; }
    public int UserId { get; set; }                // unique
    public string BaseCurrency { get; set; } = "USD";
}
