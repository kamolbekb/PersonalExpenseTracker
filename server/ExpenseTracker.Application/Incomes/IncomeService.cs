using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Incomes;

public class IncomeService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<IncomeDto>> ListAsync(int userId, DateOnly? from, DateOnly? to, int? categoryId)
    {
        var q = db.Incomes.Where(i => i.UserId == userId);
        if (from is not null) q = q.Where(i => i.ReceivedOn >= from);
        if (to is not null) q = q.Where(i => i.ReceivedOn <= to);
        if (categoryId is not null) q = q.Where(i => i.IncomeCategoryId == categoryId);
        return await q.OrderByDescending(i => i.ReceivedOn).ThenByDescending(i => i.Id)
            .Select(i => new IncomeDto(i.Id, i.Amount, i.CurrencyCode, i.IncomeCategoryId, i.ReceivedOn, i.Note))
            .ToListAsync();
    }

    public async Task<OperationResult<IncomeDto>> GetAsync(int userId, int id)
    {
        var i = await db.Incomes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (i is null) return OperationResult<IncomeDto>.NotFound();
        return OperationResult<IncomeDto>.Ok(
            new IncomeDto(i.Id, i.Amount, i.CurrencyCode, i.IncomeCategoryId, i.ReceivedOn, i.Note));
    }

    public async Task<OperationResult<IncomeDto>> CreateAsync(int userId, IncomeInput input)
    {
        if (input.Amount <= 0) return OperationResult<IncomeDto>.Bad("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(input.CurrencyCode)) return OperationResult<IncomeDto>.Bad("Currency required.");
        var ownsCategory = await db.IncomeCategories.AnyAsync(c => c.Id == input.IncomeCategoryId && c.UserId == userId);
        if (!ownsCategory) return OperationResult<IncomeDto>.Bad("Unknown category.");
        var i = new Income
        {
            UserId = userId, Amount = input.Amount,
            CurrencyCode = input.CurrencyCode.ToUpperInvariant(),
            IncomeCategoryId = input.IncomeCategoryId, ReceivedOn = input.ReceivedOn,
            Note = input.Note, CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Incomes.Add(i);
        await db.SaveChangesAsync();
        return OperationResult<IncomeDto>.Created(
            new IncomeDto(i.Id, i.Amount, i.CurrencyCode, i.IncomeCategoryId, i.ReceivedOn, i.Note));
    }

    public async Task<OperationResult<IncomeDto>> UpdateAsync(int userId, int id, IncomeInput input)
    {
        var i = await db.Incomes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (i is null) return OperationResult<IncomeDto>.NotFound();
        if (input.Amount <= 0) return OperationResult<IncomeDto>.Bad("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(input.CurrencyCode)) return OperationResult<IncomeDto>.Bad("Currency required.");
        var ownsCategory = await db.IncomeCategories.AnyAsync(c => c.Id == input.IncomeCategoryId && c.UserId == userId);
        if (!ownsCategory) return OperationResult<IncomeDto>.Bad("Unknown category.");
        i.Amount = input.Amount;
        i.CurrencyCode = input.CurrencyCode.ToUpperInvariant();
        i.IncomeCategoryId = input.IncomeCategoryId;
        i.ReceivedOn = input.ReceivedOn;
        i.Note = input.Note;
        await db.SaveChangesAsync();
        return OperationResult<IncomeDto>.Ok(
            new IncomeDto(i.Id, i.Amount, i.CurrencyCode, i.IncomeCategoryId, i.ReceivedOn, i.Note));
    }

    public async Task<OperationResult<IncomeDto>> DeleteAsync(int userId, int id)
    {
        var i = await db.Incomes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (i is null) return OperationResult<IncomeDto>.NotFound();
        db.Incomes.Remove(i);
        await db.SaveChangesAsync();
        return OperationResult<IncomeDto>.NoContent();
    }
}
