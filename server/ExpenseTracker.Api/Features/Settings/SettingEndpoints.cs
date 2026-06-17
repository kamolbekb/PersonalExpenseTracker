using ExpenseTracker.Api.Auth;
using ExpenseTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Features.Settings;

public static class SettingEndpoints
{
    public static IEndpointRouteBuilder MapSettingEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/settings");

        g.MapGet("", async (ICurrentUser cu, AppDbContext db) =>
        {
            var user = await cu.GetOrCreateAsync();
            var s = await db.Settings.FirstAsync(x => x.UserId == user.Id);
            return Results.Ok(new SettingDto(s.BaseCurrency));
        });

        g.MapPut("", async (ICurrentUser cu, AppDbContext db, SettingDto input) =>
        {
            var user = await cu.GetOrCreateAsync();
            if (string.IsNullOrWhiteSpace(input.BaseCurrency)) return Results.BadRequest("Base currency required.");
            var s = await db.Settings.FirstAsync(x => x.UserId == user.Id);
            s.BaseCurrency = input.BaseCurrency.ToUpperInvariant();
            await db.SaveChangesAsync();
            return Results.Ok(new SettingDto(s.BaseCurrency));
        });

        return api;
    }
}
