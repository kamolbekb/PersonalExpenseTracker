using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Incomes;

namespace ExpenseTracker.Api.Endpoints;

public static class IncomeEndpoints
{
    public static IEndpointRouteBuilder MapIncomeEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/incomes");

        g.MapGet("", async (ICurrentUser cu, IncomeService svc,
            DateOnly? from, DateOnly? to, int? categoryId) =>
        {
            var user = await cu.GetOrCreateAsync();
            return Results.Ok(await svc.ListAsync(user.Id, from, to, categoryId));
        });

        g.MapGet("/{id:int}", async (ICurrentUser cu, IncomeService svc, int id) =>
        {
            var user = await cu.GetOrCreateAsync();
            return EndpointResults.ToHttp(await svc.GetAsync(user.Id, id));
        });

        g.MapPost("", async (ICurrentUser cu, IncomeService svc, IncomeInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.CreateAsync(user.Id, input);
            return EndpointResults.ToHttp(result, result.Value is not null ? $"/api/incomes/{result.Value.Id}" : null);
        });

        g.MapPut("/{id:int}", async (ICurrentUser cu, IncomeService svc, int id, IncomeInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            return EndpointResults.ToHttp(await svc.UpdateAsync(user.Id, id, input));
        });

        g.MapDelete("/{id:int}", async (ICurrentUser cu, IncomeService svc, int id) =>
        {
            var user = await cu.GetOrCreateAsync();
            return EndpointResults.ToHttp(await svc.DeleteAsync(user.Id, id));
        });

        return api;
    }
}
