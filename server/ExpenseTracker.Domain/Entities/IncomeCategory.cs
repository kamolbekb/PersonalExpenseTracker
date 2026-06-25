namespace ExpenseTracker.Domain.Entities;

public class IncomeCategory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
}
