using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.IncomeCategories;

public class IncomeCategoryService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<IncomeCategoryDto>> ListAsync(int userId)
    {
        var exists = await db.IncomeCategories.AnyAsync(c => c.UserId == userId);
        if (!exists)
        {
            db.IncomeCategories.AddRange(DefaultIncomeCategories.All.Select(c =>
                new IncomeCategory { UserId = userId, Name = c.Name, Emoji = c.Emoji }));
            await db.SaveChangesAsync();
        }
        return await db.IncomeCategories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Id)
            .Select(c => new IncomeCategoryDto(c.Id, c.Name, c.Emoji))
            .ToListAsync();
    }
}
