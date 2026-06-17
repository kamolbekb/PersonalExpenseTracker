using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Features.Expenses;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/expenses");

        g.MapGet("", async (ICurrentUser cu, AppDbContext db,
            DateOnly? from, DateOnly? to, int? categoryId) =>
        {
            var user = await cu.GetOrCreateAsync();
            var q = db.Expenses.Where(e => e.UserId == user.Id);
            if (from is not null) q = q.Where(e => e.SpentOn >= from);
            if (to is not null) q = q.Where(e => e.SpentOn <= to);
            if (categoryId is not null) q = q.Where(e => e.CategoryId == categoryId);
            var items = await q.OrderByDescending(e => e.SpentOn).ThenByDescending(e => e.Id)
                .Select(e => new ExpenseDto(e.Id, e.Amount, e.CurrencyCode, e.CategoryId, e.SpentOn, e.Note))
                .ToListAsync();
            return Results.Ok(items);
        });

        g.MapPost("", async (ICurrentUser cu, AppDbContext db, ExpenseInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            if (input.Amount <= 0) return Results.BadRequest("Amount must be positive.");
            var ownsCategory = await db.Categories.AnyAsync(c => c.Id == input.CategoryId && c.UserId == user.Id);
            if (!ownsCategory) return Results.BadRequest("Unknown category.");
            var e = new Expense
            {
                UserId = user.Id, Amount = input.Amount,
                CurrencyCode = input.CurrencyCode.ToUpperInvariant(),
                CategoryId = input.CategoryId, SpentOn = input.SpentOn,
                Note = input.Note, CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Expenses.Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/expenses/{e.Id}",
                new ExpenseDto(e.Id, e.Amount, e.CurrencyCode, e.CategoryId, e.SpentOn, e.Note));
        });

        g.MapPut("/{id:int}", async (ICurrentUser cu, AppDbContext db, int id, ExpenseInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);
            if (e is null) return Results.NotFound();
            if (input.Amount <= 0) return Results.BadRequest("Amount must be positive.");
            var ownsCategory = await db.Categories.AnyAsync(c => c.Id == input.CategoryId && c.UserId == user.Id);
            if (!ownsCategory) return Results.BadRequest("Unknown category.");
            e.Amount = input.Amount;
            e.CurrencyCode = input.CurrencyCode.ToUpperInvariant();
            e.CategoryId = input.CategoryId;
            e.SpentOn = input.SpentOn;
            e.Note = input.Note;
            await db.SaveChangesAsync();
            return Results.Ok(new ExpenseDto(e.Id, e.Amount, e.CurrencyCode, e.CategoryId, e.SpentOn, e.Note));
        });

        g.MapDelete("/{id:int}", async (ICurrentUser cu, AppDbContext db, int id) =>
        {
            var user = await cu.GetOrCreateAsync();
            var e = await db.Expenses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);
            if (e is null) return Results.NotFound();
            db.Expenses.Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return api;
    }
}
