using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Features.Budgets;

public static class BudgetEndpoints
{
    public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/budgets");

        g.MapGet("", async (ICurrentUser cu, AppDbContext db) =>
        {
            var user = await cu.GetOrCreateAsync();
            return Results.Ok(await db.Budgets.Where(b => b.UserId == user.Id)
                .Select(b => new BudgetDto(b.Id, b.CategoryId, b.LimitAmount, b.CurrencyCode))
                .ToListAsync());
        });

        g.MapPut("", async (ICurrentUser cu, AppDbContext db, BudgetInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            if (input.LimitAmount <= 0) return Results.BadRequest("Limit must be positive.");
            if (string.IsNullOrWhiteSpace(input.CurrencyCode)) return Results.BadRequest("Currency required.");
            if (input.CategoryId is not null)
            {
                var ownsCategory = await db.Categories.AnyAsync(c => c.Id == input.CategoryId && c.UserId == user.Id);
                if (!ownsCategory) return Results.BadRequest("Unknown category.");
            }
            var existing = await db.Budgets.FirstOrDefaultAsync(b =>
                b.UserId == user.Id && b.CategoryId == input.CategoryId);
            if (existing is null)
            {
                existing = new Budget { UserId = user.Id, CategoryId = input.CategoryId };
                db.Budgets.Add(existing);
            }
            existing.LimitAmount = input.LimitAmount;
            existing.CurrencyCode = input.CurrencyCode.ToUpperInvariant();
            await db.SaveChangesAsync();
            return Results.Ok(new BudgetDto(existing.Id, existing.CategoryId,
                existing.LimitAmount, existing.CurrencyCode));
        });

        return api;
    }
}
