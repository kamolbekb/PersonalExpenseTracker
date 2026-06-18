namespace ExpenseTracker.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
    public bool IsArchived { get; set; }
}
