using System.Security.Claims;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Domain;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Auth;

public interface ICurrentUser { Task<User> GetOrCreateAsync(); }

public class CurrentUserAccessor(IHttpContextAccessor http, AppDbContext db) : ICurrentUser
{
    public async Task<User> GetOrCreateAsync()
    {
        var principal = http.HttpContext!.User;
        var tgId = long.Parse(principal.FindFirstValue("tg_id")!);
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramUserId == tgId);
        if (user is not null) return user;

        user = new User
        {
            TelegramUserId = tgId,
            FirstName = principal.FindFirstValue(ClaimTypes.GivenName),
            Username = principal.FindFirstValue("tg_username"),
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
