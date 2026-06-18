using ExpenseTracker.Application.Categories;
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/categories");

        g.MapGet("", async (ICurrentUser cu, CategoryService svc) =>
        {
            var user = await cu.GetOrCreateAsync();
            return Results.Ok(await svc.ListAsync(user.Id));
        });

        g.MapPost("", async (ICurrentUser cu, CategoryService svc, CategoryInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.CreateAsync(user.Id, input);
            return EndpointResults.ToHttp(result, result.Value is not null ? $"/api/categories/{result.Value.Id}" : null);
        });

        g.MapPut("/{id:int}", async (ICurrentUser cu, CategoryService svc, int id, CategoryInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.UpdateAsync(user.Id, id, input);
            return EndpointResults.ToHttp(result);
        });

        return api;
    }

}
