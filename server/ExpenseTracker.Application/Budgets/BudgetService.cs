using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Budgets;

public class BudgetService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<BudgetDto>> ListAsync(int userId)
    {
        return await db.Budgets.Where(b => b.UserId == userId)
            .Select(b => new BudgetDto(b.Id, b.CategoryId, b.LimitAmount, b.CurrencyCode))
            .ToListAsync();
    }

    public async Task<OperationResult<BudgetDto>> UpsertAsync(int userId, BudgetInput input)
    {
        if (input.LimitAmount <= 0) return OperationResult<BudgetDto>.Bad("Limit must be positive.");
        if (string.IsNullOrWhiteSpace(input.CurrencyCode)) return OperationResult<BudgetDto>.Bad("Currency required.");
        if (input.CategoryId is not null)
        {
            var ownsCategory = await db.Categories.AnyAsync(c => c.Id == input.CategoryId && c.UserId == userId);
            if (!ownsCategory) return OperationResult<BudgetDto>.Bad("Unknown category.");
        }
        var existing = await db.Budgets.FirstOrDefaultAsync(b =>
            b.UserId == userId && b.CategoryId == input.CategoryId);
        if (existing is null)
        {
            existing = new Budget { UserId = userId, CategoryId = input.CategoryId };
            db.Budgets.Add(existing);
        }
        existing.LimitAmount = input.LimitAmount;
        existing.CurrencyCode = input.CurrencyCode.ToUpperInvariant();
        await db.SaveChangesAsync();
        return OperationResult<BudgetDto>.Ok(new BudgetDto(existing.Id, existing.CategoryId,
            existing.LimitAmount, existing.CurrencyCode));
    }
}
