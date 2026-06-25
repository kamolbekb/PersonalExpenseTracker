using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.IncomeCategories;

namespace ExpenseTracker.Api.Endpoints;

public static class IncomeCategoryEndpoints
{
    public static IEndpointRouteBuilder MapIncomeCategoryEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/income-categories", async (ICurrentUser cu, IncomeCategoryService svc) =>
        {
            var user = await cu.GetOrCreateAsync();
            return Results.Ok(await svc.ListAsync(user.Id));
        });
        return api;
    }
}
