namespace ExpenseTracker.Application.Common.Interfaces;

public interface IUserContext
{
    long? TelegramUserId { get; }
    string? FirstName { get; }
    string? Username { get; }
}
