using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Users;

public class UserProvisioningService(IUserContext userContext, IApplicationDbContext db) : ICurrentUser
{
    public async Task<User> GetOrCreateAsync()
    {
        var tgId = userContext.TelegramUserId
            ?? throw new InvalidOperationException("TelegramUserId is not set.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramUserId == tgId);
        if (user is not null) return user;

        user = new User
        {
            TelegramUserId = tgId,
            FirstName = userContext.FirstName,
            Username = userContext.Username,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();   // assigns user.Id

        db.Categories.AddRange(DefaultCategories.All.Select(c =>
            new Category { UserId = user.Id, Name = c.Name, Emoji = c.Emoji }));
        db.Settings.Add(new Setting { UserId = user.Id, BaseCurrency = "USD" });
        await db.SaveChangesAsync();
        return user;
    }
}
