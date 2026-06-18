using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Categories;

public class CategoryService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(int userId)
    {
        return await db.Categories
            .Where(c => c.UserId == userId && !c.IsArchived)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Emoji, c.IsArchived))
            .ToListAsync();
    }

    public async Task<OperationResult<CategoryDto>> CreateAsync(int userId, CategoryInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return OperationResult<CategoryDto>.Bad("Name required.");
        var c = new Category { UserId = userId, Name = input.Name.Trim(), Emoji = input.Emoji };
        db.Categories.Add(c);
        await db.SaveChangesAsync();
        return OperationResult<CategoryDto>.Created(new CategoryDto(c.Id, c.Name, c.Emoji, c.IsArchived));
    }

    public async Task<OperationResult<CategoryDto>> UpdateAsync(int userId, int id, CategoryInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return OperationResult<CategoryDto>.Bad("Name required.");
        var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (c is null) return OperationResult<CategoryDto>.NotFound();
        c.Name = input.Name.Trim();
        c.Emoji = input.Emoji;
        await db.SaveChangesAsync();
        return OperationResult<CategoryDto>.Ok(new CategoryDto(c.Id, c.Name, c.Emoji, c.IsArchived));
    }
}
