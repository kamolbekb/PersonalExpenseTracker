namespace ExpenseTracker.Api.Domain;

public class User
{
    public int Id { get; set; }
    public long TelegramUserId { get; set; }   // unique
    public string? FirstName { get; set; }
    public string? Username { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
