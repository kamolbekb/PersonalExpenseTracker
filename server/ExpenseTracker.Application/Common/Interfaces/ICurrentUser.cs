using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Application.Common.Interfaces;

public interface ICurrentUser
{
    Task<User> GetOrCreateAsync();
}
