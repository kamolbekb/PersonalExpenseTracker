using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Expenses;

public class ExpenseService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<ExpenseDto>> ListAsync(int userId, DateOnly? from, DateOnly? to, int? categoryId)
    {
        var q = db.Expenses.Where(e => e.UserId == userId);
        if (from is not null) q = q.Where(e => e.SpentOn >= from);
        if (to is not null) q = q.Where(e => e.SpentOn <= to);
        if (categoryId is not null) q = q.Where(e => e.CategoryId == categoryId);
        return await q.OrderByDescending(e => e.SpentOn).ThenByDescending(e => e.Id)
            .Select(e => new ExpenseDto(e.Id, e.Amount, e.CurrencyCode, e.CategoryId, e.SpentOn, e.Note))
            .ToListAsync();
    }

    public async Task<OperationResult<ExpenseDto>> CreateAsync(int userId, ExpenseInput input)
    {
        if (input.Amount <= 0) return OperationResult<ExpenseDto>.Bad("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(input.CurrencyCode)) return OperationResult<ExpenseDto>.Bad("Currency required.");
        var ownsCategory = await db.Categories.AnyAsync(c => c.Id == input.CategoryId && c.UserId == userId);
        if (!ownsCategory) return OperationResult<ExpenseDto>.Bad("Unknown category.");
        var e = new Expense
        {
            UserId = userId, Amount = input.Amount,
            CurrencyCode = input.CurrencyCode.ToUpperInvariant(),
            CategoryId = input.CategoryId, SpentOn = input.SpentOn,
            Note = input.Note, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Expenses.Add(e);
        await db.SaveChangesAsync();
        return OperationResult<ExpenseDto>.Created(
            new ExpenseDto(e.Id, e.Amount, e.CurrencyCode, e.CategoryId, e.SpentOn, e.Note));
    }

    public async Task<OperationResult<ExpenseDto>> UpdateAsync(int userId, int id, ExpenseInput input)
    {
        var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (e is null) return OperationResult<ExpenseDto>.NotFound();
        if (input.Amount <= 0) return OperationResult<ExpenseDto>.Bad("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(input.CurrencyCode)) return OperationResult<ExpenseDto>.Bad("Currency required.");
        var ownsCategory = await db.Categories.AnyAsync(c => c.Id == input.CategoryId && c.UserId == userId);
        if (!ownsCategory) return OperationResult<ExpenseDto>.Bad("Unknown category.");
        e.Amount = input.Amount;
        e.CurrencyCode = input.CurrencyCode.ToUpperInvariant();
        e.CategoryId = input.CategoryId;
        e.SpentOn = input.SpentOn;
        e.Note = input.Note;
        await db.SaveChangesAsync();
        return OperationResult<ExpenseDto>.Ok(
            new ExpenseDto(e.Id, e.Amount, e.CurrencyCode, e.CategoryId, e.SpentOn, e.Note));
    }

    public async Task<OperationResult<ExpenseDto>> DeleteAsync(int userId, int id)
    {
        var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (e is null) return OperationResult<ExpenseDto>.NotFound();
        db.Expenses.Remove(e);
        await db.SaveChangesAsync();
        return OperationResult<ExpenseDto>.NoContent();
    }
}
