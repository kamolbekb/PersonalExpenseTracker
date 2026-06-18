using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Data;
using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Features.Categories;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/categories");

        g.MapGet("", async (ICurrentUser cu, AppDbContext db) =>
        {
            var user = await cu.GetOrCreateAsync();
            return Results.Ok(await db.Categories
                .Where(c => c.UserId == user.Id && !c.IsArchived)
                .OrderBy(c => c.Name)
                .Select(c => new CategoryDto(c.Id, c.Name, c.Emoji, c.IsArchived))
                .ToListAsync());
        });

        g.MapPost("", async (ICurrentUser cu, AppDbContext db, CategoryInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            if (string.IsNullOrWhiteSpace(input.Name)) return Results.BadRequest("Name required.");
            var c = new Category { UserId = user.Id, Name = input.Name.Trim(), Emoji = input.Emoji };
            db.Categories.Add(c);
            await db.SaveChangesAsync();
            return Results.Created($"/api/categories/{c.Id}",
                new CategoryDto(c.Id, c.Name, c.Emoji, c.IsArchived));
        });

        g.MapPut("/{id:int}", async (ICurrentUser cu, AppDbContext db, int id, CategoryInput input) =>
        {
            if (string.IsNullOrWhiteSpace(input.Name)) return Results.BadRequest("Name required.");
            var user = await cu.GetOrCreateAsync();
            var c = await db.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);
            if (c is null) return Results.NotFound();
            c.Name = input.Name.Trim();
            c.Emoji = input.Emoji;
            await db.SaveChangesAsync();
            return Results.Ok(new CategoryDto(c.Id, c.Name, c.Emoji, c.IsArchived));
        });

        return api;
    }
}
