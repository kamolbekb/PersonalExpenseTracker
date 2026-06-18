using ExpenseTracker.Application.Budgets;
using ExpenseTracker.Application.Common;
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Api.Endpoints;

public static class BudgetEndpoints
{
    public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder api)
    {
        var g = api.MapGroup("/budgets");

        g.MapGet("", async (ICurrentUser cu, BudgetService svc) =>
        {
            var user = await cu.GetOrCreateAsync();
            return Results.Ok(await svc.ListAsync(user.Id));
        });

        g.MapPut("", async (ICurrentUser cu, BudgetService svc, BudgetInput input) =>
        {
            var user = await cu.GetOrCreateAsync();
            var result = await svc.UpsertAsync(user.Id, input);
            return EndpointResults.ToHttp(result);
        });

        return api;
    }

}
